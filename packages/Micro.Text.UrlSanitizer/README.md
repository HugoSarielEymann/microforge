# Micro.Text.UrlSanitizer

## Description

Masque les valeurs sensibles (tokens, clés API, mots de passe) présentes dans une URL —
paramètres de requête **et** identifiants placés avant l'hôte — pour journalisation sécurisée.
Fournit `UrlSanitizer.Sanitize(url, options?)`. Les noms de paramètres sensibles sont
configurables via `UrlSanitizerOptions`.

## Mode d'emploi

**Quand utiliser ce package :** avant de journaliser ou d'afficher une URL qui peut contenir des secrets,
soit dans la query string (`?token=…`, `?api_key=…`, `?password=…`), soit dans les identifiants
(`https://user:motdepasse@hôte/…`). Indispensable dans les middlewares de diagnostic, les moniteurs
de disponibilité, les clients HTTP instrumentés.

**Quand ne pas l'utiliser :** pour des secrets dans le corps d'une requête JSON ou dans des en-têtes HTTP ;
ce package ne traite que l'URL elle-même. Ne l'utilisez pas non plus comme unique mécanisme de sécurité :
c'est un outil de journalisation, pas un filtre de sécurité applicatif.

**Depuis 1.1.0** : le mot de passe des identifiants d'URL est masqué par défaut. Le nom
d'utilisateur est conservé — il sert au diagnostic et n'est pas le secret. Régler
`MaskUserInfo = false` pour revenir au comportement de 1.0.0.

## Paramétrage

| Option | Type | Défaut | Rôle |
|--------|------|--------|------|
| `SensitiveQueryParams` | `IReadOnlyList<string>` | token, api_key, apikey, key, secret, password, access_token, auth, apitoken, api_token | Noms de paramètres dont la valeur est remplacée (insensible à la casse). |
| `Mask` | `string` | `***` | Texte affiché à la place de la valeur sensible. |
| `MaskUserInfo` | `bool` | `true` | Masque le mot de passe des identifiants d'URL (`user:motdepasse@hôte`). |

## Exemple

```csharp
using Micro.Text.UrlSanitizer;

// Utilisation avec les options par défaut
string safeUrl = UrlSanitizer.Sanitize(
    "https://api.example.com/data?token=secret123&page=1");
// => "https://api.example.com/data?token=***&page=1"

// Utilisation avec des options personnalisées
var opts = new UrlSanitizerOptions
{
    SensitiveQueryParams = ["custom_auth", "session_id"],
    Mask = "[REDACTED]"
};
string safeCustom = UrlSanitizer.Sanitize(
    "https://host/ep?custom_auth=abc&page=2", opts);
// => "https://host/ep?custom_auth=[REDACTED]&page=2"

// Identifiants dans l'URL (masqués par défaut depuis 1.1.0)
string safeCreds = UrlSanitizer.Sanitize("https://admin:motdepasse@api.example.com/data");
// => "https://admin:***@api.example.com/data"

logger.LogInformation("GET {Url}", UrlSanitizer.Sanitize(requestUrl));
```