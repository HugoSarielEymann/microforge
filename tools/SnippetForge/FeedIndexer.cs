using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using SnippetForge.Api;

namespace SnippetForge;

/// <summary>
/// Reconstruit l'index de recherche (registry/index.json) à partir des .nupkg
/// présents dans le feed. Le feed est la source de vérité unique ; tout le contenu
/// de registry/ en est dérivé et régénérable.
/// </summary>
public static class FeedIndexer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Scanne le feed, régénère l'index et l'écrit sur disque.</summary>
    public static IndexDocument Rebuild(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var byId = new Dictionary<string, List<(PackageMeta Meta, string Readme)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var nupkg in Directory.EnumerateFiles(root.FeedDir, "*.nupkg"))
        {
            var (meta, readme) = ReadPackage(nupkg);
            if (!byId.TryGetValue(meta.Id, out var list))
            {
                list = [];
                byId[meta.Id] = list;
            }

            list.Add((meta, readme));
        }

        var entries = byId.Values
            .Select(versions =>
            {
                var ordered = versions
                    .OrderByDescending(v => v.Meta.Version, Comparer<string>.Create(SemVerLite.Compare))
                    .ToList();
                var latest = ordered[0];
                return new IndexEntry(
                    latest.Meta.Id,
                    latest.Meta.Version,
                    ordered.Select(v => v.Meta.Version).ToList(),
                    latest.Meta.Description,
                    latest.Meta.Tags,
                    latest.Meta.Authors,
                    latest.Readme);
            })
            .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var document = new IndexDocument(DateTime.UtcNow, entries);
        Directory.CreateDirectory(root.RegistryDir);
        File.WriteAllText(root.IndexFile, JsonSerializer.Serialize(document, JsonOptions));
        return document;
    }

    /// <summary>
    /// Extrait et enregistre la surface d'API de toute version publiée qui n'en a pas encore.
    /// Une extraction impossible est signalée sans interrompre la réindexation des autres
    /// packages : l'absence de contrat est traitée comme « rupture possible » en aval.
    /// </summary>
    public static ApiSurfaceRebuildResult RebuildApiSurfaces(ForgeRoot root, IndexDocument index)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(index);

        var store = new ApiSurfaceStore(root);
        var extracted = 0;
        var warnings = new List<string>();

        foreach (var entry in index.Packages)
        {
            foreach (var version in entry.AllVersions.Where(v => !store.Contains(entry.Id, v)))
            {
                var nupkg = PackagePath(root, entry.Id, version);
                if (!File.Exists(nupkg))
                {
                    continue;
                }

                try
                {
                    store.Save(ApiSurfaceExtractor.FromPackage(nupkg, entry.Id, version));
                    extracted++;
                }
                catch (InvalidOperationException exception)
                {
                    warnings.Add(exception.Message);
                }
            }
        }

        return new ApiSurfaceRebuildResult(extracted, warnings);
    }

    /// <summary>Charge l'index depuis le disque, en le reconstruisant s'il est absent.</summary>
    public static IndexDocument Load(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!File.Exists(root.IndexFile))
        {
            return Rebuild(root);
        }

        var json = File.ReadAllText(root.IndexFile);
        return JsonSerializer.Deserialize<IndexDocument>(json, JsonOptions)
               ?? new IndexDocument(DateTime.UtcNow, []);
    }

    /// <summary>Chemin canonique d'un artefact dans le feed.</summary>
    public static string PackagePath(ForgeRoot root, string id, string version) =>
        Path.Combine(root.FeedDir, $"{id}.{version}.nupkg");

    /// <summary>Indique si une version exacte d'un package existe déjà dans le feed.</summary>
    public static bool VersionExists(ForgeRoot root, string id, string version) =>
        File.Exists(PackagePath(root, id, version));

    private static (PackageMeta Meta, string Readme) ReadPackage(string nupkgPath)
    {
        using var zip = ZipFile.OpenRead(nupkgPath);

        var nuspecEntry = zip.Entries.First(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        var metadata = nuspec.Descendants().First(e => e.Name.LocalName == "metadata");

        string Value(string name) =>
            metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

        var tags = Value("tags")
            .Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var meta = new PackageMeta(Value("id"), Value("version"), Value("description"), tags, Value("authors"));

        var readmeEntry = zip.Entries.FirstOrDefault(e =>
            e.FullName.Equals("README.md", StringComparison.OrdinalIgnoreCase));
        var readme = string.Empty;
        if (readmeEntry is not null)
        {
            using var reader = new StreamReader(readmeEntry.Open());
            readme = reader.ReadToEnd();
        }

        return (meta, readme);
    }
}
