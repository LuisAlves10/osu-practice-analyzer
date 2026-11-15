using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;

namespace SourceSpikes;

public sealed record MapVersion(int Format, int Ruleset, int? BeatmapId, int? SetId, string Md5, string Sha256);
public sealed record ReplayHeader(int Format, string BeatmapMd5);
public sealed record HistoryEvidence(int Scanned, bool ScanLimitReached, int Unreadable, int HashMismatches,
    int StandardMaps, int StandardLazerReplayContainers, int MatchedReplays, int AmbiguousReplays,
    int DistinctMatchedMapVersions, int MatchedReplaysWithDeclaredMapId, int LargestReplayCountOnOneMap);

public static class LazerHistory
{
    private const int MaxFileBytes = 16 * 1024 * 1024;
    private const int MaxBlobs = 10_000;
    private const int MinExtendedReplayVersion = 30_000_001;
    private const int MaxReviewedReplayVersion = 30_000_019;
    private static ReadOnlySpan<byte> MapPrefix => "osu file format v"u8;
    private static ReadOnlySpan<byte> Utf8Bom => [0xef, 0xbb, 0xbf];

    public static HistoryEvidence Scan(string dataRoot)
    {
        string store = Path.Combine(dataRoot, "files");
        if (!Directory.Exists(store) || File.GetAttributes(store).HasFlag(FileAttributes.ReparsePoint))
            throw new DirectoryNotFoundException();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 2,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        List<MapVersion> maps = [];
        List<ReplayHeader> replays = [];
        int scanned = 0, unreadable = 0, mismatches = 0;
        bool limited = false;
        Span<byte> prefix = stackalloc byte[32];

        foreach (string path in Directory.EnumerateFiles(store, "*", options))
        {
            string sha256 = Path.GetFileName(path);
            if (!IsStorePath(store, path, sha256))
                continue;
            if (scanned == MaxBlobs)
            {
                limited = true;
                break;
            }
            scanned++;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                long length = stream.Length;
                if (length is < 64 or > MaxFileBytes)
                    continue;

                stream.ReadExactly(prefix);
                bool isMap = TrimBom(prefix).StartsWith(MapPrefix);
                if (!isMap && !LooksLikeReplay(prefix))
                    continue;

                stream.Position = 0;
                byte[] bytes = new byte[(int)length];
                stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1 || !sha256.Equals(Hash(bytes), StringComparison.Ordinal))
                {
                    mismatches++;
                    continue;
                }

                if (isMap && ReadMap(bytes) is { Ruleset: 0 } map)
                    maps.Add(map);
                else if (!isMap && ReadReplay(bytes) is { } replay)
                    replays.Add(replay);
            }
            catch (IOException)
            {
                unreadable++;
            }
            catch (UnauthorizedAccessException)
            {
                unreadable++;
            }
        }

        var mapsByMd5 = maps.ToLookup(map => map.Md5, StringComparer.Ordinal);
        var pairs = replays.GroupBy(replay => replay.BeatmapMd5, StringComparer.Ordinal)
            .Select(group => (Maps: mapsByMd5[group.Key].ToArray(), Replays: group.Count())).ToArray();
        var unique = pairs.Where(group => group.Maps.Length == 1).ToArray();
        return new HistoryEvidence(scanned, limited, unreadable, mismatches, maps.Count, replays.Count,
            unique.Sum(group => group.Replays), pairs.Where(group => group.Maps.Length > 1).Sum(group => group.Replays),
            unique.Length, unique.Where(group => group.Maps[0].BeatmapId.HasValue).Sum(group => group.Replays),
            unique.Length == 0 ? 0 : unique.Max(group => group.Replays));
    }

    private static bool IsStorePath(string store, string path, string hash)
    {
        if (hash.Length != 64 || !hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            return false;
        string expected = Path.Combine(hash[..1], hash[..2], hash);
        return Path.GetRelativePath(store, path).Equals(expected, StringComparison.Ordinal);
    }

    public static MapVersion? ReadMap(byte[] bytes)
    {
        ReadOnlySpan<byte> content = TrimBom(bytes);
        if (!content.StartsWith(MapPrefix))
            return null;

        using var reader = new StringReader(Encoding.UTF8.GetString(content));
        string header = reader.ReadLine()!;
        if (!int.TryParse(header.AsSpan(MapPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int format)
            || format < 1)
            return null;

        string section = "";
        int ruleset = 0;
        int? mapId = null, setId = null;
        bool hasMetadata = false, hasDifficultyName = false;
        while (reader.ReadLine() is { } rawLine)
        {
            string line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line;
                hasMetadata |= section == "[Metadata]";
                if (section == "[HitObjects]")
                    break;
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon < 0)
                continue;
            string key = line[..colon].Trim();
            ReadOnlySpan<char> value = line.AsSpan(colon + 1).Trim();
            if (section == "[General]" && key == "Mode"
                && (!TryInt(value, out ruleset) || ruleset is < 0 or > 3))
                return null;
            if (section != "[Metadata]")
                continue;

            switch (key)
            {
                case "Version":
                    hasDifficultyName = !value.IsEmpty;
                    break;
                case "BeatmapID":
                    mapId = PositiveId(value);
                    break;
                case "BeatmapSetID":
                    setId = PositiveId(value);
                    break;
            }
        }

        if (!hasMetadata || !hasDifficultyName || section != "[HitObjects]")
            return null;
        return new MapVersion(format, ruleset, mapId, setId,
            Convert.ToHexStringLower(MD5.HashData(bytes)), Hash(bytes));
    }

    public static ReplayHeader? ReadReplay(byte[] bytes)
    {
        const int checksumStart = 7, checksumLength = 32;
        if (bytes.Length < checksumStart + checksumLength || !LooksLikeReplay(bytes))
            return null;

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);
        try
        {
            reader.ReadByte();
            int format = reader.ReadInt32();
            SkipBytes(reader, 2); // String marker and the checksum's fixed length.
            string md5 = Convert.ToHexStringLower(Convert.FromHexString(Encoding.ASCII.GetString(reader.ReadBytes(checksumLength))));
            SkipString(reader, 512); // Player name. Leave the bytes unread.
            SkipString(reader, 128); // Replay checksum, a different field from the map checksum.
            SkipBytes(reader, 23); // Hit counts, score, combo, perfect flag, and legacy mod mask.
            SkipString(reader, 128 * 1024); // Health graph.
            long ticks = reader.ReadInt64();
            if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks)
                return null;
            SkipLzmaBlock(reader);
            reader.ReadInt64(); // Legacy score ID.
            SkipLzmaBlock(reader); // Extended lazer score block.
            return stream.Position == stream.Length ? new ReplayHeader(format, md5) : null;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or FormatException)
        {
            return null;
        }
    }

    private static void SkipString(BinaryReader reader, int maximum)
    {
        byte marker = reader.ReadByte();
        if (marker == 0)
            return;
        if (marker != 0x0b)
            throw new InvalidDataException();
        int length = reader.Read7BitEncodedInt();
        if (length > maximum)
            throw new InvalidDataException();
        SkipBytes(reader, length);
    }

    private static void SkipLzmaBlock(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 13 || reader.ReadByte() >= 225)
            throw new InvalidDataException();
        SkipBytes(reader, length - 1);
    }

    private static void SkipBytes(BinaryReader reader, int count)
    {
        Stream stream = reader.BaseStream;
        if (count < 0 || count > stream.Length - stream.Position)
            throw new InvalidDataException();
        stream.Position += count;
    }

    private static bool LooksLikeReplay(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 7 && bytes[0] == 0
        && BinaryPrimitives.ReadInt32LittleEndian(bytes[1..]) is >= MinExtendedReplayVersion and <= MaxReviewedReplayVersion
        && bytes[5] == 0x0b && bytes[6] == 32;

    private static ReadOnlySpan<byte> TrimBom(ReadOnlySpan<byte> bytes) => bytes.StartsWith(Utf8Bom) ? bytes[3..] : bytes;

    private static bool TryInt(ReadOnlySpan<char> value, out int parsed) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);

    private static int? PositiveId(ReadOnlySpan<char> value) => TryInt(value, out int id) && id > 0 ? id : null;

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
