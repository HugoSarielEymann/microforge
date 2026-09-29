namespace Micro.Markdown.CodeSpans;

/// <summary>Réglages du repérage des plages de code.</summary>
/// <remarks>
/// Les défauts suivent ce que les éditeurs de notes courants traitent comme du code :
/// blocs délimités et segments en ligne. Les blocs indentés sont écartés par défaut, parce
/// que dans une note la même indentation sert surtout à imbriquer des listes — les
/// traiter comme du code masquerait les liens de toute une sous-liste.
/// </remarks>
public sealed class CodeSpanOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static CodeSpanOptions Default { get; } = new();

    /// <summary>Relever les blocs délimités (<c>```</c> ou <c>~~~</c>). Défaut : <see langword="true"/>.</summary>
    public bool IncludeFenced { get; init; } = true;

    /// <summary>Relever les segments en ligne (<c>`code`</c>). Défaut : <see langword="true"/>.</summary>
    public bool IncludeInline { get; init; } = true;

    /// <summary>
    /// Relever les blocs indentés de quatre espaces après une ligne vide. Défaut : <see langword="false"/>.
    /// </summary>
    public bool IncludeIndented { get; init; }

    /// <summary>
    /// Nombre maximal de lignes qu'un segment en ligne peut couvrir. Défaut : 8.
    /// </summary>
    /// <remarks>
    /// Un backtick orphelin ne doit pas avaler tout un document en attendant son partenaire.
    /// Au-delà de cette borne — ou d'une ligne vide, qui clôt toujours le paragraphe — le
    /// backtick est rendu à la prose.
    /// </remarks>
    public int MaxInlineLines { get; init; } = 8;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxInlineLines"/> est inférieur à 1.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaxInlineLines, 1);
}
