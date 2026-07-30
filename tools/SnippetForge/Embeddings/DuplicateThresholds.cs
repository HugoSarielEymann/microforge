namespace SnippetForge.Embeddings;

/// <summary>
/// Seuils de similarité au-delà desquels deux packages sont jugés proches, puis
/// quasi identiques.
///
/// Ces valeurs ne sont pas universelles : elles dépendent de l'espace vectoriel qui
/// produit les cosinus. Un modèle sémantique concentre les scores vers le haut, une
/// projection lexicale les étale vers le bas. Chaque provider porte donc sa propre
/// calibration, plutôt qu'une constante globale qui serait fausse pour l'un ou l'autre.
/// </summary>
public sealed record DuplicateThresholds(double Warning, double Blocking)
{
    /// <summary>
    /// Calibration d'un modèle d'embedding sémantique (nomic-embed-text et équivalents).
    /// </summary>
    public static DuplicateThresholds Semantic { get; } = new(Warning: 0.82, Blocking: 0.92);

    /// <summary>
    /// Calibration du repli par hachage de n-grammes. Mesurée sur la bibliothèque de
    /// référence : paires sans rapport ≈ 0.29–0.39, reformulation d'un même besoin ≈ 0.70.
    /// </summary>
    public static DuplicateThresholds Lexical { get; } = new(Warning: 0.55, Blocking: 0.68);

    /// <summary>Valide la cohérence des seuils.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si les seuils sortent de [0, 1] ou sont mal ordonnés.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Warning);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Blocking, 1.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Warning, Blocking);
    }
}
