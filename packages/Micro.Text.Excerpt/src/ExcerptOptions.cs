namespace Micro.Text.Excerpt;

/// <summary>Réglages de la découpe d'un extrait.</summary>
public sealed class ExcerptOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static ExcerptOptions Default { get; } = new();

    /// <summary>Nombre maximal de caractères gardés avant le passage. Défaut : 40.</summary>
    public int ContextBefore { get; init; } = 40;

    /// <summary>Nombre maximal de caractères gardés après le passage. Défaut : 80.</summary>
    /// <remarks>Plus large qu'avant : on lit vers la droite, et la suite d'une phrase éclaire plus que son début.</remarks>
    public int ContextAfter { get; init; } = 80;

    /// <summary>Marque posée là où l'extrait coupe le texte. Défaut : <c>"…"</c>.</summary>
    public string Ellipsis { get; init; } = "…";

    /// <summary>Ne jamais couper un mot en bordure d'extrait. Défaut : <see langword="true"/>.</summary>
    /// <remarks>Le contexte recule alors jusqu'à la frontière de mot la plus proche, sans jamais entamer le passage.</remarks>
    public bool SnapToWords { get; init; } = true;

    /// <summary>Limiter le contexte à la ligne (ou aux lignes) du passage. Défaut : <see langword="true"/>.</summary>
    /// <remarks>Une ligne voisine appartient souvent à un autre paragraphe, voire à un autre sujet.</remarks>
    public bool SingleLine { get; init; } = true;

    /// <summary>Réduire chaque suite de blancs à une espace. Défaut : <see langword="true"/>.</summary>
    public bool CollapseWhitespace { get; init; } = true;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si un contexte est négatif.</exception>
    /// <exception cref="ArgumentNullException">Si <see cref="Ellipsis"/> est nul.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ContextBefore);
        ArgumentOutOfRangeException.ThrowIfNegative(ContextAfter);
        ArgumentNullException.ThrowIfNull(Ellipsis);
    }
}
