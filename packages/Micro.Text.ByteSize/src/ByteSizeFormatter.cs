using System.Globalization;

namespace Micro.Text.ByteSize;

/// <summary>
/// Met une quantité d'octets — ou un débit — sous la forme la plus courte qu'un humain
/// lise sans compter les chiffres : la valeur est mise à l'échelle jusqu'au préfixe qui
/// la ramène sous mille (ou mille vingt-quatre), et arrondie à trois chiffres significatifs.
/// </summary>
/// <remarks>
/// L'arrondi est appliqué <em>avant</em> le choix définitif du préfixe : 1023,9 octets
/// affichés sans décimale ne donnent pas « 1024 B » mais « 1 KiB ».
/// </remarks>
public static class ByteSizeFormatter
{
    private static readonly string[] BinaryPrefixes = ["", "Ki", "Mi", "Gi", "Ti", "Pi", "Ei"];
    private static readonly string[] MetricPrefixes = ["", "k", "M", "G", "T", "P", "E"];

    /// <summary>Met en forme une quantité d'octets.</summary>
    /// <param name="bytes">Quantité à représenter ; une valeur négative est rendue avec son signe.</param>
    /// <param name="options">Paramétrage ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <returns>La quantité mise à l'échelle, suivie du séparateur et de l'unité.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="ByteSizeOptions.MaxDecimals"/> sort de l'intervalle 0 à 15.</exception>
    /// <exception cref="ArgumentNullException">Si une chaîne du paramétrage est nulle.</exception>
    public static string Format(long bytes, ByteSizeOptions? options = null)
    {
        ByteSizeOptions effective = options ?? new ByteSizeOptions();
        effective.Validate();

        // La conversion en double précède toute valeur absolue : Math.Abs(long.MinValue)
        // déborderait, alors que le double couvre l'intervalle sans exception.
        return Render(bytes, effective);
    }

    /// <summary>Met en forme un débit exprimé en octets par seconde.</summary>
    /// <param name="bytesPerSecond">
    /// Débit à représenter. Une valeur non finie (<see cref="double.NaN"/>, infini) — cas
    /// courant quand le débit vient d'une division par une durée nulle — rend
    /// <see cref="ByteSizeOptions.NonFiniteText"/> au lieu d'un texte aberrant.
    /// </param>
    /// <param name="options">Paramétrage ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <returns>Le débit mis à l'échelle, suivi de l'unité et du suffixe de débit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="ByteSizeOptions.MaxDecimals"/> sort de l'intervalle 0 à 15.</exception>
    /// <exception cref="ArgumentNullException">Si une chaîne du paramétrage est nulle.</exception>
    public static string FormatRate(double bytesPerSecond, ByteSizeOptions? options = null)
    {
        ByteSizeOptions effective = options ?? new ByteSizeOptions();
        effective.Validate();

        return double.IsFinite(bytesPerSecond)
            ? Render(bytesPerSecond, effective) + effective.RateSuffix
            : effective.NonFiniteText;
    }

    private static string Render(double raw, ByteSizeOptions options)
    {
        string[] prefixes = options.UnitSystem == ByteSizeUnitSystem.Binary ? BinaryPrefixes : MetricPrefixes;
        double factor = options.UnitSystem == ByteSizeUnitSystem.Binary ? 1024d : 1000d;

        bool negative = double.IsNegative(raw);
        double value = Math.Abs(raw);
        int index = 0;

        while (value >= factor && index < prefixes.Length - 1)
        {
            value /= factor;
            index++;
        }

        int decimals = DecimalsFor(value, index, options);

        // L'arrondi peut faire franchir le seuil (1023,9 B à zéro décimale « vaut » 1024) :
        // on remonte alors d'un préfixe plutôt que d'afficher une valeur hors échelle.
        if (Math.Round(value, decimals, MidpointRounding.AwayFromZero) >= factor && index < prefixes.Length - 1)
        {
            value /= factor;
            index++;
            decimals = DecimalsFor(value, index, options);
        }

        string number = value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), options.Provider);
        return (negative ? "-" : string.Empty) + number + options.Separator + prefixes[index] + options.ByteSymbol;
    }

    private static int DecimalsFor(double value, int prefixIndex, ByteSizeOptions options)
    {
        // Un octet ne se divise pas : sans préfixe, la valeur reste entière.
        if (prefixIndex == 0)
        {
            return 0;
        }

        if (!options.AdaptivePrecision)
        {
            return options.MaxDecimals;
        }

        int wanted = value switch
        {
            < 10d => 2,
            < 100d => 1,
            _ => 0,
        };

        return Math.Min(wanted, options.MaxDecimals);
    }
}
