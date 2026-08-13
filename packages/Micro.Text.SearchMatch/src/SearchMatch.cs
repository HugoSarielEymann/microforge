using System.Globalization;
using System.Text;

namespace Micro.Text.SearchMatch;

/// <summary>
/// Confronte une requête saisie par un humain aux champs interrogeables d'un enregistrement.
/// </summary>
/// <remarks>
/// La requête est découpée en termes séparés par des blancs ; l'enregistrement répond quand
/// <em>chaque</em> terme apparaît dans <em>au moins un</em> de ses champs. C'est ce qui rend
/// la saisie tolérante à l'ordre : « client commande » et « commande client » filtrent pareil.
///
/// Casse et diacritiques sont ignorés : quelqu'un qui tape « resume » doit trouver « Résumé ».
/// Fonction pure et déterministe, sans état ni effet de bord.
/// </remarks>
public static class SearchMatcher
{
    /// <summary>Indique qu'un enregistrement répond à la requête.</summary>
    /// <param name="query">Texte cherché ; nul, vide ou blanc accepte tout.</param>
    /// <param name="fields">
    /// Champs interrogeables de l'enregistrement. Les éléments nuls sont ignorés, ce qui
    /// dispense l'appelant de filtrer ses propriétés facultatives.
    /// </param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si l'enregistrement doit figurer dans les résultats.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="fields"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static bool Matches(string? query, IEnumerable<string?> fields, SearchMatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(fields);

        SearchMatchOptions settings = options ?? SearchMatchOptions.Default;
        settings.Validate();

        IReadOnlyList<string> terms = SplitTerms(query, settings);
        if (terms.Count == 0)
        {
            return true;
        }

        // Les champs sont repliés une seule fois, puis confrontés à tous les termes : replier
        // à chaque terme referait le même travail autant de fois qu'il y a de mots saisis.
        List<string> folded = [];
        foreach (string? field in fields)
        {
            if (!string.IsNullOrEmpty(field))
            {
                folded.Add(Fold(field, settings));
            }
        }

        if (folded.Count == 0)
        {
            return false;
        }

        foreach (string term in terms)
        {
            bool found = false;
            foreach (string field in folded)
            {
                if (Contains(field, term, settings))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Indique qu'un texte unique répond à la requête.</summary>
    /// <param name="query">Texte cherché ; nul, vide ou blanc accepte tout.</param>
    /// <param name="field">Champ interrogeable ; nul ou vide ne répond qu'à une requête vide.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si le texte doit figurer dans les résultats.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static bool Matches(string? query, string? field, SearchMatchOptions? options = null)
        => Matches(query, [field], options);

    /// <summary>Retient les éléments d'une séquence qui répondent à la requête.</summary>
    /// <typeparam name="T">Nature des éléments filtrés.</typeparam>
    /// <param name="source">Séquence à filtrer.</param>
    /// <param name="query">Texte cherché ; nul, vide ou blanc rend la séquence inchangée.</param>
    /// <param name="fieldSelector">Champs interrogeables d'un élément.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les éléments retenus, dans l'ordre de la séquence d'origine.</returns>
    /// <exception cref="ArgumentNullException">Si la séquence ou le sélecteur est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    /// <remarks>
    /// L'ordre d'origine est conservé : ce filtre ne classe pas par pertinence, il retranche.
    /// Un classement demanderait un score, donc un arbitrage que l'appelant est seul à pouvoir
    /// rendre.
    /// </remarks>
    public static IEnumerable<T> Filter<T>(
        IEnumerable<T> source,
        string? query,
        Func<T, IEnumerable<string?>> fieldSelector,
        SearchMatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fieldSelector);

        SearchMatchOptions settings = options ?? SearchMatchOptions.Default;
        settings.Validate();

        return Iterate();

        IEnumerable<T> Iterate()
        {
            foreach (T item in source)
            {
                if (Matches(query, fieldSelector(item) ?? [], settings))
                {
                    yield return item;
                }
            }
        }
    }

    /// <summary>Découpe une requête en termes déjà repliés, prêts à être confrontés.</summary>
    /// <param name="query">Texte cherché.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les termes retenus, sans doublon, dans l'ordre de saisie.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    /// <remarks>
    /// Exposé pour l'appelant qui confronte la même requête à des milliers d'enregistrements :
    /// découper une fois puis appeler <see cref="MatchesTerms"/> évite de refaire le découpage
    /// à chaque ligne.
    /// </remarks>
    public static IReadOnlyList<string> SplitTerms(string? query, SearchMatchOptions? options = null)
    {
        SearchMatchOptions settings = options ?? SearchMatchOptions.Default;
        settings.Validate();

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        List<string> terms = [];

        foreach (string brut in query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string terme = Fold(brut, settings);

            if (terme.Length < settings.MinimumTermLength || terms.Contains(terme, StringComparer.Ordinal))
            {
                continue;
            }

            terms.Add(terme);

            if (terms.Count >= settings.MaximumTerms)
            {
                break;
            }
        }

        return terms;
    }

    /// <summary>Confronte des termes déjà découpés aux champs d'un enregistrement.</summary>
    /// <param name="terms">Termes issus de <see cref="SplitTerms"/>.</param>
    /// <param name="fields">Champs interrogeables ; les éléments nuls sont ignorés.</param>
    /// <param name="options">Réglages ; doivent être ceux passés à <see cref="SplitTerms"/>.</param>
    /// <returns><see langword="true"/> si chaque terme apparaît dans au moins un champ.</returns>
    /// <exception cref="ArgumentNullException">Si les termes ou les champs sont nuls.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static bool MatchesTerms(
        IReadOnlyList<string> terms,
        IEnumerable<string?> fields,
        SearchMatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(fields);

        SearchMatchOptions settings = options ?? SearchMatchOptions.Default;
        settings.Validate();

        if (terms.Count == 0)
        {
            return true;
        }

        List<string> folded = [];
        foreach (string? field in fields)
        {
            if (!string.IsNullOrEmpty(field))
            {
                folded.Add(Fold(field, settings));
            }
        }

        foreach (string term in terms)
        {
            bool found = false;
            foreach (string field in folded)
            {
                if (Contains(field, term, settings))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string field, string term, SearchMatchOptions options)
        => options.WholeWord
            ? ContainsWord(field, term)
            : field.Contains(term, StringComparison.Ordinal);

    /// <summary>Cherche un terme délimité par des frontières de mot.</summary>
    /// <param name="field">Champ replié.</param>
    /// <param name="term">Terme replié.</param>
    /// <returns><see langword="true"/> si le terme forme un mot entier du champ.</returns>
    private static bool ContainsWord(string field, string term)
    {
        int index = 0;

        while ((index = field.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            bool debutOk = index == 0 || !char.IsLetterOrDigit(field[index - 1]);
            int fin = index + term.Length;
            bool finOk = fin == field.Length || !char.IsLetterOrDigit(field[fin]);

            if (debutOk && finOk)
            {
                return true;
            }

            index += 1;
        }

        return false;
    }

    /// <summary>
    /// Ramène un texte à sa forme comparable : décomposé, dépouillé de ses diacritiques,
    /// puis mis en minuscules invariantes.
    /// </summary>
    /// <param name="value">Texte à replier.</param>
    /// <param name="options">Réglages.</param>
    /// <returns>La forme comparable du texte.</returns>
    /// <remarks>
    /// La décomposition en <c>FormD</c> sépare la lettre de son accent, qui devient une marque
    /// non espaçante que l'on peut retirer. Sans elle, « é » resterait un caractère unique
    /// distinct de « e ». Les alphabets qui n'ont pas de forme décomposée, comme le chinois,
    /// traversent l'opération inchangés — c'est le comportement voulu : on ne cherche pas à
    /// translittérer, seulement à neutraliser l'accentuation.
    /// </remarks>
    private static string Fold(string value, SearchMatchOptions options)
    {
        string source = options.IgnoreDiacritics ? value.Normalize(NormalizationForm.FormD) : value;
        StringBuilder builder = new(source.Length);

        foreach (char c in source)
        {
            if (options.IgnoreDiacritics
                && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(options.IgnoreCase ? char.ToLowerInvariant(c) : c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
