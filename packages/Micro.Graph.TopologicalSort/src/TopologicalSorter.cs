namespace Micro.Graph.TopologicalSort;

/// <summary>
/// Ordonne les nœuds d'un graphe orienté par tri topologique (algorithme de Kahn),
/// en les regroupant au passage en vagues parallélisables.
/// </summary>
/// <remarks>
/// Fonction pure et déterministe : à collection d'entrée et arêtes identiques, la sortie est
/// toujours la même, y compris l'ordre à l'intérieur d'une vague (celui de la collection d'entrée).
/// Un cycle n'est pas une exception mais un résultat : voir <see cref="TopologicalOrder{T}.IsComplete"/>.
/// </remarks>
public static class TopologicalSorter
{
    /// <summary>
    /// Trie <paramref name="nodes"/> selon les arêtes fournies par <paramref name="edges"/>.
    /// </summary>
    /// <typeparam name="T">Type des nœuds.</typeparam>
    /// <param name="nodes">
    /// Nœuds à ordonner. L'ordre de cette collection départage les nœuds indépendants,
    /// ce qui rend le résultat reproductible.
    /// </param>
    /// <param name="edges">
    /// Arêtes sortantes d'un nœud, interprétées selon <see cref="TopologicalSortOptions.EdgeMeaning"/>.
    /// Retourner <see langword="null"/> équivaut à retourner une séquence vide.
    /// </param>
    /// <param name="options">Paramétrage ; <see langword="null"/> applique <see cref="TopologicalSortOptions.Default"/>.</param>
    /// <param name="comparer">
    /// Comparateur d'identité des nœuds ; <see langword="null"/> applique
    /// <see cref="EqualityComparer{T}.Default"/>.
    /// </param>
    /// <returns>
    /// L'ordre obtenu. En présence d'un cycle, le tri s'arrête là où il bloque et les nœuds
    /// restants sont reportés dans <see cref="TopologicalOrder{T}.CyclicNodes"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Si <paramref name="nodes"/> ou <paramref name="edges"/> est <see langword="null"/>,
    /// ou si <paramref name="nodes"/> contient un élément <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="nodes"/> contient deux fois le même nœud au sens de
    /// <paramref name="comparer"/>, ou si une arête pointe vers un nœud absent de
    /// <paramref name="nodes"/> alors que <see cref="TopologicalSortOptions.IgnoreUnknownNodes"/>
    /// vaut <see langword="false"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="options"/> est incohérent.</exception>
    public static TopologicalOrder<T> Order<T>(
        IEnumerable<T> nodes,
        Func<T, IEnumerable<T>?> edges,
        TopologicalSortOptions? options = null,
        IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        TopologicalSortOptions effective = options ?? TopologicalSortOptions.Default;
        effective.Validate();

        List<T> all = Materialize(nodes);
        Dictionary<T, int> index = BuildIndex(all, comparer);

        int count = all.Count;
        List<int>[] successors = new List<int>[count];
        int[] inDegree = new int[count];
        for (int i = 0; i < count; i++)
        {
            successors[i] = [];
        }

        bool follows = effective.EdgeMeaning == EdgeMeaning.Follows;
        for (int i = 0; i < count; i++)
        {
            IEnumerable<T>? targets = edges(all[i]);
            if (targets is null)
            {
                continue;
            }

            foreach (T target in targets)
            {
                if (target is null || !index.TryGetValue(target, out int j))
                {
                    if (effective.IgnoreUnknownNodes)
                    {
                        continue;
                    }

                    throw new ArgumentException(
                        $"L'arête partant du nœud d'indice {i} pointe vers un nœud absent de la collection. "
                        + "Fournir ce nœud, ou activer IgnoreUnknownNodes.",
                        nameof(edges));
                }

                // « Precedes » : i doit venir avant j. « Follows » : j doit venir avant i.
                int from = follows ? j : i;
                int to = follows ? i : j;
                successors[from].Add(to);
                inDegree[to]++;
            }
        }

        return Kahn(all, successors, inDegree);
    }

    private static List<T> Materialize<T>(IEnumerable<T> nodes)
        where T : notnull
    {
        List<T> all = [.. nodes];
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is null)
            {
                throw new ArgumentNullException(
                    nameof(nodes),
                    $"Le nœud d'indice {i} est nul ; un graphe ne peut pas contenir de nœud nul.");
            }
        }

        return all;
    }

    private static Dictionary<T, int> BuildIndex<T>(List<T> all, IEqualityComparer<T>? comparer)
        where T : notnull
    {
        Dictionary<T, int> index = new(all.Count, comparer ?? EqualityComparer<T>.Default);
        for (int i = 0; i < all.Count; i++)
        {
            if (!index.TryAdd(all[i], i))
            {
                throw new ArgumentException(
                    $"Le nœud d'indice {i} est déjà présent dans la collection ; les nœuds doivent être distincts.",
                    nameof(all));
            }
        }

        return index;
    }

    private static TopologicalOrder<T> Kahn<T>(List<T> all, List<int>[] successors, int[] inDegree)
        where T : notnull
    {
        int count = all.Count;
        int[] remaining = inDegree;
        bool[] emitted = new bool[count];

        List<T> sorted = new(count);
        List<IReadOnlyList<T>> waves = [];

        List<int> current = [];
        for (int i = 0; i < count; i++)
        {
            if (remaining[i] == 0)
            {
                current.Add(i);
            }
        }

        while (current.Count > 0)
        {
            T[] wave = new T[current.Count];
            for (int k = 0; k < current.Count; k++)
            {
                int i = current[k];
                wave[k] = all[i];
                sorted.Add(all[i]);
                emitted[i] = true;
            }

            waves.Add(wave);

            List<int> next = [];
            foreach (int i in current)
            {
                foreach (int j in successors[i])
                {
                    if (--remaining[j] == 0)
                    {
                        next.Add(j);
                    }
                }
            }

            // Trier par indice d'origine : une vague reste dans l'ordre de la collection d'entrée,
            // ce qui rend le résultat reproductible d'une exécution à l'autre.
            next.Sort();
            current = next;
        }

        List<T> cyclic = [];
        for (int i = 0; i < count; i++)
        {
            if (!emitted[i])
            {
                cyclic.Add(all[i]);
            }
        }

        return new TopologicalOrder<T>(sorted, waves, cyclic);
    }
}
