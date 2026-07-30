using System.Globalization;
using System.Text.RegularExpressions;

namespace Micro.Text.CompactDuration;

/// <summary>
/// Parse une chaîne de durée au format compact (ex. : 30s, 5m, 2h30m, 1h15m30s) en <see cref="TimeSpan"/>.
/// Formats reconnus : combinaison de h (heures), m (minutes), s (secondes), dans cet ordre, chacun optionnel.
/// Au moins une unité doit être présente. Les valeurs doivent être des entiers non négatifs.
/// </summary>
public static partial class CompactDurationParser
{
    // Regex : optionnellement Nh, optionnellement Nm, optionnellement Ns — au moins un groupe présent.
    [GeneratedRegex(@"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DurationRegex();

    /// <summary>
    /// Parse la chaîne <paramref name="input"/> en <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="input">Chaîne de durée compacte, ex. « 30s », « 5m », « 2h30m », « 1h15m30s ».</param>
    /// <returns>Le <see cref="TimeSpan"/> correspondant.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="input"/> est null.</exception>
    /// <exception cref="FormatException">Si le format n'est pas reconnu ou si aucune unité n'est présente.</exception>
    public static TimeSpan Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return TryConvert(input, out var result)
            ? result
            : throw new FormatException(
                $"Format de durée non reconnu : « {input} ». Exemples valides : 30s, 5m, 2h30m, 1h15m30s.");
    }

    /// <summary>
    /// Tente de parser une durée compacte, sans jamais lever d'exception.
    /// </summary>
    /// <param name="input">Chaîne de durée compacte, ou null.</param>
    /// <param name="result">Résultat si la conversion réussit, sinon <see cref="TimeSpan.Zero"/>.</param>
    /// <returns><c>true</c> si la conversion a réussi, <c>false</c> sinon.</returns>
    public static bool TryParse(string? input, out TimeSpan result)
    {
        if (input is null)
        {
            result = TimeSpan.Zero;
            return false;
        }

        return TryConvert(input, out result);
    }

    /// <summary>
    /// Cœur commun à <see cref="Parse"/> et <see cref="TryParse"/> : une seule
    /// implémentation, donc un seul comportement — la duplication précédente laissait
    /// les deux chemins diverger.
    /// </summary>
    private static bool TryConvert(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var match = DurationRegex().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        // Au moins une des trois unités doit être présente : la regex accepte le vide.
        if (!match.Groups[1].Success && !match.Groups[2].Success && !match.Groups[3].Success)
        {
            return false;
        }

        // TryParse plutôt que Parse : une valeur hors bornes est un format invalide,
        // pas une exception. Sans cela, TryParse propageait OverflowException et
        // violait le contrat même du motif TryXxx.
        if (!TryUnit(match.Groups[1], out var hours) ||
            !TryUnit(match.Groups[2], out var minutes) ||
            !TryUnit(match.Groups[3], out var seconds))
        {
            return false;
        }

        try
        {
            result = new TimeSpan(hours, minutes, seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // La combinaison dépasse les bornes de TimeSpan.
            return false;
        }
    }

    private static bool TryUnit(Group group, out int value)
    {
        value = 0;
        return !group.Success || int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
