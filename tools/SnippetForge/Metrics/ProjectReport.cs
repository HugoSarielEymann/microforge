using SnippetForge.Telemetry;

namespace SnippetForge.Metrics;

/// <summary>Un micropackage consommé par le projet, et ce qu'il représente.</summary>
public sealed record ReusedPackage(string PackageId, string Version, int SourceLines);

/// <summary>Ce que MicroForge a apporté à un projet donné.</summary>
public sealed record ProjectSavings(
    string ProjectPath,
    IReadOnlyList<ReusedPackage> Reused,
    IReadOnlyList<string> ForgedHere,
    int ForgedLines,
    int Searches,
    int Consultations,
    int Publications)
{
    /// <summary>Lignes de code générique que le projet n'a pas eu à produire.</summary>
    public int LinesNotWritten => Reused.Sum(r => r.SourceLines);

    /// <summary>Estimation basse des tokens correspondants.</summary>
    public int TokensNotWritten => (int)(LinesNotWritten * SavingsCalculator.TokensPerLine);

    /// <summary>Estimation basse des tokens investis dans ce qui a été forgé ici.</summary>
    public int TokensForged => (int)(ForgedLines * SavingsCalculator.TokensPerLine);

    /// <summary>Vrai si l'agent a interrogé la bibliothèque depuis ce projet.</summary>
    public bool QueriedLibrary => Searches + Consultations > 0;
}

/// <summary>
/// Mesure ce que la bibliothèque a apporté à **un projet**, par opposition à
/// <see cref="SavingsCalculator"/> qui juge la rentabilité de la bibliothèque entière.
///
/// Les deux chiffres ne se confondent pas. Vu d'un projet, tout package réutilisé est
/// du code qu'il n'a pas fallu produire — y compris le premier projet à s'en servir.
/// Vu de la bibliothèque, ce premier usage ne fait rien économiser puisqu'il a fallu
/// écrire le package. Le rapport de projet répond à « qu'est-ce que ça m'a apporté
/// ici », le bilan global à « la bibliothèque est-elle rentable ».
/// </summary>
public static class ProjectReportBuilder
{
    /// <summary>Construit le rapport d'un projet.</summary>
    public static ProjectSavings Build(
        string projectPath,
        IReadOnlyList<Consumers.PinnedReference> references,
        IReadOnlyDictionary<string, PackageFootprint> footprints,
        IReadOnlyList<UsageEntry> invocations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(footprints);
        ArgumentNullException.ThrowIfNull(invocations);

        var reused = references
            .Select(r => new ReusedPackage(
                r.PackageId,
                r.Version,
                footprints.TryGetValue(r.PackageId, out var footprint) ? footprint.SourceLines : 0))
            .OrderByDescending(r => r.SourceLines)
            .ThenBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Une publication depuis ce projet signifie qu'un besoin générique y est né.
        // Le package appartient dès lors à toute la bibliothèque, mais l'effort est
        // imputable à ce projet — c'est ce que « investi ici » traduit.
        var forged = invocations
            .Where(e => e.Command == "publish" && e.ExitCode == 0)
            .Select(e => e.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var forgedLines = forged.Sum(id => footprints.TryGetValue(id, out var f) ? f.TotalLines : 0);

        return new ProjectSavings(
            projectPath,
            reused,
            forged,
            forgedLines,
            invocations.Count(e => e.Command == "search"),
            invocations.Count(e => e.Command is "info" or "diff" or "duplicates"),
            forged.Count);
    }

    /// <summary>
    /// Retient les invocations lancées depuis le projet ou l'un de ses sous-dossiers.
    /// Les entrées antérieures à la journalisation du dossier courant sont ignorées :
    /// mieux vaut un rapport incomplet qu'une attribution inventée.
    /// </summary>
    public static IReadOnlyList<UsageEntry> InvocationsFrom(IReadOnlyList<UsageEntry> all, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        return all
            .Where(e => e.WorkingDirectory is not null &&
                        ConsumerRegistry.IsInside(projectPath, e.WorkingDirectory))
            .ToList();
    }
}
