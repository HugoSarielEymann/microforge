using System.Xml.Linq;
using SnippetForge.Copy;

namespace SnippetForge.Commands;

/// <summary>Distribution par copie de source, pour les projets qui ne peuvent pas référencer.</summary>
public static class CopyCommands
{
    /// <summary>
    /// Copie les sources d'un micropackage dans un projet, avec provenance.
    ///
    /// À réserver aux cas où la référence NuGet n'est pas possible : elle reste
    /// supérieure sur tous les plans (binaire déjà testé, contrat vérifié, correction
    /// propagée par une commande). La copie est un repli, et la commande le dit.
    /// </summary>
    public static int Copy(ForgeRoot root, string[] args)
    {
        var packageId = Cli.RequireArg(args, 1, "PackageId");
        var project = Path.GetFullPath(Cli.Option(args, "--project") ?? Directory.GetCurrentDirectory());
        var force = Cli.Flag(args, "--force");

        if (!Directory.Exists(project))
        {
            return Cli.Fail($"Projet introuvable : {project}");
        }

        var package = PackageSource.Resolve(root, packageId);
        var sources = SourceFiles(package).ToList();
        if (sources.Count == 0)
        {
            return Cli.Fail($"Aucun fichier source dans {package.Directory}/src.");
        }

        var into = Cli.Option(args, "--into") ?? Path.Combine("MicroForge", package.Id);
        var targetDir = Path.GetFullPath(Path.Combine(project, into));

        if (!Metrics.ConsumerRegistry.IsInside(project, targetDir))
        {
            return Cli.Fail($"La destination « {into} » sort du projet. Copie refusée.");
        }

        var manifest = CopyManifest.Load(project);

        // Une copie antérieure retouchée ne doit jamais être écrasée en silence :
        // c'est le seul moment où l'on peut encore arbitrer.
        if (manifest.Find(package.Id) is { } previous && !force)
        {
            var modified = CopyManifest.Inspect(project, previous)
                .Where(f => f.State == CopiedFileState.Modified)
                .Select(f => f.RelativePath)
                .ToList();

            if (modified.Count > 0)
            {
                Console.WriteLine($"{package.Id} {previous.Version} a été modifié localement :");
                foreach (var file in modified)
                {
                    Console.WriteLine($"  {file}");
                }

                return Cli.Fail(
                    "Copie refusée : la recopie écraserait ces modifications. " +
                    "Les reporter dans le micropackage (puis republier), ou --force pour les perdre.");
            }
        }

        Directory.CreateDirectory(targetDir);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var srcRoot = Path.Combine(package.Directory, "src");

        foreach (var source in sources)
        {
            var relativeInPackage = Path.GetRelativePath(srcRoot, source);
            var destination = Path.Combine(targetDir, relativeInPackage);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);

            hashes[Path.GetRelativePath(project, destination)] = CopyManifest.Hash(destination);
        }

        manifest.Record(new CopiedPackage(
            package.Id, package.Version, Path.GetRelativePath(project, targetDir), DateTime.UtcNow, hashes));
        manifest.Save();

        Console.WriteLine($"{package.Id} {package.Version} copié — {hashes.Count} fichier(s) dans {into}");
        Console.WriteLine($"  provenance : {Path.GetRelativePath(project, CopyManifest.PathFor(project))}");

        var dependencies = PackageDependencies(package).ToList();
        if (dependencies.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Ce package dépend de bibliothèques que la copie n'apporte pas :");
            foreach (var (id, version) in dependencies)
            {
                Console.WriteLine($"  dotnet add package {id} --version {version}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("La copie ne reçoit aucune correction automatiquement : « forge copied » dira");
        Console.WriteLine("quand une version plus récente existe. La référence NuGet reste préférable");
        Console.WriteLine("partout où elle est possible.");
        return 0;
    }

    /// <summary>État des packages copiés dans un projet : versions, retouches locales.</summary>
    public static int Copied(ForgeRoot root, string[] args)
    {
        var project = Path.GetFullPath(Cli.Arg(args, 1) ?? Directory.GetCurrentDirectory());
        var manifest = CopyManifest.Load(project);

        if (manifest.Entries.Count == 0)
        {
            Console.WriteLine($"Aucun micropackage copié dans {project}.");
            return 0;
        }

        var index = FeedIndexer.Load(root);
        var outdated = 0;
        var drifted = 0;

        Console.WriteLine($"=== {project} ===\n");

        foreach (var copied in manifest.Entries.OrderBy(e => e.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            var latest = index.Packages
                .FirstOrDefault(p => p.Id.Equals(copied.PackageId, StringComparison.OrdinalIgnoreCase))?.LatestVersion;

            var newer = latest is not null && SemVerLite.Compare(latest, copied.Version) > 0;
            var files = CopyManifest.Inspect(project, copied);
            var modified = files.Where(f => f.State == CopiedFileState.Modified).ToList();
            var absent = files.Where(f => f.State == CopiedFileState.Absent).ToList();

            var suffix = newer ? $" → {latest} disponible" : string.Empty;
            Console.WriteLine($"  {copied.PackageId} {copied.Version}{suffix}   ({copied.Directory})");

            foreach (var (path, _) in modified)
            {
                Console.WriteLine($"    MODIFIÉ  {path}");
            }

            foreach (var (path, _) in absent)
            {
                Console.WriteLine($"    ABSENT   {path}");
            }

            if (newer)
            {
                outdated++;
            }

            if (modified.Count > 0)
            {
                drifted++;
            }
        }

        Console.WriteLine();
        if (outdated > 0)
        {
            Console.WriteLine($"{outdated} package(s) en retard : « forge copy <Id> » pour recopier.");
        }

        if (drifted > 0)
        {
            Console.WriteLine($"{drifted} package(s) retouché(s) localement. Une divergence non reportée dans");
            Console.WriteLine("le micropackage se perdra à la prochaine copie, et fait diverger le corpus.");
        }

        if (outdated == 0 && drifted == 0)
        {
            Console.WriteLine("Tout est à jour et conforme à la source.");
        }

        return 0;
    }

    private static IEnumerable<string> SourceFiles(PackageSource package) =>
        Directory
            .EnumerateFiles(Path.Combine(package.Directory, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>
    /// Dépendances NuGet du package : la copie apporte le code, pas ce dont il dépend.
    /// Les taire produirait un projet qui ne compile pas.
    /// </summary>
    private static IEnumerable<(string Id, string Version)> PackageDependencies(PackageSource package) =>
        XDocument.Load(package.ProjectFile)
            .Descendants("PackageReference")
            .Select(e => (Id: e.Attribute("Include")?.Value, Version: e.Attribute("Version")?.Value))
            .Where(d => d.Id is not null && d.Version is not null)
            .Select(d => (d.Id!, d.Version!));
}
