using System.Diagnostics.CodeAnalysis;
namespace Micro.Text.EditSpan;

/// <summary>La zone qui diffère entre deux versions d'un texte.</summary>
/// <param name="Start">Position où commence la différence, identique dans les deux versions.</param>
/// <param name="RemovedLength">Longueur de la zone remplacée dans l'ancienne version.</param>
/// <param name="InsertedLength">Longueur de la zone qui la remplace dans la nouvelle version.</param>
public readonly record struct EditSpan(int Start, int RemovedLength, int InsertedLength)
{
    /// <summary>Les deux versions sont identiques.</summary>
    public bool IsEmpty => RemovedLength == 0 && InsertedLength == 0;

    /// <summary>Texte inséré, lu dans la nouvelle version.</summary>
    /// <param name="after">Nouvelle version, celle qui a servi au calcul.</param>
    /// <returns>Le texte qui remplace la zone.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="after"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="after"/> n'est pas la version du calcul.</exception>
    public string InsertedText(string after)
    {
        ArgumentNullException.ThrowIfNull(after);
        return after.Substring(Start, InsertedLength);
    }
}

/// <summary>Réglages du calcul de la zone modifiée.</summary>
public sealed class EditSpanOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static EditSpanOptions Default { get; } = new();

    /// <summary>
    /// Ne jamais couper une paire de substitution ni un saut de ligne <c>\r\n</c> en bordure de
    /// zone. Défaut : <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Remplacer une demi-paire laisserait un caractère invalide dans un contrôle d'édition le
    /// temps d'une opération ; élargir la zone d'un caractère ne coûte rien.
    /// </remarks>
    public bool KeepUnitsWhole { get; init; } = true;

    /// <summary>Vérifie la cohérence des réglages. Aucun réglage ne peut être incohérent : la méthode existe pour le contrat.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Membre d'instance exigé par le contrat commun des options (Validate), même sans réglage à contrôler.")]
    public void Validate()
    {
    }
}

/// <summary>
/// Situe la zone modifiée entre deux versions d'un texte, par le plus long préfixe commun puis
/// le plus long suffixe commun restant.
/// </summary>
/// <remarks>
/// <para>
/// L'usage type : appliquer à un contrôle d'édition le résultat d'une annulation, d'un
/// rechargement ou d'une réécriture, en ne remplaçant que ce qui change. Le curseur, le
/// défilement et la mise en forme du reste du texte sont ainsi préservés — là où remplacer le
/// texte entier remettrait tout à zéro.
/// </para>
/// <para>
/// Le résultat est une zone unique : pour plusieurs modifications éloignées, elle les englobe
/// toutes. Ce n'est pas un algorithme de différence ligne à ligne. Fonction pure, en temps
/// linéaire.
/// </para>
/// </remarks>
public static class TextEditSpan
{
    /// <summary>Calcule la zone qui diffère entre deux versions.</summary>
    /// <param name="before">Ancienne version.</param>
    /// <param name="after">Nouvelle version.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>La zone : remplacer, dans <paramref name="before"/>, <see cref="EditSpan.RemovedLength"/> caractères à partir de <see cref="EditSpan.Start"/> par <see cref="EditSpan.InsertedText"/> donne <paramref name="after"/>.</returns>
    /// <exception cref="ArgumentNullException">Si une version est nulle.</exception>
    public static EditSpan Between(string before, string after, EditSpanOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        EditSpanOptions settings = options ?? EditSpanOptions.Default;
        settings.Validate();

        int shortest = Math.Min(before.Length, after.Length);

        int prefix = before.AsSpan(0, shortest).CommonPrefixLength(after.AsSpan(0, shortest));

        int suffixLimit = shortest - prefix;
        int suffix = 0;
        while (suffix < suffixLimit && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
        {
            suffix++;
        }

        if (settings.KeepUnitsWhole)
        {
            // La zone ne doit commencer ni au milieu d'une paire, ni entre \r et \n.
            while (prefix > 0 && SplitsUnit(before, prefix))
            {
                prefix--;
            }

            while (suffix > 0 && (SplitsUnit(before, before.Length - suffix) || SplitsUnit(after, after.Length - suffix)))
            {
                suffix--;
            }
        }

        return new EditSpan(prefix, before.Length - prefix - suffix, after.Length - prefix - suffix);
    }

    /// <summary>Applique une zone calculée : la réécriture de <paramref name="before"/> qui donne la nouvelle version.</summary>
    /// <param name="before">Ancienne version.</param>
    /// <param name="span">Zone calculée par <see cref="Between"/>.</param>
    /// <param name="inserted">Texte inséré (voir <see cref="EditSpan.InsertedText"/>).</param>
    /// <returns>La nouvelle version.</returns>
    /// <exception cref="ArgumentNullException">Si un texte est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si la zone sort de <paramref name="before"/> ou si la longueur insérée ne correspond pas.</exception>
    public static string Apply(string before, EditSpan span, string inserted)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(inserted);
        ArgumentOutOfRangeException.ThrowIfNegative(span.Start, nameof(span));
        ArgumentOutOfRangeException.ThrowIfNegative(span.RemovedLength, nameof(span));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)span.Start + span.RemovedLength, before.Length, nameof(span));
        ArgumentOutOfRangeException.ThrowIfNotEqual(inserted.Length, span.InsertedLength, nameof(inserted));

        return string.Concat(before.AsSpan(0, span.Start), inserted, before.AsSpan(span.Start + span.RemovedLength));
    }

    /// <summary>Une frontière à cet index couperait-elle une paire de substitution ou un <c>\r\n</c> ?</summary>
    private static bool SplitsUnit(string text, int index)
    {
        if (index <= 0 || index >= text.Length)
        {
            return false;
        }

        return (char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]))
            || (text[index - 1] == '\r' && text[index] == '\n');
    }
}
