namespace Micro.Text.SearchMatch;

/// <summary>Une occurrence d'un terme de la requête dans un texte.</summary>
/// <param name="Start">Position de l'occurrence dans le texte d'origine, en index UTF-16.</param>
/// <param name="Length">Longueur de l'occurrence dans le texte d'origine.</param>
/// <param name="TermIndex">Rang du terme dans la requête découpée par <see cref="SearchMatcher.SplitTerms"/>.</param>
/// <remarks>
/// La longueur est celle du texte d'origine, qui peut différer de celle du terme : « é » écrit
/// en forme décomposée occupe deux caractères et répond au terme « e ».
/// </remarks>
public readonly record struct SearchOccurrence(int Start, int Length, int TermIndex)
{
    /// <summary>Position qui suit immédiatement l'occurrence.</summary>
    public int End => Start + Length;
}
