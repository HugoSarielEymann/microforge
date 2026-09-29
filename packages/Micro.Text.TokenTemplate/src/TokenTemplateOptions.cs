namespace Micro.Text.TokenTemplate;

/// <summary>Ce qu'il advient d'un jeton du gabarit dont le dictionnaire n'a pas la valeur.</summary>
public enum MissingTokenBehavior
{
    /// <summary>Le jeton est laissé tel quel, délimiteurs compris.</summary>
    Leave,

    /// <summary>Le jeton est remplacé par du vide.</summary>
    Blank,

    /// <summary>Le rendu échoue.</summary>
    Fail,
}

/// <summary>Réglages du rendu d'un gabarit.</summary>
public sealed class TokenTemplateOptions
{
    /// <summary>Réglages par défaut.</summary>
    public static TokenTemplateOptions Default { get; } = new();

    /// <summary>Délimiteur ouvrant. Défaut : <c>{</c>.</summary>
    public string Open { get; init; } = "{";

    /// <summary>Délimiteur fermant. Défaut : <c>}</c>.</summary>
    public string Close { get; init; } = "}";

    /// <summary>Sort réservé aux jetons absents du dictionnaire. Défaut : <see cref="MissingTokenBehavior.Leave"/>.</summary>
    /// <remarks>
    /// Le défaut laisse le jeton en place parce que c'est le comportement le moins destructeur :
    /// un gabarit rendu à moitié montre ce qui manque, là où un blanc l'efface silencieusement.
    /// </remarks>
    public MissingTokenBehavior OnMissing { get; init; } = MissingTokenBehavior.Leave;

    /// <summary>
    /// Reconnaître le délimiteur ouvrant doublé comme un délimiteur littéral. Défaut : <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Sans échappement, un gabarit ne pourrait jamais produire d'accolade — ce qui interdirait
    /// d'écrire un gabarit qui rend du JSON.
    /// </remarks>
    public bool AllowEscape { get; init; } = true;

    /// <summary>
    /// Transformation appliquée à chaque valeur avant insertion. Défaut : aucune.
    /// </summary>
    /// <remarks>
    /// C'est le point d'échappement contextuel : la même valeur s'insère telle quelle dans un
    /// en-tête et encodée dans une URL. Le faire ici plutôt qu'à l'appelant garantit qu'aucune
    /// valeur ne passe au travers.
    /// </remarks>
    public Func<string, string>? Transform { get; init; }

    /// <summary>Comparateur des noms de jetons. Défaut : ordinal, sensible à la casse.</summary>
    public StringComparer NameComparer { get; init; } = StringComparer.Ordinal;

    /// <summary>Longueur maximale d'un nom de jeton. Défaut : 128.</summary>
    /// <remarks>
    /// Un délimiteur ouvrant jamais refermé ferait sinon parcourir tout le reste du texte à la
    /// recherche d'un nom : la borne le transforme en simple caractère littéral.
    /// </remarks>
    public int MaxTokenLength { get; init; } = 128;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentException">Si un délimiteur est vide, ou si les deux sont identiques.</exception>
    /// <exception cref="ArgumentNullException">Si le comparateur de noms est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si la longueur maximale est nulle ou négative.</exception>
    public void Validate()
    {
        if (string.IsNullOrEmpty(Open) || string.IsNullOrEmpty(Close))
        {
            throw new ArgumentException("Les deux délimiteurs doivent être renseignés.", nameof(Open));
        }

        if (string.Equals(Open, Close, StringComparison.Ordinal))
        {
            // Des délimiteurs identiques rendraient « a {x} b {y} c » ambigu : impossible de
            // savoir si « } b {ct » est un jeton ou du texte entre deux jetons.
            throw new ArgumentException(
                "Les délimiteurs ouvrant et fermant doivent différer.", nameof(Close));
        }

        ArgumentNullException.ThrowIfNull(NameComparer);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxTokenLength, 1);
    }
}
