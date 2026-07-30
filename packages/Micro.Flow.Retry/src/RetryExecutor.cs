using Microsoft.Extensions.Logging;

namespace Micro.Flow.Retry;

/// <summary>
/// Méthode générique unique de ce micropackage : exécution d'une opération
/// asynchrone avec relances, backoff exponentiel et journalisation.
/// </summary>
public static class RetryExecutor
{
    /// <summary>
    /// Exécute <paramref name="operation"/> jusqu'à réussite ou épuisement des tentatives.
    /// </summary>
    /// <typeparam name="TResult">Type du résultat produit par l'opération.</typeparam>
    /// <param name="operation">Opération asynchrone à exécuter, honorant le jeton d'annulation.</param>
    /// <param name="options">Paramétrage des relances (tentatives, délais, prédicat).</param>
    /// <param name="logger">Journal des relances ; utiliser NullLogger.Instance pour le silence.</param>
    /// <param name="cancellationToken">Jeton d'annulation propagé à l'opération et aux attentes.</param>
    /// <returns>Le résultat de la première exécution réussie.</returns>
    /// <exception cref="ArgumentNullException">Si un argument obligatoire est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="options"/> est incohérent.</exception>
    public static async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        RetryOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();

        var delay = options.InitialDelay;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await operation(cancellationToken).ConfigureAwait(false);
                if (attempt > 1)
                {
                    LogRecovered(logger, attempt, null);
                }

                return result;
            }
            catch (Exception exception) when (attempt < options.MaxAttempts && options.ShouldRetry(exception))
            {
                LogRetrying(logger, attempt, options.MaxAttempts, delay, exception);
                await options.DelayStrategy(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromTicks((long)(delay.Ticks * options.BackoffFactor));
            }
        }
    }

    private static readonly Action<ILogger, int, int, TimeSpan, Exception?> LogRetrying =
        LoggerMessage.Define<int, int, TimeSpan>(
            LogLevel.Warning,
            new EventId(1, nameof(LogRetrying)),
            "Tentative {Attempt}/{MaxAttempts} échouée ; nouvelle tentative dans {Delay}.");

    private static readonly Action<ILogger, int, Exception?> LogRecovered =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2, nameof(LogRecovered)),
            "Opération réussie à la tentative {Attempt}.");
}
