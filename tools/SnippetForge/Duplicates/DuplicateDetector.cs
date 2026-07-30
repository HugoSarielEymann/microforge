using SnippetForge.Embeddings;

namespace SnippetForge.Duplicates;

/// <summary>
/// Verdict de similarité entre un candidat et un package déjà publié.
/// <paramref name="ApiSimilarity"/> n'est renseigné qu'après confrontation des contrats publics.
/// </summary>
public sealed record DuplicateCandidate(
    string PackageId,
    string Version,
    double Similarity,
    DuplicateSeverity Severity,
    double? ApiSimilarity = null);

/// <summary>Gravité d'une proximité détectée.</summary>
public enum DuplicateSeverity
{
    /// <summary>Sous le seuil d'alerte : packages distincts.</summary>
    None = 0,

    /// <summary>Proximité notable : à examiner avant publication.</summary>
    Warning = 1,

    /// <summary>Quasi-duplication : publication refusée sans dérogation explicite.</summary>
    Blocking = 2,
}

/// <summary>
/// Détecte les quasi-duplications par similarité vectorielle du texte descriptif
/// (identifiant + tags + description + mode d'emploi).
/// </summary>
public static class DuplicateDetector
{
    /// <summary>
    /// Construit le texte représentatif d'un package. Concentré sur l'intention
    /// (que fait le package) plutôt que sur l'implémentation.
    /// </summary>
    public static string DescribeForEmbedding(string id, IEnumerable<string> tags, string description, string readme)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(readme);

        // Le README est tronqué : les sections Exemple/Paramétrage introduisent du bruit
        // syntaxique qui rapprocherait artificiellement des packages sans rapport.
        var trimmedReadme = readme.Length > 600 ? readme[..600] : readme;
        return $"{id.Replace('.', ' ')}. Tags: {string.Join(", ", tags)}. {description} {trimmedReadme}";
    }

    /// <summary>
    /// Compare <paramref name="candidateVector"/> à chaque package publié et retourne
    /// les proximités au-dessus du seuil d'avertissement, les plus fortes d'abord.
    /// Les versions antérieures du package candidat lui-même sont exclues.
    /// </summary>
    public static IReadOnlyList<DuplicateCandidate> Detect(
        string candidateId,
        IReadOnlyList<float> candidateVector,
        IReadOnlyDictionary<string, float[]> publishedVectors,
        IReadOnlyDictionary<string, string> publishedVersions,
        DuplicateThresholds thresholds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ArgumentNullException.ThrowIfNull(candidateVector);
        ArgumentNullException.ThrowIfNull(publishedVectors);
        ArgumentNullException.ThrowIfNull(publishedVersions);
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();

        var (warningThreshold, blockingThreshold) = (thresholds.Warning, thresholds.Blocking);

        var results = new List<DuplicateCandidate>();
        foreach (var (id, vector) in publishedVectors)
        {
            if (id.Equals(candidateId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (vector.Length != candidateVector.Count)
            {
                // Vecteurs produits par des providers différents : incomparables, on saute.
                continue;
            }

            var similarity = VectorMath.Cosine(candidateVector, vector);
            var severity = similarity >= blockingThreshold ? DuplicateSeverity.Blocking
                : similarity >= warningThreshold ? DuplicateSeverity.Warning
                : DuplicateSeverity.None;

            if (severity != DuplicateSeverity.None)
            {
                results.Add(new DuplicateCandidate(
                    id,
                    publishedVersions.GetValueOrDefault(id, "?"),
                    similarity,
                    severity));
            }
        }

        return results.OrderByDescending(r => r.Similarity).ToList();
    }

    /// <summary>
    /// Seuil de similarité de forme d'API au-delà duquel une simple proximité de
    /// description devient une quasi-duplication avérée.
    /// </summary>
    public const double ApiEscalationThreshold = 0.60;

    /// <summary>
    /// Confronte chaque proximité au contrat public correspondant. Une description
    /// reformulée peut tromper la détection lexicale ; deux contrats de même forme,
    /// non. L'escalade ne s'applique qu'aux avertissements déjà émis : la forme d'API
    /// seule produirait trop de faux positifs.
    /// </summary>
    public static IReadOnlyList<DuplicateCandidate> Escalate(
        IReadOnlyList<DuplicateCandidate> candidates,
        Func<string, double?> apiSimilarity,
        double apiThreshold = ApiEscalationThreshold)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(apiSimilarity);

        return candidates
            .Select(candidate =>
            {
                var similarity = apiSimilarity(candidate.PackageId);
                if (similarity is not { } value)
                {
                    return candidate;
                }

                var escalated = candidate.Severity == DuplicateSeverity.Warning && value >= apiThreshold
                    ? DuplicateSeverity.Blocking
                    : candidate.Severity;

                return candidate with { Severity = escalated, ApiSimilarity = value };
            })
            .OrderByDescending(c => c.Severity)
            .ThenByDescending(c => c.Similarity)
            .ToList();
    }
}
