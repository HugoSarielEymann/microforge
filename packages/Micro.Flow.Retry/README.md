# Micro.Flow.Retry

## Description

Exécute une opération asynchrone avec relances configurables : nombre de tentatives, backoff exponentiel, prédicat de relance par exception, stratégie de délai injectable et journalisation via `ILogger`.

## Mode d'emploi

Utiliser ce package dès qu'un appel peut échouer de façon transitoire (HTTP, base de données, file de messages) et mérite d'être retenté. Ne pas l'utiliser pour des erreurs déterministes (validation, bug) : filtrer via `ShouldRetry`.

L'unique point d'entrée est `RetryExecutor.ExecuteAsync<TResult>(operation, options, logger, cancellationToken)`. L'opération reçoit le jeton d'annulation et doit l'honorer. Le délai réel est injectable (`DelayStrategy`), ce qui rend les tests instantanés.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `MaxAttempts` | `int` | `3` | Nombre total de tentatives, première incluse (≥ 1). |
| `InitialDelay` | `TimeSpan` | `200 ms` | Délai avant la deuxième tentative. |
| `BackoffFactor` | `double` | `2.0` | Multiplicateur du délai après chaque échec (≥ 1.0). |
| `ShouldRetry` | `Func<Exception, bool>` | toutes | Décide si l'exception justifie une relance. |
| `DelayStrategy` | `Func<TimeSpan, CancellationToken, Task>` | `Task.Delay` | Attente injectable ; substituer en test. |

## Exemple

```csharp
using Micro.Flow.Retry;
using Microsoft.Extensions.Logging.Abstractions;

var result = await RetryExecutor.ExecuteAsync(
    operation: ct => httpClient.GetStringAsync(url, ct),
    options: new RetryOptions
    {
        MaxAttempts = 5,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        ShouldRetry = ex => ex is HttpRequestException,
    },
    logger: NullLogger.Instance);
```
