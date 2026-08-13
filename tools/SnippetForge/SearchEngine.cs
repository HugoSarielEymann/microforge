namespace SnippetForge;

/// <summary>
/// Moteur de recherche hybride : score lexical pondéré (identifiant, tags, description,
/// mode d'emploi) combiné à une similarité vectorielle optionnelle.
///
/// Le lexical seul rate les synonymes (« découper » / « chunk ») ; le vectoriel seul
/// dilue les correspondances exactes de tags. La combinaison couvre les deux cas.
/// </summary>
public static class SearchEngine
{
    private const double IdWeight = 4.0;
    private const double TagWeight = 3.0;
    private const double DescriptionWeight = 2.0;
    private const double ReadmeWeight = 1.0;

    /// <summary>Poids du score sémantique dans le score final quand il est disponible.</summary>
    public const double SemanticWeight = 0.5;

    /// <summary>
    /// Recherche les packages correspondant à la requête.
    /// <paramref name="requiredTags"/> filtre strictement : seuls les packages portant
    /// tous ces tags sont retenus. <paramref name="semanticScore"/> est optionnel ;
    /// s'il est fourni, il doit retourner une similarité dans [0, 1].
    /// </summary>
    public static IReadOnlyList<SearchHit> Search(
        IndexDocument index,
        string query,
        IReadOnlyList<string> requiredTags,
        Func<IndexEntry, double>? semanticScore = null,
        string? language = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(requiredTags);

        var tokens = Tokenize(query);
        var filtered = index.Packages
            .Where(p => requiredTags.All(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            // Proposer un package Python à un projet C# serait pire qu'inutile : il
            // ferait perdre du temps et pourrait être copié à tort.
            .Where(p => language is null || p.Language.Equals(language, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
        {
            return [];
        }

        var lexical = filtered.ToDictionary(p => p.Id, p => LexicalScore(p, tokens), StringComparer.OrdinalIgnoreCase);
        var maxLexical = lexical.Values.DefaultIfEmpty(0).Max();

        var hits = filtered
            .Select(entry =>
            {
                var normalizedLexical = maxLexical > 0 ? lexical[entry.Id] / maxLexical : 0;
                var semantic = semanticScore?.Invoke(entry) ?? 0;
                var combined = semanticScore is null
                    ? normalizedLexical
                    : ((1 - SemanticWeight) * normalizedLexical) + (SemanticWeight * semantic);

                return new SearchHit(entry, combined, lexical[entry.Id], semanticScore is null ? null : semantic);
            })
            .Where(h => tokens.Count == 0 || h.Score > 0.01)
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return hits;
    }

    /// <summary>Score lexical brut d'une entrée face à des jetons de requête.</summary>
    public static double LexicalScore(IndexEntry entry, IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(tokens);

        if (tokens.Count == 0)
        {
            return 0;
        }

        var idTokens = Tokenize(entry.Id);
        var descriptionTokens = Tokenize(entry.Description);
        var readmeTokens = Tokenize(entry.Readme);
        var tagSet = new HashSet<string>(entry.Tags.Select(t => t.ToLowerInvariant()), StringComparer.Ordinal);

        double score = 0;
        foreach (var token in tokens)
        {
            if (idTokens.Contains(token)) score += IdWeight;
            if (tagSet.Any(tag => tag.Contains(token, StringComparison.Ordinal))) score += TagWeight;
            if (descriptionTokens.Contains(token)) score += DescriptionWeight;
            if (readmeTokens.Contains(token)) score += ReadmeWeight;
        }

        return score;
    }

    /// <summary>Découpe un texte en jetons normalisés (minuscules, longueur &gt; 1).</summary>
    public static List<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.ToLowerInvariant()
            .Split(
                [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}',
                 '<', '>', '"', '\'', '/', '\\', '-', '_', '=', '#', '`', '*', '|'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
