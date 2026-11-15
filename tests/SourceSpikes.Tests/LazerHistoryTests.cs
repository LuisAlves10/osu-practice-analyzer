using System.Security.Cryptography;
using System.Text;

namespace SourceSpikes.Tests;

public sealed class LazerHistoryTests
{
    // Synthetic maps and header envelopes. The test payload does not contain playable frames.
    [Fact]
    public void SameIdAndDifficultyNameDoNotMergeEditedContent()
    {
        var first = Assert.IsType<MapVersion>(LazerHistory.ReadMap(Map("64")));
        var edit = Assert.IsType<MapVersion>(LazerHistory.ReadMap(Map("96")));
        Assert.Equal(101, first.BeatmapId);
        Assert.Equal(100, first.SetId);
        Assert.Equal(first.BeatmapId, edit.BeatmapId);
        Assert.NotEqual(first.Sha256, edit.Sha256);
        Assert.NotEqual(first.Md5, edit.Md5);
    }

    [Fact]
    public void TruncatedReplayAndInvalidMapChecksumAreRejected()
    {
        var map = Assert.IsType<MapVersion>(LazerHistory.ReadMap(Map("64")));
        byte[] replay = Replay(map.Md5, 1);
        Assert.Equal(map.Md5, LazerHistory.ReadReplay(replay)?.BeatmapMd5);
        Assert.Null(LazerHistory.ReadReplay(replay[..^1]));
        replay[7] = (byte)'z';
        Assert.Null(LazerHistory.ReadReplay(replay));
    }

    [Fact]
    public void UnreviewedReplayFormatIsRejected()
    {
        byte[] replay = Replay(new string('a', 32), 1, 30_000_020);
        Assert.Null(LazerHistory.ReadReplay(replay));
    }

    [Fact]
    public void StoreMatchesOnlyVerifiedBytesAndCountsDistinctReplayBlobs()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            byte[] first = Map("64"), edit = Map("96");
            var version = Assert.IsType<MapVersion>(LazerHistory.ReadMap(first));
            Put(root, first);
            Put(root, edit);
            Put(root, Replay(version.Md5, 1));
            Put(root, Replay(version.Md5, 2));
            Put(root, Replay(new string('c', 32), 3));
            Put(root, Map("128"), new string('a', 64));

            var result = LazerHistory.Scan(root);
            Assert.Equal(6, result.Scanned);
            Assert.Equal(2, result.StandardMaps);
            Assert.Equal(1, result.HashMismatches);
            Assert.Equal(3, result.StandardLazerReplayContainers);
            Assert.Equal(2, result.MatchedReplays);
            Assert.Equal(1, result.DistinctMatchedMapVersions);
            Assert.Equal(2, result.LargestReplayCountOnOneMap);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] Map(string x) => Encoding.UTF8.GetBytes($"""
        osu file format v14

        [General]
        Mode: 0

        [Metadata]
        Title: Synthetic map
        Version: Synthetic difficulty
        BeatmapID: 101
        BeatmapSetID: 100

        [HitObjects]
        {x},64,1500,1,0,0:0:0:0:
        """);

    private static byte[] Replay(string mapHash, long id, int format = 30_000_003)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes);
        writer.Write((byte)0);
        writer.Write(format);
        writer.Write((byte)0x0b);
        writer.Write(mapHash);
        writer.Write((byte)0); // No player name.
        writer.Write((byte)0); // No replay checksum.
        writer.Write(new byte[23]);
        writer.Write((byte)0); // No health graph.
        writer.Write(DateTime.UnixEpoch.Ticks);
        writer.Write(13);
        writer.Write(new byte[13]); // Synthetic container placeholders.
        writer.Write(id);
        writer.Write(13);
        writer.Write(new byte[13]);
        return bytes.ToArray();
    }

    private static void Put(string root, byte[] bytes, string? hash = null)
    {
        hash ??= Convert.ToHexStringLower(SHA256.HashData(bytes));
        string bucket = Path.Combine(root, "files", hash[..1], hash[..2]);
        Directory.CreateDirectory(bucket);
        File.WriteAllBytes(Path.Combine(bucket, hash), bytes);
    }
}
