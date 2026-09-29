using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Micro.Text.UniqueName;

/// <summary>
/// Choisit un nom libre en numérotant le nom voulu quand il est déjà pris :
/// « Sans titre », « Sans titre 2 », « Sans titre 3 »…
/// </summary>
/// <remarks>
/// <para>
/// Le nom voulu est essayé tel quel d'abord. S'il est pris, les numéros sont essayés dans
/// l'ordre à partir de <see cref="UniqueNameOptions.FirstNumber"/>. Un nom déjà numéroté selon
/// le format voit sa numérotation poursuivie plutôt qu'empilée.
/// </para>
/// <para>
/// La question « ce nom est-il pris ? » est posée par l'appelant : un ensemble en mémoire, un
/// test d'existence de fichier, une requête. Le package n'accède à rien lui-même.
/// </para>
/// </remarks>
public static class UniqueNamer
{
    /// <summary>Choisit un nom libre, la disponibilité étant jugée par une fonction.</summary>
    /// <param name="desired">Nom voulu.</param>
    /// <param name="isTaken">Rend <see langword="true"/> si le candidat est déjà pris.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le nom voulu s'il est libre, sinon la première variante numérotée libre.</returns>
    /// <exception cref="ArgumentNullException">Si le nom ou la fonction est nul.</exception>
    /// <exception cref="ArgumentException">Si les réglages sont incohérents.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les réglages sont hors bornes.</exception>
    /// <exception cref="InvalidOperationException">Si aucun candidat n'est libre dans la limite d'essais.</exception>
    /// <remarks>
    /// Entre ce choix et l'usage du nom, un autre acteur peut le prendre : pour un fichier,
    /// créer en mode exclusif et recommencer en cas d'échec reste la seule garantie.
    /// </remarks>
    public static string MakeUnique(string desired, Func<string, bool> isTaken, UniqueNameOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(isTaken);

        UniqueNameOptions settings = options ?? UniqueNameOptions.Default;
        settings.Validate();

        if (!isTaken(desired))
        {
            return desired;
        }

        (string baseName, long next) = settings.ContinueNumbering && TryReadNumbered(desired, settings.Format, out string lu, out long numero)
            ? (lu, Math.Max(numero + 1, settings.FirstNumber))
            : (desired, settings.FirstNumber);

        for (int attempt = 1; attempt < settings.MaxAttempts; attempt++, next++)
        {
            string candidate = string.Format(CultureInfo.InvariantCulture, settings.Format, baseName, next);
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Aucun nom libre trouvé pour « " + desired + " » après " + settings.MaxAttempts.ToString(CultureInfo.InvariantCulture) + " essais.");
    }

    /// <summary>Choisit un nom qui n'est pas dans une liste de noms existants.</summary>
    /// <param name="desired">Nom voulu.</param>
    /// <param name="existing">Noms déjà pris ; les éléments nuls sont ignorés.</param>
    /// <param name="options">Réglages ; le comparateur des réglages juge l'égalité.</param>
    /// <returns>Le nom voulu s'il est libre, sinon la première variante numérotée libre.</returns>
    /// <exception cref="ArgumentNullException">Si le nom ou la liste est nul.</exception>
    /// <exception cref="ArgumentException">Si les réglages sont incohérents.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les réglages sont hors bornes.</exception>
    /// <exception cref="InvalidOperationException">Si aucun candidat n'est libre dans la limite d'essais.</exception>
    public static string MakeUnique(string desired, IEnumerable<string?> existing, UniqueNameOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(existing);

        UniqueNameOptions settings = options ?? UniqueNameOptions.Default;
        settings.Validate();

        HashSet<string> taken = new(settings.Comparer);
        foreach (string? name in existing)
        {
            if (name is not null)
            {
                taken.Add(name);
            }
        }

        return MakeUnique(desired, taken.Contains, settings);
    }

    /// <summary>Relit un nom numéroté selon le format : « Sans titre 3 » → « Sans titre », 3.</summary>
    private static bool TryReadNumbered(string name, string format, out string baseName, out long number)
    {
        baseName = name;
        number = 0;

        StringBuilder pattern = new("^");
        int position = 0;
        while (position < format.Length)
        {
            if (string.CompareOrdinal(format, position, "{0}", 0, 3) == 0)
            {
                pattern.Append("(?<nom>.+?)");
                position += 3;
            }
            else if (string.CompareOrdinal(format, position, "{1}", 0, 3) == 0)
            {
                pattern.Append("(?<num>[0-9]{1,18})");
                position += 3;
            }
            else
            {
                pattern.Append(Regex.Escape(format[position].ToString()));
                position++;
            }
        }

        pattern.Append('$');

        Match match = Regex.Match(name, pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        if (!match.Success || !long.TryParse(match.Groups["num"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }

        baseName = match.Groups["nom"].Value;
        return baseName.Length > 0;
    }
}
