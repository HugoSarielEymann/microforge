namespace Micro.Markdown.CodeSpans;

/// <summary>Nature d'une plage de code relevée dans un texte Markdown.</summary>
public enum CodeSpanKind
{
    /// <summary>Bloc délimité par une clôture de trois backticks ou tildes au moins.</summary>
    Fenced,

    /// <summary>Segment en ligne, entre deux suites de backticks de même longueur.</summary>
    Inline,

    /// <summary>Bloc indenté de quatre espaces (ou d'une tabulation) après une ligne vide.</summary>
    Indented,
}

/// <summary>Une plage de code, exprimée en positions UTF-16 du texte analysé.</summary>
/// <param name="Start">Position du premier caractère de la plage, délimiteurs compris.</param>
/// <param name="Length">Longueur de la plage, délimiteurs compris.</param>
/// <param name="Kind">Nature de la plage.</param>
/// <remarks>
/// Les délimiteurs font partie de la plage : un analyseur qui veut ignorer le code doit
/// aussi ignorer ses backticks, faute de quoi un <c>`#</c> en bord de segment passerait
/// pour une étiquette.
/// </remarks>
public readonly record struct CodeSpan(int Start, int Length, CodeSpanKind Kind)
{
    /// <summary>Position qui suit immédiatement la plage.</summary>
    public int End => Start + Length;

    /// <summary>Indique qu'une position tombe dans la plage.</summary>
    /// <param name="index">Position à tester.</param>
    /// <returns><see langword="true"/> si <paramref name="index"/> appartient à la plage.</returns>
    public bool Contains(int index) => index >= Start && index < (long)Start + Length;
}
