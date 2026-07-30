using System.Text.Json;
using SnippetForge.Remote;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>Parsing du protocole NuGet v3, testé sans réseau sur des documents réels.</summary>
public sealed class NuGetV3Tests
{
    private const string ServiceIndex = """
        {
          "version": "3.0.0",
          "resources": [
            { "@id": "https://nuget.local/v3/search", "@type": "SearchQueryService" },
            { "@id": "https://nuget.local/v3/flat2/", "@type": "PackageBaseAddress/3.0.0" },
            { "@id": "https://nuget.local/v3/registration/", "@type": "RegistrationsBaseUrl" }
          ]
        }
        """;

    [Fact]
    public void ParsePackageBaseAddress_TrouveLaRessourceParmiDautres() =>
        Assert.Equal("https://nuget.local/v3/flat2/", NuGetV3.ParsePackageBaseAddress(ServiceIndex));

    [Fact]
    public void ParsePackageBaseAddress_SansRessource_RetourneNull() =>
        Assert.Null(NuGetV3.ParsePackageBaseAddress("""{ "resources": [] }"""));

    [Fact]
    public void ParsePackageBaseAddress_JsonInvalide_LeveJsonException() =>
        Assert.ThrowsAny<JsonException>(() => NuGetV3.ParsePackageBaseAddress("pas du json"));

    [Fact]
    public void ParseVersions_LitLaListe() =>
        Assert.Equal(
            ["1.0.0", "1.1.0"],
            NuGetV3.ParseVersions("""{ "versions": ["1.0.0", "1.1.0"] }"""));

    [Fact]
    public void ParseVersions_DocumentVide_RetourneVide() =>
        Assert.Empty(NuGetV3.ParseVersions("{}"));

    [Fact]
    public void VersionIndexUrl_MetLIdentifiantEnMinuscules() =>
        Assert.Equal(
            "https://nuget.local/v3/flat2/micro.flow.retry/index.json",
            NuGetV3.VersionIndexUrl("https://nuget.local/v3/flat2/", "Micro.Flow.Retry"));

    [Fact]
    public void DownloadUrl_SuitLaConventionFlatContainer() =>
        Assert.Equal(
            "https://nuget.local/v3/flat2/micro.flow.retry/1.0.1/micro.flow.retry.1.0.1.nupkg",
            NuGetV3.DownloadUrl("https://nuget.local/v3/flat2", "Micro.Flow.Retry", "1.0.1"));
}

public sealed class FeedMirrorTests
{
    private static string CreateRemote(params (string Name, string Content)[] artifacts)
    {
        var dir = Path.Combine(Path.GetTempPath(), "microforge-remote", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (name, content) in artifacts)
        {
            File.WriteAllText(Path.Combine(dir, name), content);
        }

        return dir;
    }

    [Theory]
    [InlineData(@"C:\partage\feed", true)]
    [InlineData(@"\\serveur\forge\feed", true)]
    [InlineData("https://nuget.local/v3/index.json", false)]
    [InlineData("http://nuget.local/v3/index.json", false)]
    public void IsFolderSource_DistingueDossierEtHttp(string source, bool expected) =>
        Assert.Equal(expected, FeedMirror.IsFolderSource(source));

    [Fact]
    public void PullFromFolder_RapatrieToutCeQuiManque()
    {
        using var forge = TempForge.Create();
        var remote = CreateRemote(
            ("Micro.A.One.1.0.0.nupkg", "a"),
            ("Micro.B.Two.1.0.0.nupkg", "b"));
        try
        {
            var result = FeedMirror.PullFromFolder(forge.Root, remote, packageId: null);

            Assert.Equal(2, result.Downloaded.Count);
            Assert.Empty(result.AlreadyPresent);
            Assert.True(File.Exists(Path.Combine(forge.Path, "feed", "Micro.A.One.1.0.0.nupkg")));
        }
        finally
        {
            Directory.Delete(remote, recursive: true);
        }
    }

    [Fact]
    public void PullFromFolder_FiltreParPackage()
    {
        using var forge = TempForge.Create();
        var remote = CreateRemote(
            ("Micro.A.One.1.0.0.nupkg", "a"),
            ("Micro.B.Two.1.0.0.nupkg", "b"));
        try
        {
            var result = FeedMirror.PullFromFolder(forge.Root, remote, "Micro.A.One");

            Assert.Equal(["Micro.A.One.1.0.0.nupkg"], result.Downloaded);
        }
        finally
        {
            Directory.Delete(remote, recursive: true);
        }
    }

    [Fact]
    public void PullFromFolder_NeReecritJamaisUnArtefactPresent()
    {
        // Immutabilité (R8) : même si le distant diverge, le local fait foi.
        using var forge = TempForge.Create();
        var local = Path.Combine(forge.Path, "feed", "Micro.A.One.1.0.0.nupkg");
        File.WriteAllText(local, "version locale");
        var remote = CreateRemote(("Micro.A.One.1.0.0.nupkg", "version distante divergente"));
        try
        {
            var result = FeedMirror.PullFromFolder(forge.Root, remote, null);

            Assert.Empty(result.Downloaded);
            Assert.Single(result.AlreadyPresent);
            Assert.Equal("version locale", File.ReadAllText(local));
        }
        finally
        {
            Directory.Delete(remote, recursive: true);
        }
    }

    [Fact]
    public void PullFromFolder_EnregistreLesEmpreintes()
    {
        using var forge = TempForge.Create();
        var remote = CreateRemote(("Micro.A.One.1.0.0.nupkg", "a"));
        try
        {
            FeedMirror.PullFromFolder(forge.Root, remote, null);

            var ledger = Integrity.ArtifactLedger.Load(forge.Root);
            Assert.Empty(ledger.Verify(forge.Root));
            Assert.Single(ledger.Hashes);
        }
        finally
        {
            Directory.Delete(remote, recursive: true);
        }
    }

    [Fact]
    public void PullFromFolder_SourceInexistante_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        Assert.Throws<ArgumentException>(() => FeedMirror.PullFromFolder(
            forge.Root, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), null));
    }
}
