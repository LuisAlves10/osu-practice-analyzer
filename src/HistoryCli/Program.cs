using System.Text.Json;
using Microsoft.Data.Sqlite;
using PracticeHistory;

if (args.Length is < 2 or > 3 || !Guid.TryParse(args[1], out Guid profile) ||
    !(args[0] == "import" && args.Length == 3 || args[0] == "summary" && args.Length == 2))
{
    Console.Error.WriteLine("Usage: import <profile-guid> <lazer-data-root> | summary <profile-guid>");
    return 2;
}

try
{
    if (!File.Exists("src/HistoryCli/HistoryCli.csproj") ||
        (Directory.Exists("scratch") && File.GetAttributes("scratch").HasFlag(FileAttributes.ReparsePoint)))
        throw new InvalidOperationException("Run from the project root with a local scratch directory.");

    Directory.CreateDirectory("scratch");
    const string database = "scratch/history.sqlite";
    if (File.Exists(database) && File.GetAttributes(database).HasFlag(FileAttributes.ReparsePoint))
        throw new InvalidOperationException("A linked database is unsupported.");

    using var store = new HistoryStore(database);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var imported = args[0] == "import" ? store.ImportLazer(profile, args[2], cancellation.Token) : null;
    var attempts = store.Attempts(profile);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        imported,
        attempts = attempts.Count,
        linked = attempts.Count(a => a.Link == "linked"),
        ambiguous = attempts.Count(a => a.Link == "ambiguous"),
        largestGroup = attempts.Where(a => a.MapVersion != null).GroupBy(a => a.MapVersion)
            .Select(g => g.Count()).DefaultIfEmpty().Max(),
        unknown = attempts.Count(a => a.Outcome == Outcome.Unknown),
        performance = store.Statistics(profile)
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Import cancelled.");
    return 1;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
    or InvalidOperationException or SqliteException or ArgumentException)
{
    Console.Error.WriteLine($"History operation failed ({error.GetType().Name}).");
    return 2;
}
