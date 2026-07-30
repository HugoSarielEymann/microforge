using System.Globalization;
using System.Text;

namespace Micro.Text.Slugify;

/// <summary>
/// Méthode générique unique de ce micropackage : conversion d'une chaîne
/// arbitraire en slug URL. Fonction pure, déterministe, sans état.
/// </summary>
public static class Slugifier
{
    /// <summary>Convertit <paramref name="input"/> en slug selon <paramref name="options"/>.</summary>
    /// <param name="input">Texte source (accents, ponctuation et espaces autorisés).</param>
    /// <param name="options">Paramétrage ; null pour les valeurs par défaut.</param>
    /// <returns>Le slug, éventuellement vide si le texte ne contient aucun caractère alphanumérique.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="input"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="options"/> est incohérent.</exception>
    public static string ToSlug(string input, SlugifyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= new SlugifyOptions();
        options.Validate();

        var builder = new StringBuilder(input.Length);
        var previousWasSeparator = true;

        foreach (var character in input.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append(options.Separator);
                previousWasSeparator = true;
            }
        }

        TrimTrailingSeparator(builder, options.Separator);
        if (options.MaxLength is { } max && builder.Length > max)
        {
            builder.Length = max;
            TrimTrailingSeparator(builder, options.Separator);
        }

        var slug = builder.ToString().Normalize(NormalizationForm.FormC);
        return options.Lowercase ? slug.ToLowerInvariant() : slug;
    }

    /// <summary>Indique si <paramref name="value"/> est déjà un slug pour ces options.</summary>
    /// <param name="value">Chaîne à tester.</param>
    /// <param name="options">Paramétrage de référence ; null pour les valeurs par défaut.</param>
    /// <returns>Vrai si la slugification laisserait la chaîne inchangée.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="value"/> est nul.</exception>
    public static bool IsSlug(string value, SlugifyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Equals(value, ToSlug(value, options), StringComparison.Ordinal);
    }

    private static void TrimTrailingSeparator(StringBuilder builder, char separator)
    {
        while (builder.Length > 0 && builder[^1] == separator)
        {
            builder.Length--;
        }
    }
}
