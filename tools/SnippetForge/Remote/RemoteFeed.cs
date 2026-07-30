using System.Text.Json;

namespace SnippetForge.Remote;

/// <summary>Configuration d'un dépôt NuGet distant partagé par une équipe.</summary>
public sealed record RemoteFeedConfig(string Source, string? ApiKeyEnvironmentVariable)
{
    /// <summary>Variable d'environnement par défaut portant la clé d'API.</summary>
    public const string DefaultApiKeyVariable = "MICROFORGE_API_KEY";

    /// <summary>Nom effectif de la variable d'environnement à lire.</summary>
    public string ApiKeyVariable => ApiKeyEnvironmentVariable ?? DefaultApiKeyVariable;
}

/// <summary>
/// Publication vers un dépôt NuGet partagé (BaGet, Azure Artifacts, GitHub Packages…).
///
/// Le feed local reste la source de vérité du poste : un package est d'abord validé,
/// testé et publié localement, puis **poussé** vers le dépôt d'équipe. Cet ordre est
/// délibéré — le dépôt partagé ne doit recevoir que des artefacts déjà éprouvés.
///
/// La clé d'API n'est jamais stockée dans la configuration : seul le **nom** de la
/// variable d'environnement qui la porte l'est.
/// </summary>
public static class RemoteFeed
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Chemin du fichier de configuration du dépôt distant.</summary>
    public static string ConfigPath(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Path.Combine(root.RegistryDir, "remote.json");
    }

    /// <summary>
    /// Charge la configuration : fichier <c>registry/remote.json</c>, ou variable
    /// d'environnement <c>MICROFORGE_REMOTE</c> qui la surcharge. Null si aucune.
    /// </summary>
    public static RemoteFeedConfig? Load(ForgeRoot root)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("MICROFORGE_REMOTE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new RemoteFeedConfig(fromEnvironment, null);
        }

        var path = ConfigPath(root);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RemoteFeedConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Enregistre la configuration du dépôt distant.</summary>
    public static void Save(ForgeRoot root, RemoteFeedConfig config)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Source);

        Directory.CreateDirectory(root.RegistryDir);
        File.WriteAllText(ConfigPath(root), JsonSerializer.Serialize(config, JsonOptions));
    }

    /// <summary>
    /// Construit la ligne de commande <c>dotnet nuget push</c>. La clé d'API est
    /// résolue par l'appelant et n'apparaît jamais dans les traces affichées.
    /// </summary>
    public static string BuildPushArguments(string nupkgPath, RemoteFeedConfig config, string? apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nupkgPath);
        ArgumentNullException.ThrowIfNull(config);

        var arguments = $"nuget push \"{nupkgPath}\" --source \"{config.Source}\" --skip-duplicate";
        return string.IsNullOrWhiteSpace(apiKey) ? arguments : $"{arguments} --api-key {apiKey}";
    }

    /// <summary>Masque une clé d'API pour l'affichage.</summary>
    public static string MaskApiKey(string? apiKey) =>
        string.IsNullOrEmpty(apiKey) ? "(aucune)"
        : apiKey.Length <= 4 ? "****"
        : $"****{apiKey[^4..]}";
}
