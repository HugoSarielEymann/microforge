using Xunit;

namespace Micro.Graph.ForceLayout.Tests;

public sealed class ForceLayoutTests
{
    private static double Distance(ForceSimulation simulation, int a, int b)
    {
        double dx = simulation.GetX(a) - simulation.GetX(b);
        double dy = simulation.GetY(a) - simulation.GetY(b);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static ForceLink[] Anneau(int n)
        => Enumerable.Range(0, n).Select(i => new ForceLink(i, (i + 1) % n)).ToArray();

    private static void AssertFinite(ForceSimulation simulation)
    {
        for (int node = 0; node < simulation.NodeCount; node++)
        {
            Assert.True(double.IsFinite(simulation.GetX(node)), $"x[{node}] non fini");
            Assert.True(double.IsFinite(simulation.GetY(node)), $"y[{node}] non fini");
        }
    }

    // ==================== Comportement physique ====================

    [Fact]
    public void UnLienSeulTendVersSaLongueurAuRepos()
    {
        ForceLayoutOptions options = new() { ChargeStrength = 0, CenterStrength = 0, LinkDistance = 60 };
        ForceSimulation simulation = new(2, [new ForceLink(0, 1)], options);

        simulation.Run(1000);

        Assert.InRange(Distance(simulation, 0, 1), 55, 65);
    }

    [Fact]
    public void LaRepulsionEcarteDeuxNoeudsSansLien()
    {
        ForceSimulation simulation = new(2, [], new ForceLayoutOptions { CenterStrength = 0 });
        double avant = Distance(simulation, 0, 1);

        simulation.Run(300);

        Assert.True(Distance(simulation, 0, 1) > avant * 2);
    }

    [Fact]
    public void DesNoeudsLiesFinissentPlusProchesQueDesNoeudsNonLies()
    {
        ForceSimulation simulation = new(3, [new ForceLink(0, 1)]);

        simulation.Run(1000);

        Assert.True(Distance(simulation, 0, 1) < Distance(simulation, 0, 2));
        Assert.True(Distance(simulation, 0, 1) < Distance(simulation, 1, 2));
    }

    [Fact]
    public void LaGraviteRetientLesComposantesIsolees()
    {
        ForceSimulation avec = new(20, []);
        ForceSimulation sans = new(20, [], new ForceLayoutOptions { CenterStrength = 0 });

        avec.Run(500);
        sans.Run(500);

        (double minX, _, double maxX, _) = avec.GetBounds();
        (double minX2, _, double maxX2, _) = sans.GetBounds();
        Assert.True(maxX - minX < maxX2 - minX2);
    }

    [Fact]
    public void UnPoidsPlusFortReclamePlusDePlace()
    {
        ForceSimulation leger = new(3, [new ForceLink(0, 1), new ForceLink(0, 2)]);
        ForceSimulation lourd = new(3, [new ForceLink(0, 1), new ForceLink(0, 2)]);
        lourd.SetWeight(0, 6);

        leger.Run(1000);
        lourd.Run(1000);

        Assert.Equal(6, lourd.GetWeight(0));
        Assert.True(Distance(lourd, 0, 1) > Distance(leger, 0, 1));
    }

    [Fact]
    public void BarnesHutRestePrecheDuCalculExact()
    {
        ForceLink[] liens = Anneau(40);
        ForceSimulation approche = new(40, liens);
        ForceSimulation exact = new(40, liens, new ForceLayoutOptions { Theta = 0 });

        approche.Run(400);
        exact.Run(400);

        (double ax0, double ay0, double ax1, double ay1) = approche.GetBounds();
        (double ex0, double ey0, double ex1, double ey1) = exact.GetBounds();
        double largeurApprochee = Math.Max(ax1 - ax0, ay1 - ay0);
        double largeurExacte = Math.Max(ex1 - ex0, ey1 - ey0);

        Assert.InRange(largeurApprochee / largeurExacte, 0.85, 1.15);
    }

    [Fact]
    public void UnePorteeFinieLimiteLaRepulsion()
    {
        ForceSimulation courte = new(2, [], new ForceLayoutOptions { CenterStrength = 0, ChargeDistanceMax = 5 });
        double avant = Distance(courte, 0, 1);

        courte.Run(300);

        // Au départ, les deux nœuds sont à plus de 5 unités : ils ne se voient pas.
        Assert.True(avant > 5);
        Assert.Equal(avant, Distance(courte, 0, 1), 6);
    }

    // ==================== Refroidissement ====================

    [Fact]
    public void LaSimulationSeFigeEnTroisCentsPasEnviron()
    {
        ForceSimulation simulation = new(10, Anneau(10));

        int pas = simulation.Run(10_000);

        Assert.True(simulation.IsSettled);
        Assert.InRange(pas, 250, 350);
        Assert.False(simulation.Tick());
    }

    [Fact]
    public void UneTemperatureCibleGardeLaSimulationVivante()
    {
        ForceSimulation simulation = new(5, Anneau(5)) { AlphaTarget = 0.3 };

        Assert.Equal(1000, simulation.Run(1000));
        Assert.False(simulation.IsSettled);
        Assert.InRange(simulation.Alpha, 0.29, 0.31);

        simulation.AlphaTarget = 0;
        simulation.Run(10_000);
        Assert.True(simulation.IsSettled);
    }

    [Fact]
    public void ReheatRelanceUneSimulationFigee()
    {
        ForceSimulation simulation = new(4, Anneau(4));
        simulation.Run(10_000);

        simulation.Reheat();

        Assert.Equal(1, simulation.Alpha);
        Assert.False(simulation.IsSettled);
        simulation.Reheat(0.5);
        Assert.Equal(0.5, simulation.Alpha);
    }

    // ==================== Épinglage et positions ====================

    [Fact]
    public void UnNoeudEpingleNeBougePlus()
    {
        ForceSimulation simulation = new(4, Anneau(4));
        simulation.Pin(0, 100, -50);

        simulation.Run(200);

        Assert.True(simulation.IsPinned(0));
        Assert.Equal(100, simulation.GetX(0));
        Assert.Equal(-50, simulation.GetY(0));

        simulation.Unpin(0);
        simulation.Reheat();
        simulation.Run(200);

        Assert.False(simulation.IsPinned(0));
        Assert.NotEqual(100, simulation.GetX(0));
    }

    [Fact]
    public void SetPositionDeplaceUnNoeud()
    {
        ForceSimulation simulation = new(2, []);

        simulation.SetPosition(1, 42, -7);

        Assert.Equal(42, simulation.GetX(1));
        Assert.Equal(-7, simulation.GetY(1));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void DesNoeudsConfondusSontSeparesSansProduireDeValeurNonFinie()
    {
        ForceSimulation simulation = new(30, Anneau(30));
        for (int node = 0; node < 30; node++)
        {
            simulation.SetPosition(node, 0, 0);
        }

        simulation.Run(300);

        AssertFinite(simulation);
        Assert.True(Distance(simulation, 0, 15) > 1);
    }

    // ==================== Déterminisme et volume ====================

    [Fact]
    public void DeuxSimulationsIdentiquesDonnentLaMemeDisposition()
    {
        ForceLink[] liens = [.. Anneau(25), new ForceLink(0, 12), new ForceLink(3, 20)];
        ForceSimulation a = new(25, liens);
        ForceSimulation b = new(25, liens);

        a.Run(150);
        b.Run(150);

        for (int node = 0; node < 25; node++)
        {
            Assert.Equal(a.GetX(node), b.GetX(node));
            Assert.Equal(a.GetY(node), b.GetY(node));
        }
    }

    [Fact]
    public void UnGrandGrapheResteFiniEtSeCalculeViteSansQuadratique()
    {
        const int n = 3000;
        ForceLink[] liens = Enumerable.Range(1, n - 1).Select(i => new ForceLink(i, i / 3)).ToArray();
        ForceSimulation simulation = new(n, liens);

        var chrono = System.Diagnostics.Stopwatch.StartNew();
        for (int pas = 0; pas < 20; pas++)
        {
            simulation.Tick();
        }

        chrono.Stop();

        AssertFinite(simulation);
        Assert.True(chrono.ElapsedMilliseconds < 5000, $"20 pas en {chrono.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void LaGraineNAgitQueSurLesNoeudsConfondus()
    {
        ForceSimulation a = new(6, Anneau(6), new ForceLayoutOptions { Seed = 7 });
        ForceSimulation b = new(6, Anneau(6), new ForceLayoutOptions { Seed = 99 });
        ForceSimulation c = new(6, Anneau(6), new ForceLayoutOptions { Seed = 7 });

        // Départ en spirale : aucun nœud confondu, la graine ne sert pas.
        a.Run(50);
        b.Run(50);
        Assert.Equal(a.GetX(3), b.GetX(3));

        // Nœuds confondus : c'est la graine qui les sépare, et elle le fait de façon reproductible.
        foreach (ForceSimulation s in new[] { a, b, c })
        {
            for (int node = 0; node < 6; node++)
            {
                s.SetPosition(node, 0, 0);
            }

            s.Reheat();
        }

        c.Run(50);
        a.Run(50);
        b.Run(50);

        Assert.Equal(a.GetX(3), c.GetX(3));
        Assert.NotEqual(a.GetX(3), b.GetX(3));
        Assert.Equal(99, new ForceLayoutOptions { Seed = 99 }.Seed);
    }

    [Fact]
    public void UnLienPorteSesDeuxBouts()
    {
        ForceLink lien = new(2, 5);

        Assert.Equal(2, lien.Source);
        Assert.Equal(5, lien.Target);
        Assert.Equal(new ForceLink(2, 5), lien);
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void LesIndexExtremesSontRefusesSansDebordement()
    {
        ForceSimulation simulation = new(3, []);

        foreach (int index in new[] { int.MaxValue, int.MinValue, 3, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.GetX(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.GetY(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetPosition(index, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Pin(index, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Unpin(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.IsPinned(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetWeight(index, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.GetWeight(index));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceSimulation(2, [new ForceLink(int.MaxValue, 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceSimulation(2, [new ForceLink(0, int.MinValue)]));
        Assert.Equal(0, simulation.Run(0));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesCoordonneesTresGrandesRestentFinies()
    {
        ForceSimulation simulation = new(3, Anneau(3));
        simulation.SetPosition(0, 1e12, -1e12);
        simulation.SetPosition(1, -1e12, 1e12);
        simulation.Pin(2, 0, 0);
        simulation.SetWeight(2, 0);

        simulation.Run(400);

        AssertFinite(simulation);
        (double minX, double minY, double maxX, double maxY) = simulation.GetBounds();
        Assert.True(double.IsFinite(minX) && double.IsFinite(minY) && double.IsFinite(maxX) && double.IsFinite(maxY));
        Assert.Equal(0, simulation.GetWeight(2));
        Assert.True(simulation.IsPinned(2));
        simulation.Reheat(0);
        Assert.True(simulation.IsSettled);
    }

    [Fact]
    public void LesBouclesSontIgnoreesEtLesDoublonsGardes()
    {
        ForceSimulation simulation = new(3, [new ForceLink(0, 0), new ForceLink(0, 1), new ForceLink(1, 0)]);

        Assert.Equal(2, simulation.LinkCount);
        Assert.Equal(3, simulation.NodeCount);
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void UnGrapheVideSeSimuleSansRien()
    {
        ForceSimulation simulation = new(0, []);

        Assert.Null(Record.Exception(() => simulation.Tick()));
        Assert.Equal(0, simulation.NodeCount);
        Assert.Equal(0, simulation.LinkCount);
        Assert.Equal((0d, 0d, 0d, 0d), simulation.GetBounds());
        Assert.Equal(0, simulation.Run(0));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnNoeudUniqueResteSurPlaceAuCentre()
    {
        ForceSimulation simulation = new(1, []);

        simulation.Run(1000);

        Assert.InRange(Math.Abs(simulation.GetX(0)), 0, 10);
        Assert.InRange(Math.Abs(simulation.GetY(0)), 0, 10);
    }

    // ==================== Erreurs de paramétrage ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void DesLiensNulsSontRefuses()
    {
        Assert.Throws<ArgumentNullException>(() => new ForceSimulation(2, null!));
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void DesIndexHorsDuGrapheSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceSimulation(-1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceSimulation(2, [new ForceLink(0, 2)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceSimulation(2, [new ForceLink(-1, 0)]));

        ForceSimulation simulation = new(2, []);
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.GetX(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.GetY(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Pin(5, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Unpin(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.IsPinned(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetWeight(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.GetWeight(9));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Run(-1));
    }

    [Fact]
    [Trait("hazard", "non-finite-number")]
    public void LesValeursNonFiniesSontRefusees()
    {
        ForceSimulation simulation = new(2, []);

        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetPosition(0, double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetPosition(0, 0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Pin(0, double.NegativeInfinity, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetWeight(0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Alpha = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.AlphaTarget = 1.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Reheat(-0.1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { LinkDistance = double.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { ChargeStrength = double.PositiveInfinity }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { ChargeDistanceMax = double.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { Theta = double.NaN }.Validate());
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesReglagesHorsIntervalleSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { LinkDistance = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { LinkStrength = 1.5 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { ChargeDistanceMax = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { Theta = -0.1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { CenterStrength = 2 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { AlphaMin = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { AlphaDecay = 1.1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { AlphaTarget = -0.5 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { VelocityDecay = 3 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceLayoutOptions { InitialRadius = -2 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForceSimulation(2, [], new ForceLayoutOptions { Theta = -1 }));

        ForceLayoutOptions.Default.Validate();
        new ForceLayoutOptions { LinkStrength = 0.5, ChargeDistanceMax = 100, Theta = 0 }.Validate();
    }
}
