namespace Micro.Media.ReleaseName;

/// <summary>
/// Réglages de l'analyse d'un nom de fichier vidéo par <see cref="ReleaseNameParser"/>.
/// </summary>
/// <remarks>
/// Les défauts conviennent à une vidéothèque ordinaire : extensions vidéo et sous-titres
/// courantes, années plausibles d'un film (1888 à 2099), noms de fichiers bornés à 1024
/// caractères. Une instance se partage : elle ne change pas après sa construction.
/// </remarks>
public sealed class ReleaseNameOptions
{
    /// <summary>Extensions reconnues par défaut (sans le point, casse ignorée).</summary>
    public static IReadOnlyCollection<string> DefaultExtensions { get; } =
    [
        "mkv", "mp4", "m4v", "avi", "mov", "wmv", "webm", "ts", "m2ts", "mts", "mpg", "mpeg",
        "flv", "ogv", "ogm", "3gp", "divx", "vob", "iso", "rmvb", "srt", "ass", "ssa", "sub",
        "idx", "vtt", "nfo",
    ];

    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static ReleaseNameOptions Default { get; } = new();

    /// <summary>
    /// Extensions retirées de la fin du nom avant l'analyse, sans le point, casse ignorée.
    /// Défaut : <see cref="DefaultExtensions"/>.
    /// </summary>
    /// <remarks>
    /// Seule une extension connue est retirée : dans « Blade.Runner.1982.1080p », le dernier
    /// segment « 1080p » n'est pas une extension, et un retrait aveugle le perdrait.
    /// </remarks>
    public IReadOnlyCollection<string> Extensions { get; init; } = DefaultExtensions;

    /// <summary>Plus petite année reconnue comme année de sortie. Défaut : 1888.</summary>
    public int MinimumYear { get; init; } = 1888;

    /// <summary>Plus grande année reconnue comme année de sortie. Défaut : 2099.</summary>
    /// <remarks>
    /// Le package ne lit pas l'horloge. Un appelant qui la connaît a intérêt à poser l'année
    /// courante plus un : « Blade.Runner.2049.1080p » garde alors 2049 dans son titre au lieu
    /// d'en faire une année de sortie.
    /// </remarks>
    public int MaximumYear { get; init; } = 2099;

    /// <summary>Longueur maximale d'un nom analysé, en caractères. Défaut : 1024.</summary>
    /// <remarks>Un nom de fichier Windows ne dépasse pas 255 caractères ; au-delà, c'est une erreur d'appel.</remarks>
    public int MaximumLength { get; init; } = 1024;

    /// <summary>
    /// Étiquettes supplémentaires à traiter comme techniques (casse ignorée, mot entier) :
    /// elles coupent le titre comme « 1080p » ou « x264 ». Défaut : aucune.
    /// </summary>
    /// <remarks>
    /// Pour un site ou un groupe qui revient dans une collection (« MoviezVerse »), ou une
    /// étiquette maison. Un mot qui peut appartenir à un vrai titre n'a rien à faire ici.
    /// </remarks>
    public IReadOnlyCollection<string> ExtraTags { get; init; } = [];

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si une année sort de 1000–9999, si <see cref="MaximumYear"/> précède
    /// <see cref="MinimumYear"/>, ou si <see cref="MaximumLength"/> est nul ou négatif.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Si <see cref="Extensions"/> ou <see cref="ExtraTags"/> est nul, ou contient un élément
    /// nul ou blanc.
    /// </exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MinimumYear, 1000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MinimumYear, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumYear, MinimumYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumYear, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumLength, 1);

        if (!AreWords(Extensions))
        {
            throw new ArgumentException("Les extensions doivent être une liste de mots non vides.", nameof(Extensions));
        }

        if (!AreWords(ExtraTags))
        {
            throw new ArgumentException("Les étiquettes supplémentaires doivent être une liste de mots non vides.", nameof(ExtraTags));
        }
    }

    /// <summary>Même contrôle que <see cref="Validate"/>, sans lever.</summary>
    internal bool IsValid() =>
        MinimumYear is >= 1000 and <= 9999
        && MaximumYear >= MinimumYear
        && MaximumYear <= 9999
        && MaximumLength >= 1
        && AreWords(Extensions)
        && AreWords(ExtraTags);

    private static bool AreWords(IReadOnlyCollection<string>? words) =>
        words is not null && words.All(word => !string.IsNullOrWhiteSpace(word));
}
