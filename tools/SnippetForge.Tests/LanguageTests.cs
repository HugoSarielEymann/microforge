using SnippetForge.Languages;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Deux profils coexistent. La règle qui les rend honnêtes : un écosystème incapable
/// de prouver l'absence de rupture ne doit jamais laisser croire qu'il l'a prouvée.
/// </summary>
public sealed class LanguageProfileTests
{
    [Fact]
    public void CSharpSeulOffreLeProfilVerifie()
    {
        Assert.True(LanguageProfiles.CSharp.IsVerifiedProfile);
        Assert.All(
            LanguageProfiles.All.Where(p => p.Id != "csharp"),
            p => Assert.False(p.IsVerifiedProfile, $"{p.Id} ne devrait pas prétendre au profil vérifié"));
    }

    [Fact]
    public void LesGarantiesSontEnoncees()
    {
        Assert.Contains("SemVer opposable", LanguageProfiles.CSharp.Guarantees, StringComparison.Ordinal);
        Assert.Contains("SemVer déclaratif", LanguageProfiles.Python.Guarantees, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("python")]
    [InlineData("PYTHON")]
    [InlineData(" typescript ")]
    public void Find_EstToleranteALaCasseEtAuxEspaces(string id) =>
        Assert.NotNull(LanguageProfiles.Find(id));

    [Theory]
    [InlineData("cobol")]
    [InlineData("")]
    [InlineData(null)]
    public void Find_LangageInconnu_RetourneNull(string? id) =>
        Assert.Null(LanguageProfiles.Find(id));

    [Fact]
    public void Resolve_RetombeSurCSharp() =>
        Assert.Equal(LanguageProfiles.CSharp, LanguageProfiles.Resolve("inconnu"));

    [Fact]
    public void ChaqueProfilSaitReconnaitreDuCodeEtDesTests() =>
        Assert.All(LanguageProfiles.All, p =>
        {
            Assert.NotEmpty(p.SourceExtensions);
            Assert.NotEmpty(p.TestMarkers);
        });
}

public sealed class PackageManifestTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "microforge-manifest", Guid.NewGuid().ToString("N"));

    public PackageManifestTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void SavePuisLoad_ConserveLesMetadonnees()
    {
        new PackageManifest("Micro.Text.Slug", "1.2.0", "python", "description longue", ["text", "slug", "url"])
            .Save(_directory);

        var relu = PackageManifest.Load(_directory)!;

        Assert.Equal("Micro.Text.Slug", relu.Id);
        Assert.Equal("1.2.0", relu.Version);
        Assert.Equal(LanguageProfiles.Python, relu.Profile);
        Assert.Equal(3, relu.Tags.Count);
    }

    [Fact]
    public void Load_SansManifeste_RetourneNull() =>
        Assert.Null(PackageManifest.Load(_directory));

    [Fact]
    public void Load_ManifesteCorrompu_RetourneNull()
    {
        File.WriteAllText(PackageManifest.PathFor(_directory), "{ pas du JSON");
        Assert.Null(PackageManifest.Load(_directory));
    }

    [Fact]
    public void LangageInconnu_RetombeSurCSharpPlutotQueDePlanter()
    {
        new PackageManifest("Micro.A.One", "1.0.0", "cobol", "d", ["a", "b", "c"]).Save(_directory);

        Assert.Equal(LanguageProfiles.CSharp, PackageManifest.Load(_directory)!.Profile);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}
