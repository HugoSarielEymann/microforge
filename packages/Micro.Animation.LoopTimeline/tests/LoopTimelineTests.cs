using Xunit;

namespace Micro.Animation.LoopTimeline.Tests;

public sealed class LoopTimelineTests
{
    private const int Precision = 9;

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static LoopTimeline Boucle(double duration, bool autoReverse = false, double begin = 0, double? repeat = null)
        => new(new LoopTimelineOptions
        {
            Duration = S(duration),
            AutoReverse = autoReverse,
            BeginTime = S(begin),
            RepeatCount = repeat,
        });

    private static void AssertProgress(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Value, Precision);
    }

    // ==================== Comportement nominal ====================

    [Fact]
    public void UnAllerSimpleAvanceLineairementPuisRecommence()
    {
        LoopTimeline boucle = Boucle(4);

        AssertProgress(0, boucle.ProgressAt(S(0)));
        AssertProgress(0.25, boucle.ProgressAt(S(1)));
        AssertProgress(0.75, boucle.ProgressAt(S(3)));
        AssertProgress(0, boucle.ProgressAt(S(4)));
        AssertProgress(0.25, boucle.ProgressAt(S(5)));
        AssertProgress(0.5, boucle.ProgressAt(S(4002)));
    }

    [Fact]
    public void UnAllerRetourMonteJusquaUnPuisRedescend()
    {
        LoopTimeline boucle = Boucle(2, autoReverse: true);

        AssertProgress(0.5, boucle.ProgressAt(S(1)));
        AssertProgress(1, boucle.ProgressAt(S(2)));
        AssertProgress(0.5, boucle.ProgressAt(S(3)));
        AssertProgress(0.25, boucle.ProgressAt(S(3.5)));
        AssertProgress(0, boucle.ProgressAt(S(4)));
        AssertProgress(0.5, boucle.ProgressAt(S(5)));
    }

    [Fact]
    public void AvantLeDebutLaBoucleNaPasCommence()
    {
        LoopTimeline boucle = Boucle(4, begin: 3);

        Assert.Null(boucle.ProgressAt(S(0)));
        Assert.Null(boucle.ProgressAt(S(2.9)));
        AssertProgress(0.25, boucle.ProgressAt(S(4)));
    }

    [Fact]
    public void UnDebutNegatifDecaleLaPhase()
    {
        LoopTimeline boucle = Boucle(4, begin: -1);

        AssertProgress(0.25, boucle.ProgressAt(S(0)));
        AssertProgress(0, boucle.ProgressAt(S(3)));
    }

    [Fact]
    public void UneBoucleFinieResteSurSaValeurDeFin()
    {
        LoopTimeline aller = Boucle(1, repeat: 2);
        AssertProgress(0.5, aller.ProgressAt(S(1.5)));
        AssertProgress(1, aller.ProgressAt(S(10)));

        LoopTimeline allerRetour = Boucle(1, autoReverse: true, repeat: 1);
        AssertProgress(0.5, allerRetour.ProgressAt(S(1.5)));
        AssertProgress(0, allerRetour.ProgressAt(S(10)));
    }

    [Fact]
    public void UnNombreDIterationsFractionnaireSArreteEnCoursDeRoute()
    {
        LoopTimeline aller = Boucle(1, repeat: 2.5);
        AssertProgress(0.25, aller.ProgressAt(S(2.25)));
        AssertProgress(0.5, aller.ProgressAt(S(3)));
        AssertProgress(0.5, aller.ProgressAt(S(60)));

        // Un aller-retour et demi s'arrête au sommet.
        LoopTimeline allerRetour = Boucle(1, autoReverse: true, repeat: 1.5);
        AssertProgress(1, allerRetour.ProgressAt(S(60)));
    }

    [Fact]
    public void LaDureeActiveSeDeduitDesIterations()
    {
        Assert.Null(Boucle(2).ActiveDuration);
        Assert.Equal(S(12), Boucle(2, autoReverse: true, repeat: 3).ActiveDuration);
        Assert.Equal(S(2.5), Boucle(1, repeat: 2.5).ActiveDuration);
    }

    [Fact]
    public void LesReglagesRestentConsultables()
    {
        LoopTimelineOptions options = new() { Duration = S(3), AutoReverse = true };

        LoopTimeline boucle = new(options);

        Assert.Same(options, boucle.Options);
    }

    [Fact]
    public void LaValidationSePasseDuConstructeur()
    {
        // Des réglages se vérifient avant d'être confiés, par exemple à la lecture d'un thème.
        new LoopTimelineOptions { Duration = S(2), BeginTime = S(-5), RepeatCount = 0.5 }.Validate();

        LoopTimelineOptions fautifs = new() { Duration = S(-2) };
        ArgumentOutOfRangeException erreur = Assert.Throws<ArgumentOutOfRangeException>(fautifs.Validate);
        Assert.Equal(nameof(LoopTimelineOptions.Duration), erreur.ParamName);
    }

    [Fact]
    public void LesDefautsDecriventUneBoucleSansFinDUneSeconde()
    {
        LoopTimeline boucle = new(new LoopTimelineOptions());

        Assert.Equal(S(1), boucle.Options.Duration);
        Assert.Null(boucle.ActiveDuration);
        AssertProgress(0.5, boucle.ProgressAt(S(7.5)));
    }

    [Fact]
    public void UneBoucleParentePiloteUnEnfantQuiSeFigeASaFin()
    {
        // Storyboard en aller-retour de 4,5 s contenant une animation de 3,5 s : l'enfant
        // atteint sa fin avant son parent et y reste jusqu'au retour.
        LoopTimeline parent = Boucle(4.5, autoReverse: true);
        LoopTimeline enfant = Boucle(3.5, repeat: 1);

        double EnfantA(double t) => enfant.ProgressAt(parent.ProgressAt(S(t))!.Value * S(4.5))!.Value;

        Assert.Equal(1, EnfantA(4), Precision);
        Assert.Equal(1, EnfantA(5.5), Precision);
        Assert.Equal(3.0 / 3.5, EnfantA(6), Precision);
        Assert.Equal(0, EnfantA(9), Precision);
    }

    // ==================== Aléas ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void DesReglagesNulsSontRefuses()
        => Assert.Throws<ArgumentNullException>(() => new LoopTimeline(null!));

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UneDureeNegativeEstRefusee()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Boucle(-1));

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UnNombreDIterationsNegatifEstRefuse()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Boucle(1, repeat: -2));

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UnInstantNegatifPrecedeLeDebut()
        => Assert.Null(Boucle(1).ProgressAt(S(-0.001)));

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneDureeNulleEstRefusee()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LoopTimeline(new LoopTimelineOptions { Duration = TimeSpan.Zero }));

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ZeroIterationEstRefuse()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Boucle(1, repeat: 0));

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesBornesExactesSontCellesDeXaml()
    {
        LoopTimeline boucle = Boucle(2, begin: 1, repeat: 2);

        // Au tic près du début, la boucle a commencé ; un tic avant, non.
        AssertProgress(0, boucle.ProgressAt(S(1)));
        Assert.Null(boucle.ProgressAt(S(1) - TimeSpan.FromTicks(1)));

        // Une frontière d'itération ouvre la suivante ; la fin exacte fige la valeur de fin.
        AssertProgress(0, boucle.ProgressAt(S(3)));
        AssertProgress(1, boucle.ProgressAt(S(5)));

        // Une durée d'un seul tic reste une boucle valable.
        LoopTimeline breve = new(new LoopTimelineOptions { Duration = TimeSpan.FromTicks(1), AutoReverse = true });
        AssertProgress(1, breve.ProgressAt(TimeSpan.FromTicks(1)));
        AssertProgress(0, breve.ProgressAt(TimeSpan.FromTicks(2)));
    }

    [Theory]
    [Trait("hazard", "non-finite-number")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void UnNombreDIterationsNonFiniEstRefuse(double repeat)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Boucle(1, repeat: repeat));

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void LesInstantsExtremesNeDebordentPas()
    {
        LoopTimeline depuisLeDebutDesTemps = new(new LoopTimelineOptions
        {
            Duration = S(3),
            AutoReverse = true,
            BeginTime = TimeSpan.MinValue,
        });

        double? tresTard = depuisLeDebutDesTemps.ProgressAt(TimeSpan.MaxValue);
        Assert.NotNull(tresTard);
        Assert.InRange(tresTard.Value, 0d, 1d);

        LoopTimeline jamais = new(new LoopTimelineOptions { BeginTime = TimeSpan.MaxValue });
        Assert.Null(jamais.ProgressAt(TimeSpan.MinValue));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void UneDureeActiveDemesureeEstPlafonnee()
    {
        LoopTimeline immense = new(new LoopTimelineOptions
        {
            Duration = TimeSpan.MaxValue,
            AutoReverse = true,
            RepeatCount = 1e300,
        });

        Assert.Equal(TimeSpan.MaxValue, immense.ActiveDuration);
        AssertProgress(0.5, immense.ProgressAt(TimeSpan.FromTicks(TimeSpan.MaxValue.Ticks / 2)));
    }
}
