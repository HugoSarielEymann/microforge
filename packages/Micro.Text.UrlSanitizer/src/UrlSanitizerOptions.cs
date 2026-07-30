namespace Micro.Text.UrlSanitizer;

/// <summary>
/// Options de masquage pour <see cref="UrlSanitizer"/>.
/// </summary>
public sealed class UrlSanitizerOptions
{
    /// <summary>
    /// Noms de paramètres de requête dont la valeur doit être masquée (insensible à la casse).
    /// Par défaut : token, api_key, apikey, key, secret, password, access_token, auth.
    /// </summary>
    public IReadOnlyList<string> SensitiveQueryParams { get; init; } =
    [
        "token", "api_key", "apikey", "key", "secret",
        "password", "access_token", "auth", "apitoken", "api_token"
    ];

    /// <summary>
    /// Chaîne de remplacement affichée à la place de la valeur sensible.
    /// </summary>
    public string Mask { get; init; } = "***";

    /// <summary>
    /// Masque les identifiants placés avant l'hôte (<c>https://user:motdepasse@hote/…</c>).
    /// Activé par défaut : laisser un mot de passe en clair dans un journal est
    /// précisément ce que ce package existe pour empêcher.
    /// </summary>
    public bool MaskUserInfo { get; init; } = true;

    /// <summary>Valide les options et lève <see cref="InvalidOperationException"/> si elles sont incohérentes.</summary>
    public void Validate()
    {
        if (SensitiveQueryParams is null)
            throw new InvalidOperationException($"{nameof(SensitiveQueryParams)} ne peut pas être null.");
        if (string.IsNullOrEmpty(Mask))
            throw new InvalidOperationException($"{nameof(Mask)} ne peut pas être vide.");
    }
}
