using System.Xml.Linq;

namespace SnippetForge.Consumers;

/// <summary>Effet d'une déclaration de source sur un nuget.config.</summary>
public enum NuGetSourceOutcome
{
    /// <summary>Le fichier n'existait pas : il a été créé avec la source.</summary>
    Created = 0,

    /// <summary>La source a été ajoutée à un fichier existant.</summary>
    Added = 1,

    /// <summary>La source existait sous ce nom mais pointait ailleurs : elle a été corrigée.</summary>
    Updated = 2,

    /// <summary>La source était déjà déclarée à l'identique : aucune écriture.</summary>
    AlreadyPresent = 3,
}

/// <summary>
/// Déclare le feed MicroForge comme source NuGet d'un projet, en préservant
/// intégralement les sources déjà présentes.
///
/// Le format nuget.config est hiérarchique : un fichier dans le dossier du projet
/// s'ajoute aux configurations parentes au lieu de les remplacer. Déclarer le feed
/// ici n'ôte donc pas l'accès à nuget.org.
/// </summary>
public static class NuGetConfigWriter
{
    /// <summary>Nom sous lequel le feed est déclaré.</summary>
    public const string SourceName = "MicroForge";

    /// <summary>
    /// Garantit que <paramref name="sourcePath"/> est déclaré sous le nom
    /// <paramref name="sourceName"/> dans <paramref name="configPath"/>.
    /// L'opération est idempotente.
    /// </summary>
    /// <exception cref="ArgumentException">Si un argument est vide.</exception>
    public static NuGetSourceOutcome EnsureSource(string configPath, string sourcePath, string sourceName = SourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        if (!File.Exists(configPath))
        {
            Create(configPath, sourcePath, sourceName);
            return NuGetSourceOutcome.Created;
        }

        var document = XDocument.Load(configPath, LoadOptions.PreserveWhitespace);
        var configuration = document.Root
                            ?? throw new InvalidOperationException($"{configPath} est vide ou illisible.");

        var packageSources = configuration.Element("packageSources");
        if (packageSources is null)
        {
            packageSources = new XElement("packageSources");
            configuration.Add(packageSources);
        }

        var existing = packageSources.Elements("add")
            .FirstOrDefault(e => string.Equals(
                e.Attribute("key")?.Value, sourceName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            packageSources.Add(new XElement("add",
                new XAttribute("key", sourceName),
                new XAttribute("value", sourcePath)));
            document.Save(configPath);
            return NuGetSourceOutcome.Added;
        }

        if (string.Equals(existing.Attribute("value")?.Value, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return NuGetSourceOutcome.AlreadyPresent;
        }

        existing.SetAttributeValue("value", sourcePath);
        document.Save(configPath);
        return NuGetSourceOutcome.Updated;
    }

    /// <summary>Lit la valeur déclarée pour une source, ou null si elle est absente.</summary>
    public static string? ReadSource(string configPath, string sourceName = SourceName)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        return XDocument.Load(configPath).Root?
            .Element("packageSources")?
            .Elements("add")
            .FirstOrDefault(e => string.Equals(
                e.Attribute("key")?.Value, sourceName, StringComparison.OrdinalIgnoreCase))?
            .Attribute("value")?.Value;
    }

    private static void Create(string configPath, string sourcePath, string sourceName)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XComment(" Source NuGet locale MicroForge, ajoutée par « forge init ». " +
                         "Les sources héritées (nuget.org) restent actives. "),
            new XElement("configuration",
                new XElement("packageSources",
                    new XElement("add",
                        new XAttribute("key", sourceName),
                        new XAttribute("value", sourcePath)))));

        document.Save(configPath);
    }
}
