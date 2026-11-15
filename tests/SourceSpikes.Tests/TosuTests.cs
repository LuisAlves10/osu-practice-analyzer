using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SourceSpikes.Tests;

public sealed class TosuTests
{
    [Fact]
    public void UpstreamWikiMenuProjectionLacksClientAndFailFlag()
    {
        // Only the numeric values used by the probe, from the documented tosu v2 menu example.
        const string wikiMenuProjection = """{"state":{"number":0},"beatmap":{"time":{"live":2197}}}""";
        var sample = Assert.IsType<TosuSample>(TosuSample.Parse(Encoding.UTF8.GetBytes(wikiMenuProjection)));
        Assert.Equal(0, sample.State);
        Assert.Equal(2197, sample.MapTimeMs);
        Assert.Null(sample.Lazer);
        Assert.Null(sample.FailedFlag);
        Assert.Null(new TosuTransitions().Observe(sample));
    }

    // The remaining sequences are synthetic, designed against the pinned v4.26.3 source.
    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"state":{"number":"2"}}""")]
    [InlineData("""{"state":{"number":2.5}}""")]
    public void MalformedStateIsRejected(string payload) =>
        Assert.Null(TosuSample.Parse(Encoding.UTF8.GetBytes(payload)));

    [Fact]
    public void ResultScreenAndExplicitFailFlagRemainSeparateEvidence()
    {
        var transitions = new TosuTransitions();
        Assert.Null(transitions.Observe(new TosuSample(0, true, null, null, null)));
        Assert.Equal(Observation.StartedFromIdle, transitions.Observe(Playing(100))?.Kind);
        Assert.Null(transitions.Observe(Playing(100)));
        Assert.Equal(Observation.FailedFlagSeen, transitions.Observe(Playing(200, failed: true))?.Kind);
        Assert.Null(transitions.Observe(Playing(200, failed: true)));
        Assert.Equal(new Change(Observation.ResultsScreen, "unknown"),
            transitions.Observe(new TosuSample(7, true, 0, 300, false)));
        Assert.Null(transitions.End());
    }

    [Fact]
    public void ClockRewindAndReturnToMenuCannotProveRestartOrAbort()
    {
        var transitions = new TosuTransitions();
        Assert.Equal(Observation.StartedWithUnknownBeginning, transitions.Observe(Playing(300))?.Kind);
        Assert.Equal(new Change(Observation.ClockRewound, "unknown"), transitions.Observe(Playing(100)));
        Assert.Equal(new Change(Observation.UnknownEnd, "unknown"),
            transitions.Observe(new TosuSample(5, true, null, null, null)));
        Assert.Null(transitions.End());
    }

    [Fact]
    public void SourceLossOrRulesetChangeClosesOnlyTheOpenWindow()
    {
        var transitions = new TosuTransitions();
        Assert.Null(transitions.Observe(Playing(100) with { Lazer = false }));
        transitions.Observe(Playing(100));
        Assert.Equal(new Change(Observation.UnknownEnd, "unknown"), transitions.Observe(null));
        Assert.Null(transitions.Observe(null));
        transitions.Observe(Playing(200, failed: true));
        Assert.Equal(new Change(Observation.UnknownEnd, "unknown"),
            transitions.Observe(Playing(300) with { Ruleset = 3 }));
        Assert.Null(transitions.End());
    }

    [Fact]
    public async Task LoopbackWebSocketWritesSanitizedSamplesAndMarksLostWindowUnknown()
    {
        const string frame = """
            {"client":"lazer","state":{"number":2},"play":{"mode":{"number":0},"failed":false,
            "playerName":"SYNTHETIC_PRIVATE"},"beatmap":{"time":{"live":123.25}},"token":"SYNTHETIC_TOKEN"}
            """;
        byte[] bytes = Encoding.UTF8.GetBytes(frame);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.UseWebSockets();
        app.MapGet("/websocket/v2", async (HttpContext request) =>
        {
            using var socket = await request.WebSockets.AcceptWebSocketAsync();
            await socket.SendAsync(bytes.AsMemory(0, 20), WebSocketMessageType.Text, false, request.RequestAborted);
            await socket.SendAsync(bytes.AsMemory(20), WebSocketMessageType.Text, true, request.RequestAborted);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", request.RequestAborted);
        });

        await app.StartAsync();
        int port = new Uri(Assert.Single(app.Urls)).Port;
        using var log = new StringWriter();
        var result = await TosuCapture.RunAsync(port, TimeSpan.FromSeconds(5), log);

        Assert.Equal(new CaptureSummary("peer_closed", 1, 0, 1), result);
        string text = log.ToString();
        Assert.DoesNotContain("SYNTHETIC_", text);
        string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var first = JsonDocument.Parse(lines[0]);
        var sample = first.RootElement;
        Assert.Equal(123.25, sample.GetProperty("raw").GetProperty("mapTimeMs").GetDouble());
        Assert.Equal("started_with_unknown_beginning", sample.GetProperty("change").GetProperty("kind").GetString());
        Assert.Equal(1, sample.GetProperty("sequence").GetInt32());
        using var last = JsonDocument.Parse(lines[1]);
        Assert.Equal("unknown_end", last.RootElement.GetProperty("change").GetProperty("kind").GetString());
        Assert.Equal("unknown", last.RootElement.GetProperty("change").GetProperty("outcome").GetString());
    }

    private static TosuSample Playing(int ms, bool failed = false) => new(2, true, 0, ms, failed);
}
