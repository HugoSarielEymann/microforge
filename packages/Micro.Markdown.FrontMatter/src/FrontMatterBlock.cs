namespace Micro.Markdown.FrontMatter;

/// <summary>L'en-tête d'un document : ses positions et ses propriétés lues.</summary>
public sealed class FrontMatterBlock
{
    private readonly StringComparison _keyComparison;

    internal FrontMatterBlock(
        bool exists,
        int length,
        int contentStart,
        string content,
        IReadOnlyList<FrontMatterProperty> properties,
        StringComparison keyComparison)
    {
        Exists = exists;
        Length = length;
        ContentStart = contentStart;
        Content = content;
        Properties = properties;
        _keyComparison = keyComparison;
    }

    /// <summary>Un document sans en-tête.</summary>
    public static FrontMatterBlock None { get; } = new(false, 0, 0, string.Empty, [], StringComparison.OrdinalIgnoreCase);

    /// <summary>Le document commence par un en-tête refermé.</summary>
    public bool Exists { get; }

    /// <summary>
    /// Longueur de l'en-tête depuis le début du texte : lignes de clôture et saut de ligne final
    /// compris. C'est aussi la position où commence le corps.
    /// </summary>
    public int Length { get; }

    /// <summary>Position où commence le corps du document.</summary>
    public int BodyStart => Length;

    /// <summary>Position du texte YAML, juste après la ligne ouvrante.</summary>
    public int ContentStart { get; }

    /// <summary>Longueur du texte YAML, sans le saut de ligne qui précède la ligne fermante.</summary>
    public int ContentLength => Content.Length;

    /// <summary>Texte YAML brut, entre les deux lignes de clôture.</summary>
    public string Content { get; }

    /// <summary>Propriétés de premier niveau, dans l'ordre d'écriture.</summary>
    public IReadOnlyList<FrontMatterProperty> Properties { get; }

    /// <summary>Cherche une propriété par sa clé.</summary>
    /// <param name="key">Clé cherchée.</param>
    /// <returns>La propriété, ou <see langword="null"/> ; si la clé est répétée, la dernière l'emporte.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="key"/> est nul.</exception>
    public FrontMatterProperty? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (int index = Properties.Count - 1; index >= 0; index--)
        {
            if (string.Equals(Properties[index].Key, key, _keyComparison))
            {
                return Properties[index];
            }
        }

        return null;
    }

    /// <summary>Valeurs d'une propriété, ou une liste vide si elle est absente.</summary>
    /// <param name="key">Clé cherchée.</param>
    /// <returns>Les valeurs lues, jamais <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="key"/> est nul.</exception>
    public IReadOnlyList<string> GetValues(string key) => Find(key)?.Values ?? [];
}
