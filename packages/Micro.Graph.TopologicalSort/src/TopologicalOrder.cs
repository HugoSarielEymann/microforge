namespace Micro.Graph.TopologicalSort;

/// <summary>
/// Résultat d'un tri topologique : l'ordre obtenu, son découpage en vagues parallélisables,
/// et les nœuds que le tri n'a pas pu ordonner parce qu'ils appartiennent à un cycle.
/// </summary>
/// <typeparam name="T">Type des nœuds.</typeparam>
public sealed class TopologicalOrder<T>
{
    internal TopologicalOrder(
        IReadOnlyList<T> sorted,
        IReadOnlyList<IReadOnlyList<T>> waves,
        IReadOnlyList<T> cyclicNodes)
    {
        Sorted = sorted;
        Waves = waves;
        CyclicNodes = cyclicNodes;
    }

    /// <summary>
    /// Indique que tous les nœuds ont pu être ordonnés, c'est-à-dire que le graphe est acyclique.
    /// Lorsque cette propriété vaut <see langword="false"/>, <see cref="CyclicNodes"/> n'est pas vide.
    /// </summary>
    public bool IsComplete => CyclicNodes.Count == 0;

    /// <summary>
    /// Nœuds ordonnés : tout nœud y apparaît après ceux qui doivent le précéder.
    /// En présence d'un cycle, ne contient que la partie du graphe qui a pu être ordonnée.
    /// </summary>
    public IReadOnlyList<T> Sorted { get; }

    /// <summary>
    /// Même ordre que <see cref="Sorted"/>, regroupé en vagues : les nœuds d'une vague n'ont aucune
    /// dépendance entre eux et peuvent donc être traités en parallèle. Les vagues, elles, sont séquentielles.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<T>> Waves { get; }

    /// <summary>
    /// Nœuds impliqués dans un cycle ou dépendant d'un cycle, dans l'ordre de la collection d'entrée.
    /// Vide lorsque le graphe est acyclique.
    /// </summary>
    /// <remarks>
    /// Cette liste ne prétend pas isoler le cycle minimal : elle contient tout ce qui reste bloqué,
    /// ce qui est l'information utile pour signaler le problème à l'utilisateur.
    /// </remarks>
    public IReadOnlyList<T> CyclicNodes { get; }
}
