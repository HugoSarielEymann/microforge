namespace Micro.Graph.TopologicalSort;

/// <summary>
/// Sens de lecture des arêtes retournées par la fonction d'adjacence.
/// </summary>
/// <remarks>
/// Un même graphe se décrit indifféremment « qui vient après moi » ou « de qui je dépends ».
/// Ce réglage évite à l'appelant d'inverser lui-même ses arêtes avant l'appel.
/// </remarks>
public enum EdgeMeaning
{
    /// <summary>
    /// Les arêtes d'un nœud désignent ses <em>successeurs</em> : elles doivent être placées après lui.
    /// C'est le sens naturel d'un enchaînement d'étapes (« A précède B »).
    /// </summary>
    Precedes = 0,

    /// <summary>
    /// Les arêtes d'un nœud désignent ses <em>prédécesseurs</em> : elles doivent être placées avant lui.
    /// C'est le sens naturel d'un graphe de dépendances (« A dépend de B »).
    /// </summary>
    Follows = 1,
}
