namespace Micro.Markdown.WikiLink;

/// <summary>Réglages de l'analyse des liens wiki.</summary>
public sealed class WikiLinkOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static WikiLinkOptions Default { get; } = new();

    /// <summary>Reconnaître les intégrations <c>![[...]]</c>. Défaut : <see langword="true"/>.</summary>
    /// <remarks>Désactivé, le <c>!</c> reste de la prose et le lien est relevé comme un renvoi simple.</remarks>
    public bool AllowEmbeds { get; init; } = true;

    /// <summary>
    /// Ignorer les liens écrits dans du code (blocs délimités et segments en ligne).
    /// Défaut : <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Ne concerne que <see cref="WikiLinkParser.FindAll(string, WikiLinkOptions?)"/> et
    /// <see cref="WikiLinkParser.Rewrite"/> : <see cref="WikiLinkParser.TryParseAt"/> analyse la
    /// position qu'on lui donne, l'appelant ayant alors déjà décidé qu'elle compte.
    /// </remarks>
    public bool SkipCode { get; init; } = true;

    /// <summary>Longueur maximale du contenu entre crochets. Défaut : 512.</summary>
    /// <remarks>
    /// Un <c>[[</c> jamais refermé ne doit pas faire parcourir tout le texte à chaque
    /// occurrence : au-delà de cette borne, l'ouverture est rendue à la prose.
    /// </remarks>
    public int MaxLength { get; init; } = 512;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxLength"/> est inférieur à 1.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaxLength, 1);

    internal bool IsValid => MaxLength >= 1;
}
