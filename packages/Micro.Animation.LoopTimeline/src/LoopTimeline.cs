namespace Micro.Animation.LoopTimeline;

/// <summary>
/// Une animation en boucle réduite à son horaire : elle dit, pour un instant donné, où en est
/// l'animation — un avancement entre 0 et 1 — sans rien animer elle-même.
/// </summary>
/// <remarks>
/// <para>
/// C'est ce qui permet d'animer à la cadence de son choix. Sous XAML, un storyboard en boucle
/// fait recomposer toute la fenêtre à chaque image, soixante fois par seconde, tant qu'il
/// tourne. Une minuterie lente qui interroge <see cref="ProgressAt"/> et pose elle-même les
/// valeurs reproduit la même courbe à cinq images par seconde, et s'arrête quand on veut.
/// </para>
/// <para>
/// L'horloge est fournie par l'appelant (<see cref="ProgressAt"/> reçoit le temps écoulé) :
/// la classe est déterministe, immuable, et sûre entre fils. L'accélération se compose
/// par-dessus — l'avancement rendu est linéaire dans le temps.
/// </para>
/// </remarks>
public sealed class LoopTimeline
{
    private readonly Int128 _duration;
    private readonly Int128 _iteration;
    private readonly double? _activeTicks;

    /// <summary>Prépare l'horaire d'une boucle.</summary>
    /// <param name="options">Réglages de la boucle.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="options"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les réglages sont incohérents (voir <see cref="LoopTimelineOptions.Validate"/>).</exception>
    public LoopTimeline(LoopTimelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        Options = options;
        _duration = options.Duration.Ticks;
        _iteration = options.AutoReverse ? _duration * 2 : _duration;

        if (options.RepeatCount is { } count)
        {
            double active = count * (double)_iteration;
            _activeTicks = active;
            ActiveDuration = active >= TimeSpan.MaxValue.Ticks ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)Math.Round(active));
        }
    }

    /// <summary>Réglages de la boucle.</summary>
    public LoopTimelineOptions Options { get; }

    /// <summary>
    /// Temps pendant lequel la boucle évolue, à partir de <see cref="LoopTimelineOptions.BeginTime"/> ;
    /// <see langword="null"/> pour une boucle sans fin.
    /// </summary>
    /// <remarks>Plafonné à <see cref="TimeSpan.MaxValue"/> quand le produit le dépasse.</remarks>
    public TimeSpan? ActiveDuration { get; }

    /// <summary>Avancement de la boucle à un instant donné.</summary>
    /// <param name="elapsed">Temps écoulé depuis l'origine de l'horloge ; toute valeur est admise.</param>
    /// <returns>
    /// Un avancement entre 0 et 1 — linéaire dans le temps, à passer ensuite dans une courbe
    /// d'accélération —, ou <see langword="null"/> avant <see cref="LoopTimelineOptions.BeginTime"/>,
    /// quand la boucle n'a pas encore commencé. Après la dernière itération d'une boucle finie,
    /// la valeur de fin.
    /// </returns>
    public double? ProgressAt(TimeSpan elapsed)
    {
        // Sur 128 bits, aucun écart entre deux TimeSpan ne déborde.
        Int128 local = (Int128)elapsed.Ticks - Options.BeginTime.Ticks;
        if (local < 0)
        {
            return null;
        }

        if (_activeTicks is { } active && (double)local >= active)
        {
            return EndProgress();
        }

        return ProgressWithin(local % _iteration);
    }

    /// <summary>La valeur figée après la dernière itération.</summary>
    private double EndProgress()
    {
        double count = Options.RepeatCount!.Value;
        double fraction = count - Math.Floor(count);

        // Une itération entière finit au bout de l'aller, ou au retour à l'origine.
        if (fraction == 0)
        {
            return Options.AutoReverse ? 0d : 1d;
        }

        return ProgressWithin((Int128)(fraction * (double)_iteration));
    }

    /// <summary>Avancement à une position donnée d'une itération, retour compris.</summary>
    private double ProgressWithin(Int128 phase)
    {
        if (phase <= _duration)
        {
            return (double)phase / (double)_duration;
        }

        return (double)((_duration * 2) - phase) / (double)_duration;
    }
}
