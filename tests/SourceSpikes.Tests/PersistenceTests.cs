using Microsoft.Data.Sqlite;
using PracticeHistory;
using SourceSpikes;

namespace SourceSpikes.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("osu-history-").FullName;
    private readonly Guid profile = Guid.NewGuid();
    private string Database => Path.Combine(root, "history.sqlite");

    [Fact]
    public void ImportSurvivesReopenAndKeepsTwoReplaysInEachSelectedProfile()
    {
        var map = Seed();
        var other = Guid.NewGuid();
        string[] ids;
        using (var store = new HistoryStore(Database))
        {
            store.ImportLazer(profile, root);
            ids = store.Attempts(profile).Select(a => a.Id).ToArray();
            Assert.Equal(2, ids.Length);
            Assert.Empty(store.Attempts(other));
            Assert.Null(store.ReadAsset(other, map.Sha256));
        }

        using var reopened = new HistoryStore(Database);
        reopened.ImportLazer(profile, root);
        var attempts = reopened.Attempts(profile);
        Assert.Equal(ids, attempts.Select(a => a.Id));
        Assert.Equal(2, attempts.Select(a => a.Replay).Distinct().Count());
        Assert.All(attempts, a =>
        {
            Assert.Equal(map.Sha256, a.MapVersion);
            Assert.Equal(Outcome.Unknown, a.Outcome);
            Assert.NotNull(reopened.ReadAsset(profile, a.Replay!));
        });
        Assert.Equal(LazerHistoryTests.Map("64"), reopened.ReadAsset(profile, map.Sha256));
        reopened.ImportLazer(other, root);
        Assert.Equal(2, reopened.Attempts(other).Count);
        Assert.Equal(ids, reopened.Attempts(profile).Select(a => a.Id));
        Assert.Equal(0L, Convert.ToInt64(Sql("SELECT COUNT(*) FROM saved_score")));
        var target = Guid.Parse(ids[0]);
        Assert.Throws<InvalidDataException>(() => reopened.RecordObservation(other, "synthetic", "verified", null,
            "{}", DateTimeOffset.UnixEpoch, fact: Outcome.Pass, verifiedAttemptId: target));
        reopened.RecordObservation(profile, "synthetic", "verified", null, "{}", DateTimeOffset.UnixEpoch,
            fact: Outcome.Pass, verifiedAttemptId: target);
        Assert.Equal(1, reopened.Statistics(profile).Passes);
        Assert.Equal(0, reopened.Statistics(other).Count);
        Assert.Single(reopened.Attempts(profile), a => a.Outcome == Outcome.Unknown);
    }

    [Fact]
    public void MetricsOnlyUsePassAndFailWithAvailableValuesAndReimportIsIdempotent()
    {
        using var store = new HistoryStore(Database);
        foreach (var outcome in Enum.GetValues<Outcome>())
        {
            Record(store, outcome.ToString(), outcome, outcome == Outcome.Fail ? null : 0.8);
            Record(store, outcome.ToString(), outcome, outcome == Outcome.Fail ? null : 0.8);
        }
        var stats = store.Statistics(profile);
        Assert.Equal(new Performance(1, 1, 1, 0.8), stats);
        Assert.Equal(2, stats.Count);
        Assert.Equal(0.5, stats.CompletionRate);
        Assert.Equal(5, store.Attempts(profile).Count);
        Assert.Equal(3, store.Attempts(profile).Count(a => a.Outcome is not (Outcome.Pass or Outcome.Fail)));
    }

    [Fact]
    public void RetainedEvidenceRecalculatesUnknownAndConflictingFacts()
    {
        using var store = new HistoryStore(Database);
        var time = DateTimeOffset.UnixEpoch;
        string id = store.RecordObservation(profile, "synthetic", "partial", "attempt", """{"rewound":true}""", time);
        Assert.Equal(Outcome.Unknown, Assert.Single(store.Attempts(profile)).Outcome);
        Assert.Equal(id, store.RecordObservation(profile, "synthetic", "official", "attempt",
            """{"passed":true}""", time, fact: Outcome.Pass, isSavedScore: true));
        Assert.Equal(Outcome.Pass, Assert.Single(store.Attempts(profile)).Outcome);
        Assert.Throws<InvalidDataException>(() => store.RecordObservation(profile, "synthetic", "official", "another",
            "{}", time, fact: Outcome.Fail));
        store.RecordObservation(profile, "synthetic", "conflict", "attempt", "{}", time, fact: Outcome.Fail);
        Assert.Equal(Outcome.Unknown, Assert.Single(store.Attempts(profile)).Outcome);
        Assert.Equal(3L, Convert.ToInt64(Sql("SELECT COUNT(*) FROM raw_observation")));
        store.RecordObservation(profile, "synthetic", "score-only", null, "{}", time, isSavedScore: true);
        Assert.Equal(2L, Convert.ToInt64(Sql("SELECT COUNT(*) FROM saved_score")));
        Assert.Single(store.Attempts(profile));
    }

    [Fact]
    public void TosuUsesCaptureAndSequenceInsteadOfPayloadIdentity()
    {
        using var store = new HistoryStore(Database);
        var capture = Guid.NewGuid();
        var sample = new TosuSnapshot(2, true, 0, 123, true);
        store.RecordTosuSnapshot(profile, capture, 1, sample, DateTimeOffset.UnixEpoch);
        store.RecordTosuSnapshot(profile, capture, 1, sample, DateTimeOffset.UnixEpoch);
        store.RecordTosuSnapshot(profile, capture, 2, sample, DateTimeOffset.UnixEpoch);
        store.RecordTosuSnapshot(profile, capture, 3, null, DateTimeOffset.UnixEpoch);
        Assert.Equal(3, store.Observations(profile).Count);
        Assert.All(store.Observations(profile), observation => Assert.Null(observation.OutcomeFact));
        Assert.Empty(store.Attempts(profile));
        Assert.Empty(store.Observations(Guid.NewGuid()));
    }

    [Fact]
    public void MissingAndAmbiguousMapsStayUnlinked()
    {
        var map = Seed();
        File.Delete(Blob(map.Sha256));
        using var store = new HistoryStore(Database);
        store.ImportLazer(profile, root);
        Assert.All(store.Attempts(profile), a => Assert.Equal("missing", a.Link));

        LazerHistoryTests.Put(root, LazerHistoryTests.Map("64"));
        store.ImportLazer(profile, root);
        Assert.All(store.Attempts(profile), a => Assert.Equal("linked", a.Link));
        // Inject a second candidate into the DB to exercise the ambiguity policy without a real MD5 collision.
        Sql($"""
            INSERT INTO raw_asset VALUES ('{profile:D}','synthetic-collision',x'01','test');
            INSERT INTO map_version VALUES ('{profile:D}','synthetic-collision','{map.Md5}',0,NULL,NULL);
            """);
        Assert.All(store.Attempts(profile), a =>
        {
            Assert.Equal("ambiguous", a.Link);
            Assert.Null(a.MapVersion);
        });
    }

    [Fact]
    public void CorruptInputAndInterruptedWriteDoNotLeavePartialData()
    {
        var map = Seed();
        LazerHistoryTests.Put(root, LazerHistoryTests.Replay(map.Md5, 3)[..^1]);
        using var store = new HistoryStore(Database);
        Sql("""
            CREATE TRIGGER interrupt_import BEFORE INSERT ON attempt
            BEGIN SELECT RAISE(ABORT, 'synthetic write interruption'); END;
            """);
        Assert.Throws<SqliteException>(() => store.ImportLazer(profile, root));
        Assert.Equal(0L, Convert.ToInt64(Sql("SELECT COUNT(*) FROM raw_asset")));
        Sql("DROP TRIGGER interrupt_import");
        Assert.Throws<OperationCanceledException>(() => store.ImportLazer(profile, root, new CancellationToken(true)));
        Assert.Empty(store.Attempts(profile));
        store.ImportLazer(profile, root);
        Assert.Equal(2, store.Attempts(profile).Count);
    }

    [Fact]
    public void UpgradeFromVersionOneKeepsRawDataAndRebuildsClassification()
    {
        using (var store = new HistoryStore(Database))
            Record(store, "one", Outcome.Pass, 0.95);
        Sql("""
            DROP VIEW performance_attempt;
            DROP VIEW classified_attempt;
            DROP VIEW replay_links;
            DROP INDEX evidence_by_attempt;
            DROP INDEX map_version_checksum;
            PRAGMA user_version=1;
            """);
        using var upgraded = new HistoryStore(Database);
        Assert.Equal(2L, Convert.ToInt64(Sql("PRAGMA user_version")));
        Assert.Equal(Outcome.Pass, Assert.Single(upgraded.Attempts(profile)).Outcome);
        Assert.Equal(0.95, upgraded.Statistics(profile).AverageAccuracy);
        Assert.Equal("ok", Sql("PRAGMA integrity_check"));
    }

    private void Record(HistoryStore store, string key, Outcome outcome, double? accuracy) =>
        store.RecordObservation(profile, "synthetic", key, key, "{}", DateTimeOffset.UnixEpoch,
            fact: outcome, accuracy: accuracy);

    private MapVersion Seed()
    {
        byte[] bytes = LazerHistoryTests.Map("64");
        var map = LazerHistory.ReadMap(bytes)!;
        LazerHistoryTests.Put(root, bytes);
        LazerHistoryTests.Put(root, LazerHistoryTests.Replay(map.Md5, 1));
        LazerHistoryTests.Put(root, LazerHistoryTests.Replay(map.Md5, 2));
        return map;
    }

    private string Blob(string hash) => Path.Combine(root, "files", hash[..1], hash[..2], hash);

    private object? Sql(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Database,
            Pooling = false,
            ForeignKeys = true
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
