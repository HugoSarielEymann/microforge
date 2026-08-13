using System.Text.Json;
using System.Text.RegularExpressions;

namespace SnippetForge.Hazards;

/// <summary>Aléa déclaré par un package mais absent des tests.</summary>
public sealed record HazardGap(string HazardId, string Reason);

/// <summary>
/// Aléas déclarés par un micropackage, et vérification qu'ils sont éprouvés.
///
/// Le lien entre déclaration et preuve est un **trait xUnit** :
/// <c>[Trait("hazard", "numeric-overflow")]</c>. C'est ce qui rend l'obligation
/// mécaniquement vérifiable au lieu d'être un vœu — un aléa déclaré sans test marqué
/// fait échouer la validation.
///
/// La limite est assumée : rien n'empêche de marquer un test vide. Le trait prouve
/// l'intention, pas la profondeur — comme le reste du système.
/// </summary>
public static partial class HazardDeclaration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Nom du fichier de déclaration, à la racine du package.</summary>
    public const string FileName = "hazards.json";

    [GeneratedRegex(@"\[\s*Trait\s*\(\s*""hazard""\s*,\s*""([a-z0-9-]+)""\s*\)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex TraitRegex();

    /// <summary>
    /// Forme neutre pour les écosystèmes sans attribut : un commentaire
    /// <c># hazard: null-input</c> ou <c>// hazard: null-input</c>. Le mécanisme reste
    /// le même — déclarer, puis prouver — seule la syntaxe change.
    /// </summary>
    [GeneratedRegex(@"(?://|#)\s*hazard\s*:\s*([a-z0-9-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CommentMarkerRegex();

    /// <summary>Lit les aléas déclarés par un package (liste vide si aucun fichier).</summary>
    public static IReadOnlyList<string> Read(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        var path = Path.Combine(packageDirectory, FileName);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Analyse le contenu d'un fichier de déclaration. Deux formes acceptées : un
    /// tableau brut, ou un objet portant une propriété <c>hazards</c>.
    /// </summary>
    /// <exception cref="JsonException">Si le document n'est pas du JSON.</exception>
    public static IReadOnlyList<string> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        var array = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("hazards", out var property) ? property : default;

        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Écrit la déclaration d'un package.</summary>
    public static void Write(string packageDirectory, IReadOnlyList<string> hazardIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentNullException.ThrowIfNull(hazardIds);

        var payload = new Dictionary<string, IReadOnlyList<string>> { ["hazards"] = hazardIds };
        File.WriteAllText(
            Path.Combine(packageDirectory, FileName),
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <summary>Extrait les aléas couverts par des traits dans un corpus de tests.</summary>
    public static IReadOnlyList<string> CoveredBy(string testSources)
    {
        ArgumentNullException.ThrowIfNull(testSources);

        return TraitRegex().Matches(testSources)
            .Concat(CommentMarkerRegex().Matches(testSources))
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Confronte les aléas déclarés au catalogue et aux tests. Retourne les manquements :
    /// aléa inconnu du catalogue, ou déclaré sans test marqué.
    /// </summary>
    public static IReadOnlyList<HazardGap> Verify(
        IReadOnlyList<string> declared,
        string testSources,
        HazardCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(testSources);
        ArgumentNullException.ThrowIfNull(catalogue);

        var covered = CoveredBy(testSources);
        var gaps = new List<HazardGap>();

        foreach (var id in declared)
        {
            if (!catalogue.Contains(id))
            {
                gaps.Add(new HazardGap(
                    id,
                    $"aléa inconnu du catalogue. Les aléas connus : {string.Join(", ", catalogue.All.Select(h => h.Id))}. " +
                    $"Pour en ajouter un : forge hazards add {id} --description \"…\" --rationale \"…\""));
                continue;
            }

            if (!covered.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                gaps.Add(new HazardGap(
                    id,
                    $"déclaré mais aucun test ne porte [Trait(\"{HazardCatalogue.TraitKey}\", \"{id}\")]."));
            }
        }

        return gaps;
    }

    /// <summary>
    /// Aléas éprouvés par les tests sans avoir été déclarés. Ce n'est pas une faute :
    /// c'est une déclaration à compléter, et le signe que le corpus en sait plus que
    /// ses métadonnées.
    /// </summary>
    public static IReadOnlyList<string> UndeclaredButTested(IReadOnlyList<string> declared, string testSources)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(testSources);

        return CoveredBy(testSources)
            .Where(id => !declared.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
