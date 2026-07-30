using System.IO.Compression;
using System.Reflection;

namespace SnippetForge.Api;

/// <summary>
/// Extrait le contrat public d'un .nupkg en lisant l'assembly livrée, sans l'exécuter
/// (chargement en mode métadonnées uniquement).
/// </summary>
public static class ApiSurfaceExtractor
{
    /// <summary>Lit la surface d'API de l'assembly contenue dans <paramref name="nupkgPath"/>.</summary>
    /// <exception cref="InvalidOperationException">Si le package ne contient aucune assembly lib/.</exception>
    public static ApiSurface FromPackage(string nupkgPath, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nupkgPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "microforge-api", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            using var zip = ZipFile.OpenRead(nupkgPath);
            var entry = zip.Entries.FirstOrDefault(e =>
                            e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException(
                            $"Aucune assembly lib/*.dll dans {Path.GetFileName(nupkgPath)}.");

            var dllPath = Path.Combine(tempDir, Path.GetFileName(entry.FullName));
            entry.ExtractToFile(dllPath);

            return FromAssemblyFile(dllPath, packageId, version);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    /// <summary>Lit la surface d'API d'un fichier .dll.</summary>
    /// <exception cref="InvalidOperationException">
    /// Si une dépendance de l'assembly est introuvable : le contrat serait incomplet,
    /// et un contrat incomplet masquerait une rupture. Mieux vaut échouer explicitement.
    /// </exception>
    public static ApiSurface FromAssemblyFile(string dllPath, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dllPath);

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var assemblyDir = Path.GetDirectoryName(Path.GetFullPath(dllPath))!;
        var resolver = new ForgeAssemblyResolver([runtimeDir, assemblyDir]);

        using var context = new MetadataLoadContext(resolver);
        var assembly = context.LoadFromAssemblyPath(dllPath);

        var members = new List<string>();
        try
        {
            foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                CollectType(type, members);
            }
        }
        catch (FileNotFoundException exception)
        {
            var missing = resolver.Unresolved.Count > 0
                ? string.Join(", ", resolver.Unresolved)
                : "dépendance inconnue";

            throw new InvalidOperationException(
                $"Contrat public de {packageId} {version} non extractible : assembly(s) introuvable(s) — {missing}. " +
                "Restaurer les dépendances (dotnet restore) puis relancer « forge index ».",
                exception);
        }

        members.Sort(StringComparer.Ordinal);
        return new ApiSurface(packageId, version, members);
    }

    private static void CollectType(Type type, List<string> members)
    {
        members.Add($"type {Render(type)}");

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags).Where(m => IsVisible(m) && !m.IsSpecialName))
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => Render(p.ParameterType)));
            var arity = method.IsGenericMethodDefinition ? $"`{method.GetGenericArguments().Length}" : string.Empty;
            members.Add($"method {Render(type)}.{method.Name}{arity}({parameters}) : {Render(method.ReturnType)}");
        }

        foreach (var constructor in type.GetConstructors(flags).Where(IsVisible))
        {
            var parameters = string.Join(", ", constructor.GetParameters().Select(p => Render(p.ParameterType)));
            members.Add($"ctor {Render(type)}({parameters})");
        }

        foreach (var property in type.GetProperties(flags))
        {
            var getter = property.GetMethod is { } g && IsVisible(g) ? "get;" : string.Empty;
            var setter = property.SetMethod is { } s && IsVisible(s) ? "set;" : string.Empty;
            if (getter.Length == 0 && setter.Length == 0)
            {
                continue;
            }

            members.Add($"property {Render(type)}.{property.Name} : {Render(property.PropertyType)} {{ {getter}{setter} }}");
        }

        foreach (var field in type.GetFields(flags).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
        {
            members.Add($"field {Render(type)}.{field.Name} : {Render(field.FieldType)}");
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public).OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            CollectType(nested, members);
        }
    }

    private static bool IsVisible(MethodBase method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static string Render(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsByRef)
        {
            return $"ref {Render(type.GetElementType()!)}";
        }

        if (type.IsArray)
        {
            return $"{Render(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick > 0)
            {
                name = name[..tick];
            }

            var arguments = string.Join(", ", type.GetGenericArguments().Select(Render));
            return $"{name}<{arguments}>";
        }

        return type.FullName ?? type.Name;
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Nettoyage best-effort : un fichier temporaire résiduel n'invalide pas l'extraction.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }
}
