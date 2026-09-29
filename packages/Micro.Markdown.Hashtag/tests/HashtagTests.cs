using Xunit;

namespace Micro.Markdown.Hashtag.Tests;

public sealed class HashtagTests
{
    private static string[] Names(string text, HashtagOptions? options = null)
        => HashtagParser.FindAll(text, options).Select(t => t.Name).ToArray();

    // ==================== Cas nominal ====================

    [Fact]
    public void UneEtiquetteSimpleEstRelevee()
    {
        HashtagMatch tag = Assert.Single(HashtagParser.FindAll("Une #idée à creuser"));

        Assert.Equal("idée", tag.Name);
        Assert.Equal(4, tag.Start);
        Assert.Equal(5, tag.Length);
        Assert.Equal(9, tag.End);
    }

    [Fact]
    public void LesEtiquettesImbriqueesGardentLeurChemin()
    {
        Assert.Equal(["projet/almageste/v1"], Names("#projet/almageste/v1"));
    }

    [Fact]
    public void TiretsEtSoulignesFontPartieDuNom()
    {
        Assert.Equal(["à-lire_plus-tard"], Names("#à-lire_plus-tard."));
    }

    [Fact]
    public void LaPonctuationFinaleNAppartientPasAuNom()
    {
        Assert.Equal(["a", "b", "c"], Names("#a, #b; (#c) et c'est tout.", new HashtagOptions { AllowedPrecedingCharacters = "(" }));
    }

    [Fact]
    public void UneBarreObliqueFinaleEstRetiree()
    {
        HashtagMatch tag = Assert.Single(HashtagParser.FindAll("#dossier/ suite"));

        Assert.Equal("dossier", tag.Name);
        Assert.Equal(8, tag.Length);
    }

    [Fact]
    public void PlusieursEtiquettesSontReleveesDansLOrdre()
    {
        Assert.Equal(["un", "deux", "trois"], Names("#un #deux\n#trois"));
    }

    // ==================== Ce qui n'est pas une étiquette ====================

    [Theory]
    [InlineData("# Titre")]
    [InlineData("## Sous-titre")]
    [InlineData("page.html#section")]
    [InlineData("https://exemple.fr/#ancre")]
    [InlineData("&#233;")]
    [InlineData("[[Note#Titre]]")]
    [InlineData("[[#Titre]]")]
    [InlineData("C#")]
    [InlineData("le #12 de la rue")]
    [InlineData("#2026")]
    [InlineData("#/racine")]
    [InlineData("#")]
    [InlineData("# ")]
    [InlineData("##double")]
    public void CesFormesNeSontPasDesEtiquettes(string text)
    {
        Assert.Empty(HashtagParser.FindAll(text));
    }

    [Fact]
    public void UnChiffreSuffitQuandLaRegleEstLevee()
    {
        Assert.Equal(["2026"], Names("#2026", new HashtagOptions { RequireNonDigit = false }));
    }

    [Fact]
    public void UnChiffreSuiviDUneLettreEstUneEtiquette()
    {
        Assert.Equal(["2026-bilan", "y1984"], Names("#2026-bilan #y1984"));
    }

    [Fact]
    public void LImbricationPeutEtreDesactivee()
    {
        Assert.Equal(["a"], Names("#a/b", new HashtagOptions { AllowNested = false }));
    }

    [Fact]
    public void LeCodeEstIgnoreParDefaut()
    {
        string text = "#vrai ` #faux`\n```\n#aussi-faux\n```\n#encore-vrai";

        Assert.Equal(["vrai", "encore-vrai"], Names(text));
        Assert.Equal(4, HashtagParser.FindAll(text, new HashtagOptions { SkipCode = false }).Count);
    }

    // ==================== Unicode ====================

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesAlphabetsNonLatinsEtLesMarquesSontAcceptes()
    {
        Assert.Equal(["日本語", "संस्कृत", "Ελληνικά"], Names("#日本語 #संस्कृत #Ελληνικά"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnEmojiArreteLeNomEtLesPositionsRestentJustes()
    {
        string text = "🌌 #étoile🌟";

        HashtagMatch tag = Assert.Single(HashtagParser.FindAll(text));

        Assert.Equal("étoile", tag.Name);
        Assert.Equal(text.IndexOf('#', StringComparison.Ordinal), tag.Start);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UneLettreHorsDuPlanDeBaseEstUneLettre()
    {
        // U+1D400 MATHEMATICAL BOLD CAPITAL A : une lettre codée sur une paire de substitution.
        Assert.Equal(["\U0001D400b"], Names("#\U0001D400b"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UneDemiPaireIsoleeArreteLeNomSansLever()
    {
        Assert.Equal(["ab"], Names("#ab\ud83c"));
    }

    // ==================== Bornes et entrées limites ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LaLongueurDuNomEstBornee()
    {
        HashtagOptions options = new() { MaxLength = 3 };

        Assert.Equal(["abc"], Names("#abc", options));
        Assert.Equal(["abc"], Names("#abcdef", options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    [Trait("hazard", "malformed-input")]
    public void TryParseAtNeLeveJamais()
    {
        string[] textes = ["#", "##", "#a", " #a/", "#\ud83c", "a#b", "#-", "#/", "#\udc00x", "\t#́", new string('#', 300)];

        foreach (string text in textes)
        {
            for (int index = -2; index <= text.Length + 2; index++)
            {
                Assert.Null(Record.Exception(() => HashtagParser.TryParseAt(text, index, out _)));
            }
        }

        Assert.False(HashtagParser.TryParseAt("#a", int.MaxValue, out _));
        Assert.False(HashtagParser.TryParseAt("#a", int.MinValue, out _));
        Assert.False(HashtagParser.TryParseAt("#a", 0, out _, new HashtagOptions { MaxLength = 0 }));
        Assert.False(HashtagParser.TryParseAt("#a", 0, out _, new HashtagOptions { AllowedPrecedingCharacters = null! }));
    }

    [Fact]
    public void TryParseAtLitUneEtiquetteAUnePositionDonnee()
    {
        Assert.True(HashtagParser.TryParseAt("voir #plan", 5, out HashtagMatch tag));
        Assert.Equal("plan", tag.Name);
        Assert.False(HashtagParser.TryParseAt("voir #plan", 4, out _));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontTraitees()
    {
        Assert.False(HashtagParser.TryParseAt(null, 0, out _));
        Assert.False(HashtagParser.IsValidName(null));
        Assert.Throws<ArgumentNullException>(() => HashtagParser.FindAll(null!));
        Assert.Throws<ArgumentNullException>(() => HashtagParser.Ancestors(null!));
        Assert.Throws<ArgumentNullException>(() => HashtagParser.FindAll("#a", new HashtagOptions { AllowedPrecedingCharacters = null! }));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UneEntreeVideNeDonneRien()
    {
        Assert.Empty(HashtagParser.FindAll(string.Empty));
        Assert.False(HashtagParser.IsValidName(string.Empty));
        Assert.Empty(HashtagParser.Ancestors(string.Empty));
    }

    [Fact]
    public void DesReglagesIncoherentsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HashtagParser.FindAll("#a", new HashtagOptions { MaxLength = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashtagOptions { MaxLength = -1 }.Validate());
        HashtagOptions.Default.Validate();
        new HashtagOptions { MaxLength = 1 }.Validate();
    }

    // ==================== Validation d'un nom et ancêtres ====================

    [Theory]
    [InlineData("idée", true)]
    [InlineData("projet/almageste", true)]
    [InlineData("a b", false)]
    [InlineData("a/", false)]
    [InlineData("/a", false)]
    [InlineData("123", false)]
    [InlineData("a,b", false)]
    public void IsValidNameSuitLesReglesDuReleve(string name, bool expected)
    {
        Assert.Equal(expected, HashtagParser.IsValidName(name));
    }

    [Fact]
    public void AncestorsDonneChaqueNiveau()
    {
        Assert.Equal(["projet", "projet/almageste", "projet/almageste/v1"], HashtagParser.Ancestors("#projet/almageste/v1"));
        Assert.Equal(["a", "a/b"], HashtagParser.Ancestors("a//b/"));
        Assert.Equal(["simple"], HashtagParser.Ancestors("simple"));
    }
}
