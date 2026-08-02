using System.Text.Json;

namespace SnippetForge.Telemetry;

/// <summary>Une invocation de l'outil, telle qu'elle a eu lieu.</summary>
public sealed record UsageEntry(DateTime TimestampUtc, string Command, string Arguments, int ExitCode);

/// <summary>
/// Journal des invocations (registry/usage.log, une ligne JSON par commande).
///
/// C'est la preuve la plus directe que le workflow est réellement suivi : « l'agent
/// a-t-il cherché avant d'écrire ? » se lit dans ce journal, pas dans le code produit.
/// <c>forge bench</c> s'en sert pour attribuer à chaque manche de test les commandes
/// qu'elle a déclenchées.
/// </summary>
public static class UsageLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Chemin du journal.</summary>
    public static string PathFor(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return System.IO.Path.Combine(root.RegistryDir, "usage.log");
    }

    /// <summary>Position courante de fin de journal (0 si absent) — un signet pour ReadSince.</summary>
    public static long CurrentPosition(ForgeRoot root)
    {
        var path = PathFor(root);
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    /// <summary>
    /// Consigne une invocation. Toute défaillance du journal est silencieuse : la
    /// télémetrie ne doit jamais faire échouer la commande qu'elle observe.
    /// </summary>
    public static void Append(ForgeRoot root, string command, IReadOnlyList<string> args, int exitCode)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            Directory.CreateDirectory(root.RegistryDir);
            var entry = new UsageEntry(DateTime.UtcNow, command, Sanitize(args), exitCode);
            File.AppendAllText(PathFor(root), JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        }
        catch (IOException)
        {
            // Journal verrouillé ou disque plein : l'invocation reste prioritaire.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }

    /// <summary>Options portant une valeur sensible, masquée avant écriture.</summary>
    private static readonly string[] SensitiveOptions = ["--api-key", "--password", "--token"];

    /// <summary>
    /// Masque ce qui ne doit pas atterrir dans un fichier en clair.
    ///
    /// Le journal est un fichier ordinaire du registre, susceptible d'être partagé ou
    /// sauvegardé. Or une commande peut porter un secret sans qu'on y pense :
    /// <c>forge remote --source https://user:motdepasse@depot</c> écrirait le mot de
    /// passe tel quel.
    /// </summary>
    public static string Sanitize(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var masked = new List<string>(args.Count);
        var maskNext = false;

        foreach (var arg in args)
        {
            if (maskNext)
            {
                masked.Add("***");
                maskNext = false;
                continue;
            }

            maskNext = SensitiveOptions.Contains(arg, StringComparer.OrdinalIgnoreCase);
            masked.Add(MaskUrlCredentials(arg));
        }

        return string.Join(' ', masked);
    }

    /// <summary>Remplace le mot de passe d'une URL « schéma://user:motdepasse@hôte ».</summary>
    private static string MaskUrlCredentials(string value)
    {
        var schemeEnd = value.IndexOf("//", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return value;
        }

        var authorityStart = schemeEnd + 2;
        var authorityEnd = value.AsSpan(authorityStart).IndexOfAny('/', '?', '#');
        var limit = authorityEnd < 0 ? value.Length : authorityStart + authorityEnd;

        var at = value.LastIndexOf('@', Math.Max(0, limit - 1));
        if (at < authorityStart)
        {
            return value;
        }

        var colon = value.AsSpan(authorityStart, at - authorityStart).IndexOf(':');
        return colon < 0
            ? value
            : string.Concat(value.AsSpan(0, authorityStart + colon + 1), "***", value.AsSpan(at));
    }

    /// <summary>Lit les invocations enregistrées après <paramref name="position"/>.</summary>
    public static IReadOnlyList<UsageEntry> ReadSince(ForgeRoot root, long position)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        var path = PathFor(root);
        if (!File.Exists(path))
        {
            return [];
        }

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (position >= stream.Length)
        {
            return [];
        }

        stream.Seek(position, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);

        var entries = new List<UsageEntry>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<UsageEntry>(line, JsonOptions) is { } entry)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // Ligne tronquée (écriture concurrente) : on l'ignore plutôt que d'échouer.
            }
        }

        return entries;
    }
}
