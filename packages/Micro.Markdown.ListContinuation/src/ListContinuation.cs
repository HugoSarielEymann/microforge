using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Micro.Markdown.ListContinuation;

/// <summary>Ce qu'il faut faire d'une ligne quand on y appuie sur Entrée.</summary>
public enum ListContinuationKind
{
    /// <summary>Rien de particulier : un saut de ligne ordinaire.</summary>
    None,

    /// <summary>Poursuivre la liste ou la citation : insérer <see cref="ListContinuationResult.Prefix"/> après le saut de ligne.</summary>
    Continue,

    /// <summary>
    /// Terminer la liste : l'élément est vide, on retire son marqueur
    /// (<see cref="ListContinuationResult.RemoveStart"/>, <see cref="ListContinuationResult.RemoveLength"/>)
    /// au lieu d'insérer un saut de ligne.
    /// </summary>
    End,
}

/// <summary>La décision prise pour une ligne.</summary>
/// <param name="Kind">Poursuivre, terminer, ou ne rien faire.</param>
/// <param name="Prefix">Texte à placer au début de la nouvelle ligne (poursuite).</param>
/// <param name="RemoveStart">Début du marqueur à retirer dans la ligne (fin de liste).</param>
/// <param name="RemoveLength">Longueur du marqueur à retirer (fin de liste).</param>
public readonly record struct ListContinuationResult(ListContinuationKind Kind, string Prefix, int RemoveStart, int RemoveLength)
{
    /// <summary>Aucune action particulière.</summary>
    public static ListContinuationResult None { get; } = new(ListContinuationKind.None, string.Empty, 0, 0);
}

/// <summary>Le marqueur de liste lu en tête d'une ligne.</summary>
/// <param name="MarkerStart">Position de la puce ou du numéro (après indentation et citation).</param>
/// <param name="Bullet">Puce (<c>-</c>, <c>*</c>, <c>+</c>), ou <see langword="null"/> pour une liste numérotée.</param>
/// <param name="Number">Numéro d'une liste numérotée, sinon <see langword="null"/>.</param>
/// <param name="Delimiter">Délimiteur du numéro (<c>.</c> ou <c>)</c>), sinon <c>'\0'</c>.</param>
/// <param name="IsTask">L'élément porte une case à cocher.</param>
/// <param name="IsChecked">La case est cochée.</param>
/// <param name="ContentStart">Position où commence le contenu de l'élément.</param>
public readonly record struct ListMarker(int MarkerStart, char? Bullet, long? Number, char Delimiter, bool IsTask, bool IsChecked, int ContentStart);

/// <summary>Réglages de la poursuite des listes.</summary>
public sealed class ListContinuationOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static ListContinuationOptions Default { get; } = new();

    /// <summary>Poursuivre aussi les citations (<c>&gt; </c>). Défaut : <see langword="true"/>.</summary>
    public bool ContinueQuotes { get; init; } = true;

    /// <summary>Une nouvelle tâche part décochée, même après une tâche cochée. Défaut : <see langword="true"/>.</summary>
    public bool ResetTasks { get; init; } = true;

    /// <summary>Numéroter l'élément suivant (<c>3.</c> après <c>2.</c>). Défaut : <see langword="true"/>.</summary>
    public bool Increment { get; init; } = true;

    /// <summary>Vérifie la cohérence des réglages. Aucun réglage ne peut être incohérent : la méthode existe pour le contrat.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Membre d'instance exigé par le contrat commun des options (Validate), même sans réglage à contrôler.")]
    public void Validate()
    {
    }
}

/// <summary>
/// Calcule ce qu'un éditeur Markdown doit faire quand on appuie sur Entrée dans une liste ou
/// une citation : reprendre la même puce, le numéro suivant, une case à cocher vide, le même
/// niveau de citation — ou, sur un élément vide, terminer la liste en retirant son marqueur.
/// </summary>
/// <remarks>
/// C'est le comportement qu'on attend de tout éditeur de notes, et que chacun réécrit : la
/// logique tient ici, sans rien savoir du contrôle d'édition. Fonction pure et déterministe.
/// </remarks>
public static class ListContinuation
{
    /// <summary>Décide de l'effet d'Entrée sur une ligne.</summary>
    /// <param name="line">Texte de la ligne, sans saut de ligne.</param>
    /// <param name="caret">Position du curseur dans la ligne.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>La décision.</returns>
    /// <exception cref="ArgumentNullException">Si la ligne est nulle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si le curseur sort de la ligne.</exception>
    /// <remarks>Un curseur placé avant la fin du marqueur donne un saut de ligne ordinaire : on veut alors ouvrir une ligne au-dessus, pas une puce.</remarks>
    public static ListContinuationResult OnEnter(string line, int caret, ListContinuationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentOutOfRangeException.ThrowIfNegative(caret);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(caret, line.Length);

        ListContinuationOptions settings = options ?? ListContinuationOptions.Default;
        settings.Validate();

        int quoteEnd = SkipQuotes(line, out bool hasQuote);

        if (TryParseMarker(line, out ListMarker marker))
        {
            if (caret < marker.ContentStart)
            {
                return ListContinuationResult.None;
            }

            if (line.AsSpan(marker.ContentStart).IsWhiteSpace())
            {
                return new ListContinuationResult(ListContinuationKind.End, string.Empty, marker.MarkerStart, line.Length - marker.MarkerStart);
            }

            return new ListContinuationResult(ListContinuationKind.Continue, BuildPrefix(line, marker, settings), 0, 0);
        }

        if (hasQuote && settings.ContinueQuotes)
        {
            if (caret < quoteEnd)
            {
                return ListContinuationResult.None;
            }

            int indent = LeadingBlanks(line, 0);
            if (line.AsSpan(quoteEnd).IsWhiteSpace())
            {
                return new ListContinuationResult(ListContinuationKind.End, string.Empty, indent, line.Length - indent);
            }

            return new ListContinuationResult(ListContinuationKind.Continue, line[..quoteEnd], 0, 0);
        }

        return ListContinuationResult.None;
    }

    /// <summary>Lit le marqueur de liste d'une ligne, citations et indentation comprises.</summary>
    /// <param name="line">Texte de la ligne ; <see langword="null"/> n'a pas de marqueur.</param>
    /// <param name="marker">Le marqueur lu, ou la valeur par défaut.</param>
    /// <returns><see langword="true"/> si la ligne est un élément de liste.</returns>
    /// <remarks>Ne lève jamais.</remarks>
    public static bool TryParseMarker(string? line, out ListMarker marker)
    {
        marker = default;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        int position = SkipQuotes(line, out _);
        position = LeadingBlanks(line, position);

        if (position >= line.Length)
        {
            return false;
        }

        int markerStart = position;
        char? bullet = null;
        long? number = null;
        char delimiter = '\0';

        if (line[position] is '-' or '*' or '+')
        {
            bullet = line[position];
            position++;
        }
        else
        {
            int digits = position;
            while (digits < line.Length && digits - position < 9 && char.IsAsciiDigit(line[digits]))
            {
                digits++;
            }

            if (digits == position || digits >= line.Length || line[digits] is not ('.' or ')'))
            {
                return false;
            }

            number = long.Parse(line.AsSpan(position, digits - position), NumberStyles.None, CultureInfo.InvariantCulture);
            delimiter = line[digits];
            position = digits + 1;
        }

        // Un marqueur est suivi d'un blanc, ou termine la ligne (élément vide).
        if (position < line.Length && line[position] is not (' ' or '\t'))
        {
            return false;
        }

        position = LeadingBlanks(line, position);

        bool isTask = false;
        bool isChecked = false;
        if (line.Length - position >= 3
            && line[position] == '['
            && line[position + 1] is ' ' or 'x' or 'X'
            && line[position + 2] == ']'
            && (position + 3 == line.Length || line[position + 3] is ' ' or '\t'))
        {
            isTask = true;
            isChecked = line[position + 1] is 'x' or 'X';
            position = LeadingBlanks(line, position + 3);
        }

        marker = new ListMarker(markerStart, bullet, number, delimiter, isTask, isChecked, position);
        return true;
    }

    private static string BuildPrefix(string line, ListMarker marker, ListContinuationOptions options)
    {
        string head = line[..marker.MarkerStart];

        string symbol = marker.Bullet is { } bullet
            ? bullet.ToString()
            : (options.Increment ? marker.Number!.Value + 1 : marker.Number!.Value).ToString(CultureInfo.InvariantCulture) + marker.Delimiter;

        // L'espacement d'origine entre le marqueur et le contenu est conservé.
        int symbolEnd = marker.Bullet is not null
            ? marker.MarkerStart + 1
            : line.IndexOf(marker.Delimiter, marker.MarkerStart) + 1;
        int afterSymbol = LeadingBlanks(line, symbolEnd);
        string spacing = afterSymbol > symbolEnd ? line[symbolEnd..afterSymbol] : " ";

        if (!marker.IsTask)
        {
            return head + symbol + spacing;
        }

        char state = marker.IsChecked && !options.ResetTasks ? 'x' : ' ';
        return head + symbol + spacing + "[" + state + "] ";
    }

    /// <summary>Passe l'indentation et les marqueurs de citation (<c>&gt; &gt; </c>).</summary>
    private static int SkipQuotes(string line, out bool hasQuote)
    {
        hasQuote = false;
        int position = LeadingBlanks(line, 0);
        int end = position;

        while (position < line.Length && line[position] == '>')
        {
            hasQuote = true;
            position++;
            if (position < line.Length && line[position] == ' ')
            {
                position++;
            }

            end = position;
            position = LeadingBlanks(line, position);
            if (position >= line.Length || line[position] != '>')
            {
                break;
            }
        }

        return hasQuote ? end : 0;
    }

    private static int LeadingBlanks(string line, int from)
    {
        int position = from;
        while (position < line.Length && line[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }
}
