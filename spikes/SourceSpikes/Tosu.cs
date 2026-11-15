using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceSpikes;

public sealed record TosuSample(int State, bool? Lazer, int? Ruleset, double? MapTimeMs, bool? FailedFlag)
{
    public static TosuSample? Parse(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            JsonElement root = document.RootElement;
            if (Number(Child(root, "state"), "number") is not int state)
                return null;

            JsonElement play = Child(root, "play");
            return new TosuSample(state, Child(root, "client") switch
            {
                { ValueKind: JsonValueKind.String } value => value.GetString() switch
                {
                    "lazer" => true,
                    "stable" => false,
                    _ => null
                },
                _ => null
            }, Number(Child(play, "mode"), "number"), MapTime(root),
                Child(play, "failed").ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement Child(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : default;

    private static int? Number(JsonElement parent, string name) =>
        Child(parent, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number)
            ? number : null;

    private static double? MapTime(JsonElement root) =>
        Child(Child(Child(root, "beatmap"), "time"), "live") is { ValueKind: JsonValueKind.Number } value
            && value.TryGetDouble(out double time) && double.IsFinite(time) ? time : null;
}

public enum Observation
{
    StartedFromIdle, StartedWithUnknownBeginning, ResultsScreen, FailedFlagSeen, ClockRewound, UnknownEnd
}

public sealed record Change(Observation Kind, string? Outcome = null);

public sealed class TosuTransitions
{
    private const int Menu = 0, Play = 2, SongSelect = 5, Results = 7;
    private bool playing, idle, sawFailFlag;
    private double? previousMapTime;

    public Change? Observe(TosuSample? sample)
    {
        if (sample is { Lazer: true, State: Play, Ruleset: 0 })
        {
            Change? change = null;
            if (!playing)
                change = new Change(idle ? Observation.StartedFromIdle : Observation.StartedWithUnknownBeginning);
            else if (sample.MapTimeMs < previousMapTime)
            {
                change = new Change(Observation.ClockRewound, "unknown");
                sawFailFlag = false;
            }

            playing = true;
            idle = false;
            previousMapTime = sample.MapTimeMs;
            if (sample.FailedFlag == true && !sawFailFlag)
            {
                sawFailFlag = true;
                // Raw data retains the flag even if this sample also opens a window or rewinds.
                change ??= new Change(Observation.FailedFlagSeen);
            }
            return change;
        }

        if (playing && sample is { Lazer: true, State: Results })
        {
            End();
            return new Change(Observation.ResultsScreen, "unknown");
        }

        Change? end = End();
        idle = sample is { State: Menu or SongSelect, Lazer: not false };
        return end;
    }

    public Change? End()
    {
        Change? change = playing ? new Change(Observation.UnknownEnd, "unknown") : null;
        playing = idle = sawFailFlag = false;
        previousMapTime = null;
        return change;
    }
}

public sealed record CaptureSummary(string EndReason, int ValidSamples, int InvalidSamples, int UnknownOutcomes);

public static class TosuCapture
{
    private const int MaxMessageBytes = 1024 * 1024;
    private const int MaxMessages = 1_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static async Task<CaptureSummary> RunAsync(int port, TimeSpan duration, TextWriter log,
        CancellationToken cancellationToken = default)
    {
        var transitions = new TosuTransitions();
        using var socket = new ClientWebSocket();
        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(duration);
        var endpoint = new Uri($"ws://127.0.0.1:{port}/websocket/v2");

        int valid = 0, invalid = 0, unknown = 0, sequence = 0;
        bool connected = false;
        string reason;
        try
        {
            await socket.ConnectAsync(endpoint, invoker, deadline.Token);
            connected = true;
            byte[] buffer = new byte[MaxMessageBytes];
            reason = "message_limit";

            while (sequence < MaxMessages)
            {
                int length = 0;
                ValueWebSocketReceiveResult received;
                do
                {
                    if (length == buffer.Length)
                        throw new InvalidDataException();

                    received = await socket.ReceiveAsync(buffer.AsMemory(length), deadline.Token);
                    length += received.Count;
                } while (!received.EndOfMessage);

                if (received.MessageType == WebSocketMessageType.Close)
                {
                    reason = "peer_closed";
                    break;
                }

                TosuSample? sample = received.MessageType == WebSocketMessageType.Text
                    ? TosuSample.Parse(buffer.AsMemory(0, length)) : null;
                if (sample is null) invalid++;
                else valid++;

                await WriteAsync(sample, transitions.Observe(sample), sample is null ? "invalid_sample" : "sample");
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            reason = cancellationToken.IsCancellationRequested ? "cancelled" : "deadline";
        }
        catch (WebSocketException)
        {
            reason = connected ? "disconnected" : "provider_unavailable";
        }
        catch (HttpRequestException)
        {
            reason = connected ? "disconnected" : "provider_unavailable";
        }
        catch (InvalidDataException)
        {
            reason = "message_too_large";
        }

        await WriteAsync(null, transitions.End(), reason);
        return new CaptureSummary(reason, valid, invalid, unknown);

        async Task WriteAsync(TosuSample? raw, Change? change, string kind)
        {
            if (change?.Outcome == "unknown") unknown++;
            await log.WriteLineAsync(JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                source = "tosu-websocket-v2",
                sequence = ++sequence,
                observedAt = DateTimeOffset.UtcNow,
                kind,
                raw,
                change
            }, Json));
        }
    }
}
