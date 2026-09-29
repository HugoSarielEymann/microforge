using System.Globalization;
using Xunit;

namespace Micro.Text.ByteSize.Tests;

public sealed class ByteSizeFormatterTests
{
    /// <summary>Séparateur ordinaire, pour que les attendus restent lisibles dans ce fichier.</summary>
    private static readonly ByteSizeOptions Plain = new() { Separator = " " };

    // ---------- Cas nominaux ----------

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.00 KiB")]
    [InlineData(1536L, "1.50 KiB")]
    [InlineData(10_240L, "10.0 KiB")]
    [InlineData(102_400L, "100 KiB")]
    [InlineData(1_048_576L, "1.00 MiB")]
    [InlineData(1_073_741_824L, "1.00 GiB")]
    [InlineData(1_099_511_627_776L, "1.00 TiB")]
    public void Format_Met_A_L_Echelle_Et_Vise_Trois_Chiffres_Significatifs(long bytes, string expected) =>
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes, Plain));

    [Theory]
    [InlineData(1000L, "1.00 kB")]
    [InlineData(1_500_000L, "1.50 MB")]
    [InlineData(999L, "999 B")]
    public void Format_Applique_Les_Prefixes_Decimaux_Quand_On_Le_Demande(long bytes, string expected) =>
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes, new ByteSizeOptions
        {
            UnitSystem = ByteSizeUnitSystem.Metric,
            Separator = " ",
        }));

    [Fact]
    public void Le_Systeme_Binaire_Est_Celui_Par_Defaut()
    {
        ByteSizeOptions explicite = new() { UnitSystem = ByteSizeUnitSystem.Binary, Separator = " " };

        Assert.Equal(ByteSizeUnitSystem.Binary, new ByteSizeOptions().UnitSystem);
        Assert.Equal(ByteSizeFormatter.Format(1_048_576, Plain), ByteSizeFormatter.Format(1_048_576, explicite));
        Assert.Equal("1.00 MiB", ByteSizeFormatter.Format(1_048_576, explicite));
    }

    [Fact]
    public void Format_Derive_L_Unite_Du_Symbole_D_Octet()
    {
        ByteSizeOptions francais = new() { ByteSymbol = "o", Separator = " " };

        Assert.Equal("1.00 Kio", ByteSizeFormatter.Format(1024, francais));
        Assert.Equal("512 o", ByteSizeFormatter.Format(512, francais));
        Assert.Equal("1.00 Go", ByteSizeFormatter.Format(1_000_000_000, new ByteSizeOptions
        {
            ByteSymbol = "o",
            UnitSystem = ByteSizeUnitSystem.Metric,
            Separator = " ",
        }));
    }

    [Fact]
    public void Format_Suit_La_Culture_Fournie_Pour_Le_Separateur_Decimal()
    {
        ByteSizeOptions francais = new()
        {
            ByteSymbol = "o",
            Separator = " ",
            FormatProvider = CultureInfo.GetCultureInfo("fr-FR"),
        };

        Assert.Equal("1,50 Kio", ByteSizeFormatter.Format(1536, francais));
    }

    [Fact]
    public void Format_Sans_Precision_Adaptative_Applique_Le_Maximum_De_Decimales()
    {
        ByteSizeOptions fixe = new() { AdaptivePrecision = false, MaxDecimals = 3, Separator = " " };

        Assert.Equal("1.500 KiB", ByteSizeFormatter.Format(1536, fixe));
        Assert.Equal("100.000 KiB", ByteSizeFormatter.Format(102_400, fixe));
    }

    [Fact]
    public void Format_Plafonne_La_Precision_Adaptative_Au_Maximum_Declare()
    {
        Assert.Equal("1.5 KiB", ByteSizeFormatter.Format(1536, new ByteSizeOptions { MaxDecimals = 1, Separator = " " }));
        Assert.Equal("2 KiB", ByteSizeFormatter.Format(1536, new ByteSizeOptions { MaxDecimals = 0, Separator = " " }));
    }

    [Fact]
    public void Format_Separe_Par_Un_Espace_Insecable_Par_Defaut() =>
        Assert.Equal("1.00 KiB", ByteSizeFormatter.Format(1024));

    // ---------- Débits ----------

    [Fact]
    public void FormatRate_Ajoute_Le_Suffixe_De_Debit()
    {
        Assert.Equal("1.00 MiB/s", ByteSizeFormatter.FormatRate(1_048_576d, Plain));
        Assert.Equal("0 B/s", ByteSizeFormatter.FormatRate(0d, Plain));
        Assert.Equal("1.00 Kio par seconde", ByteSizeFormatter.FormatRate(1024d, new ByteSizeOptions
        {
            ByteSymbol = "o",
            Separator = " ",
            RateSuffix = " par seconde",
        }));
    }

    [Fact]
    public void FormatRate_Represente_Les_Debits_Fractionnaires() =>
        Assert.Equal("1.50 KiB/s", ByteSizeFormatter.FormatRate(1536.4d, Plain));

    // ---------- Erreurs de paramétrage ----------

    [Fact]
    public void Options_Validate_Refuse_Un_Nombre_De_Decimales_Hors_Bornes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteSizeOptions { MaxDecimals = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteSizeOptions { MaxDecimals = 16 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSizeFormatter.Format(1024, new ByteSizeOptions { MaxDecimals = 42 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSizeFormatter.FormatRate(1024d, new ByteSizeOptions { MaxDecimals = -3 }));

        new ByteSizeOptions { MaxDecimals = 0 }.Validate();
        new ByteSizeOptions { MaxDecimals = 15 }.Validate();
    }

    // ---------- Aléas déclarés ----------

    [Fact]
    [Trait("hazard", "null-input")]
    public void Chaines_Nulles_Du_Parametrage_Sont_Signalees()
    {
        Assert.Throws<ArgumentNullException>(() => new ByteSizeOptions { ByteSymbol = null! }.Validate());
        Assert.Throws<ArgumentNullException>(() => new ByteSizeOptions { Separator = null! }.Validate());
        Assert.Throws<ArgumentNullException>(() => new ByteSizeOptions { RateSuffix = null! }.Validate());
        Assert.Throws<ArgumentNullException>(() => new ByteSizeOptions { NonFiniteText = null! }.Validate());
        Assert.Throws<ArgumentNullException>(() => ByteSizeFormatter.Format(1024, new ByteSizeOptions { ByteSymbol = null! }));

        // Un paramétrage nul reste licite : il vaut « valeurs par défaut ».
        Assert.Equal("1.00 KiB", ByteSizeFormatter.Format(1024, null));
        Assert.Equal("1.00 KiB/s", ByteSizeFormatter.FormatRate(1024d, null));
    }

    [Fact]
    [Trait("hazard", "non-finite-number")]
    public void Debit_Non_Fini_Rend_Un_Texte_Neutre_Et_Ne_Boucle_Pas()
    {
        Assert.Equal("—", ByteSizeFormatter.FormatRate(double.NaN));
        Assert.Equal("—", ByteSizeFormatter.FormatRate(double.PositiveInfinity));
        Assert.Equal("—", ByteSizeFormatter.FormatRate(double.NegativeInfinity));
        Assert.Equal("—", ByteSizeFormatter.FormatRate(0d / 0d));
        Assert.Equal("n/d", ByteSizeFormatter.FormatRate(double.NaN, new ByteSizeOptions { NonFiniteText = "n/d" }));
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void Une_Quantite_Negative_Garde_Son_Signe()
    {
        Assert.Equal("-1.50 KiB", ByteSizeFormatter.Format(-1536, Plain));
        Assert.Equal("-512 B", ByteSizeFormatter.Format(-512, Plain));
        Assert.Equal("-1.00 MiB/s", ByteSizeFormatter.FormatRate(-1_048_576d, Plain));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void Les_Extremes_De_Int64_Ne_Debordent_Pas()
    {
        // Math.Abs(long.MinValue) lèverait : la mise à l'échelle passe par un double.
        Assert.Equal("-8.00 EiB", ByteSizeFormatter.Format(long.MinValue, Plain));
        Assert.Equal("8.00 EiB", ByteSizeFormatter.Format(long.MaxValue, Plain));

        Assert.Equal("9.22 EB", ByteSizeFormatter.Format(long.MaxValue, new ByteSizeOptions
        {
            UnitSystem = ByteSizeUnitSystem.Metric,
            Separator = " ",
        }));
        // Un débit absurde ne fait ni lever ni sortir du tableau de préfixes : il sature.
        string enorme = ByteSizeFormatter.FormatRate(double.MaxValue, Plain);
        Assert.EndsWith(" EiB/s", enorme, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void L_Arrondi_Ne_Fait_Jamais_Afficher_Une_Valeur_Hors_Echelle()
    {
        // 1 048 575 octets valent 1023,999 KiB : arrondi à l'unité, cela donnerait
        // « 1024 KiB ». Le préfixe est remonté d'un cran à la place.
        Assert.Equal("1 MiB", ByteSizeFormatter.Format(1_048_575, new ByteSizeOptions { MaxDecimals = 0, Separator = " " }));
        Assert.Equal("1.00 MiB", ByteSizeFormatter.Format(1_048_575, Plain));
        Assert.Equal("1023 B", ByteSizeFormatter.Format(1023, Plain));
        Assert.Equal("1.00 KiB", ByteSizeFormatter.Format(1024, Plain));
        Assert.Equal("1.00 kB", ByteSizeFormatter.Format(1000, new ByteSizeOptions { UnitSystem = ByteSizeUnitSystem.Metric, Separator = " " }));
        Assert.Equal("999 B", ByteSizeFormatter.Format(999, new ByteSizeOptions { UnitSystem = ByteSizeUnitSystem.Metric, Separator = " " }));
    }
}
