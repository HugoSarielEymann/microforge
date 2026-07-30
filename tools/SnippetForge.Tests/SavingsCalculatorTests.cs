using SnippetForge.Metrics;
using Xunit;

namespace SnippetForge.Tests;

public sealed class SavingsCalculatorTests
{
    private static PackageFootprint Footprint(string id, int source = 100, int tests = 60, int docs = 20) =>
        new(id, source, tests, docs);

    private static ReuseRecord Reuse(string id, params string[] consumers) => new(id, consumers);

    [Fact]
    public void UnSeulConsommateur_NEconomiseRien()
    {
        // Le point le plus important de la méthode : forger un package pour un seul
        // projet est un coût net, pas un gain. Annoncer l'inverse serait malhonnête.
        var report = SavingsCalculator.Estimate([Footprint("Micro.A.One")], [Reuse("Micro.A.One", "p1")]);

        Assert.Equal(0, report.LinesAvoided);
        Assert.Equal(0, report.RegenerationsAvoided);
        Assert.False(report.IsProfitable);
    }

    [Fact]
    public void AucunConsommateur_NEconomiseRien()
    {
        var report = SavingsCalculator.Estimate([Footprint("Micro.A.One")], []);

        Assert.Equal(0, report.LinesAvoided);
        Assert.Equal(0, Assert.Single(report.Packages).Consumers);
    }

    [Fact]
    public void TroisConsommateurs_EconomisentDeuxRegenerations()
    {
        var report = SavingsCalculator.Estimate(
            [Footprint("Micro.A.One", source: 100)],
            [Reuse("Micro.A.One", "p1", "p2", "p3")]);

        Assert.Equal(2, report.RegenerationsAvoided);
        Assert.Equal(200, report.LinesAvoided);
    }

    [Fact]
    public void SeulesLesLignesDeSrcComptentCommeEconomie()
    {
        // Tests et README relèvent de l'investissement : une IA générant du code
        // à la volée ne les aurait pas écrits.
        var report = SavingsCalculator.Estimate(
            [Footprint("Micro.A.One", source: 100, tests: 500, docs: 300)],
            [Reuse("Micro.A.One", "p1", "p2")]);

        Assert.Equal(100, report.LinesAvoided);
        Assert.Equal(900, report.InvestmentLines);
    }

    [Fact]
    public void MemeProjetReference_DeuxFois_NeCompteQuUneFois()
    {
        var report = SavingsCalculator.Estimate(
            [Footprint("Micro.A.One", source: 100)],
            [Reuse("Micro.A.One", "p1", "P1", "p2")]);

        Assert.Equal(2, Assert.Single(report.Packages).Consumers);
        Assert.Equal(100, report.LinesAvoided);
    }

    [Fact]
    public void Rentabilite_BasculeQuandLEconomieDepasseLInvestissement()
    {
        var footprint = Footprint("Micro.A.One", source: 100, tests: 50, docs: 0);

        var deficit = SavingsCalculator.Estimate([footprint], [Reuse("Micro.A.One", "p1", "p2")]);
        Assert.False(deficit.IsProfitable);
        Assert.Equal(50, deficit.LinesToBreakEven);

        var profit = SavingsCalculator.Estimate([footprint], [Reuse("Micro.A.One", "p1", "p2", "p3")]);
        Assert.True(profit.IsProfitable);
        Assert.Equal(0, profit.LinesToBreakEven);
    }

    [Fact]
    public void Tokens_SontDerivesDesLignesParUnRatioAssume()
    {
        var report = SavingsCalculator.Estimate(
            [Footprint("Micro.A.One", source: 100, tests: 0, docs: 0)],
            [Reuse("Micro.A.One", "p1", "p2")]);

        Assert.Equal((int)(100 * SavingsCalculator.TokensPerLine), report.TokensAvoided);
        Assert.Equal((int)(100 * SavingsCalculator.TokensPerLine), report.TokensInvested);
    }

    [Fact]
    public void Classement_MetLesPlusRentablesEnTete()
    {
        var report = SavingsCalculator.Estimate(
            [Footprint("Micro.A.Petit", source: 10), Footprint("Micro.B.Gros", source: 300)],
            [Reuse("Micro.A.Petit", "p1", "p2"), Reuse("Micro.B.Gros", "p1", "p2")]);

        Assert.Equal("Micro.B.Gros", report.Packages[0].PackageId);
    }

    [Fact]
    public void PackageReferenceMaisNonMesure_EstIgnore()
    {
        // Une réutilisation sans empreinte connue (package retiré des sources) ne doit
        // pas gonfler artificiellement le bilan.
        var report = SavingsCalculator.Estimate([], [Reuse("Micro.Fantome.Zero", "p1", "p2")]);

        Assert.Empty(report.Packages);
        Assert.Equal(0, report.LinesAvoided);
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => SavingsCalculator.Estimate(null!, []));
        Assert.Throws<ArgumentNullException>(() => SavingsCalculator.Estimate([], null!));
    }

    [Fact]
    public void Empreinte_AdditionneLesTroisVolumes() =>
        Assert.Equal(180, Footprint("Micro.A.One", 100, 60, 20).TotalLines);
}
