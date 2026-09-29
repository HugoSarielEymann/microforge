using System.Globalization;
using System.Text;

namespace Micro.Text.FileNameSanitize;

/// <summary>
/// Transforme un titre saisi par un humain en nom de fichier valide sous Windows.
/// </summary>
/// <remarks>
/// <para>
/// Les caractères interdits (<c>&lt; &gt; : " / \ | ? *</c>, caractères de contrôle, et ceux
/// que l'appelant ajoute) sont remplacés — ou omis quand un blanc les jouxte, pour que « Note :
/// idée » donne « Note idée » et non « Note - idée ». Les blancs sont réduits, les bords
/// débarrassés des espaces, des points et des remplacements orphelins ; un nom réservé
/// (<c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>, <c>COM1</c>–<c>COM9</c>, <c>LPT1</c>–<c>LPT9</c>,
/// avec ou sans extension) reçoit un suffixe ; la longueur est bornée sans couper un caractère.
/// </para>
/// <para>
/// Fonction pure, déterministe et idempotente : un nom déjà propre ressort inchangé.
/// </para>
/// </remarks>
public static class FileNameSanitizer
{
    private const string WindowsInvalid = "<>:\"/\\|?*";

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Transforme un titre en nom de fichier valide.</summary>
    /// <param name="name">Titre saisi ; nul ou vide rend le nom de repli.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Un nom non vide, sans caractère interdit, sans point ni espace final, non réservé.</returns>
    /// <exception cref="ArgumentNullException">Si une chaîne de réglage est nulle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="FileNameSanitizeOptions.MaxLength"/> est inférieur à 1.</exception>
    /// <exception cref="ArgumentException">Si les réglages sont incohérents (voir <see cref="FileNameSanitizeOptions.Validate"/>).</exception>
    public static string Sanitize(string? name, FileNameSanitizeOptions? options = null)
    {
        FileNameSanitizeOptions settings = options ?? FileNameSanitizeOptions.Default;
        settings.Validate();

        if (string.IsNullOrEmpty(name))
        {
            return settings.Fallback;
        }

        string cleaned = Replace(name, settings);
        cleaned = TrimEdges(cleaned, settings);
        cleaned = Truncate(cleaned, settings.MaxLength);
        cleaned = TrimEdges(cleaned, settings);

        if (cleaned.Length == 0)
        {
            return settings.Fallback;
        }

        if (IsReserved(cleaned))
        {
            cleaned = AppendToStem(cleaned, settings.ReservedNameSuffix, settings.MaxLength);
        }

        return cleaned;
    }

    /// <summary>Indique qu'un nom est déjà propre : <see cref="Sanitize"/> le rendrait inchangé.</summary>
    /// <param name="name">Nom à tester.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si le nom est non vide et valide pour ces réglages.</returns>
    /// <remarks>Ne lève jamais : un nom nul ou des réglages incohérents rendent <see langword="false"/>.</remarks>
    public static bool IsValid(string? name, FileNameSanitizeOptions? options = null)
    {
        FileNameSanitizeOptions settings = options ?? FileNameSanitizeOptions.Default;

        return !string.IsNullOrEmpty(name)
            && settings.IsValid
            && string.Equals(name, Sanitize(name, settings), StringComparison.Ordinal);
    }

    internal static bool ContainsInvalid(string value, string extra)
    {
        foreach (char c in value)
        {
            if (IsInvalid(c, extra))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Contrôle léger du nom de repli, sans passer par la validation complète des réglages.</summary>
    internal static bool IsCleanCore(string value, FileNameSanitizeOptions options)
        => !ContainsInvalid(value, options.ExtraInvalidCharacters)
            && string.Equals(value, TrimEdges(value, options), StringComparison.Ordinal)
            && !IsReserved(value);

    private static bool IsInvalid(char c, string extra)
        => c < 32 || c == 127 || WindowsInvalid.Contains(c, StringComparison.Ordinal) || extra.Contains(c, StringComparison.Ordinal);

    private static string Replace(string name, FileNameSanitizeOptions options)
    {
        StringBuilder builder = new(name.Length);

        for (int index = 0; index < name.Length; index++)
        {
            char c = name[index];

            if (char.IsWhiteSpace(c) || c < 32)
            {
                // Les sauts de ligne et tabulations deviennent des espaces : un titre collé sur
                // deux lignes reste lisible.
                if (!options.CollapseWhitespace || builder.Length == 0 || builder[^1] != ' ')
                {
                    builder.Append(options.CollapseWhitespace || c < 32 ? ' ' : c);
                }

                continue;
            }

            if (!IsInvalid(c, options.ExtraInvalidCharacters))
            {
                builder.Append(c);
                continue;
            }

            if (options.Replacement.Length == 0)
            {
                continue;
            }

            bool blankBefore = builder.Length == 0 || char.IsWhiteSpace(builder[^1]);
            bool blankAfter = IsBlankAhead(name, index + 1, options.ExtraInvalidCharacters);
            bool alreadyReplaced = builder.Length >= options.Replacement.Length
                && builder.ToString(builder.Length - options.Replacement.Length, options.Replacement.Length) == options.Replacement;

            if (!blankBefore && !blankAfter && !alreadyReplaced)
            {
                builder.Append(options.Replacement);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Retire les blancs des deux bords, les points finaux (refusés par Windows) et, sauf
    /// réglage contraire, les points de tête.
    /// </summary>
    /// <remarks>
    /// Les remplacements ne sont jamais rognés : <see cref="Replace"/> n'en insère pas en
    /// bordure, et un tiret de bord présent dans le titre d'origine lui appartient.
    /// </remarks>
    /// <summary>
    /// Ce qui suit un caractère interdit est-il un blanc ou la fin du nom ? Les autres
    /// caractères interdits qui suivent sont sautés : « Lien]] » finit bien le nom.
    /// </summary>
    private static bool IsBlankAhead(string name, int from, string extra)
    {
        for (int position = from; position < name.Length; position++)
        {
            char c = name[position];
            if (char.IsWhiteSpace(c))
            {
                return true;
            }

            if (!IsInvalid(c, extra))
            {
                return false;
            }
        }

        return true;
    }

    private static string TrimEdges(string value, FileNameSanitizeOptions options)
    {
        int start = 0;
        int end = value.Length;

        while (start < end && (char.IsWhiteSpace(value[start]) || (!options.AllowLeadingDot && value[start] == '.')))
        {
            start++;
        }

        while (end > start && (char.IsWhiteSpace(value[end - 1]) || value[end - 1] == '.'))
        {
            end--;
        }

        return value[start..end];
    }

    /// <summary>Coupe à la longueur voulue sans séparer un caractère de ses accents ni une paire de substitution.</summary>
    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        int length = 0;
        int position = 0;
        while (position < value.Length)
        {
            int element = StringInfo.GetNextTextElementLength(value, position);
            if (length + element > maxLength)
            {
                break;
            }

            length += element;
            position += element;
        }

        return value[..length];
    }

    private static bool IsReserved(string name)
    {
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string stem = (dot < 0 ? name : name[..dot]).TrimEnd();

        foreach (string reserved in ReservedNames)
        {
            if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string AppendToStem(string name, string suffix, int maxLength)
    {
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string stem = dot < 0 ? name : name[..dot];
        string rest = dot < 0 ? string.Empty : name[dot..];
        string result = stem + suffix + rest;

        // Un nom réservé fait au plus quatre caractères : le suffixe ne peut dépasser la borne
        // que si l'extension est très longue, et c'est alors elle qu'on raccourcit.
        return result.Length <= maxLength ? result : Truncate(result, maxLength);
    }
}
