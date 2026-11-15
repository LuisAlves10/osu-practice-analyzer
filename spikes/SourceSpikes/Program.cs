using System.Text.Json;
using SourceSpikes;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

try
{
    if (args is ["history"] or ["history", _])
    {
        string root = args.Length == 2
            ? args[1] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "osu");
        HistoryEvidence evidence = LazerHistory.Scan(root);
        Console.WriteLine(JsonSerializer.Serialize(evidence, json));
        return evidence.MatchedReplays > 0 ? 0 : 1;
    }

    if (args is ["tosu", _, _] && int.TryParse(args[1], out int port) && port is > 0 and <= 65535
        && int.TryParse(args[2], out int seconds) && seconds is > 0 and <= 120)
    {
        if (!File.Exists(Path.Combine("spikes", "SourceSpikes", "SourceSpikes.csproj"))
            || (Directory.Exists("scratch") && File.GetAttributes("scratch").HasFlag(FileAttributes.ReparsePoint)))
        {
            Console.Error.WriteLine("Run the capture from the project root with a local scratch directory.");
            return 2;
        }

        using var cancelled = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancelled.Cancel();
        };
        Directory.CreateDirectory("scratch");
        string name = $"tosu-{Guid.NewGuid():N}.jsonl";
        using var output = new FileStream(Path.Combine("scratch", name), FileMode.CreateNew, FileAccess.Write);
        await using var writer = new StreamWriter(output) { AutoFlush = true };
        CaptureSummary summary = await TosuCapture.RunAsync(port, TimeSpan.FromSeconds(seconds), writer, cancelled.Token);
        Console.WriteLine(JsonSerializer.Serialize(new { capture = $"scratch/{name}", summary }, json));
        return summary.ValidSamples > 0 ? 0 : 1;
    }

    Console.Error.WriteLine("Usage: history [lazer-data-root] | tosu <loopback-port> <seconds: 1-120>");
    return 2;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"Probe could not read its source or write its capture ({error.GetType().Name}).");
    return 2;
}
