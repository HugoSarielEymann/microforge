using System.Xml.Linq;

namespace SnippetForge;

/// <summary>Accès aux sources d'un micropackage sous packages/&lt;Id&gt;/.</summary>
public sealed class PackageSource
{
    private PackageSource(string directory, string projectFile)
    {
        Directory = directory;
        ProjectFile = projectFile;
    }

    /// <summary>Dossier racine du package (contient README.md, src/, tests/).</summary>
    public string Directory { get; }

    /// <summary>Chemin du .csproj de src/.</summary>
    public string ProjectFile { get; }

    /// <summary>Identifiant déclaré dans le projet.</summary>
    public string Id => Property("PackageId");

    /// <summary>Version déclarée dans le projet.</summary>
    public string Version => Property("Version");

    /// <summary>Description déclarée dans le projet.</summary>
    public string Description => Property("Description");

    /// <summary>Tags déclarés dans le projet.</summary>
    public IReadOnlyList<string> Tags => Cli.SplitList(Property("PackageTags"));

    /// <summary>Contenu du README.md du package (vide s'il est absent).</summary>
    public string Readme
    {
        get
        {
            var path = Path.Combine(Directory, "README.md");
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
    }

    /// <summary>Résout un package depuis un chemin ou un identifiant.</summary>
    /// <exception cref="ArgumentException">Si le package ou son unique .csproj est introuvable.</exception>
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

        var srcDir = Path.Combine(directory, "src");
        if (!System.IO.Directory.Exists(srcDir))
        {
            throw new ArgumentException($"Dossier src/ manquant dans « {directory} ».", nameof(pathOrId));
        }

        var projects = System.IO.Directory.GetFiles(srcDir, "*.csproj");
        if (projects.Length != 1)
        {
            throw new ArgumentException(
                $"src/ doit contenir exactement un .csproj (trouvé : {projects.Length}).", nameof(pathOrId));
        }

        return new PackageSource(directory, projects[0]);
    }

    /// <summary>Écrit une nouvelle version dans le .csproj.</summary>
    /// <exception cref="InvalidOperationException">Si l'élément Version est absent.</exception>
    public void SetVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var project = XDocument.Load(ProjectFile);
        var element = project.Descendants("Version").FirstOrDefault()
                      ?? throw new InvalidOperationException($"Élément <Version> absent de {ProjectFile}.");

        element.Value = version;
        project.Save(ProjectFile);
    }

    private string Property(string name)
    {
        var project = XDocument.Load(ProjectFile);
        return project.Descendants(name).FirstOrDefault()?.Value.Trim() ?? string.Empty;
    }
}
