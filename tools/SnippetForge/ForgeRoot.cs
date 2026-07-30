namespace SnippetForge;

/// <summary>
/// Localise la racine MicroForge et expose les chemins canoniques du système
/// (feed NuGet local, registre d'index, sources des micropackages).
/// </summary>
public sealed class ForgeRoot
{
    private ForgeRoot(string path) => Path = path;

    /// <summary>Chemin absolu de la racine MicroForge.</summary>
    public string Path { get; }

    /// <summary>Dossier du flux NuGet local (fichiers .nupkg publiés, immuables).</summary>
    public string FeedDir => System.IO.Path.Combine(Path, "feed");

    /// <summary>Dossier du registre (index de recherche).</summary>
    public string RegistryDir => System.IO.Path.Combine(Path, "registry");

    /// <summary>Fichier JSON d'index consommé par le moteur de recherche.</summary>
    public string IndexFile => System.IO.Path.Combine(RegistryDir, "index.json");

    /// <summary>Dossier contenant les sources des micropackages.</summary>
    public string PackagesDir => System.IO.Path.Combine(Path, "packages");

    /// <summary>Dossier de travail pour les artefacts de pack.</summary>
    public string ArtifactsDir => System.IO.Path.Combine(Path, ".artifacts");

    /// <summary>
    /// Fichier utilisateur mémorisant la racine. Indispensable en outil global :
    /// « forge » s'exécute alors depuis le dossier d'un projet quelconque, d'où la
    /// remontée de répertoires ne trouve rien.
    /// </summary>
    public static string UserConfigPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microforge", "root");

    /// <summary>
    /// Ouvre une racine dont le chemin est déjà connu, sans passer par la résolution
    /// implicite. Évite toute dépendance à l'état global du processus.
    /// </summary>
    /// <exception cref="ArgumentException">Si le dossier n'est pas une racine MicroForge.</exception>
    public static ForgeRoot At(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return IsRoot(path)
            ? new ForgeRoot(System.IO.Path.GetFullPath(path))
            : throw new ArgumentException(
                $"« {path} » n'est pas une racine MicroForge (RULES.md et feed/ attendus).", nameof(path));
    }

    /// <summary>
    /// Résout la racine, dans cet ordre : variable d'environnement MICROFORGE_ROOT,
    /// remontée depuis le répertoire courant, puis racine mémorisée par « forge use ».
    /// </summary>
    /// <exception cref="InvalidOperationException">Si aucune racine n'est trouvée.</exception>
    public static ForgeRoot Locate()
    {
        var env = Environment.GetEnvironmentVariable("MICROFORGE_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && IsRoot(env))
        {
            return new ForgeRoot(System.IO.Path.GetFullPath(env));
        }

        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (IsRoot(dir))
            {
                return new ForgeRoot(dir);
            }

            dir = System.IO.Path.GetDirectoryName(dir);
        }

        var remembered = ReadRemembered();
        if (remembered is not null && IsRoot(remembered))
        {
            return new ForgeRoot(System.IO.Path.GetFullPath(remembered));
        }

        throw new InvalidOperationException(
            "Racine MicroForge introuvable. Indiquez-la une fois pour toutes avec " +
            "« forge use <chemin de MicroForge> », ou définissez MICROFORGE_ROOT." +
            (remembered is not null
                ? $"\nRacine mémorisée mais invalide : {remembered}"
                : string.Empty));
    }

    /// <summary>Mémorise la racine pour les exécutions futures, depuis n'importe où.</summary>
    /// <exception cref="ArgumentException">Si le dossier n'est pas une racine MicroForge.</exception>
    public static ForgeRoot Remember(string path)
    {
        var root = At(path);
        var configPath = UserConfigPath;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, root.Path);
        return root;
    }

    /// <summary>Racine mémorisée, ou null si aucune.</summary>
    public static string? ReadRemembered()
    {
        var configPath = UserConfigPath;
        if (!File.Exists(configPath))
        {
            return null;
        }

        var content = File.ReadAllText(configPath).Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    /// <summary>Indique si un dossier est une racine MicroForge valide.</summary>
    public static bool IsRoot(string dir) =>
        !string.IsNullOrWhiteSpace(dir) &&
        File.Exists(System.IO.Path.Combine(dir, "RULES.md")) &&
        Directory.Exists(System.IO.Path.Combine(dir, "feed"));
}
