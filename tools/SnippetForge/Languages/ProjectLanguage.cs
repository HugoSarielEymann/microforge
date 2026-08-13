namespace SnippetForge.Languages;

/// <summary>
/// Reconnaît l'écosystème d'un dossier de projet par ses fichiers caractéristiques.
///
/// Sert à deux endroits où se tromper coûte cher : la recherche, qui ne doit pas
/// proposer un package inutilisable, et le raccordement, qui ne doit pas écrire des
/// instructions .NET dans un dépôt Python.
/// </summary>
public static class ProjectLanguage
{
    /// <summary>
    /// Fichiers signant un écosystème, dans l'ordre d'examen. Les marqueurs .NET
    /// passent avant `package.json` : un projet ASP.NET avec un front JavaScript
    /// reste un projet .NET du point de vue du code qu'on y écrit.
    /// </summary>
    private static readonly (string Pattern, string Language)[] Signatures =
    [
        ("*.csproj", "csharp"),
        ("*.sln", "csharp"),
        ("pyproject.toml", "python"),
        ("requirements.txt", "python"),
        ("setup.py", "python"),
        ("go.mod", "go"),
        ("Cargo.toml", "rust"),
        ("tsconfig.json", "typescript"),
        ("package.json", "javascript"),
    ];

    /// <summary>
    /// Identifiant d'écosystème du dossier, ou <see langword="null"/> si rien n'est
    /// reconnu. Le doute ne filtre pas : mieux vaut tout montrer que masquer à tort.
    /// </summary>
    /// <exception cref="ArgumentException">Si le chemin est vide.</exception>
    public static string? Detect(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (var (pattern, language) in Signatures)
        {
            if (Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Any())
            {
                return language;
            }
        }

        return null;
    }

    /// <summary>
    /// Profil du dossier, en retombant sur C# faute de reconnaissance : c'est
    /// l'écosystème de référence, et le seul dont les garanties sont complètes.
    /// </summary>
    /// <exception cref="ArgumentException">Si le chemin est vide.</exception>
    public static LanguageProfile Profile(string directory) =>
        LanguageProfiles.Resolve(Detect(directory));
}
