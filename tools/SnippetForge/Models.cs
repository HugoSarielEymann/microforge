namespace SnippetForge;

/// <summary>
/// Métadonnées d'un micropackage. <paramref name="Language"/> vaut « csharp » par
/// défaut : c'est le seul écosystème capable de produire un artefact empaqueté.
/// </summary>
public sealed record PackageMeta(
    string Id,
    string Version,
    string Description,
    IReadOnlyList<string> Tags,
    string Authors,
    string Language = "csharp");

/// <summary>Entrée d'index pour un micropackage (dernière version + historique).</summary>
public sealed record IndexEntry(
    string Id,
    string LatestVersion,
    IReadOnlyList<string> AllVersions,
    string Description,
    IReadOnlyList<string> Tags,
    string Authors,
    string Readme,
    string Language = "csharp");

/// <summary>Document d'index sérialisé dans registry/index.json.</summary>
public sealed record IndexDocument(
    DateTime GeneratedUtc,
    IReadOnlyList<IndexEntry> Packages);

/// <summary>
/// Résultat de recherche. <paramref name="Score"/> est le score combiné dans [0, 1] ;
/// les composantes lexicale et sémantique sont conservées pour expliquer le classement.
/// </summary>
public sealed record SearchHit(IndexEntry Entry, double Score, double LexicalScore, double? SemanticScore);
