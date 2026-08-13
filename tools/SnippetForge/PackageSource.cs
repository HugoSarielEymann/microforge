using System.Xml.Linq;
using SnippetForge.Languages;

namespace SnippetForge;

/// <summary>
/// Accès aux sources d'un micropackage sous packages/&lt;Id&gt;/.
///
/// Deux formes de métadonnées, selon l'écosystème : un <c>.csproj</c> en C# — il porte
/// déjà tout ce qu'il faut — et un <c>microforge.json</c> ailleurs. Le reste du
/// système ne voit que cette classe, et ignore laquelle des deux a servi.
/// </summary>
public sealed class PackageSource
{
    private readonly PackageManifest? _manifest;

    private PackageSource(string directory, string? projectFile, PackageManifest? manifest, LanguageProfile profile)
    {
        Directory = directory;
        _projectFile = projectFile;
        _manifest = manifest;
        Language = profile;
    }

    private readonly string? _projectFile;

    /// <summary>Dossier racine du package (contient README.md, src/, tests/).</summary>
    public string Directory { get; }

    /// <summary>Écosystème du package, et donc les garanties applicables.</summary>
    public LanguageProfile Language { get; }

    /// <summary>Chemin du .csproj de src/ — uniquement en C#.</summary>
    /// <exception cref="InvalidOperationException">Si le package n'est pas en C#.</exception>
    public string ProjectFile => _projectFile
        ?? throw new InvalidOperationException(
            $"{Id} est un package {Language.DisplayName} : il n'a pas de projet .NET.");

    /// <summary>Identifiant déclaré.</summary>
    public string Id => _manifest?.Id ?? Property("PackageId");

    /// <summary>Version déclarée.</summary>
    public string Version => _manifest?.Version ?? Property("Version");

    /// <summary>Description déclarée.</summary>
    public string Description => _manifest?.Description ?? Property("Description");

    /// <summary>Tags déclarés.</summary>
    public IReadOnlyList<string> Tags => _manifest?.Tags ?? Cli.SplitList(Property("PackageTags"));

    /// <summary>Contenu du README.md du package (vide s'il est absent).</summary>
    public string Readme
    {
        get
        {
            var path = Path.Combine(Directory, "README.md");
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
    }

    /// <summary>Fichiers source du package, selon les extensions de son écosystème.</summary>
    public IEnumerable<string> SourceFiles => FilesUnder(Path.Combine(Directory, "src"));

    /// <summary>Fichiers de test du package.</summary>
    public IEnumerable<string> TestFiles => FilesUnder(Path.Combine(Directory, "tests"));

    /// <summary>Résout un package depuis un chemin ou un identifiant.</summary>
    /// <exception cref="ArgumentException">Si le package est introuvable ou mal formé.</exception>
    public static PackageSource Resolve(ForgeRoot root, string pathOrId)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathOrId);

        var directory = System.IO.Directory.Exists(pathOrId)
            ? Path.GetFullPath(pathOrId)
            : Path.Combine(root.PackagesDir, pathOrId);

        if (!System.IO.Directory.Exists(directory))
        {
            throw new ArgumentException(
                $"Package introuvable : ni le chemin « {pathOrId} » ni packages/{pathOrId}.", nameof(pathOrId));
        }

        // Le manifeste prime : sa présence signale explicitement un écosystème.
        if (PackageManifest.Load(directory) is { } manifest)
        {
            if (LanguageProfiles.Find(manifest.Language) is null)
            {
                throw new ArgumentException(
                    $"Langage inconnu « {manifest.Language} » dans {PackageManifest.FileName}. " +
                    $"Connus : {LanguageProfiles.KnownIds}.", nameof(pathOrId));
            }

            return new PackageSource(directory, null, manifest, manifest.Profile);
        }

        var srcDir = Path.Combine(directory, "src");
        if (!System.IO.Directory.Exists(srcDir))
        {
            throw new ArgumentException($"Dossier src/ manquant dans « {directory} ».", nameof(pathOrId));
        }

        var projects = System.IO.Directory.GetFiles(srcDir, "*.csproj");
        return projects.Length == 1
            ? new PackageSource(directory, projects[0], null, LanguageProfiles.CSharp)
            : throw new ArgumentException(
                $"src/ doit contenir exactement un .csproj, ou le package doit déclarer son " +
                $"écosystème dans {PackageManifest.FileName} (trouvé : {projects.Length} projet(s)).",
                nameof(pathOrId));
    }

    /// <summary>Écrit une nouvelle version, quel que soit l'écosystème.</summary>
    /// <exception cref="InvalidOperationException">Si la version n'est pas déclarée.</exception>
    public void SetVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (_manifest is not null)
        {
            (_manifest with { Version = version }).Save(Directory);
            return;
        }

        var project = XDocument.Load(ProjectFile);
        var element = project.Descendants("Version").FirstOrDefault()
                      ?? throw new InvalidOperationException($"Élément <Version> absent de {ProjectFile}.");

        element.Value = version;
        project.Save(ProjectFile);
    }

    private IEnumerable<string> FilesUnder(string directory)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        return System.IO.Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => Language.SourceExtensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}__pycache__{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    private string Property(string name)
    {
        var project = XDocument.Load(ProjectFile);
        return project.Descendants(name).FirstOrDefault()?.Value.Trim() ?? string.Empty;
    }
}
