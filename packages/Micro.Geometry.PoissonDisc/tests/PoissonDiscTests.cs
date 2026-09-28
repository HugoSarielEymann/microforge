using Xunit;

namespace Micro.Geometry.PoissonDisc.Tests;

public sealed class PoissonDiscTests
{
    private static double MinimumGap(IReadOnlyList<SamplePoint> points)
    {
        double best = double.PositiveInfinity;
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                double dx = points[i].X - points[j].X;
                double dy = points[i].Y - points[j].Y;
                best = Math.Min(best, Math.Sqrt((dx * dx) + (dy * dy)));
            }
        }

        return best;
    }

    // ==================== Cas nominal ====================

    [Fact]
    public void LeNombreDePointsEstExactEtTousSontDansLEllipse()
    {
        SampleResult result = PoissonDiscSampler.SampleEllipse(26, 10, 5, 3, 2, new Random(42));

        Assert.Equal(26, result.Points.Count);
        Assert.All(result.Points, p => Assert.True((Math.Pow((p.X - 10) / 3, 2) + Math.Pow((p.Y - 5) / 2, 2)) <= 1.0000001));
    }

    [Fact]
    public void DeuxPointsSontToujoursEspacesDeLaDistanceAnnoncee()
    {
        SampleResult result = PoissonDiscSampler.SampleEllipse(40, 0, 0, 5, 5, new Random(7));

        Assert.True(result.Distance > 0);
        Assert.True(MinimumGap(result.Points) >= result.Distance - 1e-9);
    }

    [Fact]
    public void LaDistanceDeDepartTientCompteDeLaSurface()
    {
        // Peu de points dans une grande surface : pas de resserrement, distance de départ = √(aire / (n × 1,15)).
        SampleResult result = PoissonDiscSampler.SampleRectangle(10, 0, 0, 100, 100, new Random(3));

        Assert.Equal(Math.Sqrt(10000 / (10 * 1.15)), result.Distance, 6);
    }

    [Fact]
    public void LeRectangleContientTousSesPoints()
    {
        SampleResult result = PoissonDiscSampler.SampleRectangle(50, -4, 2, 8, 3, new Random(11));

        Assert.Equal(50, result.Points.Count);
        Assert.All(result.Points, p =>
        {
            Assert.InRange(p.X, -4, 4);
            Assert.InRange(p.Y, 2, 5);
        });
    }

    [Fact]
    public void UneMemeGraineDonneLeMemeSemis()
    {
        SampleResult first = PoissonDiscSampler.SampleEllipse(30, 1, 1, 2, 1, new Random(2026));
        SampleResult second = PoissonDiscSampler.SampleEllipse(30, 1, 1, 2, 1, new Random(2026));

        Assert.Equal(first.Points, second.Points);
        Assert.Equal(first.Distance, second.Distance);
    }

    [Fact]
    public void DesGrainesDifferentesDonnentDesSemisDifferents()
    {
        SampleResult first = PoissonDiscSampler.SampleEllipse(30, 1, 1, 2, 1, new Random(1));
        SampleResult second = PoissonDiscSampler.SampleEllipse(30, 1, 1, 2, 1, new Random(2));

        Assert.NotEqual(first.Points, second.Points);
    }

    [Fact]
    public void TropDePointsPourLaDistanceResserrentLeSemisSansEnPerdre()
    {
        PoissonDiscOptions dense = new() { AreaPerPoint = 0.2, AttemptsPerPoint = 5 };

        SampleResult result = PoissonDiscSampler.SampleRectangle(200, 0, 0, 1, 1, new Random(5), dense);

        Assert.Equal(200, result.Points.Count);
        Assert.True(result.Distance < Math.Sqrt(1 / (200 * 0.2)));
        Assert.True(MinimumGap(result.Points) >= result.Distance - 1e-9);
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        PoissonDiscOptions.Default.Validate();
        Assert.Equal(60, PoissonDiscOptions.Default.AttemptsPerPoint);
        Assert.Equal(0.88, PoissonDiscOptions.Default.ShrinkFactor);
        Assert.Equal(1.15, PoissonDiscOptions.Default.AreaPerPoint);
        Assert.Equal(10_000, PoissonDiscOptions.Default.MaximumCount);
    }

    // ==================== Cas limites ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ZeroPointRendUnSemisVideEtUnPointTientToujours()
    {
        Assert.Empty(PoissonDiscSampler.SampleEllipse(0, 0, 0, 1, 1, new Random(1)).Points);
        Assert.Single(PoissonDiscSampler.SampleEllipse(1, 0, 0, 1, 1, new Random(1)).Points);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneSurfaceNulleAccepteTousLesPointsSurSonSegment()
    {
        SampleResult line = PoissonDiscSampler.SampleRectangle(5, 3, 0, 0, 10, new Random(9));
        SampleResult dot = PoissonDiscSampler.SampleEllipse(4, 2, 2, 0, 0, new Random(9));

        Assert.Equal(5, line.Points.Count);
        Assert.All(line.Points, p => Assert.Equal(3, p.X));
        Assert.Equal(0, line.Distance);
        Assert.All(dot.Points, p => Assert.Equal(new SamplePoint(2, 2), p));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LeMaximumDePointsEstInclusif()
    {
        PoissonDiscOptions options = new() { MaximumCount = 3 };

        Assert.Equal(3, PoissonDiscSampler.SampleRectangle(3, 0, 0, 1, 1, new Random(1), options).Points.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(4, 0, 0, 1, 1, new Random(1), options));
    }

    // ==================== Erreurs de paramétrage ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void UneSourceDAleaNulleEstRefusee()
    {
        Assert.Throws<ArgumentNullException>(() => PoissonDiscSampler.SampleEllipse(3, 0, 0, 1, 1, null!));
        Assert.Throws<ArgumentNullException>(() => PoissonDiscSampler.SampleRectangle(3, 0, 0, 1, 1, null!));
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UnNombreOuUneDimensionNegatifsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleEllipse(-1, 0, 0, 1, 1, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleEllipse(3, 0, 0, -1, 1, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(3, 0, 0, 1, -2, new Random(1)));
    }

    [Theory]
    [Trait("hazard", "non-finite-number")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void UnNombreNonFiniEstRefuse(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleEllipse(3, value, 0, 1, 1, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleEllipse(3, 0, 0, value, 1, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(3, 0, value, 1, 1, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(3, 0, 0, 1, value, new Random(1)));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void UneSurfaceQuiDebordeEstRefuseeAuLieuDeTournerSansFin()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleEllipse(3, 0, 0, 1e200, 1e200, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(3, 0, 0, 1e300, 1e300, new Random(1)));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void LesValeursExtremesSontTraiteesSansDebordement()
    {
        // Un nombre de points démesuré est refusé avant tout calcul.
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(int.MaxValue, 0, 0, 1, 1, new Random(1)));

        // Une position extrême mais finie, sur une surface nulle : les points s'y posent.
        SampleResult far = PoissonDiscSampler.SampleEllipse(3, double.MaxValue, double.MinValue, 0, 0, new Random(1));
        Assert.All(far.Points, p => Assert.Equal(new SamplePoint(double.MaxValue, double.MinValue), p));

        // Une dimension extrême fait déborder la surface : refusée.
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleRectangle(3, 0, 0, double.MaxValue, 2, new Random(1)));

        // Très loin de l'origine, le semis reste exact et espacé.
        SampleResult distant = PoissonDiscSampler.SampleRectangle(20, 1e12, -1e12, 10, 10, new Random(4));
        Assert.Equal(20, distant.Points.Count);
        Assert.True(MinimumGap(distant.Points) >= distant.Distance - 1e-3);
    }

    [Fact]
    public void DesReglagesIncoherentsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { AttemptsPerPoint = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { MaximumCount = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { ShrinkFactor = 1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { ShrinkFactor = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { ShrinkFactor = double.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { AreaPerPoint = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoissonDiscOptions { AreaPerPoint = double.PositiveInfinity }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => PoissonDiscSampler.SampleEllipse(3, 0, 0, 1, 1, new Random(1), new PoissonDiscOptions { ShrinkFactor = 2 }));
    }
}
