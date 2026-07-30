namespace SnippetForge.Embeddings;

/// <summary>
/// Sélectionne le meilleur provider disponible : le modèle local Ollama s'il répond,
/// sinon le repli déterministe intégré. La dégradation est silencieuse mais tracée.
/// </summary>
public static class EmbeddingProviderFactory
{
    /// <summary>Résultat de la résolution : le provider et l'explication du choix.</summary>
    public sealed record Resolution(IEmbeddingProvider Provider, string Explanation, bool IsSemantic);

    /// <summary>
    /// Résout le provider. Les variables d'environnement <c>MICROFORGE_EMBED_MODEL</c> et
    /// <c>MICROFORGE_EMBED_ENDPOINT</c> permettent de pointer un autre modèle local.
    /// <paramref name="forceOffline"/> impose le repli, utile en test et en CI.
    /// </summary>
    public static async Task<Resolution> ResolveAsync(bool forceOffline = false, CancellationToken cancellationToken = default)
    {
        if (forceOffline)
        {
            return new Resolution(
                new HashingEmbeddingProvider(),
                "Provider local déterministe (mode hors ligne imposé).",
                IsSemantic: false);
        }

        var model = Environment.GetEnvironmentVariable("MICROFORGE_EMBED_MODEL") ?? OllamaEmbeddingProvider.DefaultModel;
        var endpoint = Environment.GetEnvironmentVariable("MICROFORGE_EMBED_ENDPOINT") ?? OllamaEmbeddingProvider.DefaultEndpoint;

        if (await OllamaEmbeddingProvider.IsAvailableAsync(model, endpoint, cancellationToken).ConfigureAwait(false))
        {
            return new Resolution(
                new OllamaEmbeddingProvider(model, endpoint),
                $"Modèle sémantique local « {model} » via Ollama.",
                IsSemantic: true);
        }

        return new Resolution(
            new HashingEmbeddingProvider(),
            $"Repli lexical déterministe (Ollama/« {model} » indisponible ; " +
            "pour la recherche sémantique : ollama serve puis ollama pull " + model + ").",
            IsSemantic: false);
    }
}
