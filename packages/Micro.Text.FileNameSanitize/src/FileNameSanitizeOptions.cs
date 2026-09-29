namespace Micro.Text.FileNameSanitize;

/// <summary>Réglages de la transformation d'un titre en nom de fichier.</summary>
public sealed class FileNameSanitizeOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static FileNameSanitizeOptions Default { get; } = new();

    /// <summary>Texte mis à la place d'un caractère interdit. Défaut : <c>"-"</c>.</summary>
    /// <remarks>
    /// Le remplacement est omis quand un blanc le jouxte, et jamais doublé : « Note : idée »
    /// donne « Note idée », « 12/09 » donne « 12-09 ». Une chaîne vide retire simplement les
    /// caractères interdits.
    /// </remarks>
    public string Replacement { get; init; } = "-";

    /// <summary>
    /// Caractères refusés en plus de ceux que Windows interdit (<c>&lt; &gt; : " / \ | ? *</c> et
    /// caractères de contrôle). Défaut : aucun.
    /// </summary>
    /// <remarks>
    /// Un outil de notes à liens wiki ajoutera <c>"#^[]"</c> : ces caractères sont permis par le
    /// système de fichiers mais cassent la syntaxe <c>[[Titre#section]]</c>.
    /// </remarks>
    public string ExtraInvalidCharacters { get; init; } = string.Empty;

    /// <summary>Longueur maximale du nom, en unités UTF-16. Défaut : 200.</summary>
    /// <remarks>
    /// Sous la limite de 255 de NTFS, pour laisser place à une extension et à un suffixe de
    /// dédoublonnage. La coupe ne sépare jamais une paire de substitution ni un caractère de
    /// ses accents combinants.
    /// </remarks>
    public int MaxLength { get; init; } = 200;

    /// <summary>Nom rendu quand il ne reste rien du titre. Défaut : <c>"untitled"</c>.</summary>
    public string Fallback { get; init; } = "untitled";

    /// <summary>Suffixe accolé à un nom réservé par Windows (<c>CON</c>, <c>NUL</c>, <c>COM1</c>…). Défaut : <c>"_"</c>.</summary>
    public string ReservedNameSuffix { get; init; } = "_";

    /// <summary>Réduire chaque suite de blancs à une espace. Défaut : <see langword="true"/>.</summary>
    public bool CollapseWhitespace { get; init; } = true;

    /// <summary>Accepter un point en tête de nom. Défaut : <see langword="false"/>.</summary>
    /// <remarks>Un nom qui commence par un point est masqué par beaucoup d'outils : un titre ne devrait pas le devenir par accident.</remarks>
    public bool AllowLeadingDot { get; init; }

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentNullException">Si une chaîne de réglage est nulle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxLength"/> est inférieur à 1.</exception>
    /// <exception cref="ArgumentException">
    /// Si le remplacement ou le suffixe contient un caractère interdit, ou si le nom de repli
    /// n'est pas lui-même un nom valide.
    /// </exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Replacement);
        ArgumentNullException.ThrowIfNull(ExtraInvalidCharacters);
        ArgumentNullException.ThrowIfNull(Fallback);
        ArgumentNullException.ThrowIfNull(ReservedNameSuffix);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxLength, 1);

        if (FileNameSanitizer.ContainsInvalid(Replacement, ExtraInvalidCharacters))
        {
            throw new ArgumentException("Le remplacement contient lui-même un caractère interdit.", nameof(Replacement));
        }

        if (ReservedNameSuffix.Length == 0 || FileNameSanitizer.ContainsInvalid(ReservedNameSuffix, ExtraInvalidCharacters))
        {
            throw new ArgumentException("Le suffixe des noms réservés doit être non vide et sans caractère interdit.", nameof(ReservedNameSuffix));
        }

        if (Fallback.Length == 0 || Fallback.Length > MaxLength || !FileNameSanitizer.IsCleanCore(Fallback, this))
        {
            throw new ArgumentException("Le nom de repli doit être lui-même un nom valide.", nameof(Fallback));
        }
    }

    internal bool IsValid
    {
        get
        {
            try
            {
                Validate();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
