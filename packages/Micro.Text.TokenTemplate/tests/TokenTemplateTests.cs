using Xunit;

namespace Micro.Text.TokenTemplate.Tests;

public sealed class TokenTemplateTests
{
    private static Dictionary<string, string> Valeurs(params (string Nom, string Valeur)[] paires)
        => paires.ToDictionary(p => p.Nom, p => p.Valeur, StringComparer.Ordinal);

    // ==================== Cas nominal ====================

    [Fact]
    public void UnJetonEstRemplaceParSaValeur()
    {
        TokenTemplateResult rendu = TokenTemplate.Render(
            "Bonjour {prenom} !", Valeurs(("prenom", "Camille")));

        Assert.Equal("Bonjour Camille !", rendu.Text);
        Assert.True(rendu.IsComplete);
    }

    [Fact]
    public void PlusieursJetonsSontTousRemplaces()
    {
        TokenTemplateResult rendu = TokenTemplate.Render(
            "https://api/{version}/clients/{id}",
            Valeurs(("version", "v2"), ("id", "417")));

        Assert.Equal("https://api/v2/clients/417", rendu.Text);
    }

    [Fact]
    public void UnJetonRepeteEstRemplacePartoutMaisCompteUneFois()
    {
        TokenTemplateResult rendu = TokenTemplate.Render("{a}-{a}-{a}", Valeurs(("a", "x")));

        Assert.Equal("x-x-x", rendu.Text);
        Assert.Equal(["a"], rendu.UsedTokens);
    }

    [Fact]
    public void LesJetonsConsommesSontRendusDansLOrdre()
    {
        TokenTemplateResult rendu = TokenTemplate.Render(
            "{b} {a} {b}", Valeurs(("a", "1"), ("b", "2"), ("c", "3")));

        // « c » n'apparaît pas au gabarit : il reste disponible pour l'appelant.
        Assert.Equal(["b", "a"], rendu.UsedTokens);
    }

    [Fact]
    public void UnTexteSansJetonTraverseInchange()
    {
        Assert.Equal("rien à faire", TokenTemplate.Render("rien à faire", Valeurs()).Text);
    }

    [Fact]
    public void UnJetonSeulConstitueToutLeGabarit()
    {
        Assert.Equal("valeur", TokenTemplate.Render("{x}", Valeurs(("x", "valeur"))).Text);
    }

    [Fact]
    public void UneValeurVideEstUneValeur()
    {
        TokenTemplateResult rendu = TokenTemplate.Render("[{x}]", Valeurs(("x", "")));

        Assert.Equal("[]", rendu.Text);
        Assert.True(rendu.IsComplete);
        Assert.Equal(["x"], rendu.UsedTokens);
    }

    // ==================== Jetons manquants ====================

    [Fact]
    public void ParDefautUnJetonSansValeurResteEnPlace()
    {
        TokenTemplateResult rendu = TokenTemplate.Render("a {absent} b", Valeurs());

        // Le moins destructeur : le gabarit rendu montre ce qui manque.
        Assert.Equal("a {absent} b", rendu.Text);
        Assert.Equal(["absent"], rendu.MissingTokens);
        Assert.False(rendu.IsComplete);
    }

    [Fact]
    public void UnJetonSansValeurPeutEtreEfface()
    {
        TokenTemplateResult rendu = TokenTemplate.Render(
            "a{absent}b", Valeurs(), new TokenTemplateOptions { OnMissing = MissingTokenBehavior.Blank });

        Assert.Equal("ab", rendu.Text);
        Assert.Equal(["absent"], rendu.MissingTokens);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnJetonSansValeurPeutFaireEchouerLeRendu()
    {
        FormatException echec = Assert.Throws<FormatException>(
            () => TokenTemplate.Render(
                "a{absent}b", Valeurs(), new TokenTemplateOptions { OnMissing = MissingTokenBehavior.Fail }));

        Assert.Contains("absent", echec.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnJetonManquantRepeteNEstSignaleQuUneFois()
    {
        Assert.Equal(["x"], TokenTemplate.Render("{x}{x}{x}", Valeurs()).MissingTokens);
    }

    // ==================== Délimiteurs et échappement ====================

    [Fact]
    public void UnDelimiteurDoubleDonneUnDelimiteurLitteral()
    {
        // Sans cela, un gabarit ne pourrait jamais produire de JSON.
        TokenTemplateResult rendu = TokenTemplate.Render("{{\"a\": {v}}", Valeurs(("v", "1")));

        Assert.Equal("{\"a\": 1}", rendu.Text);
    }

    [Fact]
    public void LEchappementPeutEtreCoupe()
    {
        TokenTemplateOptions brut = new() { AllowEscape = false };

        // Sans échappement, « {{ » ne vaut plus un délimiteur littéral : le premier « { »
        // ouvre un jeton dont le nom commence par une accolade, et ce jeton n'existe pas.
        Assert.Equal("{{a}", TokenTemplate.Render("{{a}", Valeurs(("a", "x")), brut).Text);

        // Avec échappement, le meme gabarit rend une accolade suivie de « a} ».
        Assert.Equal("{a}", TokenTemplate.Render("{{a}", Valeurs(("a", "x"))).Text);
    }

    [Fact]
    public void LesDelimiteursSontConfigurables()
    {
        TokenTemplateOptions doubles = new() { Open = "${", Close = "}" };

        Assert.Equal("valeur", TokenTemplate.Render("${x}", Valeurs(("x", "valeur")), doubles).Text);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnDelimiteurJamaisRefermeResteLitteral()
    {
        Assert.Equal("a {sans fin", TokenTemplate.Render("a {sans fin", Valeurs()).Text);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnDelimiteurFermantIsoleResteLitteral()
    {
        Assert.Equal("a } b", TokenTemplate.Render("a } b", Valeurs()).Text);
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnJetonSansNomResteLitteral()
    {
        Assert.Equal("a{}b", TokenTemplate.Render("a{}b", Valeurs()).Text);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnNomPlusLongQueLaBorneResteLitteral()
    {
        TokenTemplateOptions courte = new() { MaxTokenLength = 3 };
        const string gabarit = "{abcd}";

        Assert.Equal(gabarit, TokenTemplate.Render(gabarit, Valeurs(("abcd", "x")), courte).Text);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnNomExactementALaBorneEstRemplace()
    {
        TokenTemplateOptions courte = new() { MaxTokenLength = 3 };

        Assert.Equal("x", TokenTemplate.Render("{abc}", Valeurs(("abc", "x")), courte).Text);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnTresLongTexteSansFermetureNeParcourtPasIndefiniment()
    {
        string gabarit = "{" + new string('a', 100_000);

        Assert.Equal(gabarit, TokenTemplate.Render(gabarit, Valeurs()).Text);
    }

    // ==================== Transformation ====================

    [Fact]
    public void LaTransformationSAppliqueAChaqueValeur()
    {
        TokenTemplateOptions encode = new() { Transform = Uri.EscapeDataString };

        TokenTemplateResult rendu = TokenTemplate.Render(
            "https://api/clients/{nom}", Valeurs(("nom", "Du Pont & Fils")), encode);

        Assert.Equal("https://api/clients/Du%20Pont%20%26%20Fils", rendu.Text);
    }

    [Fact]
    public void LaTransformationNeToucheNiAuTexteFixeNiAuxJetonsManquants()
    {
        TokenTemplateOptions majuscules = new() { Transform = v => v.ToUpperInvariant() };

        Assert.Equal(
            "fixe ABC {absent}",
            TokenTemplate.Render("fixe {a} {absent}", Valeurs(("a", "abc")), majuscules).Text);
    }

    // ==================== Casse et comparaison ====================

    [Fact]
    public void LaCasseDesNomsCompteParDefaut()
    {
        Assert.Equal(["Nom"], TokenTemplate.Render("{Nom}", Valeurs(("nom", "x"))).MissingTokens);
    }

    [Fact]
    public void LaCasseDesNomsPeutEtreIgnoree()
    {
        TokenTemplateOptions souple = new() { NameComparer = StringComparer.OrdinalIgnoreCase };
        Dictionary<string, string> valeurs = new(StringComparer.OrdinalIgnoreCase) { ["nom"] = "x" };

        Assert.Equal("x", TokenTemplate.Render("{Nom}", valeurs, souple).Text);
    }

    // ==================== Inventaire ====================

    [Fact]
    public void ReleverLesJetonsSansValeurs()
    {
        Assert.Equal(
            ["version", "id"],
            TokenTemplate.FindTokens("https://api/{version}/clients/{id}?x={version}"));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void ReleverLesJetonsDUnGabaritVideDonneRien()
    {
        Assert.Empty(TokenTemplate.FindTokens(null));
        Assert.Empty(TokenTemplate.FindTokens(""));
        Assert.Empty(TokenTemplate.FindTokens("sans jeton"));
    }

    [Fact]
    public void ReleverLesJetonsHonoreLesDelimiteurs()
    {
        TokenTemplateOptions doubles = new() { Open = "<<", Close = ">>" };

        Assert.Equal(["a"], TokenTemplate.FindTokens("x <<a>> {b}", doubles));
    }

    // ==================== Entrées vides et nulles ====================

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnGabaritVideRendDuVide()
    {
        Assert.Equal(string.Empty, TokenTemplate.Render(null, Valeurs()).Text);
        Assert.Equal(string.Empty, TokenTemplate.Render("", Valeurs()).Text);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnDictionnaireNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => TokenTemplate.Render("{a}", null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UneValeurNulleEstTraiteeCommeVide()
    {
        Dictionary<string, string> valeurs = new(StringComparer.Ordinal) { ["a"] = null! };

        TokenTemplateResult rendu = TokenTemplate.Render("[{a}]", valeurs);

        Assert.Equal("[]", rendu.Text);
        Assert.True(rendu.IsComplete);
    }

    // ==================== Réglages invalides ====================

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnDelimiteurVideEstRefuse()
    {
        Assert.Throws<ArgumentException>(() => new TokenTemplateOptions { Open = "" }.Validate());
        Assert.Throws<ArgumentException>(() => new TokenTemplateOptions { Close = "" }.Validate());
    }

    [Fact]
    public void DesDelimiteursIdentiquesSontRefuses()
    {
        Assert.Throws<ArgumentException>(() => new TokenTemplateOptions { Open = "%", Close = "%" }.Validate());
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnComparateurNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => new TokenTemplateOptions { NameComparer = null! }.Validate());
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneLongueurMaximaleNulleOuNegativeEstRefusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenTemplateOptions { MaxTokenLength = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenTemplateOptions { MaxTokenLength = -1 }.Validate());
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        TokenTemplateOptions.Default.Validate();

        Assert.Equal("{", TokenTemplateOptions.Default.Open);
        Assert.Equal("}", TokenTemplateOptions.Default.Close);
        Assert.True(TokenTemplateOptions.Default.AllowEscape);
        Assert.Equal(MissingTokenBehavior.Leave, TokenTemplateOptions.Default.OnMissing);
        Assert.Equal(128, TokenTemplateOptions.Default.MaxTokenLength);
        Assert.Null(TokenTemplateOptions.Default.Transform);
    }

    // ==================== Unicode ====================

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnNomAccentueEstUnNomCommeUnAutre()
    {
        Assert.Equal("12", TokenTemplate.Render("{numéro}", Valeurs(("numéro", "12"))).Text);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnePaireDeSubstitutionTraverseIntacte()
    {
        Assert.Equal(
            "\U0001F680 x",
            TokenTemplate.Render("\U0001F680 {a}", Valeurs(("a", "x"))).Text);
    }

    [Fact]
    public void LeMemeAppelDonneToujoursLeMemeResultat()
    {
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("a-1", TokenTemplate.Render("a-{n}", Valeurs(("n", "1"))).Text);
        }
    }
}
