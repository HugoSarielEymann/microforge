using SnippetForge.Telemetry;
using Xunit;

namespace SnippetForge.Tests;

public sealed class UsageLogTests
{
    [Fact]
    public void Append_PuisReadSince_RestitueLesInvocations()
    {
        using var forge = TempForge.Create();

        UsageLog.Append(forge.Root, "search", ["retry", "http"], 0);
        UsageLog.Append(forge.Root, "publish", ["Micro.A.One"], 1);

        var entries = UsageLog.ReadSince(forge.Root, 0);

        Assert.Equal(2, entries.Count);
        Assert.Equal("search", entries[0].Command);
        Assert.Equal("retry http", entries[0].Arguments);
        Assert.Equal(0, entries[0].ExitCode);
        Assert.Equal(1, entries[1].ExitCode);
    }

    [Fact]
    public void ReadSince_AvecSignet_NeRelitPasLePasse()
    {
        using var forge = TempForge.Create();
        UsageLog.Append(forge.Root, "list", [], 0);

        var bookmark = UsageLog.CurrentPosition(forge.Root);
        UsageLog.Append(forge.Root, "search", ["slug"], 0);

        var entry = Assert.Single(UsageLog.ReadSince(forge.Root, bookmark));
        Assert.Equal("search", entry.Command);
    }

    [Fact]
    public void ReadSince_JournalAbsent_RetourneVide()
    {
        using var forge = TempForge.Create();
        Assert.Empty(UsageLog.ReadSince(forge.Root, 0));
        Assert.Equal(0, UsageLog.CurrentPosition(forge.Root));
    }

    [Fact]
    public void ReadSince_LigneCorrompue_EstIgnoreeSansEchouer()
    {
        using var forge = TempForge.Create();
        UsageLog.Append(forge.Root, "list", [], 0);
        File.AppendAllText(UsageLog.PathFor(forge.Root), "{ tronqué" + Environment.NewLine);
        UsageLog.Append(forge.Root, "stats", [], 0);

        Assert.Equal(2, UsageLog.ReadSince(forge.Root, 0).Count);
    }

    [Fact]
    public void Append_HorodateEnUtc()
    {
        using var forge = TempForge.Create();
        var before = DateTime.UtcNow.AddSeconds(-1);

        UsageLog.Append(forge.Root, "list", [], 0);

        var entry = Assert.Single(UsageLog.ReadSince(forge.Root, 0));
        Assert.InRange(entry.TimestampUtc, before, DateTime.UtcNow.AddSeconds(1));
    }
}
