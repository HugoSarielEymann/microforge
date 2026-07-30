namespace Micro.Flow.Retry;

/// <summary>
/// Paramétrage des relances : tout effet non déterministe (délai réel)
/// est injectable pour garder l'exécuteur testable et pur.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>Nombre total de tentatives, première incluse. Minimum 1.</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>Délai avant la deuxième tentative ; multiplié par <see cref="BackoffFactor"/> ensuite.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Facteur multiplicatif appliqué au délai après chaque échec. Minimum 1.0.</summary>
    public double BackoffFactor { get; init; } = 2.0;

    /// <summary>Prédicat décidant si une exception justifie une relance. Par défaut : toutes.</summary>
    public Func<Exception, bool> ShouldRetry { get; init; } = static _ => true;

    /// <summary>
    /// Stratégie d'attente injectable (horloge abstraite). Par défaut : <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// Injecter un enregistreur en test pour vérifier les délais sans attendre réellement.
    /// </summary>
    public Func<TimeSpan, CancellationToken, Task> DelayStrategy { get; init; } =
        static (delay, cancellationToken) => Task.Delay(delay, cancellationToken);

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si une borne est violée.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(BackoffFactor, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(InitialDelay, TimeSpan.Zero);
    }
}
