using System.Globalization;
using System.Text;

namespace Micro.Text.IdentifierCase;

/// <summary>
/// Transforme un libellé écrit pour un humain en identifiant exploitable par un langage
/// ou un format de données, dans la convention demandée.
/// </summary>
/// <remarks>
/// Fonction pure et déterministe, sans dépendance. Le libellé est d'abord découpé en mots —
/// sur la ponctuation, les espaces, et les changements de casse — puis recomposé selon la
/// convention. « Numéro de TVA intracommunautaire » devient ainsi <c>NumeroDeTvaIntracommunautaire</c>
/// en Pascal ou <c>numero_de_tva_intracommunautaire</c> en Snake.
/// </remarks>
public static class IdentifierCaser
{
    /// <summary>Translittérations que la décomposition Unicode ne couvre pas : ces lettres n'ont pas de forme décomposée.</summary>
    private static readonly Dictionary<char, string> Transliterations = new()
    {
        ['æ'] = "ae", ['Æ'] = "Ae",
        ['œ'] = "oe", ['Œ'] = "Oe",
        ['ø'] = "o", ['Ø'] = "O",
        ['ß'] = "ss",
        ['đ'] = "d", ['Đ'] = "D",
        ['ð'] = "d", ['Ð'] = "D",
        ['ł'] = "l", ['Ł'] = "L",
        ['þ'] = "th", ['Þ'] = "Th",
        ['ħ'] = "h", ['Ħ'] = "H",
        ['ı'] = "i", ['İ'] = "I",
    };

    /// <summary>
    /// Convertit <paramref name="label"/> en identifiant selon <paramref name="options"/>.
    /// </summary>
    /// <param name="label">Libellé source, tel que saisi par un humain.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> applique <see cref="IdentifierCaseOptions.Default"/>.</param>
    /// <returns>L'identifiant produit, jamais vide.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="label"/> est <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="label"/> ne contient aucun caractère exploitable une fois le paramétrage
    /// appliqué, ou si <paramref name="options"/> est incohérent.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <see cref="IdentifierCaseOptions.MaxLength"/> est inférieur à 1.
    /// </exception>
    public static string ToIdentifier(string label, IdentifierCaseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(label);

        IdentifierCaseOptions effective = options ?? IdentifierCaseOptions.Default;
        effective.Validate();

        string? identifier = Build(label, effective);
        return identifier
            ?? throw new ArgumentException(
                "Le libellé ne contient aucune lettre ni chiffre exploitable pour former un identifiant.",
                nameof(label));
    }

    /// <summary>
    /// Variante non levante de <see cref="ToIdentifier"/>.
    /// </summary>
    /// <param name="label">Libellé source ; <see langword="null"/> est accepté et rend <see langword="false"/>.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> applique <see cref="IdentifierCaseOptions.Default"/>.</param>
    /// <param name="identifier">L'identifiant produit, ou la chaîne vide en cas d'échec.</param>
    /// <returns>
    /// <see langword="true"/> si un identifiant a pu être formé. <see langword="false"/> si le libellé
    /// est nul, vide, dépourvu de caractère exploitable, ou si le paramétrage est incohérent.
    /// </returns>
    /// <remarks>
    /// Cette méthode ne lève jamais : elle est destinée aux saisies en cours de frappe,
    /// où un libellé transitoirement inexploitable est normal et non une erreur.
    /// </remarks>
    public static bool TryToIdentifier(string? label, IdentifierCaseOptions? options, out string identifier)
    {
        identifier = string.Empty;

        if (label is null)
        {
            return false;
        }

        IdentifierCaseOptions effective = options ?? IdentifierCaseOptions.Default;
        if (!IsCoherent(effective))
        {
            return false;
        }

        string? built = Build(label, effective);
        if (built is null)
        {
            return false;
        }

        identifier = built;
        return true;
    }

    private static bool IsCoherent(IdentifierCaseOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Produit l'identifiant, ou <see langword="null"/> si le libellé ne donne aucun mot.</summary>
    private static string? Build(string label, IdentifierCaseOptions options)
    {
        List<string> words = SplitWords(label, options.AsciiOnly);
        if (words.Count == 0)
        {
            return null;
        }

        string identifier = Compose(words, options.Style);

        if (identifier.Length > 0 && char.IsDigit(identifier[0]))
        {
            identifier = options.LeadingDigitPrefix + identifier;
        }

        if (options.MaxLength is { } max && identifier.Length > max)
        {
            identifier = identifier[..max].TrimEnd('_', '-');
            if (identifier.Length == 0)
            {
                return null;
            }
        }

        // Volontairement après la coupe : un identifiant qui entre en collision avec un mot
        // réservé est inutilisable, alors qu'un identifiant trop long reste valide.
        if (options.ReservedWords?.Contains(identifier) == true)
        {
            identifier += options.ReservedWordSuffix;
        }

        return identifier;
    }

    private static List<string> SplitWords(string label, bool asciiOnly)
    {
        string flattened = Flatten(label);

        List<string> words = [];
        StringBuilder current = new();

        for (int i = 0; i < flattened.Length; i++)
        {
            char c = flattened[i];

            if (!IsUsable(c, asciiOnly))
            {
                Flush(words, current);
                continue;
            }

            if (current.Length > 0 && StartsNewWord(flattened, i, current[^1]))
            {
                Flush(words, current);
            }

            current.Append(c);
        }

        Flush(words, current);
        return words;

        static void Flush(List<string> words, StringBuilder current)
        {
            if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }
    }

    /// <summary>Retire les diacritiques et translittère les lettres sans forme décomposée.</summary>
    private static string Flatten(string label)
    {
        string decomposed = label.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (Transliterations.TryGetValue(c, out string? replacement))
            {
                builder.Append(replacement);
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool IsUsable(char c, bool asciiOnly)
        => asciiOnly
            ? c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
            : char.IsLetterOrDigit(c);

    /// <summary>
    /// Détecte une frontière de mot interne au flux de lettres : passage en majuscule après
    /// une minuscule ou un chiffre, ou fin d'un sigle (« HTTPRéponse » donne « HTTP », « Reponse »).
    /// </summary>
    private static bool StartsNewWord(string text, int index, char previous)
    {
        char c = text[index];
        if (!char.IsUpper(c))
        {
            return false;
        }

        if (!char.IsUpper(previous))
        {
            return true;
        }

        return index + 1 < text.Length && char.IsLower(text[index + 1]);
    }

    private static string Compose(List<string> words, IdentifierStyle style) => style switch
    {
        IdentifierStyle.Pascal => string.Concat(words.Select(Capitalize)),
        IdentifierStyle.Camel => string.Concat(words.Select(static (w, i) => i == 0 ? Lower(w) : Capitalize(w))),
        IdentifierStyle.Snake => string.Join('_', words.Select(Lower)),
        IdentifierStyle.Kebab => string.Join('-', words.Select(Lower)),
        _ => string.Join('_', words.Select(static w => w.ToUpperInvariant())),
    };

    private static string Capitalize(string word)
        => string.Create(word.Length, word, static (span, source) =>
        {
            span[0] = char.ToUpperInvariant(source[0]);
            for (int i = 1; i < source.Length; i++)
            {
                span[i] = char.ToLowerInvariant(source[i]);
            }
        });

    /// <summary>
    /// Minuscules caractère par caractère plutôt que <c>ToLowerInvariant()</c> : les conventions
    /// snake_case et kebab-case exigent des minuscules, ce que la règle « normaliser en majuscules »
    /// interdirait à tort.
    /// </summary>
    private static string Lower(string word)
        => string.Create(word.Length, word, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                span[i] = char.ToLowerInvariant(source[i]);
            }
        });
}
