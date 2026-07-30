using Xunit;

namespace SnippetForge.Tests;

public sealed class SearchEngineTests
{
    private static IndexEntry Entry(string id, string description, params string[] tags) =>
        new(id, "1.0.0", ["1.0.0"], description, tags, "MicroForge", string.Empty);

    private static readonly IndexDocument Index = new(DateTime.UtcNow,
    [
        Entry("Micro.Flow.Retry", "Relances avec backoff exponentiel.", "retry", "flow", "async"),
        Entry("Micro.Text.Slugify", "Conversion en slug URL.", "text", "slug", "url"),
        Entry("Micro.Collections.Chunk", "Découpe une séquence en lots.", "collections", "chunk", "batch"),
    ]);

    [Fact]
    public void Search_TrouveParIdentifiant()
    {
        var hits = SearchEngine.Search(Index, "retry", []);
        Assert.Equal("Micro.Flow.Retry", hits[0].Entry.Id);
    }

    [Fact]
    public void Search_TrouveParDescription()
    {
        var hits = SearchEngine.Search(Index, "backoff", []);
        Assert.Equal("Micro.Flow.Retry", hits[0].Entry.Id);
    }

    [Fact]
    public void Search_FiltreStrictementParTags()
    {
        var hits = SearchEngine.Search(Index, string.Empty, ["text"]);
        Assert.Single(hits);
        Assert.Equal("Micro.Text.Slugify", hits[0].Entry.Id);
    }

    [Fact]
    public void Search_TagInexistant_NeRetourneRien() =>
        Assert.Empty(SearchEngine.Search(Index, "retry", ["inexistant"]));

    [Fact]
    public void Search_SansCorrespondance_RetourneVide() =>
        Assert.Empty(SearchEngine.Search(Index, "cryptographie quantique", []));

    [Fact]
    public void Search_ScoreNormalise_EstDansLIntervalleUnitaire()
    {
        var hits = SearchEngine.Search(Index, "retry flow", []);
        Assert.All(hits, h => Assert.InRange(h.Score, 0, 1));
        Assert.Equal(1.0, hits[0].Score, precision: 6);
    }

    [Fact]
    public void Search_SansProviderSemantique_NeRapportePasDeScoreSemantique()
    {
        var hits = SearchEngine.Search(Index, "retry", []);
        Assert.Null(hits[0].SemanticScore);
    }

    [Fact]
    public void Search_AvecScoreSemantique_LeCombineAuLexical()
    {
        // Le lexical ne connaît pas « découper » ; le sémantique le rattrape.
        var hits = SearchEngine.Search(
            Index, "découper", [],
            entry => entry.Id == "Micro.Collections.Chunk" ? 0.95 : 0.05);

        Assert.Equal("Micro.Collections.Chunk", hits[0].Entry.Id);
        Assert.Equal(0.95, hits[0].SemanticScore!.Value, precision: 6);
        Assert.Equal(0.475, hits[0].Score, precision: 6);
    }

    [Fact]
    public void Search_LeSemantiquePeutSurclasserUnLexicalFaible()
    {
        // « retry lots » : Retry domine le lexical, Chunk n'a qu'une correspondance
        // faible, Slugify aucune. Un fort signal sémantique doit faire passer Slugify
        // devant Chunk malgré son score lexical nul.
        var lexicalOnly = SearchEngine.Search(Index, "retry lots", []);
        Assert.DoesNotContain(lexicalOnly, h => h.Entry.Id == "Micro.Text.Slugify");

        var hybrid = SearchEngine.Search(
            Index, "retry lots", [],
            entry => entry.Id == "Micro.Text.Slugify" ? 1.0 : 0.0);

        var slugifyRank = hybrid.ToList().FindIndex(h => h.Entry.Id == "Micro.Text.Slugify");
        var chunkRank = hybrid.ToList().FindIndex(h => h.Entry.Id == "Micro.Collections.Chunk");

        Assert.True(slugifyRank < chunkRank, $"rangs : Slugify {slugifyRank}, Chunk {chunkRank}");
    }

    [Fact]
    public void Search_ScoresLexicauxEgaux_LeSemantiqueDepartage()
    {
        // « micro » correspond identiquement aux trois identifiants : seul le
        // signal sémantique peut alors ordonner les résultats.
        var hybrid = SearchEngine.Search(
            Index, "micro", [],
            entry => entry.Id == "Micro.Flow.Retry" ? 1.0 : 0.1);

        Assert.Equal("Micro.Flow.Retry", hybrid[0].Entry.Id);
    }

    [Fact]
    public void Search_ConservetLeScoreLexicalBrut()
    {
        var hits = SearchEngine.Search(Index, "retry", []);
        Assert.True(hits[0].LexicalScore > 0);
    }

    [Fact]
    public void Search_RequeteVideSansTags_RetourneToutLeCatalogue() =>
        Assert.Equal(3, SearchEngine.Search(Index, string.Empty, []).Count);

    [Fact]
    public void Search_IndexVide_RetourneVide() =>
        Assert.Empty(SearchEngine.Search(new IndexDocument(DateTime.UtcNow, []), "retry", []));

    [Theory]
    [InlineData("Micro.Flow.Retry", new[] { "micro", "flow", "retry" })]
    [InlineData("slug-url_parse", new[] { "slug", "url", "parse" })]
    [InlineData("a b cd", new[] { "cd" })]
    public void Tokenize_NormaliseEtEcarteLesJetonsTropCourts(string input, string[] expected) =>
        Assert.Equal(expected, SearchEngine.Tokenize(input));

    [Fact]
    public void LexicalScore_PondereLIdentifiantPlusQueLaDescription()
    {
        var idMatch = SearchEngine.LexicalScore(Entry("Micro.A.Retry", "sans rapport"), ["retry"]);
        var descriptionMatch = SearchEngine.LexicalScore(Entry("Micro.A.One", "gère le retry"), ["retry"]);

        Assert.True(idMatch > descriptionMatch);
    }

    [Fact]
    public void LexicalScore_SansJeton_VautZero() =>
        Assert.Equal(0, SearchEngine.LexicalScore(Entry("Micro.A.One", "description"), []));
}
