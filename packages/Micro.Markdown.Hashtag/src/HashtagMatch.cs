namespace Micro.Markdown.Hashtag;

/// <summary>Une étiquette relevée dans un texte.</summary>
/// <param name="Start">Position du <c>#</c>, en index UTF-16.</param>
/// <param name="Length">Longueur, <c>#</c> compris.</param>
/// <param name="Name">Nom de l'étiquette, sans le <c>#</c> : <c>projet/almageste</c>.</param>
public readonly record struct HashtagMatch(int Start, int Length, string Name)
{
    /// <summary>Position qui suit immédiatement l'étiquette.</summary>
    public int End => Start + Length;
}
