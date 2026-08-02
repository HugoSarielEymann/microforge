using SnippetForge.Consumers;
using SnippetForge.Metrics;
using SnippetForge.Telemetry;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Le rapport de projet et le bilan global mesurent deux choses différentes : ce que
/// la bibliothèque a apporté ICI, contre sa rentabilité d'ensemble. Ces tests fixent
/// la distinction.
/// </summary>
public sealed class ProjectReportTests
{
    private static readonly string Project = OperatingSystem.IsWindows()
        ? @"C:\projets\MonApp"
        : "/projets/MonApp";

    private static Dictionary<string, PackageFootprint> Footprints() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Micro.Flow.Retry"] = new("Micro.Flow.Retry", SourceLines: 90, TestLines: 60, DocumentationLines: 30),
        ["Micro.Text.Slugify"] = new("Micro.Text.Slugify", SourceLines: 85, TestLines: 55, DocumentationLines: 25),
        ["Micro.Time.Parse"] = new("Micro.Time.Parse", SourceLines: 50, TestLines: 40, DocumentationLines: 20),
    };

    private static UsageEntry Entry(string command, string arguments, string? directory, int exitCode = 0) =>
        new(DateTime.UtcNow, command, arguments, exitCode, directory);

    [Fact]
    public void ProjetSansReference_NeCompteAucuneEconomie()
    {
        var report = ProjectReportBuilder.Build(Project, [], Footprints(), []);

        Assert.Equal(0, report.LinesNotWritten);
        Assert.Empty(report.Reused);
        Assert.False(report.QueriedLibrary);
    }

    [Fact]
    public void ChaquePackageReutilise_CompteDesLePremierUsage()
    {
        // C'est la différence avec forge stats : vu du projet, un package réutilisé est
        // du code non produit, même si la bibliothèque a dû l'écrire une fois.
        var report = ProjectReportBuilder.Build(
            Project,
            [new PinnedReference("Micro.Flow.Retry", "1.0.2")],
            Footprints(),
            []);

        Assert.Equal(90, report.LinesNotWritten);
        Assert.Equal((int)(90 * SavingsCalculator.TokensPerLine), report.TokensNotWritten);
    }

    [Fact]
    public void PlusieursPackages_SAdditionnent()
    {
        var report = ProjectReportBuilder.Build(
            Project,
            [new PinnedReference("Micro.Flow.Retry", "1.0.2"), new PinnedReference("Micro.Text.Slugify", "1.1.1")],
            Footprints(),
            []);

        Assert.Equal(175, report.LinesNotWritten);
        Assert.Equal("Micro.Flow.Retry", report.Reused[0].PackageId); // le plus volumineux d'abord
    }

    [Fact]
    public void PackageInconnuDeLaBibliotheque_CompteZeroPlutotQueDInventer()
    {
        var report = ProjectReportBuilder.Build(
            Project, [new PinnedReference("Micro.Absent.Zero", "1.0.0")], Footprints(), []);

        Assert.Equal(0, Assert.Single(report.Reused).SourceLines);
    }

    [Fact]
    public void PublicationReussie_EstImputeeAuProjet()
    {
        var report = ProjectReportBuilder.Build(
            Project, [], Footprints(),
            [Entry("publish", "Micro.Time.Parse", Project)]);

        Assert.Equal(["Micro.Time.Parse"], report.ForgedHere);
        Assert.Equal(110, report.ForgedLines); // src + tests + doc : l'effort complet
        Assert.Equal(1, report.Publications);
    }

    [Fact]
    public void PublicationEchouee_NEstPasImputee()
    {
        var report = ProjectReportBuilder.Build(
            Project, [], Footprints(),
            [Entry("publish", "Micro.Time.Parse", Project, exitCode: 1)]);

        Assert.Empty(report.ForgedHere);
    }

    [Fact]
    public void ActiviteEstComptee()
    {
        var report = ProjectReportBuilder.Build(
            Project, [], Footprints(),
            [
                Entry("search", "retry http", Project),
                Entry("search", "slug", Project),
                Entry("info", "Micro.Flow.Retry", Project),
                Entry("list", string.Empty, Project),
            ]);

        Assert.Equal(2, report.Searches);
        Assert.Equal(1, report.Consultations);
        Assert.True(report.QueriedLibrary);
    }

    [Fact]
    public void InvocationsFrom_RetientLeProjetEtSesSousDossiers()
    {
        var sousDossier = Path.Combine(Project, "src");
        var ailleurs = OperatingSystem.IsWindows() ? @"C:\projets\Autre" : "/projets/Autre";

        var retenues = ProjectReportBuilder.InvocationsFrom(
            [
                Entry("search", "a", Project),
                Entry("search", "b", sousDossier),
                Entry("search", "c", ailleurs),
            ],
            Project);

        Assert.Equal(2, retenues.Count);
    }

    [Fact]
    public void InvocationsFrom_IgnoreLesEntreesSansDossier()
    {
        // Journal antérieur au suivi du dossier courant : mieux vaut un rapport
        // incomplet qu'une attribution inventée.
        var retenues = ProjectReportBuilder.InvocationsFrom([Entry("search", "a", null)], Project);

        Assert.Empty(retenues);
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ProjectReportBuilder.Build(Project, null!, Footprints(), []));
        Assert.Throws<ArgumentNullException>(() => ProjectReportBuilder.Build(Project, [], null!, []));
        Assert.Throws<ArgumentNullException>(() => ProjectReportBuilder.Build(Project, [], Footprints(), null!));
        Assert.Throws<ArgumentNullException>(() => ProjectReportBuilder.InvocationsFrom(null!, Project));
    }
}
