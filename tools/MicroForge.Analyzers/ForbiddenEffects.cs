using System.Collections.Immutable;

namespace MicroForge.Analyzers;

/// <summary>Un effet ambiant interdit dans un micropackage, et son remplacement.</summary>
/// <param name="Id">Identifiant du diagnostic, affiché dans l'IDE.</param>
/// <param name="Title">Titre court.</param>
/// <param name="Remedy">Ce qu'il faut faire à la place.</param>
public sealed record ForbiddenEffect(string Id, string Title, string Remedy);

/// <summary>
/// Catalogue des effets ambiants proscrits par R3, et de leur remplacement.
///
/// L'analyseur travaille sur le **modèle sémantique** : il compare le symbole résolu,
/// pas le texte. Un alias (<c>using C = System.Console;</c>), un <c>using static</c>
/// ou une méthode d'extension ne le contournent donc pas — c'était la limite connue
/// de la détection par expression régulière qu'il remplace.
/// </summary>
public static class ForbiddenEffects
{
    /// <summary>Préfixe commun des diagnostics MicroForge.</summary>
    public const string Category = "MicroForge.Purity";

    /// <summary>Écriture directe sur la console.</summary>
    public static ForbiddenEffect Console { get; } = new(
        "MFG001",
        "Sortie console dans un micropackage",
        "Injecter Microsoft.Extensions.Logging.ILogger. Un micropackage n'écrit jamais directement : c'est à l'appelant de décider où va la trace.");

    /// <summary>Lecture de l'heure courante depuis une horloge ambiante.</summary>
    public static ForbiddenEffect AmbientClock { get; } = new(
        "MFG002",
        "Horloge ambiante dans un micropackage",
        "Injecter TimeProvider (ou Func<DateTimeOffset>). Sans cela, le comportement dépend du moment d'exécution et les tests deviennent instables.");

    /// <summary>Attente bloquante.</summary>
    public static ForbiddenEffect BlockingSleep { get; } = new(
        "MFG003",
        "Attente bloquante dans un micropackage",
        "Injecter une stratégie de délai asynchrone (Func<TimeSpan, CancellationToken, Task>). Un test ne doit pas attendre réellement.");

    /// <summary>Accès au système de fichiers.</summary>
    public static ForbiddenEffect FileSystem { get; } = new(
        "MFG004",
        "Accès au système de fichiers dans un micropackage",
        "Recevoir un Stream ou une chaîne en paramètre. Le micropackage transforme des données, il ne décide pas d'où elles viennent.");

    /// <summary>Lancement de processus.</summary>
    public static ForbiddenEffect ProcessLaunch { get; } = new(
        "MFG005",
        "Lancement de processus dans un micropackage",
        "Hors périmètre : un micropackage est une capacité pure, pas un orchestrateur.");

    /// <summary>Aléa non reproductible.</summary>
    public static ForbiddenEffect UnseededRandom { get; } = new(
        "MFG006",
        "Aléa non reproductible dans un micropackage",
        "Accepter une graine ou une instance Random en paramètre. Sans cela, un échec de test n'est pas rejouable.");

    /// <summary>Blocage synchrone sur une opération asynchrone.</summary>
    public static ForbiddenEffect SyncOverAsync { get; } = new(
        "MFG007",
        "Blocage synchrone sur du code asynchrone",
        "Rester asynchrone de bout en bout. Ce motif provoque des interblocages selon le contexte de synchronisation de l'appelant.");

    /// <summary>Lecture de l'environnement du processus.</summary>
    public static ForbiddenEffect Environment { get; } = new(
        "MFG008",
        "Lecture de l'environnement dans un micropackage",
        "Passer la valeur en paramètre. Une capacité dont le comportement dépend d'une variable d'environnement n'est pas paramétrable par l'appelant.");

    /// <summary>Tous les effets du catalogue.</summary>
    // ImmutableArray.Create plutôt qu'une expression de collection : la version de
    // System.Collections.Immutable livrée avec netstandard2.0 ne les prend pas en charge.
    public static ImmutableArray<ForbiddenEffect> All { get; } = ImmutableArray.Create(
        Console, AmbientClock, BlockingSleep, FileSystem,
        ProcessLaunch, UnseededRandom, SyncOverAsync, Environment);
}
