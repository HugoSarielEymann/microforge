namespace Micro.Text.IdentifierCase;

/// <summary>Paramétrage de la conversion d'un libellé en identifiant.</summary>
public sealed class IdentifierCaseOptions
{
    /// <summary>Paramétrage par défaut : <see cref="IdentifierStyle.Pascal"/>, ASCII uniquement.</summary>
    public static IdentifierCaseOptions Default { get; } = new();

    /// <summary>Convention appliquée. Défaut : <see cref="IdentifierStyle.Pascal"/>.</summary>
    public IdentifierStyle Style { get; init; } = IdentifierStyle.Pascal;

    /// <summary>
    /// Restreint le résultat à <c>[A-Za-z0-9]</c>. Défaut : <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Les diacritiques sont retirés dans les deux cas (« prénom » donne « prenom ») : c'est la
    /// décomposition Unicode, pas ce réglage. Ce réglage décide du sort des lettres qui n'ont
    /// aucune forme ASCII — cyrillique, grec, idéogrammes. À <see langword="true"/> elles sont
    /// écartées, ce qu'exigent les grammaires d'identifiants ASCII (Protobuf, SQL, JSON Schema).
    /// À <see langword="false"/> elles sont conservées, ce qu'autorisent C# ou Java.
    /// </remarks>
    public bool AsciiOnly { get; init; } = true;

    /// <summary>
    /// Préfixe ajouté lorsque l'identifiant commencerait par un chiffre. Défaut : <c>"_"</c>.
    /// </summary>
    public string LeadingDigitPrefix { get; init; } = "_";

    /// <summary>Longueur maximale ; <see langword="null"/> pour illimitée. La coupe ne laisse pas de séparateur final.</summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Mots réservés du langage cible. Si l'identifiant produit en fait partie,
    /// <see cref="ReservedWordSuffix"/> lui est ajouté. <see langword="null"/> pour ne rien vérifier.
    /// </summary>
    /// <remarks>
    /// Le package reste neutre vis-à-vis du langage : c'est l'appelant qui fournit la liste,
    /// et le comparateur qu'elle porte décide de la sensibilité à la casse.
    /// </remarks>
    public IReadOnlySet<string>? ReservedWords { get; init; }

    /// <summary>Suffixe ajouté à un identifiant réservé. Défaut : <c>"_"</c>.</summary>
    public string ReservedWordSuffix { get; init; } = "_";

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxLength"/> est inférieur à 1.</exception>
    /// <exception cref="ArgumentException">
    /// Si <see cref="Style"/> est inconnu, si <see cref="LeadingDigitPrefix"/> est nul, ou si
    /// <see cref="ReservedWords"/> est renseigné alors que <see cref="ReservedWordSuffix"/> est vide.
    /// </exception>
    public void Validate()
    {
        if (Style is not (IdentifierStyle.Pascal or IdentifierStyle.Camel or IdentifierStyle.Snake
            or IdentifierStyle.Kebab or IdentifierStyle.ScreamingSnake))
        {
            throw new ArgumentException("Convention de nommage inconnue.", nameof(Style));
        }

        if (MaxLength is { } max)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        }

        if (LeadingDigitPrefix is null)
        {
            throw new ArgumentException(
                "Le préfixe de chiffre initial ne peut pas être nul ; utiliser la chaîne vide pour ne rien ajouter.",
                nameof(LeadingDigitPrefix));
        }

        if (ReservedWords is { Count: > 0 } && string.IsNullOrEmpty(ReservedWordSuffix))
        {
            throw new ArgumentException(
                "Un suffixe non vide est nécessaire pour désambiguïser un mot réservé.",
                nameof(ReservedWordSuffix));
        }
    }
}
