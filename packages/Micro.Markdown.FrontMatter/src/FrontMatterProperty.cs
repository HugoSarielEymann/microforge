namespace Micro.Markdown.FrontMatter;

/// <summary>Forme sous laquelle une propriété a été écrite.</summary>
public enum FrontMatterValueKind
{
    /// <summary>Clé sans valeur : <c>statut:</c>.</summary>
    Empty,

    /// <summary>Valeur simple, guillemets retirés : <c>titre: "Plan"</c>.</summary>
    Scalar,

    /// <summary>Liste en ligne <c>[a, b]</c> ou à tirets sur les lignes suivantes.</summary>
    List,

    /// <summary>Texte multiligne introduit par <c>|</c> (lignes conservées) ou <c>&gt;</c> (lignes repliées).</summary>
    Text,

    /// <summary>
    /// Structure imbriquée (dictionnaire, liste d'objets) que ce lecteur ne décompose pas :
    /// <see cref="FrontMatterProperty.RawValue"/> la rend telle qu'écrite.
    /// </summary>
    Complex,
}

/// <summary>Une propriété de premier niveau de l'en-tête.</summary>
/// <param name="Key">Clé, guillemets retirés.</param>
/// <param name="Values">
/// Valeurs lues : une seule pour un scalaire ou un texte, plusieurs pour une liste, aucune pour
/// une clé vide ou une structure complexe.
/// </param>
/// <param name="Kind">Forme d'écriture de la valeur.</param>
/// <param name="RawValue">Valeur telle qu'écrite dans l'en-tête, lignes suivantes comprises.</param>
/// <param name="Line">Numéro de la ligne de la clé dans le document, à partir de 0.</param>
public sealed record FrontMatterProperty(string Key, IReadOnlyList<string> Values, FrontMatterValueKind Kind, string RawValue, int Line)
{
    /// <summary>Première valeur, ou <see langword="null"/> s'il n'y en a aucune.</summary>
    public string? Value => Values.Count > 0 ? Values[0] : null;
}
