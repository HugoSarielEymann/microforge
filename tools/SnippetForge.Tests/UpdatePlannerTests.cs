using SnippetForge.Consumers;
using Xunit;

namespace SnippetForge.Tests;

public sealed class UpdatePlannerTests
{
    private static readonly PinnedReference Reference = new("Micro.A.One", "1.0.0");

    private static UpdatePlanItem Plan(
        IReadOnlyList<string> versions,
        Func<string, bool>? deprecated = null,
        Func<string, string, bool>? breaking = null) =>
        UpdatePlanner.Plan(Reference, versions, deprecated ?? (_ => false), breaking ?? ((_, _) => false));

    [Fact]
    public void PackageAbsentDuFeed_EstIntrouvable()
    {
        var item = Plan([]);

        Assert.Equal(UpdateClassification.Unavailable, item.Classification);
        Assert.Null(item.SafeTarget);
    }

    [Fact]
    public void AucuneVersionPlusRecente_EstAJour()
    {
        var item = Plan(["0.9.0", "1.0.0"]);

        Assert.Equal(UpdateClassification.UpToDate, item.Classification);
        Assert.Null(item.LatestTarget);
    }

    [Fact]
    public void MonteeMineureSansRupture_EstSure()
    {
        var item = Plan(["1.0.0", "1.1.0", "1.2.0"]);

        Assert.Equal(UpdateClassification.Safe, item.Classification);
        Assert.Equal("1.2.0", item.SafeTarget);
    }

    [Fact]
    public void MonteeMajeure_ExigeUneRelecture()
    {
        var item = Plan(["1.0.0", "2.0.0"]);

        Assert.Equal(UpdateClassification.Review, item.Classification);
        Assert.Null(item.SafeTarget);
        Assert.Equal("2.0.0", item.LatestTarget);
    }

    [Fact]
    public void MonteeMineureMaisContratRompu_ExigeUneRelecture()
    {
        // Un incrément mineur incorrect (contrat rompu) ne doit pas être appliqué en aveugle.
        var item = Plan(["1.0.0", "1.1.0"], breaking: (_, _) => true);

        Assert.Equal(UpdateClassification.Review, item.Classification);
        Assert.Null(item.SafeTarget);
    }

    [Fact]
    public void MajeureDisponible_MaisMineureSure_ProposeLaSure()
    {
        var item = Plan(["1.0.0", "1.3.0", "2.0.0"]);

        Assert.Equal(UpdateClassification.Safe, item.Classification);
        Assert.Equal("1.3.0", item.SafeTarget);
        Assert.Equal("2.0.0", item.LatestTarget);
        Assert.Contains("2.0.0", item.Rationale, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionCibleDepreciee_EstEcartee()
    {
        var item = Plan(["1.0.0", "1.1.0"], deprecated: v => v == "1.1.0");

        Assert.Equal(UpdateClassification.Review, item.Classification);
        Assert.Null(item.SafeTarget);
    }

    [Fact]
    public void VersionCouranteDepreciee_AvecEchappatoireSure_EstSignaleeEtSure()
    {
        var item = Plan(["1.0.0", "1.1.0"], deprecated: v => v == "1.0.0");

        Assert.Equal(UpdateClassification.Safe, item.Classification);
        Assert.True(item.CurrentIsDeprecated);
        Assert.Equal("1.1.0", item.SafeTarget);
    }

    [Fact]
    public void VersionCouranteDepreciee_SansEchappatoire_ExigeUneMigration()
    {
        var item = Plan(["1.0.0", "2.0.0"], deprecated: v => v == "1.0.0");

        Assert.Equal(UpdateClassification.Review, item.Classification);
        Assert.True(item.CurrentIsDeprecated);
        Assert.Contains("migration", item.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VersionCouranteDepreciee_SeuleVersion_ExigeUneMigration()
    {
        var item = Plan(["1.0.0"], deprecated: _ => true);

        Assert.Equal(UpdateClassification.Review, item.Classification);
        Assert.True(item.CurrentIsDeprecated);
    }

    [Fact]
    public void ChoisitLaPlusHauteVersionSure_PasLaPremiere()
    {
        var item = Plan(["1.0.0", "1.1.0", "1.9.0", "1.10.0"]);
        Assert.Equal("1.10.0", item.SafeTarget);
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => UpdatePlanner.Plan(null!, [], _ => false, (_, _) => false));
        Assert.Throws<ArgumentNullException>(() => UpdatePlanner.Plan(Reference, null!, _ => false, (_, _) => false));
        Assert.Throws<ArgumentNullException>(() => UpdatePlanner.Plan(Reference, [], null!, (_, _) => false));
        Assert.Throws<ArgumentNullException>(() => UpdatePlanner.Plan(Reference, [], _ => false, null!));
    }
}
