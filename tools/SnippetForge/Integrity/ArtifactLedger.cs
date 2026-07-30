using System.Security.Cryptography;
using System.Text.Json;

namespace SnippetForge.Integrity;

/// <summary>Écart constaté entre le feed et le registre d'empreintes.</summary>
public enum IntegrityIssue
{
    /// <summary>Artefact présent dans le feed mais jamais enregistré : déposé à la main.</summary>
    Unregistered = 0,

    /// <summary>Empreinte différente de celle enregistrée : l'artefact a été modifié.</summary>
    Tampered = 1,

    /// <summary>Artefact enregistré mais absent du feed : suppression d'une version.</summary>
    Missing = 2,
}

/// <summary>Un écart, avec le nom de fichier concerné.</summary>
public sealed record IntegrityFinding(string FileName, IntegrityIssue Issue);

/// <summary>
/// Registre des empreintes SHA-256 des artefacts publiés (registry/artifacts.json).
///
/// Le geste du développeur ne change pas : <c>forge publish</c> enregistre l'empreinte
/// tout seul. Le registre ne sert qu'à répondre à une question que le système ne savait
/// pas poser : « ce .nupkg est-il bien celui qui est passé par la validation ? »
///
/// Ce n'est pas une signature cryptographique : quiconque peut écrire dans
/// <c>registry/</c> peut aussi y inscrire l'empreinte d'un artefact falsifié. Cela
/// détecte l'accident et le dépôt manuel, pas un adversaire déterminé. Sur un dépôt
/// partagé, c'est la signature NuGet qui joue ce rôle.
/// </summary>
public sealed class ArtifactLedger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly Dictionary<string, string> _hashes;

    private ArtifactLedger(string path, Dictionary<string, string> hashes)
    {
        _path = path;
        _hashes = hashes;
    }

    /// <summary>Empreintes enregistrées, par nom de fichier.</summary>
    public IReadOnlyDictionary<string, string> Hashes => _hashes;

    /// <summary>Charge le registre d'empreintes (vide s'il est absent ou illisible).</summary>
    public static ArtifactLedger Load(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var path = Path.Combine(root.RegistryDir, "artifacts.json");
        if (!File.Exists(path))
        {
            return new ArtifactLedger(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), JsonOptions);
            return new ArtifactLedger(
                path,
                new Dictionary<string, string>(hashes ?? [], StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return new ArtifactLedger(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>Enregistre l'empreinte d'un artefact fraîchement publié.</summary>
    public string Record(string nupkgPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nupkgPath);

        var hash = ComputeHash(nupkgPath);
        _hashes[Path.GetFileName(nupkgPath)] = hash;
        return hash;
    }

    /// <summary>
    /// Confronte le contenu du feed au registre. Le résultat est vide si tout concorde.
    /// </summary>
    public IReadOnlyList<IntegrityFinding> Verify(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var findings = new List<IntegrityFinding>();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root.FeedDir, "*.nupkg"))
        {
            var name = Path.GetFileName(file);
            present.Add(name);

            if (!_hashes.TryGetValue(name, out var expected))
            {
                findings.Add(new IntegrityFinding(name, IntegrityIssue.Unregistered));
                continue;
            }

            if (!string.Equals(ComputeHash(file), expected, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new IntegrityFinding(name, IntegrityIssue.Tampered));
            }
        }

        foreach (var name in _hashes.Keys.Where(k => !present.Contains(k)))
        {
            findings.Add(new IntegrityFinding(name, IntegrityIssue.Missing));
        }

        return findings.OrderBy(f => f.Issue).ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Enregistre les artefacts présents mais inconnus. Sert à adopter un feed
    /// antérieur à l'introduction du registre, sans avoir à tout republier.
    /// </summary>
    /// <returns>Le nombre d'artefacts adoptés.</returns>
    public int AdoptExisting(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var adopted = 0;
        foreach (var file in Directory.EnumerateFiles(root.FeedDir, "*.nupkg"))
        {
            if (_hashes.ContainsKey(Path.GetFileName(file)))
            {
                continue;
            }

            Record(file);
            adopted++;
        }

        return adopted;
    }

    /// <summary>Écrit le registre sur disque.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_hashes, JsonOptions));
    }

    /// <summary>Empreinte SHA-256 d'un fichier, en hexadécimal minuscule.</summary>
    public static string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Formule lisible d'un écart.</summary>
    public static string Describe(IntegrityFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return finding.Issue switch
        {
            IntegrityIssue.Unregistered =>
                $"{finding.FileName} : présent dans le feed mais jamais enregistré. " +
                "Déposé à la main sans passer par « forge publish » — donc sans validation ni tests.",
            IntegrityIssue.Tampered =>
                $"{finding.FileName} : empreinte différente de celle enregistrée. " +
                "L'artefact a été modifié après publication, ce que R8 interdit.",
            _ =>
                $"{finding.FileName} : enregistré mais absent du feed. " +
                "Une version publiée a été supprimée ; les projets qui l'épinglent ne compileront plus.",
        };
    }
}
