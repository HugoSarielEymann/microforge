using System.Reflection;

namespace SnippetForge;

/// <summary>
/// Version de l'outil. Sans elle, impossible de dire « corrigé en 0.2.1 », ni de
/// détecter qu'un registre a été écrit par une version antérieure au format courant.
/// </summary>
public static class ForgeVersion
{
    /// <summary>
    /// Version du format des fichiers de <c>registry/</c>. À incrémenter lorsqu'une
    /// évolution rend les fichiers existants illisibles ou trompeurs — les données
    /// dérivées seront alors régénérées automatiquement.
    /// </summary>
    public const int RegistryFormat = 1;

    /// <summary>Version sémantique de l'outil, lue dans l'assembly.</summary>
    public static string Current { get; } =
        typeof(ForgeVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? typeof(ForgeVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>Ligne d'identification affichée par <c>forge --version</c>.</summary>
    public static string Banner =>
        $"MicroForge {Current} (format de registre {RegistryFormat})";
}
