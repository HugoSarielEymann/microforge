using SnippetForge.Metrics;
using Xunit;

namespace SnippetForge.Tests;

public sealed class ConsumerRegistryTests
{
    [Fact]
    public void Register_PuisSaveEtLoad_ConserveLesProjets()
    {
        using var forge = TempForge.Create();
        var projectDir = Path.Combine(forge.Path, "projet");
        Directory.CreateDirectory(projectDir);

        var registry = ConsumerRegistry.Load(forge.Root);
        Assert.True(registry.Register(projectDir));
        registry.Save();

        Assert.Single(ConsumerRegistry.Load(forge.Root).Entries);
    }

    [Fact]
    public void Register_DeuxFois_NeDupliquePas()
    {
        using var forge = TempForge.Create();
        var projectDir = Path.Combine(forge.Path, "projet");
        Directory.CreateDirectory(projectDir);

        var registry = ConsumerRegistry.Load(forge.Root);
        Assert.True(registry.Register(projectDir));
        Assert.False(registry.Register(projectDir));
        Assert.False(registry.Register(projectDir + Path.DirectorySeparatorChar));

        Assert.Single(registry.Entries);
    }

    [Fact]
    public void PruneMissing_RetireLesProjetsSupprimes()
    {
        using var forge = TempForge.Create();
        var present = Path.Combine(forge.Path, "present");
        var absent = Path.Combine(forge.Path, "absent");
        Directory.CreateDirectory(present);
        Directory.CreateDirectory(absent);

        var registry = ConsumerRegistry.Load(forge.Root);
        registry.Register(present);
        registry.Register(absent);
        Directory.Delete(absent);

        Assert.Equal(1, registry.PruneMissing());
        Assert.Single(registry.Entries);
    }

    [Fact]
    public void ExternalTo_ExclutLesProjetsInternesALaForge()
    {
        using var forge = TempForge.Create();
        var interne = Path.Combine(forge.Path, "demo");
        var externe = Path.Combine(Path.GetTempPath(), "microforge-externe", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(interne);
        Directory.CreateDirectory(externe);

        try
        {
            var registry = ConsumerRegistry.Load(forge.Root);
            registry.Register(interne);
            registry.Register(externe);

            var external = registry.ExternalTo(forge.Root);

            Assert.Equal(2, registry.Entries.Count);
            Assert.Equal(Path.GetFullPath(externe), Assert.Single(external).Path);
        }
        finally
        {
            Directory.Delete(externe, recursive: true);
        }
    }

    [Theory]
    [InlineData(@"C:\forge", @"C:\forge\demo", true)]
    [InlineData(@"C:\forge", @"C:\forge", true)]
    [InlineData(@"C:\forge", @"C:\forge\a\b\c", true)]
    [InlineData(@"C:\forge", @"C:\autre\projet", false)]
    [InlineData(@"C:\forge", @"C:\forgerie", false)]
    public void IsInside_DistingueLesSousDossiersDesHomonymes(string parent, string candidate, bool expected) =>
        Assert.Equal(expected, ConsumerRegistry.IsInside(parent, candidate));

    /// <summary>
    /// Régression : « forge init ~/.claude --no-nuget-config » enregistrait ce dossier
    /// comme consommateur, alors qu'il ne peut par construction référencer aucun
    /// package. Le bilan annonçait « 6 raccordés, dont 4 référencent ».
    /// </summary>
    [Fact]
    public void Register_NEstPasAppeleSansSourceNuGet_LeDenominateurResteJuste()
    {
        using var forge = TempForge.Create();
        var dossierSansProjet = Path.Combine(forge.Path, "instructions-seules");
        Directory.CreateDirectory(dossierSansProjet);

        var registry = ConsumerRegistry.Load(forge.Root);

        // Le comportement testé côté commande : ne pas appeler Register du tout.
        Assert.Empty(registry.Entries);
        Assert.Empty(registry.ExternalTo(forge.Root));
    }

    [Fact]
    public void Load_FichierCorrompu_RepartDUnRegistreVide()
    {
        using var forge = TempForge.Create();
        File.WriteAllText(Path.Combine(forge.Path, "registry", "consumers.json"), "{ pas du JSON");

        Assert.Empty(ConsumerRegistry.Load(forge.Root).Entries);
    }

    [Fact]
    public void Register_CheminVide_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        Assert.Throws<ArgumentException>(() => ConsumerRegistry.Load(forge.Root).Register("  "));
    }
}

public sealed class FootprintScannerTests
{
    [Fact]
    public void Scan_CompteSourcesTestsEtDocumentation()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage(
            "Micro.A.One",
            sourceCode: "line1\n\nline2\nline3",
            testCode: "t1\nt2");

        var footprint = FootprintScanner.Scan(directory)!;

        Assert.Equal("Micro.A.One", footprint.PackageId);
        Assert.Equal(3, footprint.SourceLines);
        Assert.Equal(2, footprint.TestLines);
        Assert.True(footprint.DocumentationLines > 0);
    }

    [Fact]
    public void Scan_IgnoreLesLignesVides()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.A.One", sourceCode: "a\n\n\n   \n\nb");

        Assert.Equal(2, FootprintScanner.Scan(directory)!.SourceLines);
    }

    [Fact]
    public void Scan_IgnoreLesArtefactsDeBuild()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.A.One", sourceCode: "seule ligne");
        var objDir = Path.Combine(directory, "src", "obj", "Debug");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "Generated.cs"), string.Join('\n', Enumerable.Repeat("bruit", 500)));

        Assert.Equal(1, FootprintScanner.Scan(directory)!.SourceLines);
    }

    [Fact]
    public void Scan_DossierSansSrc_RetourneNull()
    {
        using var forge = TempForge.Create();
        var directory = Path.Combine(forge.Path, "packages", "PasUnPackage");
        Directory.CreateDirectory(directory);

        Assert.Null(FootprintScanner.Scan(directory));
    }

    [Fact]
    public void ScanAll_MesureTousLesPackages()
    {
        using var forge = TempForge.Create();
        forge.WritePackage("Micro.A.One");
        forge.WritePackage("Micro.B.Two");

        Assert.Equal(2, FootprintScanner.ScanAll(forge.Root).Count);
    }

    [Fact]
    public void ScanAll_BibliothequeVide_RetourneListeVide()
    {
        using var forge = TempForge.Create();
        Assert.Empty(FootprintScanner.ScanAll(forge.Root));
    }
}
