namespace SnippetForge.Consumers;

/// <summary>Classement d'une référence face aux versions disponibles dans le feed.</summary>
public enum UpdateClassification
{
    /// <summary>Le package référencé n'existe pas (ou plus) dans le feed.</summary>
    Unavailable = 0,

    /// <summary>Rien de plus récent à installer, et la version courante n'est pas dépréciée.</summary>
    UpToDate = 1,

    /// <summary>Une montée de version préserve le contrat : elle ne peut pas casser la compilation.</summary>
    Safe = 2,

    /// <summary>Une intervention humaine est nécessaire : rupture de contrat ou version dépréciée.</summary>
    Review = 3,
}

/// <summary>Décision de mise à jour pour une référence donnée.</summary>
public sealed record UpdatePlanItem(
    string PackageId,
    string CurrentVersion,
    string? SafeTarget,
    string? LatestTarget,
    UpdateClassification Classification,
    string Rationale,
    bool CurrentIsDeprecated);

/// <summary>
/// Décide, pour chaque référence épinglée, si une montée de version est sûre.
///
/// « Sûre » a ici un sens précis et vérifiable : même numéro majeur et aucun membre
/// public retiré entre la version courante et la cible. Une telle mise à jour ne peut
/// pas casser la compilation du consommateur ; le comportement, lui, reste à valider
/// par la suite de tests du consommateur (voir « forge update --test »).
/// </summary>
public static class UpdatePlanner
{
    /// <summary>
    /// Construit la décision pour une référence.
    /// </summary>
    /// <param name="reference">Référence épinglée dans le projet consommateur.</param>
    /// <param name="availableVersions">Versions présentes dans le feed pour ce package.</param>
    /// <param name="isDeprecated">Prédicat de dépréciation d'une version.</param>
    /// <param name="isBreaking">Prédicat de rupture de contrat entre deux versions (origine, cible).</param>
    public static UpdatePlanItem Plan(
        PinnedReference reference,
        IReadOnlyList<string> availableVersions,
        Func<string, bool> isDeprecated,
        Func<string, string, bool> isBreaking)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(availableVersions);
        ArgumentNullException.ThrowIfNull(isDeprecated);
        ArgumentNullException.ThrowIfNull(isBreaking);

        var currentIsDeprecated = isDeprecated(reference.Version);

        if (availableVersions.Count == 0)
        {
            return new UpdatePlanItem(
                reference.PackageId, reference.Version, null, null,
                UpdateClassification.Unavailable,
                "Package absent du feed : source NuGet non configurée, ou package jamais publié.",
                currentIsDeprecated);
        }

        var newer = availableVersions
            .Where(v => SemVerLite.Compare(v, reference.Version) > 0)
            .OrderByDescending(v => v, Comparer<string>.Create(SemVerLite.Compare))
            .ToList();

        var latestTarget = newer.FirstOrDefault();

        var safeTarget = newer.FirstOrDefault(v =>
            !isDeprecated(v) &&
            SemVerLite.SameMajor(reference.Version, v) &&
            !isBreaking(reference.Version, v));

        if (currentIsDeprecated)
        {
            return new UpdatePlanItem(
                reference.PackageId, reference.Version, safeTarget, latestTarget,
                safeTarget is not null ? UpdateClassification.Safe : UpdateClassification.Review,
                safeTarget is not null
                    ? $"Version courante dépréciée ; {safeTarget} conserve le contrat."
                    : "Version courante dépréciée et aucune montée sans rupture : migration manuelle requise.",
                CurrentIsDeprecated: true);
        }

        if (safeTarget is not null)
        {
            return new UpdatePlanItem(
                reference.PackageId, reference.Version, safeTarget, latestTarget,
                UpdateClassification.Safe,
                latestTarget is not null && latestTarget != safeTarget
                    ? $"{safeTarget} préserve le contrat ; {latestTarget} existe mais rompt le contrat."
                    : $"{safeTarget} préserve le contrat public.",
                CurrentIsDeprecated: false);
        }

        if (latestTarget is not null)
        {
            return new UpdatePlanItem(
                reference.PackageId, reference.Version, null, latestTarget,
                UpdateClassification.Review,
                $"{latestTarget} rompt le contrat public ou est dépréciée : relecture nécessaire.",
                CurrentIsDeprecated: false);
        }

        return new UpdatePlanItem(
            reference.PackageId, reference.Version, null, null,
            UpdateClassification.UpToDate,
            "Version la plus récente du feed.",
            CurrentIsDeprecated: false);
    }
}
