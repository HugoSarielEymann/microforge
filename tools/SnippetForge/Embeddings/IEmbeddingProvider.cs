namespace SnippetForge.Embeddings;

/// <summary>
/// Source de vecteurs sémantiques. Deux implémentations coexistent : un modèle local
/// servi par Ollama (sémantique réelle) et un repli déterministe intégré (lexical),
/// afin que la recherche vectorielle fonctionne sans aucune installation.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Nom court du provider, tracé dans le cache pour invalider les vecteurs hétérogènes.</summary>
    string Name { get; }

    /// <summary>Dimension des vecteurs produits.</summary>
    int Dimensions { get; }

    /// <summary>Seuils de duplication calibrés pour l'espace vectoriel de ce provider.</summary>
    DuplicateThresholds Thresholds { get; }

    /// <summary>Produit le vecteur normalisé représentant <paramref name="text"/>.</summary>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
