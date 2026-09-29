namespace Micro.Markdown.Hashtag;

/// <summary>Réglages du repérage des étiquettes.</summary>
/// <remarks>
/// Les défauts reproduisent les règles d'Obsidian : lettres de tout alphabet, chiffres,
/// <c>_</c>, <c>-</c> et <c>/</c> pour l'imbrication ; au moins un caractère qui ne soit pas
/// un chiffre ; un <c>#</c> en début de texte ou précédé d'un blanc.
/// </remarks>
public sealed class HashtagOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static HashtagOptions Default { get; } = new();

    /// <summary>Ignorer les étiquettes écrites dans du code. Défaut : <see langword="true"/>.</summary>
    /// <remarks>Ne concerne que <see cref="HashtagParser.FindAll"/> : <see cref="HashtagParser.TryParseAt"/> analyse la position reçue.</remarks>
    public bool SkipCode { get; init; } = true;

    /// <summary>Accepter la barre oblique des étiquettes imbriquées (<c>#projet/almageste</c>). Défaut : <see langword="true"/>.</summary>
    public bool AllowNested { get; init; } = true;

    /// <summary>Exiger au moins un caractère non numérique : <c>#2026</c> n'est alors pas une étiquette. Défaut : <see langword="true"/>.</summary>
    /// <remarks>Sans cette règle, un numéro de ticket ou une date (« le #12 ») deviendrait une étiquette.</remarks>
    public bool RequireNonDigit { get; init; } = true;

    /// <summary>
    /// Caractères qui peuvent précéder le <c>#</c>, en plus d'un blanc ou du début du texte.
    /// Défaut : aucun.
    /// </summary>
    /// <remarks>
    /// Le défaut est strict pour écarter les ancres d'URL (<c>/#section</c>), les entités HTML
    /// (<c>&amp;#233;</c>) et les sections de lien (<c>[[Note#Titre]]</c>). Ajouter <c>"("</c>
    /// pour accepter <c>(#idée)</c>.
    /// </remarks>
    public string AllowedPrecedingCharacters { get; init; } = string.Empty;

    /// <summary>Longueur maximale d'un nom d'étiquette, en unités UTF-16. Défaut : 128.</summary>
    public int MaxLength { get; init; } = 128;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentNullException">Si <see cref="AllowedPrecedingCharacters"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxLength"/> est inférieur à 1.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(AllowedPrecedingCharacters);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxLength, 1);
    }

    internal bool IsValid => AllowedPrecedingCharacters is not null && MaxLength >= 1;
}
