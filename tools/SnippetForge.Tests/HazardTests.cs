using SnippetForge.Hazards;
using Xunit;

namespace SnippetForge.Tests;

public sealed class HazardCatalogueTests
{
    [Fact]
    public void CatalogueLivre_CouvreLesModesDeDefaillanceClassiques()
    {
        var catalogue = HazardCatalogue.From(HazardCatalogue.BuiltIn);

        Assert.True(catalogue.Contains("null-input"));
        Assert.True(catalogue.Contains("numeric-overflow"));
        Assert.True(catalogue.Contains("secret-leak"));
        Assert.True(catalogue.Contains("cancellation"));
    }

    [Fact]
    public void ChaqueAleaLivre_ExpliquePourquoiIlMeriteUnTest()
    {
        Assert.All(HazardCatalogue.BuiltIn, hazard =>
        {
            Assert.True(HazardCatalogue.IsValidId(hazard.Id), $"identifiant invalide : {hazard.Id}");
            Assert.NotEmpty(hazard.Description);
            Assert.NotEmpty(hazard.Rationale);
        });
    }

    [Fact]
    public void Load_SansFichier_RetourneLesAleasLivres()
    {
        using var forge = TempForge.Create();
        Assert.Equal(HazardCatalogue.BuiltIn.Count, HazardCatalogue.Load(forge.Root).All.Count);
    }

    [Fact]
    public void AddOrReplace_PuisLoad_ConserveLAleaAjoute()
    {
        using var forge = TempForge.Create();
        var catalogue = HazardCatalogue.Load(forge.Root);

        catalogue.AddOrReplace(forge.Root, new Hazard(
            "leap-second", "Seconde intercalaire.", "Les horloges sautent une seconde.", ["23:59:60"]));

        var reloaded = HazardCatalogue.Load(forge.Root);
        Assert.True(reloaded.Contains("leap-second"));
        Assert.Equal("Seconde intercalaire.", reloaded.Find("leap-second")!.Description);
    }

    [Fact]
    public void AddOrReplace_NePersistePasLesAleasLivresInchanges()
    {
        // Le fichier reste lisible : il ne contient que ce que l'outil ne sait pas déjà.
        using var forge = TempForge.Create();
        var catalogue = HazardCatalogue.Load(forge.Root);

        catalogue.AddOrReplace(forge.Root, new Hazard("leap-second", "d", "r", []));

        var content = File.ReadAllText(Path.Combine(forge.Path, HazardCatalogue.FileName));
        Assert.Contains("leap-second", content, StringComparison.Ordinal);
        Assert.DoesNotContain("null-input", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOrReplace_PeutRedefinirUnAleaLivre()
    {
        using var forge = TempForge.Create();
        var catalogue = HazardCatalogue.Load(forge.Root);

        catalogue.AddOrReplace(forge.Root, new Hazard("null-input", "Définition maison.", "Raison maison.", []));

        Assert.Equal("Définition maison.", HazardCatalogue.Load(forge.Root).Find("null-input")!.Description);
    }

    [Theory]
    [InlineData("null-input", true)]
    [InlineData("overflow2", true)]
    [InlineData("Null-Input", false)]
    [InlineData("null_input", false)]
    [InlineData("-leading", false)]
    [InlineData("trailing-", false)]
    [InlineData("", false)]
    public void IsValidId_ImposeUnSlugMinuscule(string id, bool expected) =>
        Assert.Equal(expected, HazardCatalogue.IsValidId(id));

    [Fact]
    public void AddOrReplace_IdentifiantInvalide_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        var catalogue = HazardCatalogue.Load(forge.Root);

        Assert.Throws<ArgumentException>(() =>
            catalogue.AddOrReplace(forge.Root, new Hazard("Null Input", "d", "r", [])));
    }

    [Fact]
    public void Load_FichierCorrompu_RetombeSurLesAleasLivres()
    {
        // Un catalogue illisible ne doit pas interdire toute publication.
        using var forge = TempForge.Create();
        File.WriteAllText(Path.Combine(forge.Path, HazardCatalogue.FileName), "{ pas du JSON");

        Assert.Equal(HazardCatalogue.BuiltIn.Count, HazardCatalogue.Load(forge.Root).All.Count);
    }
}

public sealed class HazardDeclarationTests
{
    private static HazardCatalogue Catalogue() => HazardCatalogue.From(
    [
        new("null-input", "d", "r", []),
        new("numeric-overflow", "d", "r", []),
    ]);

    [Fact]
    public void Parse_AccepteUnTableauBrut() =>
        Assert.Equal(["null-input", "numeric-overflow"],
            HazardDeclaration.Parse("""["null-input", "numeric-overflow"]"""));

    [Fact]
    public void Parse_AccepteUnObjetAvecPropriete() =>
        Assert.Equal(["null-input"],
            HazardDeclaration.Parse("""{ "hazards": ["null-input"] }"""));

    [Fact]
    public void Parse_DedupliqueEtIgnoreLesVides() =>
        Assert.Equal(["null-input"],
            HazardDeclaration.Parse("""["null-input", "null-input", "", "  "]"""));

    [Fact]
    public void CoveredBy_ExtraitLesTraitsXUnit()
    {
        const string tests = """
            [Fact]
            [Trait("hazard", "numeric-overflow")]
            public void Debordement() { }

            [Theory]
            [Trait( "hazard" , "null-input" )]
            public void Nul() { }
            """;

        var covered = HazardDeclaration.CoveredBy(tests);

        Assert.Contains("numeric-overflow", covered, StringComparer.Ordinal);
        Assert.Contains("null-input", covered, StringComparer.Ordinal);
    }

    [Fact]
    public void CoveredBy_IgnoreLesAutresTraits() =>
        Assert.Empty(HazardDeclaration.CoveredBy("""[Trait("category", "integration")]"""));

    [Fact]
    public void Verify_AleaDeclareEtProuve_NeProduitAucunManquement()
    {
        const string tests = """[Trait("hazard", "null-input")] public void T() { }""";

        Assert.Empty(HazardDeclaration.Verify(["null-input"], tests, Catalogue()));
    }

    [Fact]
    public void Verify_AleaDeclareSansTestMarque_EstUnManquement()
    {
        // Déclarer sans prouver serait pire que ne rien déclarer : cela ferait croire
        // le cas couvert.
        var gap = Assert.Single(HazardDeclaration.Verify(["null-input"], "public void T() { }", Catalogue()));

        Assert.Equal("null-input", gap.HazardId);
        Assert.Contains("aucun test", gap.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_AleaInconnuDuCatalogue_EstUnManquement()
    {
        var gap = Assert.Single(HazardDeclaration.Verify(["invente"], string.Empty, Catalogue()));

        Assert.Contains("inconnu du catalogue", gap.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_AucuneDeclaration_NeProduitAucunManquement() =>
        Assert.Empty(HazardDeclaration.Verify([], string.Empty, Catalogue()));

    [Fact]
    public void UndeclaredButTested_SignaleLaDeclarationACompleter()
    {
        const string tests = """
            [Trait("hazard", "null-input")] public void A() { }
            [Trait("hazard", "numeric-overflow")] public void B() { }
            """;

        Assert.Equal(["numeric-overflow"], HazardDeclaration.UndeclaredButTested(["null-input"], tests));
    }

    [Fact]
    public void WriteePuisRead_ConserveLaDeclaration()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.A.One");

        HazardDeclaration.Write(directory, ["null-input", "numeric-overflow"]);

        Assert.Equal(["null-input", "numeric-overflow"], HazardDeclaration.Read(directory));
    }

    [Fact]
    public void Read_SansFichier_RetourneVide()
    {
        using var forge = TempForge.Create();
        Assert.Empty(HazardDeclaration.Read(forge.WritePackage("Micro.A.One")));
    }

    [Fact]
    public void Read_FichierCorrompu_RetourneVide()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.A.One");
        File.WriteAllText(Path.Combine(directory, HazardDeclaration.FileName), "{ pas du JSON");

        Assert.Empty(HazardDeclaration.Read(directory));
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => HazardDeclaration.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => HazardDeclaration.CoveredBy(null!));
        Assert.Throws<ArgumentNullException>(() => HazardDeclaration.Verify(null!, "", Catalogue()));
        Assert.Throws<ArgumentNullException>(() => HazardDeclaration.Verify([], null!, Catalogue()));
        Assert.Throws<ArgumentNullException>(() => HazardDeclaration.Verify([], "", null!));
    }
}
