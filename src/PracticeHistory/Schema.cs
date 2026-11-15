namespace PracticeHistory;

internal static class Schema
{
    internal static readonly string[] Migrations =
    [
        """
        CREATE TABLE profile (
            id TEXT PRIMARY KEY
        ) STRICT;
        CREATE TABLE raw_asset (
            profile TEXT NOT NULL REFERENCES profile(id),
            hash TEXT NOT NULL,
            bytes BLOB NOT NULL,
            imported_at TEXT NOT NULL,
            PRIMARY KEY(profile,hash)
        ) STRICT;
        CREATE TABLE map_version (
            profile TEXT NOT NULL,
            hash TEXT NOT NULL,
            md5 TEXT NOT NULL,
            ruleset INTEGER NOT NULL,
            declared_map_id INTEGER,
            declared_set_id INTEGER,
            PRIMARY KEY(profile,hash),
            FOREIGN KEY(profile,hash) REFERENCES raw_asset(profile,hash)
        ) STRICT;
        CREATE TABLE replay_asset (
            profile TEXT NOT NULL,
            hash TEXT NOT NULL,
            map_md5 TEXT NOT NULL,
            format INTEGER NOT NULL,
            PRIMARY KEY(profile,hash),
            FOREIGN KEY(profile,hash) REFERENCES raw_asset(profile,hash)
        ) STRICT;
        CREATE TABLE attempt (
            profile TEXT NOT NULL REFERENCES profile(id),
            id TEXT NOT NULL,
            source TEXT NOT NULL,
            source_key TEXT NOT NULL,
            replay TEXT,
            PRIMARY KEY(profile,id),
            UNIQUE(profile,source,source_key),
            FOREIGN KEY(profile,replay) REFERENCES replay_asset(profile,hash)
        ) STRICT;
        CREATE TABLE raw_observation (
            profile TEXT NOT NULL,
            source TEXT NOT NULL,
            source_key TEXT NOT NULL,
            attempt TEXT,
            received_at TEXT NOT NULL,
            version INTEGER NOT NULL CHECK(version > 0),
            payload TEXT NOT NULL CHECK(json_valid(payload)),
            outcome_fact TEXT CHECK(outcome_fact IN ('PASS','FAIL','RESTART','QUIT')),
            accuracy REAL CHECK(accuracy BETWEEN 0 AND 1),
            is_score INTEGER NOT NULL CHECK(is_score IN (0,1)),
            PRIMARY KEY(profile,source,source_key),
            FOREIGN KEY(profile) REFERENCES profile(id),
            FOREIGN KEY(profile,attempt) REFERENCES attempt(profile,id)
        ) STRICT;
        CREATE TABLE saved_score (
            profile TEXT NOT NULL REFERENCES profile(id),
            source TEXT NOT NULL,
            source_key TEXT NOT NULL,
            attempt TEXT,
            PRIMARY KEY(profile,source,source_key),
            FOREIGN KEY(profile,source,source_key) REFERENCES raw_observation(profile,source,source_key),
            FOREIGN KEY(profile,attempt) REFERENCES attempt(profile,id)
        ) STRICT;
        """,
        """
        CREATE INDEX map_version_checksum ON map_version(profile,ruleset,md5);
        CREATE INDEX evidence_by_attempt ON raw_observation(profile,attempt);
        CREATE VIEW replay_links AS
        SELECT r.profile, r.hash AS replay, CASE WHEN COUNT(m.hash)=1 THEN MIN(m.hash) END AS map_version,
            CASE COUNT(m.hash) WHEN 0 THEN 'missing' WHEN 1 THEN 'linked' ELSE 'ambiguous' END AS link_status
        FROM replay_asset r LEFT JOIN map_version m
        ON m.profile=r.profile AND m.md5=r.map_md5 AND m.ruleset=0
        GROUP BY r.profile,r.hash;
        CREATE VIEW classified_attempt AS
        SELECT a.*, CASE WHEN COUNT(DISTINCT e.outcome_fact)=1 THEN MIN(e.outcome_fact)
            ELSE 'UNKNOWN' END AS outcome, 1 AS classifier_version,
            CASE WHEN COUNT(DISTINCT e.accuracy)=1 THEN MIN(e.accuracy) END AS accuracy
        FROM attempt a LEFT JOIN raw_observation e ON e.profile=a.profile AND e.attempt=a.id
        GROUP BY a.profile,a.id;
        CREATE VIEW performance_attempt AS
        SELECT * FROM classified_attempt WHERE outcome IN ('PASS','FAIL');
        """
    ];
}
