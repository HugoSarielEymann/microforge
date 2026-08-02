using SnippetForge.Integrity;

namespace SnippetForge.Remote;

/// <summary>Résultat d'une synchronisation depuis le dépôt d'équipe.</summary>
public sealed record PullResult(IReadOnlyList<string> Downloaded, IReadOnlyList<string> AlreadyPresent);

/// <summary>
/// Rapatrie des artefacts du dépôt d'équipe vers le feed local — le pendant de
/// <c>forge push</c>. Le modèle est celui d'un clone : chaque poste travaille sur sa
/// copie locale (rapide, hors ligne), le dépôt distant est le point de partage.
///
/// Deux formes de source : un dossier partagé (chemin ou UNC), copié directement, ou
/// un dépôt NuGet v3 HTTP, interrogé via son index de service.
/// </summary>
public static class FeedMirror
{
    /// <summary>
    /// Construit le chemin d'un artefact dans le feed, en refusant tout ce qui
    /// s'échapperait du dossier.
    ///
    /// Le contrôle n'est pas théorique : la version rapatriée provient du **dépôt
    /// distant** (elle est lue dans son index de versions). Un serveur compromis, ou
    /// simplement mal écrit, qui renverrait « ../../../autre » ferait écrire hors du
    /// feed. L'identifiant, lui, vient de la ligne de commande — même classe de risque.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si le nom sort du feed.</exception>
    public static string ResolveArtifactPath(ForgeRoot root, string packageId, string version)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var fileName = $"{packageId}.{version}.nupkg";

        // Un nom d'artefact est un segment unique : ni séparateur, ni remontée.
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            fileName.Contains("..", StringComparison.Ordinal) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException(
                $"Nom d'artefact refusé : « {fileName} ». Identifiant ou version invalide.");
        }

        var destination = Path.GetFullPath(Path.Combine(root.FeedDir, fileName));

        // Ceinture et bretelles : on vérifie aussi le chemin résolu.
        if (!Metrics.ConsumerRegistry.IsInside(root.FeedDir, destination))
        {
            throw new InvalidOperationException(
                $"Chemin d'artefact hors du feed : « {destination} ». Écriture refusée.");
        }

        return destination;
    }

    /// <summary>Vrai si la source est un dossier (chemin local ou partage réseau).</summary>
    public static bool IsFolderSource(string source) =>
        !string.IsNullOrWhiteSpace(source) &&
        !source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rapatrie depuis un dossier partagé. <paramref name="packageId"/> null = tout.
    /// Un artefact déjà présent localement n'est jamais réécrit : le feed est immuable.
    /// </summary>
    public static PullResult PullFromFolder(ForgeRoot root, string sourceDirectory, string? packageId)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        if (!Directory.Exists(sourceDirectory))
        {
            throw new ArgumentException($"Source introuvable : {sourceDirectory}", nameof(sourceDirectory));
        }

        var pattern = packageId is null ? "*.nupkg" : $"{packageId}.*.nupkg";
        var downloaded = new List<string>();
        var present = new List<string>();
        var ledger = ArtifactLedger.Load(root);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, pattern))
        {
            var name = Path.GetFileName(file);
            var destination = Path.Combine(root.FeedDir, name);

            if (File.Exists(destination))
            {
                present.Add(name);
                continue;
            }

            File.Copy(file, destination);
            ledger.Record(destination);
            downloaded.Add(name);
        }

        ledger.Save();
        return new PullResult(downloaded, present);
    }

    /// <summary>
    /// Rapatrie un package depuis un dépôt NuGet v3 HTTP. Version absente = la plus
    /// haute publiée. Retourne le nom de l'artefact, déjà présent ou téléchargé.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si la source ou le package est introuvable.</exception>
    public static async Task<PullResult> PullFromHttpAsync(
        ForgeRoot root,
        string sourceUrl,
        string packageId,
        string? version,
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(http);

        var serviceIndex = await http.GetStringAsync(sourceUrl, cancellationToken).ConfigureAwait(false);
        var baseAddress = NuGetV3.ParsePackageBaseAddress(serviceIndex)
            ?? throw new InvalidOperationException(
                $"La source {sourceUrl} n'expose pas de ressource {NuGetV3.PackageBaseAddressType}.");

        var versionsJson = await http
            .GetStringAsync(NuGetV3.VersionIndexUrl(baseAddress, packageId), cancellationToken)
            .ConfigureAwait(false);
        var versions = NuGetV3.ParseVersions(versionsJson);

        var wanted = version ?? SemVerLite.Latest(versions)
            ?? throw new InvalidOperationException($"Aucune version publiée pour {packageId} sur {sourceUrl}.");

        if (!versions.Contains(wanted, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Version {wanted} de {packageId} absente du dépôt. Disponibles : {string.Join(", ", versions)}.");
        }

        var destination = ResolveArtifactPath(root, packageId, wanted);
        var fileName = Path.GetFileName(destination);
        if (File.Exists(destination))
        {
            return new PullResult([], [fileName]);
        }

        var bytes = await http
            .GetByteArrayAsync(NuGetV3.DownloadUrl(baseAddress, packageId, wanted), cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);

        var ledger = ArtifactLedger.Load(root);
        ledger.Record(destination);
        ledger.Save();

        return new PullResult([fileName], []);
    }
}
