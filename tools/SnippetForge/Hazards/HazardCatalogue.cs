using System.Text.Json;

namespace SnippetForge.Hazards;

/// <summary>
/// Une classe d'entrées dangereuses, éprouvée une fois et réutilisable partout.
/// </summary>
/// <param name="Id">Identifiant stable, en minuscules avec tirets.</param>
/// <param name="Description">Ce que l'aléa désigne, en une phrase.</param>
/// <param name="Rationale">Pourquoi il mérite un test : le mode de défaillance évité.</param>
/// <param name="Examples">Valeurs ou situations concrètes à éprouver.</param>
public sealed record Hazard(string Id, string Description, string Rationale, IReadOnlyList<string> Examples);

/// <summary>
/// Catalogue partagé des aléas de test.
///
/// C'est la mémoire du système : quand un agent découvre qu'une capacité casse sur
/// une entrée particulière, l'aléa est consigné une fois et **toute** capacité de même
/// nature en hérite l'obligation. La liste ne se perd plus entre deux sessions, et
/// s'enrichit au lieu d'être redécouverte.
///
/// Le catalogue livré couvre les modes de défaillance classiques ; il est destiné à
/// grossir (<c>forge hazards add</c>). Le fichier <c>hazards.json</c> à la racine est
/// versionné avec le dépôt : c'est un acquis collectif, pas un état local.
/// </summary>
public sealed class HazardCatalogue
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Dictionary<string, Hazard> _hazards;

    private HazardCatalogue(IEnumerable<Hazard> hazards) =>
        _hazards = hazards.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Aléas connus, triés par identifiant.</summary>
    public IReadOnlyList<Hazard> All =>
        _hazards.Values.OrderBy(h => h.Id, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Nom du fichier de catalogue, à la racine de la forge.</summary>
    public const string FileName = "hazards.json";

    /// <summary>Clé du trait xUnit portant l'aléa couvert par un test.</summary>
    public const string TraitKey = "hazard";

    /// <summary>Aléas livrés d'origine : les modes de défaillance que l'on revoit sans cesse.</summary>
    public static IReadOnlyList<Hazard> BuiltIn { get; } =
    [
        new("null-input",
            "Entrée nulle atteignant la capacité.",
            "La référence nulle est le premier cas oublié ; le contrat doit dire s'il la refuse ou l'accepte.",
            ["null", "chaîne nulle dans une collection non nulle"]),

        new("empty-input",
            "Entrée vide mais non nulle.",
            "Vide et nul suivent souvent des chemins différents, et le vide passe plus facilement les gardes.",
            ["\"\"", "\"   \" (espaces seuls)", "tableau de longueur zéro"]),

        new("numeric-overflow",
            "Valeur hors des bornes du type cible.",
            "Un débordement transforme un cas d'erreur en exception inattendue, ou pire, en valeur fausse.",
            ["int.MaxValue", "valeur dépassant la capacité après multiplication", "somme de deux maxima"]),

        new("malformed-input",
            "Entrée syntaxiquement invalide.",
            "Une capacité de parsing doit échouer proprement, jamais par exception non documentée.",
            ["texte arbitraire", "format partiel", "séparateurs en trop"]),

        new("boundary-value",
            "Valeur exactement à la limite acceptée ou refusée.",
            "Les erreurs d'un cran (« off-by-one ») se logent précisément là.",
            ["zéro", "un", "longueur maximale exacte", "longueur maximale plus un"]),

        new("negative-value",
            "Valeur négative là où un positif est attendu.",
            "Un négatif non validé produit des tailles, index ou délais absurdes.",
            ["-1", "délai négatif", "compte négatif"]),

        new("empty-collection",
            "Séquence sans élément.",
            "Les agrégations et découpages sur ensemble vide renvoient souvent autre chose que prévu.",
            ["liste vide", "énumérable qui ne produit rien"]),

        new("unicode-edge",
            "Caractères hors ASCII : diacritiques, paires de substitution, sens d'écriture.",
            "Le découpage par caractère casse sur les paires de substitution ; les accents changent la longueur selon la normalisation.",
            ["émoji", "caractères combinés", "texte de droite à gauche", "formes NFC et NFD"]),

        new("cancellation",
            "Jeton d'annulation déjà déclenché, ou déclenché en cours d'exécution.",
            "Une opération qui ignore l'annulation bloque l'arrêt propre de l'application.",
            ["CancellationToken déjà annulé", "annulation pendant une attente"]),

        new("concurrent-access",
            "Appels simultanés sur la même instance ou le même état partagé.",
            "Un état statique mutable transforme une capacité pure en source de corruption.",
            ["deux tâches parallèles", "instance partagée entre threads"]),

        new("secret-leak",
            "Donnée sensible susceptible d'apparaître dans une sortie destinée à être journalisée.",
            "Un secret laissé en clair dans un journal est une fuite durable, souvent invisible en test nominal.",
            ["mot de passe dans une URL", "jeton en paramètre", "clé dans un message d'erreur"]),

        new("timezone-shift",
            "Décalage horaire, heure d'été, instants non locaux.",
            "Un calcul de date juste en UTC devient faux à l'heure locale, deux fois par an.",
            ["passage à l'heure d'été", "UTC contre heure locale", "date sans fuseau"]),
    ];

    /// <summary>
    /// Charge le catalogue : les aléas livrés, complétés et éventuellement redéfinis par
    /// <c>hazards.json</c> à la racine de la forge.
    /// </summary>
    public static HazardCatalogue Load(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var merged = BuiltIn.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(root.Path, FileName);

        if (File.Exists(path))
        {
            try
            {
                var custom = JsonSerializer.Deserialize<List<Hazard>>(File.ReadAllText(path), JsonOptions) ?? [];
                foreach (var hazard in custom)
                {
                    merged[hazard.Id] = hazard;
                }
            }
            catch (JsonException)
            {
                // Catalogue illisible : on retombe sur les aléas livrés plutôt que
                // d'interdire toute publication.
            }
        }

        return new HazardCatalogue(merged.Values);
    }

    /// <summary>Construit un catalogue à partir d'une liste explicite (tests).</summary>
    public static HazardCatalogue From(IEnumerable<Hazard> hazards)
    {
        ArgumentNullException.ThrowIfNull(hazards);
        return new HazardCatalogue(hazards);
    }

    /// <summary>Retourne l'aléa correspondant, ou null s'il est inconnu.</summary>
    public Hazard? Find(string id) =>
        string.IsNullOrWhiteSpace(id) ? null : _hazards.GetValueOrDefault(id);

    /// <summary>Indique si l'identifiant est connu du catalogue.</summary>
    public bool Contains(string id) => Find(id) is not null;

    /// <summary>
    /// Ajoute ou remplace un aléa et réécrit le fichier de catalogue.
    /// C'est le geste d'apprentissage : ce qu'un agent a découvert une fois est acquis.
    /// </summary>
    /// <exception cref="ArgumentException">Si l'identifiant n'est pas un slug valide.</exception>
    public void AddOrReplace(ForgeRoot root, Hazard hazard)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(hazard);

        if (!IsValidId(hazard.Id))
        {
            throw new ArgumentException(
                $"Identifiant d'aléa invalide : « {hazard.Id} ». Attendu : minuscules et tirets, ex. « null-input ».",
                nameof(hazard));
        }

        _hazards[hazard.Id] = hazard;

        // Seuls les aléas qui diffèrent des valeurs livrées sont persistés : le fichier
        // reste lisible et ne duplique pas ce que l'outil connaît déjà.
        var builtIn = BuiltIn.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase);
        var custom = _hazards.Values
            .Where(h => !builtIn.TryGetValue(h.Id, out var original) || original != h)
            .OrderBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        File.WriteAllText(Path.Combine(root.Path, FileName), JsonSerializer.Serialize(custom, JsonOptions));
    }

    /// <summary>Valide la forme d'un identifiant d'aléa.</summary>
    public static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') &&
        !id.StartsWith('-') &&
        !id.EndsWith('-');
}
