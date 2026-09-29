using System.Globalization;

namespace Micro.Text.ByteSize;

/// <summary>Système de préfixes employé pour mettre une quantité d'octets à l'échelle.</summary>
public enum ByteSizeUnitSystem
{
    /// <summary>Préfixes binaires, facteur 1024 : Ki, Mi, Gi… (norme CEI 60027-2).</summary>
    Binary = 0,

    /// <summary>Préfixes décimaux, facteur 1000 : k, M, G… (norme SI, celle des constructeurs de disques).</summary>
    Metric = 1,
}

/// <summary>Paramétrage de la mise en forme d'une quantité d'octets.</summary>
public sealed class ByteSizeOptions
{
    /// <summary>Système de préfixes. Défaut : <see cref="ByteSizeUnitSystem.Binary"/>.</summary>
    public ByteSizeUnitSystem UnitSystem { get; init; } = ByteSizeUnitSystem.Binary;

    /// <summary>
    /// Symbole de l'unité de base, accolé au préfixe pour former l'unité affichée.
    /// <c>"B"</c> donne <c>B, KiB, MiB</c> ; <c>"o"</c> donne <c>o, Kio, Mio</c>. Défaut : <c>"B"</c>.
    /// </summary>
    public string ByteSymbol { get; init; } = "B";

    /// <summary>
    /// Nombre maximal de décimales. La précision adaptative ne peut que descendre en dessous.
    /// Défaut : 2.
    /// </summary>
    public int MaxDecimals { get; init; } = 2;

    /// <summary>
    /// Vise trois chiffres significatifs : <c>1,23 Mio</c>, <c>12,3 Mio</c>, <c>123 Mio</c>.
    /// Désactivée, la mise en forme applique systématiquement <see cref="MaxDecimals"/>.
    /// Les octets non préfixés restent toujours entiers. Défaut : vrai.
    /// </summary>
    public bool AdaptivePrecision { get; init; } = true;

    /// <summary>Séparateur inséré entre le nombre et l'unité. Défaut : espace insécable.</summary>
    public string Separator { get; init; } = " ";

    /// <summary>Suffixe ajouté par <see cref="ByteSizeFormatter.FormatRate"/>. Défaut : <c>"/s"</c>.</summary>
    public string RateSuffix { get; init; } = "/s";

    /// <summary>
    /// Texte rendu par <see cref="ByteSizeFormatter.FormatRate"/> pour une valeur non finie
    /// (<see cref="double.NaN"/>, infini). Défaut : <c>"—"</c>.
    /// </summary>
    public string NonFiniteText { get; init; } = "—";

    /// <summary>
    /// Culture appliquée au nombre — c'est elle qui décide du séparateur décimal.
    /// Nulle, la culture invariante s'applique : le rendu reste déterministe quelle que
    /// soit la culture du thread appelant. Défaut : nulle.
    /// </summary>
    public IFormatProvider? FormatProvider { get; init; }

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxDecimals"/> sort de l'intervalle 0 à 15.</exception>
    /// <exception cref="ArgumentNullException">Si <see cref="ByteSymbol"/>, <see cref="Separator"/>, <see cref="RateSuffix"/> ou <see cref="NonFiniteText"/> est nul.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaxDecimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxDecimals, 15);
        ArgumentNullException.ThrowIfNull(ByteSymbol);
        ArgumentNullException.ThrowIfNull(Separator);
        ArgumentNullException.ThrowIfNull(RateSuffix);
        ArgumentNullException.ThrowIfNull(NonFiniteText);
    }

    internal IFormatProvider Provider => FormatProvider ?? CultureInfo.InvariantCulture;
}
