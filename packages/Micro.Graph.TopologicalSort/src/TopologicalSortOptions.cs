namespace Micro.Graph.TopologicalSort;

/// <summary>Paramétrage du tri topologique.</summary>
public sealed class TopologicalSortOptions
{
    /// <summary>Paramétrage par défaut : arêtes lues comme des successeurs, nœuds inconnus refusés.</summary>
    public static TopologicalSortOptions Default { get; } = new();

    /// <summary>
    /// Sens de lecture des arêtes. Défaut : <see cref="EdgeMeaning.Precedes"/>.
    /// </summary>
    public EdgeMeaning EdgeMeaning { get; init; } = EdgeMeaning.Precedes;

    /// <summary>
    /// Comportement face à une arête pointant vers un nœud absent de la collection fournie.
    /// À <see langword="false"/> (défaut), une telle arête lève ; à <see langword="true"/>, elle est ignorée.
    /// </summary>
    /// <remarks>
    /// Passer à <see langword="true"/> pour trier un sous-graphe sans avoir à filtrer les arêtes sortantes.
    /// </remarks>
    public bool IgnoreUnknownNodes { get; init; }

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <see cref="EdgeMeaning"/> ne fait pas partie des valeurs définies.
    /// </exception>
    public void Validate()
    {
        if (EdgeMeaning is not (EdgeMeaning.Precedes or EdgeMeaning.Follows))
        {
            throw new ArgumentOutOfRangeException(
                nameof(EdgeMeaning),
                EdgeMeaning,
                "Sens d'arête inconnu.");
        }
    }
}
