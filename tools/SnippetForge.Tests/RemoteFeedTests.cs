using SnippetForge.Remote;
using Xunit;

namespace SnippetForge.Tests;

public sealed class RemoteFeedTests
{
    private const string Source = "https://nuget.interne.local/v3/index.json";

    [Fact]
    public void SaveePuisLoad_ConserveLaConfiguration()
    {
        using var forge = TempForge.Create();

        RemoteFeed.Save(forge.Root, new RemoteFeedConfig(Source, "MA_CLE"));
        var loaded = RemoteFeed.Load(forge.Root)!;

        Assert.Equal(Source, loaded.Source);
        Assert.Equal("MA_CLE", loaded.ApiKeyVariable);
    }

    [Fact]
    public void Load_SansConfiguration_RetourneNull()
    {
        using var forge = TempForge.Create();
        Assert.Null(RemoteFeed.Load(forge.Root));
    }

    [Fact]
    public void Load_FichierCorrompu_RetourneNull()
    {
        using var forge = TempForge.Create();
        File.WriteAllText(RemoteFeed.ConfigPath(forge.Root), "{ pas du JSON");

        Assert.Null(RemoteFeed.Load(forge.Root));
    }

    [Fact]
    public void VariableDeCle_ADefautStandard() =>
        Assert.Equal(RemoteFeedConfig.DefaultApiKeyVariable, new RemoteFeedConfig(Source, null).ApiKeyVariable);

    /// <summary>
    /// La clé d'API ne doit jamais être écrite dans le registre : seul le nom de la
    /// variable d'environnement qui la porte est persisté.
    /// </summary>
    [Fact]
    public void LaCleNEstJamaisEcriteSurDisque()
    {
        using var forge = TempForge.Create();
        RemoteFeed.Save(forge.Root, new RemoteFeedConfig(Source, "MA_CLE"));

        var content = File.ReadAllText(RemoteFeed.ConfigPath(forge.Root));

        Assert.Contains("MA_CLE", content, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey\":", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPushArguments_EncadreLesCheminsEtEviteLesDoublons()
    {
        var arguments = RemoteFeed.BuildPushArguments(
            @"C:\dossier avec espaces\p.1.0.0.nupkg", new RemoteFeedConfig(Source, null), "secret");

        Assert.Contains(@"""C:\dossier avec espaces\p.1.0.0.nupkg""", arguments, StringComparison.Ordinal);
        Assert.Contains($"--source \"{Source}\"", arguments, StringComparison.Ordinal);
        Assert.Contains("--skip-duplicate", arguments, StringComparison.Ordinal);
        Assert.Contains("--api-key secret", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPushArguments_SansCle_NAjoutePasLOption()
    {
        var arguments = RemoteFeed.BuildPushArguments("p.nupkg", new RemoteFeedConfig(Source, null), null);
        Assert.DoesNotContain("--api-key", arguments, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "(aucune)")]
    [InlineData("", "(aucune)")]
    [InlineData("abc", "****")]
    [InlineData("0123456789abcdef", "****cdef")]
    public void MaskApiKey_NeRevelePasLaCle(string? key, string expected) =>
        Assert.Equal(expected, RemoteFeed.MaskApiKey(key));

    [Fact]
    public void Save_SourceVide_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        Assert.Throws<ArgumentException>(() => RemoteFeed.Save(forge.Root, new RemoteFeedConfig("  ", null)));
    }
}
