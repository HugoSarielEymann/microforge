namespace Micro.Graph.ForceLayout;

/// <summary>
/// Dispose les nœuds d'un graphe dans le plan par simulation de forces : chaque lien est un
/// ressort, chaque paire de nœuds se repousse, une légère gravité ramène tout vers l'origine.
/// </summary>
/// <remarks>
/// <para>
/// C'est l'algorithme de d3-force, transposé : intégration de Verlet à vitesse amortie, et
/// répulsion approchée par un quadtree de Barnes-Hut, en <c>O(n log n)</c> par pas au lieu de
/// <c>O(n²)</c>. La simulation se refroidit à chaque pas (<see cref="Alpha"/>) jusqu'à se figer ;
/// <see cref="Reheat"/> la relance après une modification, et <see cref="AlphaTarget"/> la garde
/// vivante pendant un glisser.
/// </para>
/// <para>
/// Tout est déterministe : la position de départ suit une spirale de phyllotaxie, et le
/// minuscule aléa qui sépare deux nœuds confondus vient d'une graine. Deux simulations
/// construites pareil donnent la même disposition au bit près.
/// </para>
/// <para>
/// La classe n'est pas sûre face à des appels simultanés : une simulation se pilote depuis un
/// seul fil, typiquement celui qui dessine.
/// </para>
/// </remarks>
public sealed class ForceSimulation
{
    private const int MaxTreeDepth = 48;
    private const double DistanceMin2 = 1;

    private readonly ForceLayoutOptions _options;
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _vx;
    private readonly double[] _vy;
    private readonly double[] _fx;
    private readonly double[] _fy;
    private readonly double[] _weight;

    private readonly int[] _source;
    private readonly int[] _target;
    private readonly double[] _strength;
    private readonly double[] _bias;

    private readonly Random _random;

    private Quad[] _quads = new Quad[64];
    private int _quadCount;
    private readonly int[] _nextPoint;
    private readonly Stack<int> _visit = new();

    private double _alpha = 1;
    private double _alphaTarget;

    /// <summary>Prépare une simulation sur des nœuds numérotés de 0 à <paramref name="nodeCount"/> − 1.</summary>
    /// <param name="nodeCount">Nombre de nœuds.</param>
    /// <param name="links">Liens entre nœuds ; les boucles (un nœud vers lui-même) sont ignorées.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si le nombre de nœuds est négatif, si un lien désigne un nœud inexistant, ou si les
    /// réglages sont invalides.
    /// </exception>
    /// <exception cref="ArgumentNullException">Si <paramref name="links"/> est nul.</exception>
    public ForceSimulation(int nodeCount, IEnumerable<ForceLink> links, ForceLayoutOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nodeCount);
        ArgumentNullException.ThrowIfNull(links);

        _options = options ?? ForceLayoutOptions.Default;
        _options.Validate();

        NodeCount = nodeCount;
        _x = new double[nodeCount];
        _y = new double[nodeCount];
        _vx = new double[nodeCount];
        _vy = new double[nodeCount];
        _fx = new double[nodeCount];
        _fy = new double[nodeCount];
        _weight = new double[nodeCount];
        _nextPoint = new int[nodeCount];
        _random = new Random(_options.Seed);
        _alphaTarget = _options.AlphaTarget;

        List<ForceLink> kept = [];
        int[] degree = new int[nodeCount];
        foreach (ForceLink link in links)
        {
            if ((uint)link.Source >= (uint)nodeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(links), link.Source, "Un lien désigne un nœud source inexistant.");
            }

            if ((uint)link.Target >= (uint)nodeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(links), link.Target, "Un lien désigne un nœud cible inexistant.");
            }

            if (link.Source == link.Target)
            {
                continue;
            }

            kept.Add(link);
            degree[link.Source]++;
            degree[link.Target]++;
        }

        LinkCount = kept.Count;
        _source = new int[kept.Count];
        _target = new int[kept.Count];
        _strength = new double[kept.Count];
        _bias = new double[kept.Count];

        for (int index = 0; index < kept.Count; index++)
        {
            int source = kept[index].Source;
            int target = kept[index].Target;
            _source[index] = source;
            _target[index] = target;
            _strength[index] = double.IsNaN(_options.LinkStrength)
                ? 1.0 / Math.Min(degree[source], degree[target])
                : _options.LinkStrength;
            _bias[index] = (double)degree[source] / (degree[source] + degree[target]);
        }

        // Spirale de phyllotaxie : chaque nœud sur l'angle d'or, à un rayon qui croît comme la
        // racine de son rang. La disposition de départ est ainsi régulière et reproductible.
        double angleOr = Math.PI * (3 - Math.Sqrt(5));
        for (int node = 0; node < nodeCount; node++)
        {
            double radius = _options.InitialRadius * Math.Sqrt(0.5 + node);
            double angle = node * angleOr;
            _x[node] = radius * Math.Cos(angle);
            _y[node] = radius * Math.Sin(angle);
            _fx[node] = double.NaN;
            _fy[node] = double.NaN;
            _weight[node] = 1;
        }
    }

    /// <summary>Nombre de nœuds.</summary>
    public int NodeCount { get; }

    /// <summary>Nombre de liens retenus, boucles écartées.</summary>
    public int LinkCount { get; }

    /// <summary>Température courante, entre 0 et 1 : l'amplitude des mouvements du prochain pas.</summary>
    /// <exception cref="ArgumentOutOfRangeException">À l'écriture, si la valeur n'est pas comprise entre 0 et 1.</exception>
    public double Alpha
    {
        get => _alpha;
        set => _alpha = RequireUnit(value, nameof(Alpha));
    }

    /// <summary>Température vers laquelle la simulation tend, entre 0 et 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">À l'écriture, si la valeur n'est pas comprise entre 0 et 1.</exception>
    /// <remarks>La porter à 0,3 pendant un glisser, puis la ramener à 0 au lâcher : c'est le geste de d3.</remarks>
    public double AlphaTarget
    {
        get => _alphaTarget;
        set => _alphaTarget = RequireUnit(value, nameof(AlphaTarget));
    }

    /// <summary>La simulation est figée : sa température est sous <see cref="ForceLayoutOptions.AlphaMin"/>.</summary>
    public bool IsSettled => _alpha < _options.AlphaMin;

    /// <summary>Avance la simulation d'un pas.</summary>
    /// <returns><see langword="true"/> si la simulation n'est pas encore figée après ce pas.</returns>
    /// <remarks>Un pas est calculé même sur une simulation figée : l'appelant décide quand s'arrêter.</remarks>
    public bool Tick()
    {
        _alpha += (_alphaTarget - _alpha) * _options.AlphaDecay;

        ApplyLinks();
        ApplyCharge();
        ApplyCenter();

        double keep = 1 - _options.VelocityDecay;
        for (int node = 0; node < NodeCount; node++)
        {
            if (double.IsNaN(_fx[node]))
            {
                _vx[node] *= keep;
                _vy[node] *= keep;
                _x[node] += _vx[node];
                _y[node] += _vy[node];
            }
            else
            {
                _x[node] = _fx[node];
                _y[node] = _fy[node];
                _vx[node] = 0;
                _vy[node] = 0;
            }
        }

        return !IsSettled;
    }

    /// <summary>Enchaîne les pas jusqu'à ce que la simulation se fige, dans une limite.</summary>
    /// <param name="maxTicks">Nombre maximal de pas.</param>
    /// <returns>Le nombre de pas effectués.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="maxTicks"/> est négatif.</exception>
    /// <remarks>Sert à « préchauffer » une disposition avant de l'afficher, pour ne pas montrer la spirale de départ.</remarks>
    public int Run(int maxTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxTicks);

        int ticks = 0;
        while (ticks < maxTicks && !IsSettled)
        {
            Tick();
            ticks++;
        }

        return ticks;
    }

    /// <summary>Relance la simulation, par exemple après un ajout ou un déplacement.</summary>
    /// <param name="alpha">Température de relance, entre 0 et 1. Défaut : 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si la valeur n'est pas comprise entre 0 et 1.</exception>
    public void Reheat(double alpha = 1) => Alpha = alpha;

    /// <summary>Abscisse d'un nœud.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <returns>L'abscisse courante.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas.</exception>
    public double GetX(int node) => _x[RequireNode(node)];

    /// <summary>Ordonnée d'un nœud.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <returns>L'ordonnée courante.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas.</exception>
    public double GetY(int node) => _y[RequireNode(node)];

    /// <summary>Déplace un nœud, vitesse annulée.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <param name="x">Nouvelle abscisse.</param>
    /// <param name="y">Nouvelle ordonnée.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas ou si une coordonnée n'est pas finie.</exception>
    /// <remarks>Sert à reprendre une disposition précédente quand le graphe change.</remarks>
    public void SetPosition(int node, double x, double y)
    {
        RequireNode(node);
        RequireFinite(x, nameof(x));
        RequireFinite(y, nameof(y));

        _x[node] = x;
        _y[node] = y;
        _vx[node] = 0;
        _vy[node] = 0;
    }

    /// <summary>Épingle un nœud à une position : il ne bouge plus, mais continue d'agir sur les autres.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <param name="x">Abscisse d'épinglage.</param>
    /// <param name="y">Ordonnée d'épinglage.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas ou si une coordonnée n'est pas finie.</exception>
    public void Pin(int node, double x, double y)
    {
        RequireNode(node);
        RequireFinite(x, nameof(x));
        RequireFinite(y, nameof(y));

        _fx[node] = x;
        _fy[node] = y;
        _x[node] = x;
        _y[node] = y;
    }

    /// <summary>Libère un nœud épinglé.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas.</exception>
    public void Unpin(int node)
    {
        RequireNode(node);
        _fx[node] = double.NaN;
        _fy[node] = double.NaN;
    }

    /// <summary>Indique qu'un nœud est épinglé.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <returns><see langword="true"/> si le nœud est épinglé.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas.</exception>
    public bool IsPinned(int node) => !double.IsNaN(_fx[RequireNode(node)]);

    /// <summary>Multiplie la répulsion qu'exerce un nœud. Défaut : 1.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <param name="weight">Multiplicateur positif ou nul ; 0 rend le nœud sans effet sur les autres.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas, ou si le poids est négatif ou non fini.</exception>
    /// <remarks>Un nœud dessiné plus gros peut ainsi réclamer plus de place autour de lui.</remarks>
    public void SetWeight(int node, double weight)
    {
        RequireNode(node);
        RequireFinite(weight, nameof(weight));
        ArgumentOutOfRangeException.ThrowIfNegative(weight);

        _weight[node] = weight;
    }

    /// <summary>Multiplicateur de répulsion d'un nœud.</summary>
    /// <param name="node">Rang du nœud.</param>
    /// <returns>Le poids courant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si le nœud n'existe pas.</exception>
    public double GetWeight(int node) => _weight[RequireNode(node)];

    /// <summary>Rectangle englobant de tous les nœuds.</summary>
    /// <returns>Les bornes ; toutes nulles s'il n'y a aucun nœud.</returns>
    public (double MinX, double MinY, double MaxX, double MaxY) GetBounds()
    {
        if (NodeCount == 0)
        {
            return (0, 0, 0, 0);
        }

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        for (int node = 0; node < NodeCount; node++)
        {
            minX = Math.Min(minX, _x[node]);
            minY = Math.Min(minY, _y[node]);
            maxX = Math.Max(maxX, _x[node]);
            maxY = Math.Max(maxY, _y[node]);
        }

        return (minX, minY, maxX, maxY);
    }

    // ==================== Forces ====================

    private void ApplyLinks()
    {
        for (int index = 0; index < LinkCount; index++)
        {
            int source = _source[index];
            int target = _target[index];

            double dx = _x[target] + _vx[target] - _x[source] - _vx[source];
            double dy = _y[target] + _vy[target] - _y[source] - _vy[source];
            if (dx == 0)
            {
                dx = Jiggle();
            }

            if (dy == 0)
            {
                dy = Jiggle();
            }

            double length = Math.Sqrt((dx * dx) + (dy * dy));
            double pull = (length - _options.LinkDistance) / length * _alpha * _strength[index];
            dx *= pull;
            dy *= pull;

            double bias = _bias[index];
            _vx[target] -= dx * bias;
            _vy[target] -= dy * bias;
            _vx[source] += dx * (1 - bias);
            _vy[source] += dy * (1 - bias);
        }
    }

    private void ApplyCenter()
    {
        double pull = _options.CenterStrength * _alpha;
        if (pull == 0)
        {
            return;
        }

        for (int node = 0; node < NodeCount; node++)
        {
            _vx[node] -= _x[node] * pull;
            _vy[node] -= _y[node] * pull;
        }
    }

    private void ApplyCharge()
    {
        if (NodeCount < 2 || _options.ChargeStrength == 0)
        {
            return;
        }

        BuildTree();
        Accumulate();

        double theta2 = _options.Theta * _options.Theta;
        double distanceMax2 = double.IsPositiveInfinity(_options.ChargeDistanceMax)
            ? double.PositiveInfinity
            : _options.ChargeDistanceMax * _options.ChargeDistanceMax;

        for (int node = 0; node < NodeCount; node++)
        {
            _visit.Clear();
            _visit.Push(0);

            while (_visit.Count > 0)
            {
                ref Quad quad = ref _quads[_visit.Pop()];
                if (quad.Value == 0)
                {
                    continue;
                }

                double dx = quad.Cx - _x[node];
                double dy = quad.Cy - _y[node];
                double l = (dx * dx) + (dy * dy);

                // Assez loin : le groupe entier agit comme un seul nœud en son barycentre.
                if (quad.Size * quad.Size < theta2 * l)
                {
                    if (l < distanceMax2)
                    {
                        Repel(node, ref dx, ref dy, ref l, quad.Value);
                    }

                    continue;
                }

                if (quad.Child0 >= 0)
                {
                    for (int child = 0; child < 4; child++)
                    {
                        _visit.Push(quad.Child0 + child);
                    }

                    continue;
                }

                if (l >= distanceMax2)
                {
                    continue;
                }

                int first = quad.FirstPoint;
                if (first != node || _nextPoint[first] != -1)
                {
                    if (dx == 0)
                    {
                        dx = Jiggle();
                        l += dx * dx;
                    }

                    if (dy == 0)
                    {
                        dy = Jiggle();
                        l += dy * dy;
                    }

                    if (l < DistanceMin2)
                    {
                        l = Math.Sqrt(DistanceMin2 * l);
                    }
                }

                for (int point = first; point != -1; point = _nextPoint[point])
                {
                    if (point == node)
                    {
                        continue;
                    }

                    double w = _options.ChargeStrength * _weight[point] * _alpha / l;
                    _vx[node] += dx * w;
                    _vy[node] += dy * w;
                }
            }
        }
    }

    private void Repel(int node, ref double dx, ref double dy, ref double l, double value)
    {
        if (dx == 0)
        {
            dx = Jiggle();
            l += dx * dx;
        }

        if (dy == 0)
        {
            dy = Jiggle();
            l += dy * dy;
        }

        if (l < DistanceMin2)
        {
            l = Math.Sqrt(DistanceMin2 * l);
        }

        double w = value * _alpha / l;
        _vx[node] += dx * w;
        _vy[node] += dy * w;
    }

    // ==================== Quadtree de Barnes-Hut ====================

    private struct Quad
    {
        public double X0;
        public double Y0;
        public double Size;
        public int Child0;
        public int FirstPoint;
        public double Value;
        public double Cx;
        public double Cy;
    }

    private void BuildTree()
    {
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        for (int node = 0; node < NodeCount; node++)
        {
            minX = Math.Min(minX, _x[node]);
            minY = Math.Min(minY, _y[node]);
            maxX = Math.Max(maxX, _x[node]);
            maxY = Math.Max(maxY, _y[node]);
        }

        double size = Math.Max(maxX - minX, maxY - minY);
        size = size > 0 ? size * 1.0001 : 1;

        _quadCount = 0;
        NewQuad(minX, minY, size);

        for (int node = 0; node < NodeCount; node++)
        {
            Insert(node);
        }
    }

    private int NewQuad(double x0, double y0, double size)
    {
        if (_quadCount == _quads.Length)
        {
            Array.Resize(ref _quads, _quads.Length * 2);
        }

        _quads[_quadCount] = new Quad { X0 = x0, Y0 = y0, Size = size, Child0 = -1, FirstPoint = -1 };
        return _quadCount++;
    }

    private void Insert(int node)
    {
        int quad = 0;
        int depth = 0;
        double px = _x[node];
        double py = _y[node];

        while (true)
        {
            if (_quads[quad].Child0 < 0)
            {
                int first = _quads[quad].FirstPoint;
                if (first == -1)
                {
                    _quads[quad].FirstPoint = node;
                    _nextPoint[node] = -1;
                    return;
                }

                // Nœuds confondus, ou arbre trop profond : on les chaîne dans la même feuille.
                if ((_x[first] == px && _y[first] == py) || depth >= MaxTreeDepth)
                {
                    _nextPoint[node] = first;
                    _quads[quad].FirstPoint = node;
                    return;
                }

                Subdivide(quad);
                int destination = ChildFor(quad, _x[first], _y[first]);
                _quads[destination].FirstPoint = first;
                _quads[quad].FirstPoint = -1;
            }

            quad = ChildFor(quad, px, py);
            depth++;
        }
    }

    private void Subdivide(int quad)
    {
        double half = _quads[quad].Size / 2;
        double x0 = _quads[quad].X0;
        double y0 = _quads[quad].Y0;

        int child0 = NewQuad(x0, y0, half);
        NewQuad(x0 + half, y0, half);
        NewQuad(x0, y0 + half, half);
        NewQuad(x0 + half, y0 + half, half);

        _quads[quad].Child0 = child0;
    }

    private int ChildFor(int quad, double px, double py)
    {
        ref Quad q = ref _quads[quad];
        double half = q.Size / 2;
        int right = px >= q.X0 + half ? 1 : 0;
        int bottom = py >= q.Y0 + half ? 2 : 0;
        return q.Child0 + right + bottom;
    }

    /// <summary>
    /// Calcule, de bas en haut, la charge totale et le barycentre de chaque case. Les enfants
    /// étant toujours créés après leur parent, un parcours à rebours des index suffit.
    /// </summary>
    private void Accumulate()
    {
        for (int index = _quadCount - 1; index >= 0; index--)
        {
            ref Quad quad = ref _quads[index];

            double strength = 0;
            double weight = 0;
            double cx = 0;
            double cy = 0;

            if (quad.Child0 >= 0)
            {
                for (int child = 0; child < 4; child++)
                {
                    ref Quad sub = ref _quads[quad.Child0 + child];
                    double c = Math.Abs(sub.Value);
                    if (c > 0)
                    {
                        strength += sub.Value;
                        weight += c;
                        cx += c * sub.Cx;
                        cy += c * sub.Cy;
                    }
                }
            }
            else
            {
                for (int point = quad.FirstPoint; point != -1; point = _nextPoint[point])
                {
                    double charge = _options.ChargeStrength * _weight[point];
                    double c = Math.Abs(charge);
                    strength += charge;
                    weight += c;
                    cx += c * _x[point];
                    cy += c * _y[point];
                }

                if (weight == 0 && quad.FirstPoint != -1)
                {
                    cx = _x[quad.FirstPoint];
                    cy = _y[quad.FirstPoint];
                    weight = 1;
                }
            }

            quad.Value = strength;
            quad.Cx = weight > 0 ? cx / weight : quad.X0 + (quad.Size / 2);
            quad.Cy = weight > 0 ? cy / weight : quad.Y0 + (quad.Size / 2);
        }
    }

    // ==================== Utilitaires ====================

    private double Jiggle() => (_random.NextDouble() - 0.5) * 1e-6;

    private int RequireNode(int node)
    {
        if ((uint)node >= (uint)NodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(node), node, "Ce nœud n'existe pas.");
        }

        return node;
    }

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "La coordonnée doit être un nombre fini.");
        }
    }

    private static double RequireUnit(double value, string name)
    {
        if (double.IsNaN(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(name, value, "La valeur doit être comprise entre 0 et 1.");
        }

        return value;
    }
}
