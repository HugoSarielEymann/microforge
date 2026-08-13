namespace Micro.Schema.SampleShape;

/// <summary>Nature d'un nœud de forme, telle qu'un échantillon permet de la deviner.</summary>
public enum ShapeKind
{
    /// <summary>Nature indécidable : valeur nulle, élément vide, tableau sans élément.</summary>
    Unknown,

    /// <summary>Chaîne de caractères.</summary>
    Text,

    /// <summary>Nombre sans partie décimale.</summary>
    WholeNumber,

    /// <summary>Nombre à virgule.</summary>
    FractionalNumber,

    /// <summary>Vrai ou faux.</summary>
    Boolean,

    /// <summary>Instant, reconnu à sa forme ISO 8601.</summary>
    Timestamp,

    /// <summary>Structure composée de champs nommés.</summary>
    Structure,
}

/// <summary>
/// Un nœud de forme : un nom, une nature, une cardinalité, et les champs qu'il contient.
/// </summary>
/// <param name="Name">Nom du champ ; vide pour la racine anonyme d'un échantillon.</param>
/// <param name="Kind">Nature devinée.</param>
/// <param name="IsRepeated">Le champ porte plusieurs valeurs.</param>
/// <param name="Children">Champs contenus, vides sauf pour <see cref="ShapeKind.Structure"/>.</param>
/// <remarks>
/// La forme dit ce que l'échantillon <em>contient</em>, jamais ce qu'il <em>vaut</em> : les
/// valeurs sont lues pour deviner les natures, puis jetées. C'est ce qui permet de coller une
/// charge utile réelle sans craindre d'en emporter les données.
/// </remarks>
public sealed record ShapeNode(
    string Name,
    ShapeKind Kind,
    bool IsRepeated,
    IReadOnlyList<ShapeNode> Children)
{
    /// <summary>Crée un nœud sans enfant.</summary>
    /// <param name="name">Nom du champ.</param>
    /// <param name="kind">Nature du champ.</param>
    /// <param name="isRepeated">Le champ porte plusieurs valeurs.</param>
    /// <returns>Le nœud construit.</returns>
    public static ShapeNode Leaf(string name, ShapeKind kind, bool isRepeated = false)
        => new(name, kind, isRepeated, []);

    /// <summary>Crée un nœud de structure.</summary>
    /// <param name="name">Nom du champ.</param>
    /// <param name="children">Champs contenus.</param>
    /// <param name="isRepeated">Le champ porte plusieurs structures.</param>
    /// <returns>Le nœud construit.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="children"/> est nul.</exception>
    public static ShapeNode Structure(string name, IReadOnlyList<ShapeNode> children, bool isRepeated = false)
    {
        ArgumentNullException.ThrowIfNull(children);
        return new ShapeNode(name, ShapeKind.Structure, isRepeated, children);
    }

    /// <summary>Nombre total de nœuds de cette branche, racine comprise.</summary>
    public int Count => 1 + Children.Sum(c => c.Count);

    /// <summary>Compare deux formes champ par champ.</summary>
    /// <param name="other">Forme à comparer.</param>
    /// <returns><see langword="true"/> si les deux formes décrivent la même chose.</returns>
    /// <remarks>
    /// L'égalité synthétisée d'un <c>record</c> comparerait <see cref="Children"/> par
    /// référence, et deux formes identiques issues de deux lectures ne seraient jamais égales.
    /// Or comparer deux formes est précisément ce qu'on veut faire : vérifier qu'un
    /// aller-retour n'a rien perdu, ou qu'un échantillon retouché change vraiment quelque chose.
    /// </remarks>
    public bool Equals(ShapeNode? other)
        => other is not null
           && string.Equals(Name, other.Name, StringComparison.Ordinal)
           && Kind == other.Kind
           && IsRepeated == other.IsRepeated
           && Children.SequenceEqual(other.Children);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode empreinte = new();
        empreinte.Add(Name, StringComparer.Ordinal);
        empreinte.Add(Kind);
        empreinte.Add(IsRepeated);

        foreach (ShapeNode enfant in Children)
        {
            empreinte.Add(enfant);
        }

        return empreinte.ToHashCode();
    }
}
