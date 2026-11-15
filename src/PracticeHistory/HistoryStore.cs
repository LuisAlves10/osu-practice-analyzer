using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SourceSpikes;

namespace PracticeHistory;

public enum Outcome { Unknown, Pass, Fail, Restart, Quit }

public sealed record AttemptRow(string Id, string? MapVersion, string? Replay, string? Link, Outcome Outcome,
    double? Accuracy, int ClassifierVersion);
public sealed record StoredObservation(string Source, string Key, string? Attempt, string ReceivedAt,
    int Version, string Json, string? OutcomeFact);
public sealed record TosuSnapshot(int? State, bool? Lazer, int? Ruleset, double? MapTimeMs, bool? FailedFlag);
public sealed record Performance(int Passes, int Fails, int AccuracySamples, double? AverageAccuracy)
{
    public int Count => Passes + Fails;
    public double? CompletionRate => Count == 0 ? null : (double)Passes / Count;
}

public sealed class HistoryStore : IDisposable
{
    private const string TosuSource = "tosu-websocket-v2";
    private readonly SqliteConnection db;

    public HistoryStore(string path)
    {
        db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true,
            Pooling = false
        }.ToString());
        try
        {
            db.Open();
            using var tx = db.BeginTransaction();
            int version = Convert.ToInt32(Scalar(tx, "PRAGMA user_version"));
            if (version < 0 || version > Schema.Migrations.Length)
                throw new InvalidDataException("Unsupported database version.");
            while (version < Schema.Migrations.Length)
            {
                Execute(tx, Schema.Migrations[version]);
                version++;
                Execute(tx, "PRAGMA user_version=" + version);
            }
            tx.Commit();
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    public HistoryEvidence ImportLazer(Guid profile, string root, CancellationToken cancellation = default)
    {
        using var tx = db.BeginTransaction();
        AddProfile(tx, profile);
        string importedAt = DateTimeOffset.UtcNow.ToString("O");
        var report = LazerHistory.Scan(root, (bytes, map, replay) =>
        {
            string hash = map?.Sha256 ?? Convert.ToHexStringLower(SHA256.HashData(bytes));
            Execute(tx, """
                INSERT INTO raw_asset VALUES ($p,$h,$bytes,$at) ON CONFLICT DO NOTHING
                """, ("p", profile), ("h", hash), ("bytes", bytes), ("at", importedAt));
            if (map is not null)
            {
                Execute(tx, """
                    INSERT INTO map_version VALUES ($p,$h,$md5,$mode,$map,$set) ON CONFLICT DO NOTHING
                    """, ("p", profile), ("h", hash), ("md5", map.Md5), ("mode", map.Ruleset),
                    ("map", map.BeatmapId), ("set", map.SetId));
                return;
            }

            Execute(tx, """
                INSERT INTO replay_asset VALUES ($p,$h,$md5,$format) ON CONFLICT DO NOTHING
                """, ("p", profile), ("h", hash), ("md5", replay!.BeatmapMd5), ("format", replay.Format));
            Execute(tx, """
                INSERT INTO attempt VALUES ($p,$id,'lazer-file',$h,$h) ON CONFLICT DO NOTHING
                """, ("p", profile), ("id", Guid.NewGuid()), ("h", hash));
        }, cancellation);

        cancellation.ThrowIfCancellationRequested();
        if (report.ScanLimitReached)
            throw new InvalidDataException("The scan limit was reached. The incomplete batch was rolled back.");
        tx.Commit();
        return report;
    }

    // Callers must provide a stable source key and only submit an outcome that the source explicitly establishes.
    public string RecordObservation(Guid profile, string source, string observationKey, string? attemptKey,
        string json, DateTimeOffset receivedAt, int sourceVersion = 1, Outcome fact = Outcome.Unknown,
        bool isSavedScore = false, double? accuracy = null, Guid? verifiedAttemptId = null)
    {
        if (source is "lazer-file" or TosuSource)
            throw new ArgumentException("Reserved adapter source.", nameof(source));
        return Record(profile, source, observationKey, attemptKey, json, receivedAt, sourceVersion, fact,
            isSavedScore, accuracy, verifiedAttemptId);
    }

    public void RecordTosuSnapshot(Guid profile, Guid capture, int sequence, TosuSnapshot? sample,
        DateTimeOffset receivedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        string key = FormattableString.Invariant($"{capture:D}/{sequence}");
        Record(profile, TosuSource, key, null, JsonSerializer.Serialize(sample), receivedAt, 1, Outcome.Unknown,
            false, null, null);
    }

    private string Record(Guid profile, string source, string observationKey, string? attemptKey,
        string json, DateTimeOffset receivedAt, int sourceVersion, Outcome fact, bool isSavedScore, double? accuracy,
        Guid? verifiedAttemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationKey);
        if (attemptKey is not null) ArgumentException.ThrowIfNullOrWhiteSpace(attemptKey);
        if (!Enum.IsDefined(fact)) throw new ArgumentOutOfRangeException(nameof(fact));
        if (accuracy is double value && (!double.IsFinite(value) || value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(accuracy));
        if (attemptKey is not null && verifiedAttemptId.HasValue)
            throw new ArgumentException("Choose either a source attempt key or a verified existing attempt.");
        if (attemptKey is null && !verifiedAttemptId.HasValue && (fact != Outcome.Unknown || accuracy.HasValue))
            throw new ArgumentException("Attempt evidence requires a stable attempt key.");
        using var tx = db.BeginTransaction();
        AddProfile(tx, profile);
        string? id = verifiedAttemptId?.ToString("D");
        if (verifiedAttemptId.HasValue && Scalar(tx, "SELECT id FROM attempt WHERE profile=$p AND id=$id",
            ("p", profile), ("id", id)) is null)
            throw new InvalidDataException("Verified target attempt was not found in this profile.");
        if (attemptKey is not null)
        {
            Execute(tx, """
                INSERT INTO attempt VALUES ($p,$id,$s,$key,NULL) ON CONFLICT DO NOTHING
                """, ("p", profile), ("id", Guid.NewGuid()), ("s", source), ("key", attemptKey));
            id = (string)Scalar(tx, "SELECT id FROM attempt WHERE profile=$p AND source=$s AND source_key=$key",
                ("p", profile), ("s", source), ("key", attemptKey))!;
        }

        string? outcome = fact == Outcome.Unknown ? null : fact.ToString().ToUpperInvariant();
        var key = new (string, object?)[] { ("p", profile), ("s", source), ("k", observationKey) };
        var values = key.Concat([("a", id), ("v", sourceVersion), ("j", json), ("o", outcome),
            ("accuracy", accuracy), ("isScore", isSavedScore)]).ToArray();
        int inserted = Execute(tx, """
            INSERT INTO raw_observation VALUES ($p,$s,$k,$a,$time,$v,$j,$o,$accuracy,$isScore) ON CONFLICT DO NOTHING
            """, values.Append(("time", receivedAt.ToUniversalTime().ToString("O"))).ToArray());

        if (inserted == 0 && Convert.ToInt32(Scalar(tx, """
            SELECT COUNT(*) FROM raw_observation WHERE profile=$p AND source=$s AND source_key=$k
            AND attempt IS $a AND version=$v AND payload=$j AND outcome_fact IS $o
            AND accuracy IS $accuracy AND is_score=$isScore
            """, values)) != 1)
            throw new InvalidDataException("Source observation key already identifies different evidence.");

        if (isSavedScore)
            Execute(tx, "INSERT INTO saved_score VALUES ($p,$s,$k,$a) ON CONFLICT DO NOTHING",
                key.Append(("a", id)).ToArray());
        tx.Commit();
        return id ?? observationKey;
    }

    public IReadOnlyList<AttemptRow> Attempts(Guid profile)
    {
        using var cmd = Command(null, """
            SELECT a.id,l.map_version,a.replay,l.link_status,a.outcome,a.accuracy,a.classifier_version
            FROM classified_attempt a LEFT JOIN replay_links l ON l.profile=a.profile AND l.replay=a.replay
            WHERE a.profile=$p ORDER BY a.id
            """, ("p", profile));
        using var reader = cmd.ExecuteReader();
        List<AttemptRow> rows = [];
        while (reader.Read())
            rows.Add(new(reader.GetString(0), OptionalString(reader, 1), OptionalString(reader, 2),
                OptionalString(reader, 3), Enum.Parse<Outcome>(reader.GetString(4), ignoreCase: true),
                reader.IsDBNull(5) ? null : reader.GetDouble(5), reader.GetInt32(6)));
        return rows;
    }

    public Performance Statistics(Guid profile)
    {
        using var cmd = Command(null, """
            SELECT COUNT(CASE WHEN outcome='PASS' THEN 1 END), COUNT(CASE WHEN outcome='FAIL' THEN 1 END),
                COUNT(accuracy), AVG(accuracy) FROM performance_attempt WHERE profile=$p
            """, ("p", profile));
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetDouble(3));
    }

    public byte[]? ReadAsset(Guid profile, string hash) =>
        (byte[]?)Scalar(null, "SELECT bytes FROM raw_asset WHERE profile=$p AND hash=$h", ("p", profile), ("h", hash));

    public IReadOnlyList<StoredObservation> Observations(Guid profile)
    {
        using var cmd = Command(null, """
            SELECT source,source_key,attempt,received_at,version,payload,outcome_fact
            FROM raw_observation WHERE profile=$p ORDER BY received_at,source,source_key
            """, ("p", profile));
        using var reader = cmd.ExecuteReader();
        List<StoredObservation> rows = [];
        while (reader.Read())
            rows.Add(new(reader.GetString(0), reader.GetString(1), OptionalString(reader, 2), reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5), OptionalString(reader, 6)));
        return rows;
    }

    private static string? OptionalString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private void AddProfile(SqliteTransaction tx, Guid profile) =>
        Execute(tx, "INSERT INTO profile VALUES ($p) ON CONFLICT DO NOTHING", ("p", profile));

    private int Execute(SqliteTransaction? tx, string sql, params (string, object?)[] values)
    {
        using var cmd = Command(tx, sql, values);
        return cmd.ExecuteNonQuery();
    }

    private object? Scalar(SqliteTransaction? tx, string sql, params (string, object?)[] values)
    {
        using var cmd = Command(tx, sql, values);
        return cmd.ExecuteScalar();
    }

    private SqliteCommand Command(SqliteTransaction? tx, string sql, params (string, object?)[] values)
    {
        var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in values)
            cmd.Parameters.AddWithValue("$" + name, value is Guid guid ? guid.ToString("D") : value ?? DBNull.Value);
        return cmd;
    }

    public void Dispose() => db.Dispose();
}
