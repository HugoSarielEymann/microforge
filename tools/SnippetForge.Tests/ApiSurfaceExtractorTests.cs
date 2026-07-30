using SnippetForge.Api;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// L'extraction est exercée sur une assembly réellement compilée (Micro.Text.Slugify,
/// référencée par ce projet de test), pas sur une chaîne de caractères.
/// </summary>
public sealed class ApiSurfaceExtractorTests
{
    private static string SlugifyAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "Micro.Text.Slugify.dll");

    [Fact]
    public void FromAssemblyFile_ExtraitLesTypesPublics()
    {
        var surface = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");

        Assert.Contains("type Micro.Text.Slugify.Slugifier", surface.Members, StringComparer.Ordinal);
        Assert.Contains("type Micro.Text.Slugify.SlugifyOptions", surface.Members, StringComparer.Ordinal);
    }

    [Fact]
    public void FromAssemblyFile_ExtraitLesMethodesAvecSignature()
    {
        var surface = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");

        Assert.Contains(
            surface.Members,
            m => m.StartsWith("method Micro.Text.Slugify.Slugifier.ToSlug(", StringComparison.Ordinal) &&
                 m.EndsWith(" : System.String", StringComparison.Ordinal));
    }

    [Fact]
    public void FromAssemblyFile_ExtraitLesProprietesAvecAccesseurs()
    {
        var surface = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");

        Assert.Contains(
            surface.Members,
            m => m.StartsWith("property Micro.Text.Slugify.SlugifyOptions.Separator", StringComparison.Ordinal) &&
                 m.Contains("get;", StringComparison.Ordinal));
    }

    [Fact]
    public void FromAssemblyFile_NExposePasLesMembresPrives()
    {
        var surface = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");

        Assert.DoesNotContain(surface.Members, m => m.Contains("TrimTrailingSeparator", StringComparison.Ordinal));
        Assert.DoesNotContain(surface.Members, m => m.Contains(".Normalize(", StringComparison.Ordinal));
    }

    [Fact]
    public void FromAssemblyFile_EstDeterministeEtTrie()
    {
        var first = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");
        var second = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");

        Assert.Equal(first.Members, second.Members);
        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(first.Members.OrderBy(m => m, StringComparer.Ordinal), first.Members);
    }

    [Fact]
    public void FromAssemblyFile_ComparaisonAvecSoiMeme_NeDetectaAucuneRupture()
    {
        var surface = ApiSurfaceExtractor.FromAssemblyFile(SlugifyAssemblyPath, "Micro.Text.Slugify", "1.0.0");
        var diff = ApiDiff.Between(surface, surface);

        Assert.True(diff.IsIdentical);
        Assert.Equal(BumpLevel.Patch, diff.RequiredBump);
    }

    [Fact]
    public void FromAssemblyFile_AssemblyIsoleeAvecDependance_ResoutViaLeCacheNuGet()
    {
        // Reproduit la situation réelle : le .dll extrait d'un .nupkg est seul dans un
        // dossier temporaire, sans ses dépendances à côté. Micro.Flow.Retry référence
        // Microsoft.Extensions.Logging.Abstractions dans sa signature publique.
        var isolated = Path.Combine(Path.GetTempPath(), "microforge-isolated", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        try
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Micro.Flow.Retry.dll");
            var target = Path.Combine(isolated, "Micro.Flow.Retry.dll");
            File.Copy(source, target);

            var surface = ApiSurfaceExtractor.FromAssemblyFile(target, "Micro.Flow.Retry", "1.0.0");

            Assert.Contains("type Micro.Flow.Retry.RetryExecutor", surface.Members, StringComparer.Ordinal);
            Assert.Contains(
                surface.Members,
                m => m.Contains("ExecuteAsync", StringComparison.Ordinal) &&
                     m.Contains("Microsoft.Extensions.Logging.ILogger", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(isolated, recursive: true);
        }
    }

    [Fact]
    public void FromPackage_FichierInexistant_Leve() =>
        Assert.ThrowsAny<Exception>(() =>
            ApiSurfaceExtractor.FromPackage(
                Path.Combine(Path.GetTempPath(), "inexistant.nupkg"), "Micro.X.Y", "1.0.0"));
}
