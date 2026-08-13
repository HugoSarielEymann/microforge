using Xunit;

namespace Micro.Text.SearchMatch.Tests;

public sealed class SearchMatchTests
{
    private sealed record Etape(string Nom, string? Description, string[] Tags);

    private static readonly string[] UnSeulElement = ["a"];

    // ==================== Cas nominal ====================

    [Fact]
    public void UnTermePresentDansUnChampRetientLEnregistrement()
    {
        Assert.True(SearchMatcher.Matches("commande", ["Créer une commande", "api"]));
    }

    [Fact]
    public void UnTermeAbsentDeTousLesChampsEcarteLEnregistrement()
    {
        Assert.False(SearchMatcher.Matches("facture", ["Créer une commande", "api"]));
    }

    [Fact]
    public void ChaqueTermeDoitEtreTrouve()
    {
        string[] champs = ["Créer une commande", "api"];

        Assert.True(SearchMatcher.Matches("commande api", champs));
        Assert.False(SearchMatcher.Matches("commande base", champs));
    }

    [Fact]
    public void LOrdreDesTermesNeCompteBas()
    {
        string[] champs = ["Appel base de données", "sql"];

        Assert.True(SearchMatcher.Matches("base appel", champs));
        Assert.True(SearchMatcher.Matches("appel base", champs));
    }

    [Fact]
    public void UnTermePeutEtreTrouveDansUnAutreChampQueLePremier()
    {
        Assert.True(SearchMatcher.Matches("json rest", ["Appel REST", "json", "http"]));
    }

    [Fact]
    public void LaRechercheEstInsensibleALaCasse()
    {
        Assert.True(SearchMatcher.Matches("COMMANDE", ["créer une commande"]));
        Assert.True(SearchMatcher.Matches("commande", ["CRÉER UNE COMMANDE"]));
    }

    [Fact]
    public void LaSurchargeMonoChampSeComporteCommeLaListe()
    {
        Assert.True(SearchMatcher.Matches("commande", "Créer une commande"));
        Assert.False(SearchMatcher.Matches("facture", "Créer une commande"));
    }

    [Fact]
    public void UnFragmentSuffitParDefaut()
    {
        Assert.True(SearchMatcher.Matches("comm", ["commande"]));
    }

    // ==================== Requête vide ou insignifiante ====================

    [Fact]
    public void UneRequeteVideAccepteTout()
    {
        Assert.True(SearchMatcher.Matches(null, ["quoi que ce soit"]));
        Assert.True(SearchMatcher.Matches("", ["quoi que ce soit"]));
        Assert.True(SearchMatcher.Matches("   \t ", ["quoi que ce soit"]));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UneRequeteVideAccepteMemeUnEnregistrementSansChamp()
    {
        Assert.True(SearchMatcher.Matches("", []));
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void UnEnregistrementSansChampNeRepondAAucuneRecherche()
    {
        Assert.False(SearchMatcher.Matches("commande", []));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnChampVideEstIgnoreSansEcarterLesAutres()
    {
        Assert.True(SearchMatcher.Matches("commande", ["", "commande"]));
    }

    // ==================== Entrées nulles ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnChampNulEstIgnore()
    {
        Assert.True(SearchMatcher.Matches("commande", [null, "commande", null]));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnChampUniqueNulNeRepondPas()
    {
        Assert.False(SearchMatcher.Matches("commande", (string?)null));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UneListeDeChampsNulleEstRefusee()
    {
        Assert.Throws<ArgumentNullException>(
            () => SearchMatcher.Matches("commande", (IEnumerable<string?>)null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UneSequenceOuUnSelecteurNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(
            () => SearchMatcher.Filter<string>(null!, "a", s => [s]).ToList());

        Assert.Throws<ArgumentNullException>(
            () => SearchMatcher.Filter(UnSeulElement, "a", null!).ToList());
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnSelecteurQuiRendNulEquivautAUnElementSansChamp()
    {
        List<string> retenus = [.. SearchMatcher.Filter(UnSeulElement, "a", _ => null!)];

        Assert.Empty(retenus);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void DesTermesNulsSontRefuses()
    {
        Assert.Throws<ArgumentNullException>(() => SearchMatcher.MatchesTerms(null!, ["a"]));
        Assert.Throws<ArgumentNullException>(() => SearchMatcher.MatchesTerms([], null!));
    }

    // ==================== Diacritiques et Unicode ====================

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesAccentsSontIgnoresDansLesDeuxSens()
    {
        Assert.True(SearchMatcher.Matches("resume", ["Résumé de la commande"]));
        Assert.True(SearchMatcher.Matches("Résumé", ["resume de la commande"]));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnAccentDejaDecomposeEstTraiteCommeUnAccentCompose()
    {
        // Deux representations du meme mot : é compose (U+00E9), et e + accent
        // aigu combinant (U+0065 U+0301). Le repli doit les reconcilier.
        const string compose = "\u00E9tape";
        const string decompose = "e\u0301tape";

        Assert.NotEqual(compose, decompose, StringComparer.Ordinal);
        Assert.True(SearchMatcher.Matches(compose, [decompose + " suivante"]));
        Assert.True(SearchMatcher.Matches(decompose, [compose + " suivante"]));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesCedillesEtLigaturesAccentueesSontRepliees()
    {
        Assert.True(SearchMatcher.Matches("francais", ["Français"]));
        Assert.True(SearchMatcher.Matches("uber", ["Über"]));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnAlphabetSansFormeDecomposeeTraverseInchange()
    {
        Assert.True(SearchMatcher.Matches("客", ["客户订单"]));
        Assert.False(SearchMatcher.Matches("户单", ["客户订单"]));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnePaireDeSubstitutionNEstPasCoupee()
    {
        Assert.True(SearchMatcher.Matches("\U0001F680", ["fusée \U0001F680 lancée"]));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesAccentsPeuventEtreConserves()
    {
        SearchMatchOptions strict = new() { IgnoreDiacritics = false };

        Assert.False(SearchMatcher.Matches("resume", ["Résumé"], strict));
        Assert.True(SearchMatcher.Matches("résumé", ["Résumé"], strict));
    }

    // ==================== Réglages ====================

    [Fact]
    public void LaCassePeutEtreRespectee()
    {
        SearchMatchOptions strict = new() { IgnoreCase = false };

        Assert.False(SearchMatcher.Matches("COMMANDE", ["commande"], strict));
        Assert.True(SearchMatcher.Matches("commande", ["commande"], strict));
    }

    [Fact]
    public void LeMotEntierEcarteLesFragments()
    {
        SearchMatchOptions mot = new() { WholeWord = true };

        Assert.False(SearchMatcher.Matches("chat", ["achat groupé"], mot));
        Assert.True(SearchMatcher.Matches("chat", ["le chat noir"], mot));
        Assert.True(SearchMatcher.Matches("chat", ["chat"], mot));
        Assert.True(SearchMatcher.Matches("chat", ["ranger le chat"], mot));
    }

    [Fact]
    public void LeMotEntierAccepteUneFrontierePonctuee()
    {
        SearchMatchOptions mot = new() { WholeWord = true };

        Assert.True(SearchMatcher.Matches("chat", ["(chat) noir"], mot));
        Assert.False(SearchMatcher.Matches("chat", ["chat9"], mot));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnTermePlusCourtQueLeSeuilEstIgnoreSansEcarterLesAutres()
    {
        SearchMatchOptions seuil = new() { MinimumTermLength = 3 };

        // « de » est écarté ; « commande » décide seul.
        Assert.True(SearchMatcher.Matches("de commande", ["Créer une commande"], seuil));
        Assert.False(SearchMatcher.Matches("de facture", ["Créer une commande"], seuil));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnTermeExactementAuSeuilEstRetenu()
    {
        SearchMatchOptions seuil = new() { MinimumTermLength = 3 };

        Assert.Equal(["api"], SearchMatcher.SplitTerms("api", seuil));
        Assert.Empty(SearchMatcher.SplitTerms("ap", seuil));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void TousLesTermesEcartesReviennentAUneRequeteVide()
    {
        SearchMatchOptions seuil = new() { MinimumTermLength = 4 };

        Assert.True(SearchMatcher.Matches("de la", ["n'importe quoi"], seuil));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LeNombreDeTermesEstBorne()
    {
        SearchMatchOptions borne = new() { MaximumTerms = 2 };

        Assert.Equal(["un", "deux"], SearchMatcher.SplitTerms("un deux trois quatre", borne));

        // Les termes au-delà de la borne sont ignorés, pas rejetés.
        Assert.True(SearchMatcher.Matches("un deux absent", ["un deux"], borne));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnSeuilDeLongueurNegatifEstRefuse()
    {
        SearchMatchOptions invalide = new() { MinimumTermLength = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => invalide.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => SearchMatcher.Matches("a", ["a"], invalide));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneBorneDeTermesNulleOuNegativeEstRefusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SearchMatchOptions { MaximumTerms = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SearchMatchOptions { MaximumTerms = -3 }.Validate());
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        SearchMatchOptions.Default.Validate();

        Assert.True(SearchMatchOptions.Default.IgnoreCase);
        Assert.True(SearchMatchOptions.Default.IgnoreDiacritics);
        Assert.False(SearchMatchOptions.Default.WholeWord);
        Assert.Equal(1, SearchMatchOptions.Default.MinimumTermLength);
        Assert.Equal(12, SearchMatchOptions.Default.MaximumTerms);
    }

    // ==================== Découpage ====================

    [Fact]
    public void LesTermesSontReplieDedoublonnesEtOrdonnes()
    {
        Assert.Equal(["resume", "client"], SearchMatcher.SplitTerms("Résumé   client RESUME"));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UneRequeteSansTermeDonneUneListeVide()
    {
        Assert.Empty(SearchMatcher.SplitTerms(null));
        Assert.Empty(SearchMatcher.SplitTerms("   "));
    }

    [Fact]
    public void LesTabulationsEtRetoursLigneSeparentLesTermes()
    {
        Assert.Equal(["un", "deux", "trois"], SearchMatcher.SplitTerms("un\tdeux\r\ntrois"));
    }

    [Fact]
    public void DesTermesDejaDecoupesDonnentLeMemeVerdict()
    {
        IReadOnlyList<string> termes = SearchMatcher.SplitTerms("Résumé client");

        Assert.True(SearchMatcher.MatchesTerms(termes, ["resume de la commande", "client"]));
        Assert.False(SearchMatcher.MatchesTerms(termes, ["resume de la commande"]));
    }

    [Fact]
    public void DesTermesVidesAcceptentTout()
    {
        Assert.True(SearchMatcher.MatchesTerms([], []));
        Assert.True(SearchMatcher.MatchesTerms([], ["quoi que ce soit"]));
    }

    // ==================== Filtrage ====================

    [Fact]
    public void LeFiltreRetientLesElementsQuiRepondent()
    {
        Etape[] etapes =
        [
            new("Appel REST", "Interroge une API", ["api", "http"]),
            new("Requête SQL", "Interroge une base", ["base", "sql"]),
            new("Découper un texte", null, ["texte"]),
        ];

        List<Etape> retenus = [.. SearchMatcher.Filter(
            etapes,
            "interroge",
            e => [e.Nom, e.Description, .. e.Tags])];

        Assert.Equal(["Appel REST", "Requête SQL"], retenus.Select(e => e.Nom));
    }

    [Fact]
    public void LeFiltreConserveLOrdreDOrigine()
    {
        string[] source = ["zebre", "abeille", "zebu"];

        Assert.Equal(["zebre", "zebu"], SearchMatcher.Filter(source, "z", s => [s]));
    }

    [Fact]
    public void UnFiltreSansRequeteRendLaSequenceInchangee()
    {
        string[] source = ["a", "b", "c"];

        Assert.Equal(source, SearchMatcher.Filter(source, "  ", s => [s]));
    }

    [Fact]
    public void LeFiltreEstDifferePasEvalueALAppel()
    {
        bool parcouru = false;

        IEnumerable<string> Source()
        {
            parcouru = true;
            yield return "a";
        }

        IEnumerable<string> filtre = SearchMatcher.Filter(Source(), "a", s => [s]);
        Assert.False(parcouru);

        _ = filtre.ToList();
        Assert.True(parcouru);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void LeFiltreValideSesArgumentsAvantTouteEnumeration()
    {
        // La validation ne doit pas attendre le premier MoveNext : l'appelant qui construit
        // sa requête loin de l'endroit où il l'énumère doit être averti tout de suite.
        Assert.Throws<ArgumentNullException>(() => SearchMatcher.Filter<string>(null!, "a", s => [s]));
    }

    [Fact]
    public void LeMemeAppelDonneToujoursLeMemeResultat()
    {
        for (int i = 0; i < 5; i++)
        {
            Assert.True(SearchMatcher.Matches("resume client", ["Résumé", "Client final"]));
        }
    }
}
