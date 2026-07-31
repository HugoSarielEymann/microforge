using System.Text.RegularExpressions;
using SnippetForge.Api;

namespace SnippetForge.Quality;

/// <summary>Nature d'une remarque de revue.</summary>
public enum ReviewSeverity
{
    /// <summary>Point à examiner : l'outil ne peut pas trancher, un relecteur le peut.</summary>
    Question = 0,

    /// <summary>Absence constatée : un élément public n'apparaît nulle part dans les tests.</summary>
    Gap = 1,
}

/// <summary>Une remarque adressée au relecteur.</summary>
public sealed record ReviewFinding(ReviewSeverity Severity, string Category, string Subject, string Question);

/// <summary>
/// Prépare la relecture d'un micropackage fraîchement forgé.
///
/// Le validateur garantit que des tests existent et passent — pas qu'ils couvrent
/// les bons cas. C'est la limite mesurée en conditions réelles : deux packages forgés
/// par un agent, deux défauts sur des cas limites non testés (un <c>TryParse</c> qui
/// levait, un masquage de secrets incomplet).
///
/// Cette analyse ne tranche rien : elle **rassemble les questions** qu'un relecteur —
/// humain, ou un second agent distinct de celui qui a forgé — doit se poser. Les
/// remarques sont heuristiques et peuvent être sans objet ; leur valeur est de
/// diriger l'attention, pas de rendre un verdict.
/// </summary>
public static partial class ReviewAnalyzer
{
    [GeneratedRegex(@"<exception\s+cref\s*=\s*""(?:T:)?([A-Za-z0-9_.]+)""")]
    private static partial Regex DocumentedExceptionRegex();

    [GeneratedRegex(@"^(method|property|ctor|field)\s+([A-Za-z0-9_.<>`]+?)\.([A-Za-z0-9_]+)(?:`\d+)?\s*[(:]")]
    private static partial Regex MemberRegex();

    private static readonly string[] NumericTypes =
        ["System.Int32", "System.Int64", "System.Double", "System.Decimal", "System.Single"];

    /// <summary>
    /// Cherche un identifiant en tant que mot entier. Une simple recherche de
    /// sous-chaîne ferait passer « Parse » pour couvert dès que « TryParse » apparaît —
    /// une absence réelle deviendrait invisible, soit le sens dangereux de l'erreur.
    /// </summary>
    private static bool Mentions(string text, string identifier) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(identifier)}\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// Confronte le contrat public au code de test. <paramref name="sourceText"/> et
    /// <paramref name="testText"/> sont les sources concaténées de src/ et tests/.
    /// </summary>
    public static IReadOnlyList<ReviewFinding> Analyze(ApiSurface surface, string sourceText, string testText)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(testText);

        var findings = new List<ReviewFinding>();

        findings.AddRange(UncoveredMembers(surface, testText));
        findings.AddRange(UndocumentedTryPatterns(surface, testText));
        findings.AddRange(UntestedDocumentedExceptions(sourceText, testText));
        findings.AddRange(NumericBoundaries(surface, testText));

        return findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Membres publics dont le nom n'apparaît nulle part dans les tests.</summary>
    private static IEnumerable<ReviewFinding> UncoveredMembers(ApiSurface surface, string testText)
    {
        foreach (var (kind, member, name) in Members(surface))
        {
            // Les accesseurs générés et les constructeurs sans paramètre ne portent pas
            // de comportement propre : les signaler noierait les vraies absences.
            if (kind == "ctor" && member.EndsWith("()", StringComparison.Ordinal))
            {
                continue;
            }

            if (!Mentions(testText, name))
            {
                yield return new ReviewFinding(
                    ReviewSeverity.Gap,
                    "couverture",
                    name,
                    $"Le membre public « {name} » n'est cité dans aucun test. " +
                    "Est-il réellement destiné au public, ou faut-il le couvrir ?");
            }
        }
    }

    /// <summary>
    /// Un <c>TryXxx</c> ne doit jamais lever, quelle que soit l'entrée : c'est le
    /// contrat du motif. C'est précisément ce qui avait échappé à la manche 4.
    /// </summary>
    private static IEnumerable<ReviewFinding> UndocumentedTryPatterns(ApiSurface surface, string testText)
    {
        foreach (var (kind, _, name) in Members(surface))
        {
            if (kind != "method" || !name.StartsWith("Try", StringComparison.Ordinal))
            {
                continue;
            }

            if (!Mentions(testText, name))
            {
                continue; // déjà signalé comme absence de couverture
            }

            yield return new ReviewFinding(
                ReviewSeverity.Question,
                "contrat TryXxx",
                name,
                $"« {name} » suit le motif TryXxx : il ne doit **jamais** lever, même sur " +
                "entrée aberrante (valeur hors bornes, chaîne absurde, nul). Un test le " +
                "vérifie-t-il explicitement ?");
        }
    }

    /// <summary>Exceptions documentées dans le contrat XML, absentes des tests.</summary>
    private static IEnumerable<ReviewFinding> UntestedDocumentedExceptions(string sourceText, string testText)
    {
        var documented = DocumentedExceptionRegex()
            .Matches(sourceText)
            .Select(m => m.Groups[1].Value.Split('.')[^1])
            .Distinct(StringComparer.Ordinal);

        foreach (var exception in documented)
        {
            if (!Mentions(testText, exception))
            {
                yield return new ReviewFinding(
                    ReviewSeverity.Gap,
                    "exception documentée",
                    exception,
                    $"La documentation annonce « {exception} », mais aucun test ne la " +
                    "provoque. Le contrat est-il exact ?");
            }
        }
    }

    /// <summary>Signatures prenant un type numérique : les bornes sont-elles éprouvées ?</summary>
    private static IEnumerable<ReviewFinding> NumericBoundaries(ApiSurface surface, string testText)
    {
        var mentionsBoundary =
            testText.Contains("MaxValue", StringComparison.Ordinal) ||
            testText.Contains("MinValue", StringComparison.Ordinal) ||
            testText.Contains("Overflow", StringComparison.Ordinal);

        if (mentionsBoundary)
        {
            yield break;
        }

        foreach (var (kind, member, name) in Members(surface))
        {
            if (kind != "method" || !NumericTypes.Any(t => member.Contains(t, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return new ReviewFinding(
                ReviewSeverity.Question,
                "bornes numériques",
                name,
                $"« {name} » manipule des nombres, et aucun test ne mentionne de valeur " +
                "extrême. Zéro, négatif, MaxValue et débordement sont-ils couverts ?");
        }
    }

    /// <summary>Décompose les membres du contrat en (nature, signature, nom simple).</summary>
    private static IEnumerable<(string Kind, string Member, string Name)> Members(ApiSurface surface)
    {
        foreach (var member in surface.Members)
        {
            var match = MemberRegex().Match(member);
            if (match.Success)
            {
                yield return (match.Groups[1].Value, member, match.Groups[3].Value);
            }
        }
    }
}
