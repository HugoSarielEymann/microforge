using SnippetForge.Api;
using SnippetForge.Duplicates;
using SnippetForge.Embeddings;
using SnippetForge.Lifecycle;

namespace SnippetForge.Commands;

/// <summary>Commandes de consultation : recherche, fiche, inventaire, index, diff, doublons.</summary>
public static class QueryCommands
{
    /// <summary>Recherche hybride lexicale + sémantique.</summary>
    public static async Task<int> SearchAsync(ForgeRoot root, string[] args)
    {
        var query = string.Join(' ', args.Skip(1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)));
        var tags = Cli.SplitList(Cli.Option(args, "--tags"));
        var index = FeedIndexer.Load(root);
        var deprecations = DeprecationRegistry.Load(root);

        SemanticIndex? semantic = null;
        Func<IndexEntry, double>? semanticScore = null;

        if (!Cli.Flag(args, "--lexical") && index.Packages.Count > 0)
        {
            semantic = await SemanticIndex.BuildAsync(root, index, Cli.Flag(args, "--offline")).ConfigureAwait(false);
            Console.WriteLine($"Recherche : {semantic.Explanation}");
            if (!string.IsNullOrWhiteSpace(query))
            {
                var queryVector = await semantic.EmbedAsync(query).ConfigureAwait(false);
                semanticScore = entry => semantic.SimilarityTo(entry.Id, queryVector);
            }
        }

        var hits = SearchEngine.Search(index, query, tags, semanticScore);
        semantic?.Dispose();

        if (hits.Count == 0)
        {
            Console.WriteLine("Aucun micropackage ne correspond : candidat à la création (forge new).");
            return 0;
        }

        Console.WriteLine($"\n{hits.Count} résultat(s) — versions publiées immuables :\n");
        foreach (var hit in hits.Take(10))
        {
            var entry = hit.Entry;
            var deprecated = deprecations.For(entry.Id, entry.LatestVersion);
            var badge = deprecated.Count > 0 ? "  [DÉPRÉCIÉ]" : string.Empty;

            var scoreDetail = hit.SemanticScore is { } sem
                ? $"score {hit.Score:0.00} (lexical {hit.LexicalScore:0.#}, sémantique {sem:0.00})"
                : $"score {hit.Score:0.00} (lexical {hit.LexicalScore:0.#})";

            Console.WriteLine($"  {entry.Id} {entry.LatestVersion}{badge}  [{scoreDetail}]");
            Console.WriteLine($"    tags : {string.Join(", ", entry.Tags)}");
            Console.WriteLine($"    {Cli.Truncate(entry.Description, 110)}");
            foreach (var entryDeprecation in deprecated)
            {
                Console.WriteLine($"    DÉPRÉCIÉ : {entryDeprecation.Reason}" +
                                  (entryDeprecation.Replacement is { } r ? $" → utiliser {r}" : string.Empty));
            }

            Console.WriteLine($"    mode d'emploi : forge info {entry.Id}");
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>Fiche complète d'un package : contrat, versions, dépréciations, mode d'emploi.</summary>
    public static int Info(ForgeRoot root, string[] args)
    {
        var id = Cli.RequireArg(args, 1, "PackageId");
        var index = FeedIndexer.Load(root);
        var entry = index.Packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Cli.Fail($"Package inconnu dans le feed : {id}");
        }

        var deprecations = DeprecationRegistry.Load(root);
        var surfaces = new ApiSurfaceStore(root);

        Console.WriteLine($"{entry.Id} {entry.LatestVersion}");
        Console.WriteLine($"tags : {string.Join(", ", entry.Tags)}");
        Console.WriteLine();
        Console.WriteLine("versions publiées :");
        foreach (var version in entry.AllVersions)
        {
            var digest = surfaces.Load(entry.Id, version)?.Digest ?? "(contrat non extrait)";
            var applicable = deprecations.For(entry.Id, version);
            var suffix = applicable.Count > 0 ? "  DÉPRÉCIÉ" : string.Empty;
            Console.WriteLine($"  {version,-10} contrat {digest}{suffix}");
            foreach (var deprecation in applicable)
            {
                Console.WriteLine($"             ↳ {deprecation.Reason}" +
                                  (deprecation.Replacement is { } r ? $" → {r}" : string.Empty));
            }
        }

        Console.WriteLine();
        Console.WriteLine($"installation : dotnet add package {entry.Id} --version {entry.LatestVersion}");
        Console.WriteLine();
        Console.WriteLine(entry.Readme);
        return 0;
    }

    /// <summary>Inventaire du feed.</summary>
    public static int List(ForgeRoot root)
    {
        var index = FeedIndexer.Load(root);
        var deprecations = DeprecationRegistry.Load(root);

        Console.WriteLine($"{index.Packages.Count} micropackage(s) dans le feed :");
        foreach (var entry in index.Packages)
        {
            var badge = deprecations.For(entry.Id, entry.LatestVersion).Count > 0 ? " [DÉPRÉCIÉ]" : string.Empty;
            Console.WriteLine($"  {entry.Id} {entry.LatestVersion}{badge} — {Cli.Truncate(entry.Description, 80)}");
        }

        return 0;
    }

    /// <summary>Régénère index, surfaces d'API et vecteurs depuis le feed.</summary>
    public static async Task<int> IndexAsync(ForgeRoot root, string[] args)
    {
        var document = FeedIndexer.Rebuild(root);
        Console.WriteLine($"Index régénéré : {document.Packages.Count} package(s).");

        var surfaces = FeedIndexer.RebuildApiSurfaces(root, document);
        Console.WriteLine($"Contrats publics : {surfaces.Extracted} nouvellement extrait(s).");
        foreach (var warning in surfaces.Warnings)
        {
            Console.WriteLine($"  AVERTISSEMENT : {warning}");
        }

        var semantic = await SemanticIndex.BuildAsync(root, document, Cli.Flag(args, "--offline")).ConfigureAwait(false);
        Console.WriteLine($"Vecteurs : {semantic.Vectors.Count} — {semantic.Explanation}");
        semantic.Dispose();
        return 0;
    }

    /// <summary>Différence de contrat public entre deux versions d'un package.</summary>
    public static int Diff(ForgeRoot root, string[] args)
    {
        var id = Cli.RequireArg(args, 1, "PackageId");
        var fromVersion = Cli.RequireArg(args, 2, "version d'origine");
        var toVersion = Cli.RequireArg(args, 3, "version cible");

        var store = new ApiSurfaceStore(root);
        var from = store.Load(id, fromVersion);
        var to = store.Load(id, toVersion);

        if (from is null || to is null)
        {
            return Cli.Fail($"Contrat non extrait pour {id} {(from is null ? fromVersion : toVersion)} — lancer « forge index ».");
        }

        var diff = ApiDiff.Between(from, to);
        Console.WriteLine($"{id} : {fromVersion} ({from.Digest}) → {toVersion} ({to.Digest})");
        Console.WriteLine();

        if (diff.IsIdentical)
        {
            Console.WriteLine("Contrat public identique : montée de version sans risque de compilation.");
            return 0;
        }

        foreach (var removed in diff.Removed)
        {
            Console.WriteLine($"  - {removed}");
        }

        foreach (var added in diff.Added)
        {
            Console.WriteLine($"  + {added}");
        }

        Console.WriteLine();
        Console.WriteLine(diff.IsBreaking
            ? $"RUPTURE : {diff.Removed.Count} membre(s) retiré(s) — incrément majeur exigé."
            : $"Ajout rétrocompatible : {diff.Added.Count} membre(s) — incrément mineur exigé.");
        return 0;
    }

    /// <summary>Recherche les quasi-duplications d'un package déjà publié dans la bibliothèque.</summary>
    public static async Task<int> DuplicatesAsync(ForgeRoot root, string[] args)
    {
        var index = FeedIndexer.Load(root);
        if (index.Packages.Count < 2)
        {
            Console.WriteLine("Moins de deux packages publiés : aucune duplication possible.");
            return 0;
        }

        var semantic = await SemanticIndex.BuildAsync(root, index, Cli.Flag(args, "--offline")).ConfigureAwait(false);
        Console.WriteLine($"Analyse de duplication : {semantic.Explanation}");
        if (!semantic.IsSemantic)
        {
            Console.WriteLine("Note : sans modèle sémantique, la synonymie n'est pas détectée (proximité lexicale seule).");
        }

        Console.WriteLine();

        // --all affiche toutes les paires avec leur score : indispensable pour calibrer
        // les seuils sur une bibliothèque réelle.
        var showAll = Cli.Flag(args, "--all");
        var thresholds = showAll ? semantic.Thresholds with { Warning = 0.0 } : semantic.Thresholds;

        Console.WriteLine($"Seuils calibrés pour cet espace vectoriel : alerte {semantic.Thresholds.Warning:0.00}, blocage {semantic.Thresholds.Blocking:0.00}.\n");

        var reported = new HashSet<string>(StringComparer.Ordinal);
        var found = 0;

        foreach (var entry in index.Packages)
        {
            var candidates = DuplicateDetector.Detect(
                entry.Id, semantic.Vectors[entry.Id], semantic.Vectors, semantic.Versions, thresholds);

            foreach (var candidate in candidates)
            {
                var pairKey = string.CompareOrdinal(entry.Id, candidate.PackageId) < 0
                    ? $"{entry.Id}|{candidate.PackageId}"
                    : $"{candidate.PackageId}|{entry.Id}";

                if (!reported.Add(pairKey))
                {
                    continue;
                }

                // Le seuil abaissé par --all sert à lister ; l'étiquette reste jugée
                // sur la calibration réelle de l'espace vectoriel.
                var real = semantic.Thresholds;
                var (label, counts) = candidate.Similarity >= real.Blocking ? ("QUASI-DOUBLON", true)
                    : candidate.Similarity >= real.Warning ? ("proximité    ", true)
                    : ("sans rapport ", false);

                if (counts)
                {
                    found++;
                }

                Console.WriteLine($"  [{label}] {entry.Id} ~ {candidate.PackageId} : {candidate.Similarity:0.000}");
            }
        }

        semantic.Dispose();

        if (found == 0)
        {
            Console.WriteLine($"Aucune paire au-dessus du seuil d'alerte ({semantic.Thresholds.Warning:0.00}).");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"{found} paire(s) à examiner. Fusionner par dépréciation :");
            Console.WriteLine("  forge deprecate <PackageId> --reason \"…\" --replacement <PackageId conservé>");
        }

        return 0;
    }
}
