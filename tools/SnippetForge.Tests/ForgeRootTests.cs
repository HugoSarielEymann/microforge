using Xunit;

namespace SnippetForge.Tests;

public sealed class ForgeRootTests
{
    [Fact]
    public void At_RacineValide_ExposeLesCheminsCanoniques()
    {
        using var forge = TempForge.Create();
        var root = ForgeRoot.At(forge.Path);

        Assert.Equal(Path.Combine(forge.Path, "feed"), root.FeedDir);
        Assert.Equal(Path.Combine(forge.Path, "registry"), root.RegistryDir);
        Assert.Equal(Path.Combine(forge.Path, "packages"), root.PackagesDir);
        Assert.Equal(Path.Combine(forge.Path, "registry", "index.json"), root.IndexFile);
    }

    [Fact]
    public void At_DossierSansRulesMd_LeveArgumentException()
    {
        var directory = Path.Combine(Path.GetTempPath(), "microforge-invalid", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "feed"));
        try
        {
            Assert.Throws<ArgumentException>(() => ForgeRoot.At(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void At_CheminInexistant_LeveArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            ForgeRoot.At(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    [Fact]
    public void At_CheminVide_LeveArgumentException() =>
        Assert.Throws<ArgumentException>(() => ForgeRoot.At("  "));

    [Fact]
    public void At_NEcritRienDansLEnvironnementDuProcessus()
    {
        // Garde-fou : la résolution explicite ne doit jamais toucher à l'état global,
        // sous peine de rendre les tests parallèles indéterministes.
        var before = Environment.GetEnvironmentVariable("MICROFORGE_ROOT");
        using var forge = TempForge.Create();
        _ = ForgeRoot.At(forge.Path);

        Assert.Equal(before, Environment.GetEnvironmentVariable("MICROFORGE_ROOT"));
    }
}
