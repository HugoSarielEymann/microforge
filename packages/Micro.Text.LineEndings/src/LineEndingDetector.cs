namespace Micro.Text.LineEndings;

/// <summary>Convention de fin de ligne.</summary>
public enum LineEnding
{
    /// <summary>Aucune fin de ligne dans le texte.</summary>
    None,

    /// <summary><c>\n</c> : Unix, macOS, la plupart des outils modernes.</summary>
    Lf,

    /// <summary><c>\r\n</c> : Windows.</summary>
    CrLf,

    /// <summary><c>\r</c> seul : ancien Mac OS.</summary>
    Cr,
}

/// <summary>Décompte des fins de ligne d'un texte, par convention.</summary>
/// <param name="Lf">Nombre de <c>\n</c> isolés.</param>
/// <param name="CrLf">Nombre de <c>\r\n</c>.</param>
/// <param name="Cr">Nombre de <c>\r</c> isolés.</param>
public readonly record struct LineEndingProfile(int Lf, int CrLf, int Cr)
{
    /// <summary>Nombre total de fins de ligne.</summary>
    public int Total => Lf + CrLf + Cr;

    /// <summary>Plus d'une convention est présente.</summary>
    public bool IsMixed => (Lf > 0 ? 1 : 0) + (CrLf > 0 ? 1 : 0) + (Cr > 0 ? 1 : 0) > 1;

    /// <summary>
    /// La convention la plus fréquente, ou <see cref="LineEnding.None"/> s'il n'y en a aucune.
    /// À égalité, <c>\r\n</c> l'emporte sur <c>\n</c>, qui l'emporte sur <c>\r</c>.
    /// </summary>
    public LineEnding Dominant
    {
        get
        {
            if (Total == 0)
            {
                return LineEnding.None;
            }

            if (CrLf >= Lf && CrLf >= Cr)
            {
                return LineEnding.CrLf;
            }

            return Lf >= Cr ? LineEnding.Lf : LineEnding.Cr;
        }
    }
}

/// <summary>Réglages de la détection.</summary>
public sealed class LineEndingOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static LineEndingOptions Default { get; } = new();

    /// <summary>
    /// Convention rendue par <see cref="LineEndingDetector.Detect"/> pour un texte sans fin de
    /// ligne. Défaut : <see cref="LineEnding.Lf"/>.
    /// </summary>
    /// <remarks>
    /// Un texte d'une seule ligne ne dit rien de sa convention ; l'appelant choisit celle qu'il
    /// écrira s'il en ajoute. Ne peut pas valoir <see cref="LineEnding.None"/>.
    /// </remarks>
    public LineEnding Fallback { get; init; } = LineEnding.Lf;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="Fallback"/> vaut <see cref="LineEnding.None"/> ou n'est pas défini.</exception>
    public void Validate()
    {
        if (Fallback is not (LineEnding.Lf or LineEnding.CrLf or LineEnding.Cr))
        {
            throw new ArgumentOutOfRangeException(nameof(Fallback), Fallback, "Le repli doit être une convention réelle : Lf, CrLf ou Cr.");
        }
    }
}

/// <summary>
/// Détecte la convention de fin de ligne d'un texte, pour le réécrire sans changer ce que
/// l'utilisateur n'a pas modifié.
/// </summary>
/// <remarks>
/// L'usage type : un éditeur lit un fichier, travaille en interne avec une convention unique,
/// puis réécrit avec la convention d'origine. Sans cela, enregistrer une note en <c>\n</c> dans
/// un dépôt en <c>\r\n</c> ferait apparaître chaque ligne comme modifiée. La normalisation
/// elle-même est laissée à <see cref="string.ReplaceLineEndings(string)"/> du framework.
/// </remarks>
public static class LineEndingDetector
{
    /// <summary>Compte les fins de ligne d'un texte, par convention.</summary>
    /// <param name="text">Texte à analyser.</param>
    /// <returns>Le décompte.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    public static LineEndingProfile Analyze(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int lf = 0;
        int crlf = 0;
        int cr = 0;

        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            if (c == '\n')
            {
                lf++;
            }
            else if (c == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    crlf++;
                    index++;
                }
                else
                {
                    cr++;
                }
            }
        }

        return new LineEndingProfile(lf, crlf, cr);
    }

    /// <summary>Donne la convention dominante d'un texte, ou le repli s'il n'en contient aucune.</summary>
    /// <param name="text">Texte à analyser.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Une convention réelle, jamais <see cref="LineEnding.None"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les réglages sont invalides.</exception>
    public static LineEnding Detect(string text, LineEndingOptions? options = null)
    {
        LineEndingOptions settings = options ?? LineEndingOptions.Default;
        settings.Validate();

        LineEnding dominant = Analyze(text).Dominant;
        return dominant == LineEnding.None ? settings.Fallback : dominant;
    }

    /// <summary>Séquence de caractères d'une convention.</summary>
    /// <param name="ending">Convention réelle.</param>
    /// <returns><c>"\n"</c>, <c>"\r\n"</c> ou <c>"\r"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Pour <see cref="LineEnding.None"/> ou une valeur non définie.</exception>
    public static string ToSequence(LineEnding ending) => ending switch
    {
        LineEnding.Lf => "\n",
        LineEnding.CrLf => "\r\n",
        LineEnding.Cr => "\r",
        _ => throw new ArgumentOutOfRangeException(nameof(ending), ending, "Aucune séquence pour cette convention."),
    };
}
