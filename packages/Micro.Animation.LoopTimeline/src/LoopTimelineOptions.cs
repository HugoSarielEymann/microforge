namespace Micro.Animation.LoopTimeline;

/// <summary>
/// Réglages d'une <see cref="LoopTimeline"/> : durée d'un aller, instant du premier aller,
/// aller-retour et nombre de répétitions.
/// </summary>
/// <remarks>
/// Les noms et la sémantique sont ceux des lignes de temps XAML (WPF, UWP, WinUI) —
/// <c>Duration</c>, <c>BeginTime</c>, <c>AutoReverse</c>, <c>RepeatBehavior</c> — pour qu'une
/// animation de storyboard se transcrive propriété pour propriété.
/// </remarks>
public sealed class LoopTimelineOptions
{
    /// <summary>Durée d'un aller, strictement positive. Défaut : une seconde.</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Instant du premier aller, compté depuis l'origine de l'horloge. Défaut : zéro.
    /// </summary>
    /// <remarks>
    /// Avant cet instant, la boucle n'a pas commencé : <see cref="LoopTimeline.ProgressAt"/>
    /// rend <see langword="null"/>, et la propriété animée garde sa valeur de base — comme sous
    /// XAML. Une valeur négative décale la phase : la boucle se présente comme si elle tournait
    /// déjà depuis ce laps de temps.
    /// </remarks>
    public TimeSpan BeginTime { get; init; }

    /// <summary>
    /// Chaque aller est suivi d'un retour de même durée : l'avancement monte de 0 à 1, puis
    /// redescend. Défaut : faux — l'avancement retombe à 0 au début de chaque itération.
    /// </summary>
    public bool AutoReverse { get; init; }

    /// <summary>
    /// Nombre d'itérations — un aller, ou un aller-retour avec <see cref="AutoReverse"/> —,
    /// éventuellement fractionnaire ; <see langword="null"/> pour une boucle sans fin.
    /// Défaut : <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Après la dernière itération, l'avancement reste figé sur sa valeur de fin, comme une
    /// animation XAML dont le remplissage est <c>HoldEnd</c>.
    /// </remarks>
    public double? RepeatCount { get; init; }

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <see cref="Duration"/> est nulle ou négative, ou si <see cref="RepeatCount"/> n'est
    /// ni <see langword="null"/> ni un nombre fini strictement positif.
    /// </exception>
    public void Validate()
    {
        if (Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "La durée d'un aller doit être strictement positive.");
        }

        if (RepeatCount is { } count && (!double.IsFinite(count) || count <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), count, "Le nombre d'itérations doit être un nombre fini strictement positif, ou null pour une boucle sans fin.");
        }
    }
}
