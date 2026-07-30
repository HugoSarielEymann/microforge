using System.Reflection;

namespace SnippetForge.Api;

/// <summary>
/// Résolveur d'assemblies pour l'extraction de contrat. Cherche successivement dans
/// les assemblies du runtime, les dossiers fournis, puis le cache global NuGet.
///
/// Ce dernier point est indispensable : un micropackage extrait du feed est seul dans
/// un dossier temporaire, alors que son contrat référence ses dépendances
/// (Microsoft.Extensions.Logging.Abstractions, par exemple).
/// </summary>
public sealed class ForgeAssemblyResolver : MetadataAssemblyResolver
{
    private readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Construit le résolveur à partir des dossiers de recherche prioritaires.</summary>
    public ForgeAssemblyResolver(IEnumerable<string> searchDirectories)
    {
        ArgumentNullException.ThrowIfNull(searchDirectories);

        foreach (var directory in searchDirectories.Where(Directory.Exists))
        {
            foreach (var dll in Directory.EnumerateFiles(directory, "*.dll"))
            {
                _byName.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
            }
        }
    }

    /// <summary>Noms d'assemblies qui n'ont pas pu être résolus lors de l'extraction.</summary>
    public IReadOnlyCollection<string> Unresolved => _unresolved;

    private readonly HashSet<string> _unresolved = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assemblyName);

        var name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (_byName.TryGetValue(name, out var known))
        {
            return context.LoadFromAssemblyPath(known);
        }

        var fromCache = FindInNuGetCache(name);
        if (fromCache is null)
        {
            _unresolved.Add(name);
            return null;
        }

        _byName[name] = fromCache;
        return context.LoadFromAssemblyPath(fromCache);
    }

    /// <summary>Racine du cache global NuGet.</summary>
    public static string NuGetCacheRoot =>
        Environment.GetEnvironmentVariable("NUGET_PACKAGES")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    /// <summary>
    /// Recherche une assembly par son nom simple dans le cache NuGet, en privilégiant
    /// la version la plus récente.
    /// </summary>
    private static string? FindInNuGetCache(string simpleName)
    {
        // Le cache NuGet nomme ses dossiers d'après l'identifiant du package en
        // minuscules ; il coïncide avec le nom de l'assembly dans la quasi-totalité
        // des cas, ce qui évite d'énumérer tout le cache.
        var packageDir = Path.Combine(NuGetCacheRoot, simpleName.ToLowerInvariant());
        if (!Directory.Exists(packageDir))
        {
            return null;
        }

        var versions = Directory.EnumerateDirectories(packageDir)
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var version in versions)
        {
            var lib = Path.Combine(version, "lib");
            if (!Directory.Exists(lib))
            {
                continue;
            }

            var match = Directory
                .EnumerateFiles(lib, $"{simpleName}.dll", SearchOption.AllDirectories)
                .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
