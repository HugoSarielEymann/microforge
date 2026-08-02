using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MicroForge.Analyzers;

/// <summary>
/// Signale les effets ambiants interdits par la règle R3 de MicroForge.
///
/// Cet analyseur remplace une détection par expression régulière qui se contournait
/// trivialement : un alias <c>using C = System.Console;</c> passait au travers. Ici la
/// comparaison porte sur le **symbole résolu** par le compilateur, ce qui rend les
/// alias, les <c>using static</c> et les noms partiellement qualifiés sans effet.
///
/// Il s'adresse autant à l'humain qu'à l'agent : les violations apparaissent dans
/// l'IDE à la frappe, avant même la compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AmbientEffectAnalyzer : DiagnosticAnalyzer
{
    private static DiagnosticDescriptor Describe(ForbiddenEffect effect) => new(
        effect.Id,
        effect.Title,
        "{0} — {1}",
        ForbiddenEffects.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: effect.Remedy,
        helpLinkUri: "https://github.com/HugoSarielEymann/microforge/blob/main/RULES.md");

    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> Descriptors =
        ForbiddenEffects.All.ToImmutableDictionary(e => e.Id, Describe);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        Descriptors.Values.ToImmutableArray();

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var operation = (IInvocationOperation)context.Operation;
        var method = operation.TargetMethod;
        var type = FullName(method.ContainingType);

        var effect = type switch
        {
            "System.Console" => ForbiddenEffects.Console,
            "System.Threading.Thread" when method.Name == "Sleep" => ForbiddenEffects.BlockingSleep,
            "System.IO.File" or "System.IO.Directory" => ForbiddenEffects.FileSystem,
            "System.Diagnostics.Process" when method.Name == "Start" => ForbiddenEffects.ProcessLaunch,
            "System.Environment" when method.Name is "Exit" or "GetEnvironmentVariable" or "GetEnvironmentVariables"
                => ForbiddenEffects.Environment,
            _ => SyncOverAsyncEffect(method),
        };

        if (effect is not null)
        {
            Report(context, effect, operation.Syntax.GetLocation(), $"{Simple(method.ContainingType)}.{method.Name}");
        }
    }

    /// <summary>
    /// <c>Task.Wait()</c> et <c>.GetAwaiter().GetResult()</c> bloquent le fil courant
    /// sur une opération asynchrone — source classique d'interblocage.
    /// </summary>
    private static ForbiddenEffect? SyncOverAsyncEffect(IMethodSymbol method)
    {
        if (method.Name == "Wait" && IsTask(method.ContainingType))
        {
            return ForbiddenEffects.SyncOverAsync;
        }

        // GetResult() est porté par TaskAwaiter, pas par Task : on reconnaît le type
        // de l'awaiter plutôt que la chaîne d'appels.
        return method.Name == "GetResult" &&
               method.ContainingType?.Name.Contains("Awaiter") == true
            ? ForbiddenEffects.SyncOverAsync
            : null;
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var operation = (IPropertyReferenceOperation)context.Operation;
        var property = operation.Property;
        var type = FullName(property.ContainingType);

        var effect = (type, property.Name) switch
        {
            ("System.DateTime" or "System.DateTimeOffset", "Now" or "UtcNow" or "Today")
                => ForbiddenEffects.AmbientClock,
            ("System.Environment", "CurrentDirectory" or "MachineName" or "UserName")
                => ForbiddenEffects.Environment,
            _ when property.Name == "Result" && IsTask(property.ContainingType)
                => ForbiddenEffects.SyncOverAsync,
            _ => null,
        };

        if (effect is not null)
        {
            Report(context, effect, operation.Syntax.GetLocation(), $"{Simple(property.ContainingType)}.{property.Name}");
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var operation = (IObjectCreationOperation)context.Operation;

        // Un Random sans graine n'est pas rejouable ; avec graine, il l'est.
        if (FullName(operation.Type) == "System.Random" && operation.Arguments.Length == 0)
        {
            Report(context, ForbiddenEffects.UnseededRandom, operation.Syntax.GetLocation(), "new Random()");
        }
    }

    private static bool IsTask(INamedTypeSymbol? type) =>
        FullName(type) is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>"
            or "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask<TResult>";

    /// <summary>Nom pleinement qualifié, sans préfixe « global:: » ni arguments substitués.</summary>
    private static string? FullName(ITypeSymbol? type) =>
        type?.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);

    private static string Simple(ITypeSymbol? type) => type?.Name ?? "?";

    private static void Report(OperationAnalysisContext context, ForbiddenEffect effect, Location location, string usage)
    {
        context.ReportDiagnostic(Diagnostic.Create(Descriptors[effect.Id], location, usage, effect.Remedy));
    }
}
