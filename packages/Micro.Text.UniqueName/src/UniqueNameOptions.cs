namespace Micro.Text.UniqueName;

/// <summary>Réglages du choix d'un nom libre.</summary>
public sealed class UniqueNameOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static UniqueNameOptions Default { get; } = new();

    /// <summary>
    /// Forme d'un nom numéroté : <c>{0}</c> reçoit le nom de base, <c>{1}</c> le numéro.
    /// Défaut : <c>"{0} {1}"</c> (« Sans titre 2 »).
    /// </summary>
    /// <remarks>
    /// <c>"{0} ({1})"</c> donne la forme de l'Explorateur Windows (« Copie (2) »),
    /// <c>"{0}-{1}"</c> celle des identifiants. Les deux jetons sont obligatoires.
    /// </remarks>
    public string Format { get; init; } = "{0} {1}";

    /// <summary>Premier numéro essayé quand le nom voulu est pris. Défaut : 2.</summary>
    /// <remarks>Le nom sans numéro compte comme le premier : le suivant est donc le deuxième.</remarks>
    public int FirstNumber { get; init; } = 2;

    /// <summary>
    /// Poursuivre la numérotation d'un nom déjà numéroté. Défaut : <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Dupliquer « Sans titre 2 » donne alors « Sans titre 3 » et non « Sans titre 2 2 » : le
    /// nom voulu est relu selon <see cref="Format"/>, et c'est sa base qu'on numérote.
    /// </remarks>
    public bool ContinueNumbering { get; init; } = true;

    /// <summary>Nombre maximal de candidats essayés avant d'abandonner. Défaut : 10 000.</summary>
    public int MaxAttempts { get; init; } = 10_000;

    /// <summary>
    /// Comparateur des noms, pour la surcharge qui reçoit la liste des noms existants.
    /// Défaut : ordinal insensible à la casse, comme les systèmes de fichiers de Windows et macOS.
    /// </summary>
    public IEqualityComparer<string> Comparer { get; init; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentNullException">Si <see cref="Format"/> ou <see cref="Comparer"/> est nul.</exception>
    /// <exception cref="ArgumentException">Si <see cref="Format"/> ne contient pas <c>{0}</c> et <c>{1}</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="FirstNumber"/> est négatif ou <see cref="MaxAttempts"/> inférieur à 1.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Format);
        ArgumentNullException.ThrowIfNull(Comparer);
        ArgumentOutOfRangeException.ThrowIfNegative(FirstNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxAttempts, 1);

        if (!Format.Contains("{0}", StringComparison.Ordinal) || !Format.Contains("{1}", StringComparison.Ordinal))
        {
            throw new ArgumentException("Le format doit contenir {0} (le nom) et {1} (le numéro).", nameof(Format));
        }

        try
        {
            _ = string.Format(System.Globalization.CultureInfo.InvariantCulture, Format, "nom", 2);
        }
        catch (FormatException erreur)
        {
            throw new ArgumentException("Le format n'est pas un format composite valide : " + erreur.Message, nameof(Format), erreur);
        }
    }
}
