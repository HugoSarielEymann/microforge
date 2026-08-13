using Xunit;

namespace Micro.Graph.TopologicalSort.Tests;

public sealed class TopologicalSorterTests
{
    /// <summary>Graphe des exemples : A → B → D, A → C → D. Deux chemins, un point de jonction.</summary>
    private static IEnumerable<string> Diamond(string node) => node switch
    {
        "A" => ["B", "C"],
        "B" => ["D"],
        "C" => ["D"],
        _ => [],
    };

    [Fact]
    public void Order_ChaineSimple_RespecteLOrdre()
    {
        var result = TopologicalSorter.Order(
            ["C", "A", "B"],
            n => n switch { "A" => ["B"], "B" => ["C"], _ => (IEnumerable<string>?)[] });

        Assert.True(result.IsComplete);
        Assert.Equal(["A", "B", "C"], result.Sorted);
    }

    [Fact]
    public void Order_Losange_RegroupeLesNoeudsIndependantsDansUneMemeVague()
    {
        var result = TopologicalSorter.Order(["A", "B", "C", "D"], Diamond);

        Assert.True(result.IsComplete);
        Assert.Equal(3, result.Waves.Count);
        Assert.Equal(["A"], result.Waves[0]);
        Assert.Equal(["B", "C"], result.Waves[1]);
        Assert.Equal(["D"], result.Waves[2]);
    }

    [Fact]
    public void Order_Vagues_ConcatenationEgaleSorted()
    {
        var result = TopologicalSorter.Order(["A", "B", "C", "D"], Diamond);

        Assert.Equal(result.Sorted, result.Waves.SelectMany(w => w));
    }

    [Fact]
    public void Order_VagueInterne_SuitLOrdreDeLaCollectionDEntree()
    {
        // C avant B en entrée : la vague doit refléter cet ordre, pas celui des arêtes.
        var result = TopologicalSorter.Order(["A", "C", "B", "D"], Diamond);

        Assert.Equal(["C", "B"], result.Waves[1]);
    }

    [Fact]
    public void Order_SensFollows_InverseLaLectureDesAretes()
    {
        // « B dépend de A », « C dépend de B ».
        var result = TopologicalSorter.Order(
            ["A", "B", "C"],
            n => n switch { "B" => ["A"], "C" => ["B"], _ => (IEnumerable<string>?)[] },
            new TopologicalSortOptions { EdgeMeaning = EdgeMeaning.Follows });

        Assert.True(result.IsComplete);
        Assert.Equal(["A", "B", "C"], result.Sorted);
    }

    [Fact]
    public void Order_NoeudSansAucuneArete_EstOrdonneDansLaPremiereVague()
    {
        var result = TopologicalSorter.Order(
            ["A", "Z", "B"],
            n => n == "A" ? ["B"] : (IEnumerable<string>?)[]);

        Assert.True(result.IsComplete);
        Assert.Equal(["A", "Z"], result.Waves[0]);
    }

    [Fact]
    public void Order_AretesEnDouble_NeFaussentPasLeTri()
    {
        var result = TopologicalSorter.Order(
            ["A", "B"],
            n => n == "A" ? ["B", "B"] : (IEnumerable<string>?)[]);

        Assert.True(result.IsComplete);
        Assert.Equal(["A", "B"], result.Sorted);
    }

    [Fact]
    public void Order_ComparateurPersonnalise_IdentifieLesNoeudsSelonCeComparateur()
    {
        var result = TopologicalSorter.Order(
            ["Alpha", "Beta"],
            n => n == "Alpha" ? ["BETA"] : (IEnumerable<string>?)[],
            options: null,
            comparer: StringComparer.OrdinalIgnoreCase);

        Assert.True(result.IsComplete);
        Assert.Equal(["Alpha", "Beta"], result.Sorted);
    }

    [Fact]
    public void Order_FonctionDAretesRetournantNull_EquivautAAucuneArete()
    {
        var result = TopologicalSorter.Order<string>(["A", "B"], _ => null);

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Sorted.Count);
    }

    // ---- Cycles : un résultat, pas une exception ----

    [Fact]
    public void Order_Cycle_EstSignaleSansLever()
    {
        var result = TopologicalSorter.Order(
            ["A", "B"],
            n => n == "A" ? ["B"] : (IEnumerable<string>?)["A"]);

        Assert.False(result.IsComplete);
        Assert.Empty(result.Sorted);
        Assert.Equal(["A", "B"], result.CyclicNodes);
    }

    [Fact]
    public void Order_BoucleSurSoiMeme_EstUnCycle()
    {
        var result = TopologicalSorter.Order(["A"], _ => ["A"]);

        Assert.False(result.IsComplete);
        Assert.Equal(["A"], result.CyclicNodes);
    }

    [Fact]
    public void Order_CyclePartiel_OrdonneCeQuiPeutLEtreEtReporteLeReste()
    {
        // S est libre ; B et C se bloquent mutuellement ; D dépend du blocage.
        var result = TopologicalSorter.Order(
            ["S", "B", "C", "D"],
            n => n switch { "B" => ["C"], "C" => ["B"], "D" => (IEnumerable<string>?)[], _ => [] });

        Assert.False(result.IsComplete);
        Assert.Equal(["S", "D"], result.Sorted);
        Assert.Equal(["B", "C"], result.CyclicNodes);
    }

    // ---- Aléas déclarés ----

    [Fact]
    [Trait("hazard", "null-input")]
    public void Order_CollectionNulle_Leve()
    {
        Assert.Throws<ArgumentNullException>(
            () => TopologicalSorter.Order<string>(null!, _ => []));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void Order_FonctionDAretesNulle_Leve()
    {
        Assert.Throws<ArgumentNullException>(
            () => TopologicalSorter.Order<string>(["A"], null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void Order_NoeudNulDansLaCollection_Leve()
    {
        Assert.Throws<ArgumentNullException>(
            () => TopologicalSorter.Order<string>(["A", null!], _ => []));
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void Order_CollectionVide_RendUnResultatCompletEtVide()
    {
        var result = TopologicalSorter.Order<string>([], _ => []);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Sorted);
        Assert.Empty(result.Waves);
        Assert.Empty(result.CyclicNodes);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void Order_AreteVersUnNoeudAbsent_Leve()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => TopologicalSorter.Order(["A"], _ => ["Fantome"]));

        Assert.Equal("edges", ex.ParamName);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void Order_AreteVersUnNoeudAbsent_EstIgnoreeSiDemande()
    {
        var result = TopologicalSorter.Order(
            ["A"],
            _ => ["Fantome"],
            new TopologicalSortOptions { IgnoreUnknownNodes = true });

        Assert.True(result.IsComplete);
        Assert.Equal(["A"], result.Sorted);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void Order_NoeudEnDouble_Leve()
    {
        Assert.Throws<ArgumentException>(
            () => TopologicalSorter.Order(["A", "A"], _ => (IEnumerable<string>?)[]));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void Order_AreteNulleDansLaSequence_EstTraiteeCommeUnNoeudInconnu()
    {
        Assert.Throws<ArgumentException>(
            () => TopologicalSorter.Order<string>(["A"], _ => [null!]));
    }

    // ---- Paramétrage ----

    [Fact]
    public void Validate_SensDArreteInconnu_Leve()
    {
        var options = new TopologicalSortOptions { EdgeMeaning = (EdgeMeaning)42 };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Order_ParametrageIncoherent_Leve()
    {
        var options = new TopologicalSortOptions { EdgeMeaning = (EdgeMeaning)42 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => TopologicalSorter.Order(["A"], _ => (IEnumerable<string>?)[], options));
    }

    [Fact]
    public void Default_EstLeParametrageDocumente()
    {
        Assert.Equal(EdgeMeaning.Precedes, TopologicalSortOptions.Default.EdgeMeaning);
        Assert.False(TopologicalSortOptions.Default.IgnoreUnknownNodes);
    }
}
