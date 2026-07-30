using SnippetForge.Duplicates;

namespace SnippetForge.Embeddings;

/// <summary>
/// Vue vectorielle du feed : un vecteur par package (sa dernière version), calculé
/// à la demande et mis en cache. Sert à la fois la recherche sémantique et la
/// détection de quasi-duplication.
/// </summary>
public sealed class SemanticIndex
{
    private readonly IEmbeddingProvider _provider;
    private readonly EmbeddingStore _store;

    private SemanticIndex(
        IEmbeddingProvider provider,
        EmbeddingStore store,
        IReadOnlyDictionary<string, float[]> vectors,
        IReadOnlyDictionary<string, string> versions,
        string explanation,
        bool isSemantic)
    {
        _provider = provider;
        _store = store;
        Vectors = vectors;
        Versions = versions;
        Explanation = explanation;
        IsSemantic = isSemantic;
    }

    /// <summary>Vecteur de chaque package, indexé par identifiant.</summary>
    public IReadOnlyDictionary<string, float[]> Vectors { get; }

    /// <summary>Version indexée pour chaque package.</summary>
    public IReadOnlyDictionary<string, string> Versions { get; }

    /// <summary>Explication du provider retenu, affichable à l'utilisateur.</summary>
    public string Explanation { get; }

    /// <summary>Vrai si les vecteurs proviennent d'un vrai modèle sémantique.</summary>
    public bool IsSemantic { get; }

    /// <summary>Seuils de duplication calibrés pour l'espace vectoriel courant.</summary>
    public DuplicateThresholds Thresholds => _provider.Thresholds;

    /// <summary>Construit l'index vectoriel du feed, en réutilisant le cache disque.</summary>
    public static async Task<SemanticIndex> BuildAsync(
        ForgeRoot root,
        IndexDocument index,
        bool forceOffline = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(index);

        var resolution = await EmbeddingProviderFactory.ResolveAsync(forceOffline, cancellationToken).ConfigureAwait(false);
        var store = EmbeddingStore.Load(root);

        var vectors = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in index.Packages)
        {
            var text = DuplicateDetector.DescribeForEmbedding(entry.Id, entry.Tags, entry.Description, entry.Readme);
            var key = $"{entry.Id}@{entry.LatestVersion}";
            vectors[entry.Id] = await store.GetOrComputeAsync(key, text, resolution.Provider, cancellationToken).ConfigureAwait(false);
            versions[entry.Id] = entry.LatestVersion;
        }

        store.PruneTo(index.Packages.Select(p => $"{p.Id}@{p.LatestVersion}"));
        store.Save();

        return new SemanticIndex(resolution.Provider, store, vectors, versions, resolution.Explanation, resolution.IsSemantic);
    }

    /// <summary>Vectorise un texte libre (requête de recherche ou description candidate).</summary>
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        _provider.EmbedAsync(text, cancellationToken);

    /// <summary>Similarité entre un vecteur de requête et le package indiqué, ou 0 si inconnu.</summary>
    public double SimilarityTo(string packageId, IReadOnlyList<float> queryVector) =>
        Vectors.TryGetValue(packageId, out var vector) && vector.Length == queryVector.Count
            ? VectorMath.Cosine(queryVector, vector)
            : 0;

    /// <summary>Libère les ressources du provider sous-jacent si nécessaire.</summary>
    public void Dispose()
    {
        _store.Save();
        (_provider as IDisposable)?.Dispose();
    }
}
