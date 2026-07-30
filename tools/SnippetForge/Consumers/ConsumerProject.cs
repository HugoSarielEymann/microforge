using System.Xml.Linq;

namespace SnippetForge.Consumers;

/// <summary>Référence NuGet épinglée dans un projet consommateur.</summary>
public sealed record PinnedReference(string PackageId, string Version);

/// <summary>
/// Lecture et réécriture des &lt;PackageReference&gt; d'un projet consommateur.
/// Les versions sont toujours épinglées : un build est reproductible, une mise à
/// jour est un acte délibéré et traçable.
/// </summary>
public static class ConsumerProject
{
    /// <summary>Préfixe des packages gérés par MicroForge.</summary>
    public const string ForgePrefix = "Micro.";

    /// <summary>Résout le .csproj à traiter à partir d'un chemin de fichier ou de dossier.</summary>
    /// <exception cref="ArgumentException">Si aucun projet unique n'est trouvé.</exception>
    public static string ResolveProjectFile(string pathOrDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathOrDirectory);

        if (File.Exists(pathOrDirectory) && pathOrDirectory.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(pathOrDirectory);
        }

        if (Directory.Exists(pathOrDirectory))
        {
            var projects = Directory.GetFiles(pathOrDirectory, "*.csproj");
            if (projects.Length == 1)
            {
                return Path.GetFullPath(projects[0]);
            }

            throw new ArgumentException(
                $"{projects.Length} projets trouvés dans « {pathOrDirectory} » : désigner le .csproj explicitement.",
                nameof(pathOrDirectory));
        }

        throw new ArgumentException($"Projet introuvable : « {pathOrDirectory} ».", nameof(pathOrDirectory));
    }

    /// <summary>Liste les références MicroForge épinglées par le projet.</summary>
    public static IReadOnlyList<PinnedReference> ReadForgeReferences(string projectFile)
    {
        var project = XDocument.Load(projectFile);
        return project.Descendants("PackageReference")
            .Select(e => new
            {
                Id = e.Attribute("Include")?.Value,
                Version = e.Attribute("Version")?.Value ?? e.Element("Version")?.Value,
            })
            .Where(r => r.Id is not null &&
                        r.Version is not null &&
                        r.Id.StartsWith(ForgePrefix, StringComparison.Ordinal))
            .Select(r => new PinnedReference(r.Id!, r.Version!))
            .OrderBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Applique de nouvelles versions aux références indiquées et réécrit le projet.
    /// </summary>
    /// <returns>Le nombre de références effectivement modifiées.</returns>
    public static int ApplyVersions(string projectFile, IReadOnlyDictionary<string, string> newVersions)
    {
        ArgumentNullException.ThrowIfNull(newVersions);

        var project = XDocument.Load(projectFile);
        var changed = 0;

        foreach (var element in project.Descendants("PackageReference"))
        {
            var id = element.Attribute("Include")?.Value;
            if (id is null || !newVersions.TryGetValue(id, out var version))
            {
                continue;
            }

            if (element.Attribute("Version") is { } attribute)
            {
                if (attribute.Value == version) continue;
                attribute.Value = version;
            }
            else if (element.Element("Version") is { } child)
            {
                if (child.Value == version) continue;
                child.Value = version;
            }
            else
            {
                continue;
            }

            changed++;
        }

        if (changed > 0)
        {
            project.Save(projectFile);
        }

        return changed;
    }
}
