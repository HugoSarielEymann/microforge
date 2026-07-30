// Démo consommateur : la logique générique vient des micropackages du feed local,
// seul l'assemblage (spécifique) vit ici.
using Micro.Flow.Retry;
using Micro.Text.Slugify;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
var logger = loggerFactory.CreateLogger("Demo");

var attempts = 0;
var titre = await RetryExecutor.ExecuteAsync(
    operation: _ => ++attempts < 3
        ? Task.FromException<string>(new InvalidOperationException("panne transitoire simulée"))
        : Task.FromResult("Écrire du C# réutilisable : la méthode MicroForge !"),
    options: new RetryOptions { MaxAttempts = 5, InitialDelay = TimeSpan.FromMilliseconds(50) },
    logger: logger);

Console.WriteLine($"Titre récupéré (tentative {attempts}) : {titre}");
Console.WriteLine($"Slug : {Slugifier.ToSlug(titre, new SlugifyOptions { MaxLength = 40 })}");
