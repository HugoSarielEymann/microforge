namespace Micro.Markdown.WikiLink;

/// <summary>Un lien wiki relevé dans un texte, décomposé et positionné.</summary>
/// <remarks>
/// La forme complète est <c>[[cible#section|alias]]</c>, précédée d'un <c>!</c> pour une
/// intégration. Toutes les positions sont des index UTF-16 dans le texte analysé : elles
/// permettent de réécrire la cible sans toucher au reste, ou de colorer chaque partie.
/// </remarks>
public sealed record WikiLinkMatch
{
    /// <summary>Position du premier caractère : le <c>!</c> d'une intégration, sinon le premier <c>[</c>.</summary>
    public required int Start { get; init; }

    /// <summary>Longueur totale, crochets et <c>!</c> compris.</summary>
    public required int Length { get; init; }

    /// <summary>
    /// Cible du lien, débarrassée des blancs de bord : un titre de note, un chemin
    /// (<c>Dossier/Note</c>) ou un fichier (<c>image.png</c>). Vide pour un lien vers une
    /// section de la note courante (<c>[[#Section]]</c>).
    /// </summary>
    public required string Target { get; init; }

    /// <summary>Position de la cible dans le texte, blancs de bord exclus.</summary>
    public required int TargetStart { get; init; }

    /// <summary>Longueur de la cible dans le texte.</summary>
    public required int TargetLength { get; init; }

    /// <summary>Section visée après <c>#</c>, ou <see langword="null"/>. Peut contenir d'autres <c>#</c> pour des sous-sections.</summary>
    public string? Heading { get; init; }

    /// <summary>Bloc visé après <c>#^</c>, sans l'accent circonflexe, ou <see langword="null"/>.</summary>
    public string? BlockId { get; init; }

    /// <summary>Texte affiché à la place de la cible, après <c>|</c>, ou <see langword="null"/>.</summary>
    public string? Alias { get; init; }

    /// <summary>Position de l'alias dans le texte, ou -1 s'il n'y en a pas.</summary>
    public int AliasStart { get; init; } = -1;

    /// <summary>Longueur de l'alias dans le texte, ou 0.</summary>
    public int AliasLength { get; init; }

    /// <summary>Le lien est une intégration (<c>![[...]]</c>) plutôt qu'un renvoi.</summary>
    public bool IsEmbed { get; init; }

    /// <summary>Position qui suit immédiatement le lien.</summary>
    public int End => Start + Length;

    /// <summary>Position du contenu, juste après les crochets ouvrants.</summary>
    public int ContentStart => Start + (IsEmbed ? 3 : 2);

    /// <summary>Longueur du contenu, entre les crochets.</summary>
    public int ContentLength => Length - (IsEmbed ? 5 : 4);

    /// <summary>
    /// Ce qu'un lecteur doit voir : l'alias s'il existe, sinon la cible suivie de la section
    /// (<c>Note › Section</c>), sinon la seule section pour un lien interne.
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (Alias is not null)
            {
                return Alias;
            }

            string? sub = Heading ?? (BlockId is null ? null : "^" + BlockId);
            if (sub is null)
            {
                return Target;
            }

            return Target.Length == 0 ? sub : Target + " › " + sub;
        }
    }
}
