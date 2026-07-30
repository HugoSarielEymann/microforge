namespace SnippetForge.Consumers;

/// <summary>Description sommaire d'un dossier cible, pour décider s'il est un projet.</summary>
public sealed record ProjectShape(int FileCount, int ProjectCount, bool Truncated)
{
    /// <summary>Au-delà, le dossier est trop volumineux pour être capturé fichier par fichier.</summary>
    public const int MaxFilesForSnapshot = 5000;

    /// <summary>Au-delà, le dossier ressemble à un conteneur de dépôts, pas à un projet.</summary>
    public const int MaxProjectsForSingleProject = 3;

    /// <summary>
    /// Vrai si la cible ressemble à un dossier parent plutôt qu'à un projet.
    /// Une analyse tronquée suffit à conclure : elle signifie que le plafond de
    /// fichiers demandé a été franchi, quel que soit ce plafond.
    /// </summary>
    public bool LooksLikeContainer =>
        Truncated || ProjectCount > MaxProjectsForSingleProject || FileCount > MaxFilesForSnapshot;

    /// <summary>Explication destinée à l'utilisateur, ou null si la cible est plausible.</summary>
    public string? Warning => LooksLikeContainer
        ? $"ce dossier contient {(Truncated ? "plus de " : string.Empty)}{FileCount} fichier(s) " +
          $"et {(Truncated ? "au moins " : string.Empty)}{ProjectCount} projet(s) — " +
          "il ressemble à un dossier parent, pas à un projet."
        : null;
}

/// <summary>
/// Reconnaît un dossier parent (« source\repos ») d'un vrai dossier de projet.
///
/// Sans ce contrôle, « forge init » dépose des instructions valables pour des dizaines
/// de dépôts sans rapport, et « forge bench start » tente d'empreindre des dizaines de
/// milliers de fichiers — au point de ne jamais terminer. Le comptage s'arrête tôt :
/// analyser le dossier ne doit pas coûter ce que l'on cherche à éviter.
/// </summary>
public static class ProjectShapeAnalyzer
{
    private static readonly string[] IgnoredSegments = ["bin", "obj", ".git", ".vs", "node_modules", "packages"];

    /// <summary>
    /// Compte fichiers et projets, en s'arrêtant dès que le plafond est franchi.
    /// </summary>
    public static ProjectShape Analyze(string directory, int fileCeiling = ProjectShape.MaxFilesForSnapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(fileCeiling, 1);

        var files = 0;
        var projects = 0;

        foreach (var file in EnumerateRelevantFiles(directory))
        {
            files++;
            if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                projects++;
            }

            if (files > fileCeiling)
            {
                return new ProjectShape(fileCeiling, projects, Truncated: true);
            }
        }

        return new ProjectShape(files, projects, Truncated: false);
    }

    private static IEnumerable<string> EnumerateRelevantFiles(string directory)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var file in Directory.EnumerateFiles(directory, "*", options))
        {
            var relative = Path.GetRelativePath(directory, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Le dernier segment est le fichier : on n'inspecte que les dossiers traversés.
            if (segments.Take(segments.Length - 1)
                .Any(s => IgnoredSegments.Contains(s, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return file;
        }
    }
}
