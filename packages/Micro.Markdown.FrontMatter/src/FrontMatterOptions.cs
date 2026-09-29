namespace Micro.Markdown.FrontMatter;

/// <summary>Réglages de la lecture de l'en-tête.</summary>
public sealed class FrontMatterOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static FrontMatterOptions Default { get; } = new();

    /// <summary>Accepter <c>...</c> comme ligne fermante, en plus de <c>---</c>. Défaut : <see langword="true"/>.</summary>
    /// <remarks>C'est la marque de fin de document YAML ; certains outils l'écrivent.</remarks>
    public bool AllowDotsClosing { get; init; } = true;

    /// <summary>Ignorer la casse des clés dans <see cref="FrontMatterBlock.Find"/>. Défaut : <see langword="true"/>.</summary>
    /// <remarks>
    /// « Tags » et « tags » désignent la même propriété pour un humain qui édite ses notes à
    /// la main ; un YAML strict les distinguerait.
    /// </remarks>
    public bool IgnoreKeyCase { get; init; } = true;

    /// <summary>Longueur maximale explorée pour trouver la ligne fermante. Défaut : 65 536.</summary>
    /// <remarks>
    /// Un document qui commence par <c>---</c> sans jamais le refermer n'a pas d'en-tête : il
    /// commence par un filet. La borne évite de parcourir un très long texte pour le découvrir.
    /// </remarks>
    public int MaxLength { get; init; } = 65_536;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxLength"/> est inférieur à 3.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaxLength, 3);

    internal bool IsValid => MaxLength >= 3;
}
