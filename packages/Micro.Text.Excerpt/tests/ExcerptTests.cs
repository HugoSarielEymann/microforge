using Xunit;

namespace Micro.Text.Excerpt.Tests;

public sealed class ExcerptTests
{
    private static TextExcerpt Autour(string text, string passage, ExcerptOptions? options = null)
    {
        int start = text.IndexOf(passage, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return ExcerptBuilder.Around(text, start, passage.Length, options);
    }

    // ==================== Cas nominal ====================

    [Fact]
    public void UnTexteCourtEstRenduEntierAvecLePassagePositionne()
    {
        TextExcerpt extrait = Autour("Le portail s'ouvre à l'aube.", "portail");

        Assert.Equal("Le portail s'ouvre à l'aube.", extrait.Text);
        Assert.Equal("portail", extrait.Highlight);
        Assert.Equal(3, extrait.HighlightStart);
        Assert.False(extrait.StartsTruncated);
        Assert.False(extrait.EndsTruncated);
    }

    [Fact]
    public void UnLongTexteEstCoupeAuxMotsAvecDesPointsDeSuspension()
    {
        string text = "Il était une fois une très longue phrase qui parle d'un portail doré au fond de la galaxie lointaine et silencieuse.";
        ExcerptOptions options = new() { ContextBefore = 12, ContextAfter = 15 };

        TextExcerpt extrait = Autour(text, "portail", options);

        Assert.Equal("…parle d'un portail doré au fond…", extrait.Text);
        Assert.Equal("portail", extrait.Highlight);
        Assert.True(extrait.StartsTruncated);
        Assert.True(extrait.EndsTruncated);
    }

    [Fact]
    public void SansCalageLesMotsPeuventEtreCoupes()
    {
        string text = "abcdefghij PASSAGE klmnopqrst";
        ExcerptOptions options = new() { ContextBefore = 4, ContextAfter = 4, SnapToWords = false };

        TextExcerpt extrait = Autour(text, "PASSAGE", options);

        Assert.Equal("…hij PASSAGE klm…", extrait.Text);
    }

    [Fact]
    public void LeContexteResteSurLaLigneDuPassage()
    {
        string text = "Ligne au-dessus\nla ligne du passage ici\nligne au-dessous";

        TextExcerpt extrait = Autour(text, "passage");

        Assert.Equal("la ligne du passage ici", extrait.Text);
        Assert.False(extrait.StartsTruncated);
        Assert.False(extrait.EndsTruncated);
    }

    [Fact]
    public void LeContextePeutFranchirLesLignes()
    {
        TextExcerpt extrait = Autour("avant\npassage\naprès", "passage", new ExcerptOptions { SingleLine = false });

        Assert.Equal("avant passage après", extrait.Text);
        Assert.Equal("passage", extrait.Highlight);
    }

    [Fact]
    public void LesBlancsSontReduitsEtLePassageRepositionne()
    {
        TextExcerpt extrait = Autour("  un    deux\t\ttrois  ", "trois");

        Assert.Equal("un deux trois", extrait.Text);
        Assert.Equal("trois", extrait.Highlight);
    }

    [Fact]
    public void UnPassageAvecDesBlancsEstReduitAussi()
    {
        TextExcerpt extrait = Autour("a deux    mots b", "deux    mots");

        Assert.Equal("a deux mots b", extrait.Text);
        Assert.Equal("deux mots", extrait.Highlight);
    }

    [Fact]
    public void LesBlancsPeuventEtreConserves()
    {
        TextExcerpt extrait = Autour("a  b", "b", new ExcerptOptions { CollapseWhitespace = false });

        Assert.Equal("a  b", extrait.Text);
        Assert.Equal(3, extrait.HighlightStart);
    }

    [Fact]
    public void LesPointsDeSuspensionSontParametrables()
    {
        TextExcerpt extrait = Autour("un deux trois quatre cinq", "trois", new ExcerptOptions { ContextBefore = 5, ContextAfter = 7, Ellipsis = "[…] " });

        Assert.Equal("[…] deux trois quatre[…] ", extrait.Text);
        Assert.Equal("trois", extrait.Highlight);
    }

    [Fact]
    public void UnPassageLongNEstJamaisRaccourci()
    {
        string passage = new('x', 500);
        TextExcerpt extrait = Autour("début " + passage + " fin", passage, new ExcerptOptions { ContextBefore = 2, ContextAfter = 2 });

        Assert.Equal(passage, extrait.Highlight);
    }

    // ==================== Bornes ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnPassageDeLongueurNulleMarqueUnPoint()
    {
        TextExcerpt extrait = ExcerptBuilder.Around("abc def", 4, 0);

        Assert.Equal(0, extrait.HighlightLength);
        Assert.Equal(4, extrait.HighlightStart);
        Assert.Equal(string.Empty, extrait.Highlight);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesPassagesEnBordureDeTexteSontAcceptes()
    {
        Assert.Equal("abc", ExcerptBuilder.Around("abc", 0, 3).Highlight);
        Assert.Equal(string.Empty, ExcerptBuilder.Around("abc", 3, 0).Highlight);
        Assert.Equal("c", ExcerptBuilder.Around("abc", 2, 1).Highlight);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnContexteNulNeGardeQueLePassage()
    {
        TextExcerpt extrait = Autour("un deux trois", "deux", new ExcerptOptions { ContextBefore = 0, ContextAfter = 0 });

        Assert.Equal("…deux…", extrait.Text);
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UnPassageHorsDuTexteEstRefuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExcerptBuilder.Around("abc", -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExcerptBuilder.Around("abc", 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExcerptBuilder.Around("abc", 4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExcerptBuilder.Around("abc", 2, 2));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void DesValeursExtremesNeDebordentPas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExcerptBuilder.Around("abc", 1, int.MaxValue));

        TextExcerpt extrait = ExcerptBuilder.Around("un deux trois", 3, 4, new ExcerptOptions { ContextBefore = int.MaxValue, ContextAfter = int.MaxValue });
        Assert.Equal("un deux trois", extrait.Text);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnePaireDeSubstitutionNEstJamaisCoupee()
    {
        string text = "🌌🌌🌌 passage 🌟🌟🌟";
        ExcerptOptions options = new() { ContextBefore = 3, ContextAfter = 3, SnapToWords = false };

        TextExcerpt extrait = Autour(text, "passage", options);

        Assert.DoesNotContain(extrait.Text, c => char.IsSurrogate(c) && !IsPaired(extrait.Text, c));
        Assert.Equal("passage", extrait.Highlight);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesMotsAccentuesSontDesMots()
    {
        string text = "écoutèrent l'étoile filante";
        TextExcerpt extrait = Autour(text, "étoile", new ExcerptOptions { ContextBefore = 5, ContextAfter = 3 });

        Assert.Equal("…l'étoile…", extrait.Text);
    }

    // ==================== Entrées limites et paramétrage ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontRefusees()
    {
        Assert.Throws<ArgumentNullException>(() => ExcerptBuilder.Around(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => ExcerptBuilder.Around("a", 0, 1, new ExcerptOptions { Ellipsis = null! }));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnTexteVideDonneUnExtraitVide()
    {
        TextExcerpt extrait = ExcerptBuilder.Around(string.Empty, 0, 0);

        Assert.Equal(string.Empty, extrait.Text);
        Assert.False(extrait.StartsTruncated);
        Assert.False(extrait.EndsTruncated);
    }

    [Fact]
    public void LesReglagesIncoherentsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExcerptOptions { ContextBefore = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExcerptOptions { ContextAfter = -1 }.Validate());
        ExcerptOptions.Default.Validate();
    }

    private static bool IsPaired(string text, char c)
    {
        int index = text.IndexOf(c, StringComparison.Ordinal);
        return char.IsHighSurrogate(c)
            ? index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
            : index > 0 && char.IsHighSurrogate(text[index - 1]);
    }
}
