using Xunit;

namespace Micro.Markdown.ListContinuation.Tests;

public sealed class ListContinuationTests
{
    private static ListContinuationResult AuBout(string line, ListContinuationOptions? options = null)
        => ListContinuation.OnEnter(line, line.Length, options);

    [Theory]
    [InlineData("- item", "- ")]
    [InlineData("* item", "* ")]
    [InlineData("+ item", "+ ")]
    [InlineData("  - imbriqué", "  - ")]
    [InlineData("\t- tabulé", "\t- ")]
    [InlineData("-   large", "-   ")]
    public void UnePuceSePoursuitALIdentique(string line, string prefix)
    {
        ListContinuationResult result = AuBout(line);

        Assert.Equal(ListContinuationKind.Continue, result.Kind);
        Assert.Equal(prefix, result.Prefix);
    }

    [Theory]
    [InlineData("1. premier", "2. ")]
    [InlineData("9) neuvième", "10) ")]
    [InlineData("  41. suite", "  42. ")]
    public void UnNumeroEstIncremente(string line, string prefix)
    {
        Assert.Equal(prefix, AuBout(line).Prefix);
    }

    [Fact]
    public void LIncrementPeutEtreDesactive()
    {
        Assert.Equal("1. ", AuBout("1. a", new ListContinuationOptions { Increment = false }).Prefix);
    }

    [Fact]
    public void UneTacheCocheeDonneUneTacheVide()
    {
        Assert.Equal("- [ ] ", AuBout("- [x] fait").Prefix);
        Assert.Equal("- [ ] ", AuBout("- [ ] à faire").Prefix);
        Assert.Equal("- [x] ", AuBout("- [X] fait", new ListContinuationOptions { ResetTasks = false }).Prefix);
    }

    [Fact]
    public void UneListeDansUneCitationGardeLaCitation()
    {
        Assert.Equal("> - ", AuBout("> - dans une citation").Prefix);
        Assert.Equal("> > 3. ", AuBout("> > 2. imbriqué").Prefix);
    }

    [Fact]
    public void UneCitationSePoursuit()
    {
        ListContinuationResult result = AuBout("> Les étoiles…");

        Assert.Equal(ListContinuationKind.Continue, result.Kind);
        Assert.Equal("> ", result.Prefix);
        Assert.Equal(ListContinuationKind.None, AuBout("> texte", new ListContinuationOptions { ContinueQuotes = false }).Kind);
    }

    [Theory]
    [InlineData("- ", 0, 2)]
    [InlineData("-", 0, 1)]
    [InlineData("  - [ ] ", 2, 6)]
    [InlineData("3.  ", 0, 4)]
    public void UnElementVideTermineLaListe(string line, int start, int length)
    {
        ListContinuationResult result = AuBout(line);

        Assert.Equal(ListContinuationKind.End, result.Kind);
        Assert.Equal(start, result.RemoveStart);
        Assert.Equal(length, result.RemoveLength);
    }

    [Fact]
    public void UneCitationVideSeTermine()
    {
        ListContinuationResult result = AuBout("  > ");

        Assert.Equal(ListContinuationKind.End, result.Kind);
        Assert.Equal(2, result.RemoveStart);
        Assert.Equal(2, result.RemoveLength);
    }

    [Fact]
    public void UnCurseurAvantLeContenuDonneUnSautOrdinaire()
    {
        Assert.Equal(ListContinuationKind.None, ListContinuation.OnEnter("- item", 1).Kind);
        Assert.Equal(ListContinuationKind.Continue, ListContinuation.OnEnter("- item", 2).Kind);
        Assert.Equal(ListContinuationKind.None, ListContinuation.OnEnter("> texte", 1).Kind);
    }

    [Fact]
    public void UnCurseurAuMilieuDuContenuPoursuitQuandMeme()
    {
        Assert.Equal("- ", ListContinuation.OnEnter("- premier second", 9).Prefix);
    }

    [Theory]
    [InlineData("texte ordinaire")]
    [InlineData("-sans espace")]
    [InlineData("1.sans espace")]
    [InlineData("**gras**")]
    [InlineData("# Titre")]
    [InlineData("1234567890. trop long")]
    [InlineData("")]
    public void CesLignesNeSontPasDesListes(string line)
    {
        Assert.Equal(ListContinuationKind.None, AuBout(line).Kind);
        Assert.False(ListContinuation.TryParseMarker(line, out _));
    }

    [Fact]
    public void LeMarqueurLuDecritLaLigne()
    {
        Assert.True(ListContinuation.TryParseMarker("  > 12) [x] tâche", out ListMarker marker));

        Assert.Equal(4, marker.MarkerStart);
        Assert.Null(marker.Bullet);
        Assert.Equal(12, marker.Number);
        Assert.Equal(')', marker.Delimiter);
        Assert.True(marker.IsTask);
        Assert.True(marker.IsChecked);
        Assert.Equal(12, marker.ContentStart);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UneCaseMalFormeeNEstPasUneTache()
    {
        Assert.True(ListContinuation.TryParseMarker("- [y] pas une case", out ListMarker marker));
        Assert.False(marker.IsTask);
        Assert.True(ListContinuation.TryParseMarker("- [ ]collé", out marker));
        Assert.False(marker.IsTask);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LeContenuUnicodeNeGenePas()
    {
        Assert.Equal("- ", AuBout("- 🌟 étoile").Prefix);
        Assert.Equal(ListContinuationKind.End, AuBout("-  ").Kind);
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void UnTresGrandNumeroNeDebordePas()
    {
        Assert.Equal("1000000000. ", AuBout("999999999. dernier").Prefix);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UneLigneNulleEstRefuseeParOnEnterMaisPasParTryParse()
    {
        Assert.Throws<ArgumentNullException>(() => ListContinuation.OnEnter(null!, 0));
        Assert.False(ListContinuation.TryParseMarker(null, out _));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnCurseurHorsDeLaLigneEstRefuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ListContinuation.OnEnter("- a", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ListContinuation.OnEnter("- a", 4));
        Assert.Equal(ListContinuationKind.None, ListContinuation.OnEnter(string.Empty, 0).Kind);
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        ListContinuationOptions.Default.Validate();
        Assert.Equal(ListContinuationKind.None, ListContinuationResult.None.Kind);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnCurseurExtremeEstRefuseSansDebordement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ListContinuation.OnEnter("- a", int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => ListContinuation.OnEnter("- a", int.MinValue));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void TryParseMarkerNeLeveJamais()
    {
        string[] absurdes =
        [
            "-", "1.", "1)", "[ ]", "- [", "- [x", "- [x]", ">", "> >", ">>>>>", "	", "   ", "99999999999999999999. x",
            "- [ ] ", "0.", "-	[x]	", new string('>', 500), new string('1', 500) + ". a",
        ];

        foreach (string line in absurdes)
        {
            Assert.Null(Record.Exception(() => ListContinuation.TryParseMarker(line, out _)));
            Assert.Null(Record.Exception(() => ListContinuation.OnEnter(line, line.Length)));
            Assert.Null(Record.Exception(() => ListContinuation.OnEnter(line, 0)));
        }
    }
}
