namespace Micro.Media.ReleaseName;

/// <summary>
/// Ce qu'un nom de fichier vidéo dit de son contenu : un film (titre, année) ou un épisode
/// (série, saison, numéro, titre d'épisode), débarrassé de ses étiquettes techniques.
/// </summary>
/// <remarks>
/// Une information absente du nom reste <see langword="null"/> : le package ne devine pas une
/// saison qu'il ne lit pas (« Episode 12 » n'a pas de saison ; c'est à l'appelant de la tirer
/// du dossier « Saison 2 » s'il le souhaite).
/// </remarks>
public sealed class ReleaseInfo
{
    /// <summary>
    /// Titre du film, ou de la série pour un épisode, tel qu'écrit dans le nom (les points et
    /// soulignés d'un nom de release deviennent des espaces). Vide si le nom n'en porte pas,
    /// comme « S01E01 - Pilote.mkv ».
    /// </summary>
    public required string Title { get; init; }

    /// <summary>Année de sortie (film) ou de début (série), si le nom la porte.</summary>
    public int? Year { get; init; }

    /// <summary>Numéro de saison, si le nom le porte ; 0 désigne les épisodes spéciaux.</summary>
    public int? Season { get; init; }

    /// <summary>Numéro d'épisode, si le nom le porte.</summary>
    public int? Episode { get; init; }

    /// <summary>
    /// Dernier épisode d'un fichier qui en réunit plusieurs (« S01E01E02 », « S01E01-E02 ») ;
    /// <see langword="null"/> pour un épisode unique.
    /// </summary>
    public int? LastEpisode { get; init; }

    /// <summary>Titre de l'épisode, s'il suit le numéro ; sinon <see langword="null"/>.</summary>
    public string? EpisodeTitle { get; init; }

    /// <summary>
    /// Étiquettes techniques écartées du titre, dans l'ordre et l'écriture du nom
    /// (« 1080p », « WEB-DL », « x264 », « VOSTFR »…). Les noms de sites n'y figurent pas.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Le nom désigne-t-il un épisode (un numéro d'épisode a été lu) ?</summary>
    public bool IsEpisode => Episode is not null;
}
