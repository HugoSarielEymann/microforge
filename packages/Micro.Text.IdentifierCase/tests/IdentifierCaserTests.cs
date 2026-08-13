using Xunit;

namespace Micro.Text.IdentifierCase.Tests;

public sealed class IdentifierCaserTests
{
    private static IdentifierCaseOptions Style(IdentifierStyle style) => new() { Style = style };

    // ---- Conventions ----

    [Theory]
    [InlineData(IdentifierStyle.Pascal, "NomDuClient")]
    [InlineData(IdentifierStyle.Camel, "nomDuClient")]
    [InlineData(IdentifierStyle.Snake, "nom_du_client")]
    [InlineData(IdentifierStyle.Kebab, "nom-du-client")]
    [InlineData(IdentifierStyle.ScreamingSnake, "NOM_DU_CLIENT")]
    public void ToIdentifier_AppliqueLaConventionDemandee(IdentifierStyle style, string attendu)
    {
        Assert.Equal(attendu, IdentifierCaser.ToIdentifier("Nom du client", Style(style)));
    }

    [Fact]
    public void ToIdentifier_ParDefaut_EstEnPascal()
    {
        Assert.Equal("NomDuClient", IdentifierCaser.ToIdentifier("Nom du client"));
    }

    // ---- Découpage en mots ----

    [Fact]
    public void ToIdentifier_LibelleDejaEnCamel_EstRedecoupe()
    {
        Assert.Equal("nom_client", IdentifierCaser.ToIdentifier("nomClient", Style(IdentifierStyle.Snake)));
    }

    [Fact]
    public void ToIdentifier_SigleSuiviDUnMot_SepareLesDeux()
    {
        Assert.Equal("HttpResponse", IdentifierCaser.ToIdentifier("HTTPResponse"));
    }

    [Fact]
    public void ToIdentifier_SigleSeul_RestUnSeulMot()
    {
        Assert.Equal("Tva", IdentifierCaser.ToIdentifier("TVA"));
    }

    [Fact]
    public void ToIdentifier_PonctuationEtEspaces_SontDesSeparateurs()
    {
        Assert.Equal("adresse_de_livraison", IdentifierCaser.ToIdentifier(
            "  Adresse / de.livraison  ", Style(IdentifierStyle.Snake)));
    }

    [Fact]
    public void ToIdentifier_ChiffreCollleAUnMot_ResteDansLeMemeMot()
    {
        Assert.Equal("Adresse2", IdentifierCaser.ToIdentifier("adresse2"));
    }

    [Fact]
    public void ToIdentifier_SeparateursConsecutifs_NeCreentPasDeMotVide()
    {
        Assert.Equal("a_b", IdentifierCaser.ToIdentifier("a --- b", Style(IdentifierStyle.Snake)));
    }

    // ---- Chiffre initial ----

    [Fact]
    public void ToIdentifier_IdentifiantCommencantParUnChiffre_EstPrefixe()
    {
        Assert.Equal("_2024Rapport", IdentifierCaser.ToIdentifier("2024 rapport"));
    }

    [Fact]
    public void ToIdentifier_PrefixePersonnalise_EstUtilise()
    {
        var options = new IdentifierCaseOptions { LeadingDigitPrefix = "n" };

        Assert.Equal("n2024Rapport", IdentifierCaser.ToIdentifier("2024 rapport", options));
    }

    [Fact]
    public void ToIdentifier_PrefixeVide_LaisseLeChiffreInitial()
    {
        var options = new IdentifierCaseOptions { LeadingDigitPrefix = string.Empty };

        Assert.Equal("2024Rapport", IdentifierCaser.ToIdentifier("2024 rapport", options));
    }

    // ---- Mots réservés ----

    [Fact]
    public void ToIdentifier_MotReserve_RecoitLeSuffixe()
    {
        var options = new IdentifierCaseOptions
        {
            Style = IdentifierStyle.Snake,
            ReservedWords = new HashSet<string>(StringComparer.Ordinal) { "class" },
        };

        Assert.Equal("class_", IdentifierCaser.ToIdentifier("Class", options));
    }

    [Fact]
    public void ToIdentifier_MotNonReserve_EstInchange()
    {
        var options = new IdentifierCaseOptions
        {
            Style = IdentifierStyle.Snake,
            ReservedWords = new HashSet<string>(StringComparer.Ordinal) { "class" },
        };

        Assert.Equal("classe", IdentifierCaser.ToIdentifier("Classe", options));
    }

    [Fact]
    public void ToIdentifier_ComparateurDuJeuDeMotsReserves_FaitFoi()
    {
        var options = new IdentifierCaseOptions
        {
            ReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SELECT" },
        };

        Assert.Equal("Select_", IdentifierCaser.ToIdentifier("select", options));
    }

    // ---- Unicode ----

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void ToIdentifier_Diacritiques_SontRetires()
    {
        Assert.Equal("numero_de_tva_intracommunautaire", IdentifierCaser.ToIdentifier(
            "Numéro de TVA intracommunautaire", Style(IdentifierStyle.Snake)));
    }

    [Theory]
    [Trait("hazard", "unicode-edge")]
    [InlineData("Straße", "Strasse")]
    [InlineData("Sønderjylland", "Sonderjylland")]
    [InlineData("Cœur", "Coeur")]
    [InlineData("Łódź", "Lodz")]
    public void ToIdentifier_LettresSansFormeDecomposee_SontTranslitterees(string libelle, string attendu)
    {
        Assert.Equal(attendu, IdentifierCaser.ToIdentifier(libelle));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void ToIdentifier_PaireDeSubstitution_EstTraiteeCommeUnSeparateur()
    {
        Assert.Equal("Lancement", IdentifierCaser.ToIdentifier("🚀 lancement"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void ToIdentifier_EcritureNonLatine_EstEcarteeEnModeAscii()
    {
        Assert.Throws<ArgumentException>(() => IdentifierCaser.ToIdentifier("привет"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void ToIdentifier_EcritureNonLatine_EstConserveeHorsModeAscii()
    {
        var options = new IdentifierCaseOptions { AsciiOnly = false };

        Assert.Equal("Привет", IdentifierCaser.ToIdentifier("привет", options));
    }

    // ---- Longueur maximale ----

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ToIdentifier_LongueurExacte_NEstPasCoupee()
    {
        var options = new IdentifierCaseOptions { Style = IdentifierStyle.Snake, MaxLength = 13 };

        Assert.Equal("nom_du_client", IdentifierCaser.ToIdentifier("Nom du client", options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ToIdentifier_CoupeNeLaissePasDeSeparateurFinal()
    {
        var options = new IdentifierCaseOptions { Style = IdentifierStyle.Snake, MaxLength = 7 };

        Assert.Equal("nom_du", IdentifierCaser.ToIdentifier("Nom du client", options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ToIdentifier_LongueurMaximaleDeUn_RendUnSeulCaractere()
    {
        var options = new IdentifierCaseOptions { MaxLength = 1 };

        Assert.Equal("N", IdentifierCaser.ToIdentifier("Nom du client", options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ToIdentifier_CoupeNeLaissantQueDesSeparateurs_Leve()
    {
        var options = new IdentifierCaseOptions
        {
            Style = IdentifierStyle.Snake,
            LeadingDigitPrefix = "_",
            MaxLength = 1,
        };

        // « _2024 » coupé à 1 caractère ne laisse que le préfixe, donc rien d'exploitable.
        Assert.Throws<ArgumentException>(() => IdentifierCaser.ToIdentifier("2024", options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ToIdentifier_SuffixeDeMotReserve_PeutDepasserLaLongueurMaximale()
    {
        var options = new IdentifierCaseOptions
        {
            Style = IdentifierStyle.Snake,
            MaxLength = 3,
            ReservedWords = new HashSet<string>(StringComparer.Ordinal) { "for" },
        };

        // Collision réservée : la correction prime sur la longueur, et c'est documenté.
        Assert.Equal("for_", IdentifierCaser.ToIdentifier("format", options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void Validate_LongueurMaximaleNulle_Leve()
    {
        var options = new IdentifierCaseOptions { MaxLength = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    // ---- Entrées limites ----

    [Fact]
    [Trait("hazard", "null-input")]
    public void ToIdentifier_LibelleNul_Leve()
    {
        Assert.Throws<ArgumentNullException>(() => IdentifierCaser.ToIdentifier(null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void TryToIdentifier_LibelleNul_RendFauxSansLever()
    {
        Assert.False(IdentifierCaser.TryToIdentifier(null, null, out string identifier));
        Assert.Equal(string.Empty, identifier);
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void ToIdentifier_LibelleVide_Leve()
    {
        Assert.Throws<ArgumentException>(() => IdentifierCaser.ToIdentifier(string.Empty));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void ToIdentifier_LibelleSansAucuneLettreNiChiffre_Leve()
    {
        Assert.Throws<ArgumentException>(() => IdentifierCaser.ToIdentifier("--- !!! ---"));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void TryToIdentifier_LibelleVide_RendFaux()
    {
        Assert.False(IdentifierCaser.TryToIdentifier(string.Empty, null, out _));
    }

    // ---- Variante non levante ----

    [Fact]
    public void TryToIdentifier_LibelleValide_RendVraiEtLIdentifiant()
    {
        Assert.True(IdentifierCaser.TryToIdentifier("Nom du client", null, out string identifier));
        Assert.Equal("NomDuClient", identifier);
    }

    [Fact]
    public void TryToIdentifier_ParametrageIncoherent_RendFauxSansLever()
    {
        var options = new IdentifierCaseOptions { MaxLength = 0 };

        Assert.False(IdentifierCaser.TryToIdentifier("Nom du client", options, out _));
    }

    [Fact]
    public void TryToIdentifier_ConventionInconnue_RendFauxSansLever()
    {
        var options = new IdentifierCaseOptions { Style = (IdentifierStyle)99 };

        Assert.False(IdentifierCaser.TryToIdentifier("Nom du client", options, out _));
    }

    /// <summary>
    /// Contrat du motif TryXxx : aucune combinaison d'entrée et de paramétrage, si aberrante
    /// soit-elle, ne doit produire d'exception. Seul le booléen rend compte de l'échec.
    /// </summary>
    public static TheoryData<string?, IdentifierCaseOptions?> EntreesAberrantes() => new()
    {
        { null, null },
        { string.Empty, null },
        { "   ", null },
        { "!!!", new IdentifierCaseOptions { Style = (IdentifierStyle)(-1) } },
        { "привет", new IdentifierCaseOptions { MaxLength = int.MinValue } },
        { "2024", new IdentifierCaseOptions { LeadingDigitPrefix = null!, MaxLength = 0 } },
        { "\0�", new IdentifierCaseOptions { AsciiOnly = false } },
        { "🚀🚀🚀", null },
        { new string('a', 10_000), new IdentifierCaseOptions { MaxLength = 1 } },
        {
            "class",
            new IdentifierCaseOptions
            {
                ReservedWords = new HashSet<string>(StringComparer.Ordinal) { "Class" },
                ReservedWordSuffix = string.Empty,
            }
        },
    };

    [Theory]
    [MemberData(nameof(EntreesAberrantes))]
    [Trait("hazard", "malformed-input")]
    public void TryToIdentifier_EntreeAberrante_NeLeveJamais(string? label, IdentifierCaseOptions? options)
    {
        var exception = Record.Exception(
            () => IdentifierCaser.TryToIdentifier(label, options, out _));

        Assert.Null(exception);
    }

    // ---- Paramétrage ----

    [Fact]
    public void Validate_ConventionInconnue_Leve()
    {
        var options = new IdentifierCaseOptions { Style = (IdentifierStyle)99 };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_PrefixeNul_Leve()
    {
        var options = new IdentifierCaseOptions { LeadingDigitPrefix = null! };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_MotsReservesSansSuffixe_Leve()
    {
        var options = new IdentifierCaseOptions
        {
            ReservedWords = new HashSet<string>(StringComparer.Ordinal) { "class" },
            ReservedWordSuffix = string.Empty,
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void Validate_JeuDeMotsReservesVideSansSuffixe_EstTolere()
    {
        var options = new IdentifierCaseOptions
        {
            ReservedWords = new HashSet<string>(StringComparer.Ordinal),
            ReservedWordSuffix = string.Empty,
        };

        options.Validate();
    }

    [Fact]
    public void Default_EstLeParametrageDocumente()
    {
        Assert.Equal(IdentifierStyle.Pascal, IdentifierCaseOptions.Default.Style);
        Assert.True(IdentifierCaseOptions.Default.AsciiOnly);
        Assert.Equal("_", IdentifierCaseOptions.Default.LeadingDigitPrefix);
        Assert.Null(IdentifierCaseOptions.Default.MaxLength);
        Assert.Null(IdentifierCaseOptions.Default.ReservedWords);
    }

    [Fact]
    public void ToIdentifier_EstDeterministe()
    {
        string premier = IdentifierCaser.ToIdentifier("Numéro de TVA");
        string second = IdentifierCaser.ToIdentifier("Numéro de TVA");

        Assert.Equal(premier, second);
    }
}
