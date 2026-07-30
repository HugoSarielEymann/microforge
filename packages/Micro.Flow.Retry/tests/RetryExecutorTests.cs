using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Micro.Flow.Retry.Tests;

public sealed class RetryExecutorTests
{
    private static RetryOptions InstantOptions(int maxAttempts = 3, Func<Exception, bool>? shouldRetry = null, List<TimeSpan>? delays = null) =>
        new()
        {
            MaxAttempts = maxAttempts,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            ShouldRetry = shouldRetry ?? (static _ => true),
            DelayStrategy = (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            },
        };

    [Fact]
    public async Task ReussiteImmediate_RetourneLeResultat_SansAttendre()
    {
        var delays = new List<TimeSpan>();

        var result = await RetryExecutor.ExecuteAsync(
            _ => Task.FromResult(42), InstantOptions(delays: delays), NullLogger.Instance);

        Assert.Equal(42, result);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task EchecsTransitoires_RelanceAvecBackoffExponentiel_PuisReussit()
    {
        var delays = new List<TimeSpan>();
        var attempts = 0;

        var result = await RetryExecutor.ExecuteAsync(
            _ => ++attempts < 3
                ? Task.FromException<string>(new InvalidOperationException("transitoire"))
                : Task.FromResult("ok"),
            InstantOptions(maxAttempts: 5, delays: delays),
            NullLogger.Instance);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
        Assert.Equal([TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200)], delays);
    }

    [Fact]
    public async Task TentativesEpuisees_PropageLaDerniereException()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryExecutor.ExecuteAsync<int>(
                _ =>
                {
                    attempts++;
                    throw new InvalidOperationException("toujours en échec");
                },
                InstantOptions(maxAttempts: 3),
                NullLogger.Instance));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExceptionNonRelançable_PropageImmediatement()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            RetryExecutor.ExecuteAsync<int>(
                _ =>
                {
                    attempts++;
                    throw new ArgumentException("déterministe");
                },
                InstantOptions(maxAttempts: 5, shouldRetry: static ex => ex is InvalidOperationException),
                NullLogger.Instance));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Annulation_PropageOperationCanceled_SansRelancer()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RetryExecutor.ExecuteAsync(
                _ => Task.FromResult(1), InstantOptions(), NullLogger.Instance, source.Token));
    }

    [Fact]
    public async Task OptionsIncoherentes_LeveArgumentOutOfRange()
    {
        var options = new RetryOptions { MaxAttempts = 0 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            RetryExecutor.ExecuteAsync(_ => Task.FromResult(1), options, NullLogger.Instance));
    }

    [Fact]
    public async Task ArgumentsNuls_LeventArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            RetryExecutor.ExecuteAsync<int>(null!, InstantOptions(), NullLogger.Instance));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            RetryExecutor.ExecuteAsync(_ => Task.FromResult(1), null!, NullLogger.Instance));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            RetryExecutor.ExecuteAsync(_ => Task.FromResult(1), InstantOptions(), null!));
    }
}
