namespace Micro.Text.SearchMatch;

/// <summary>
/// Réglages de la confrontation entre une requête et les champs d'un enregistrement.
/// </summary>
/// <remarks>
/// Les défauts visent le cas courant d'un champ de recherche dans une interface : tolérant sur
/// la casse et les accents, exigeant sur la présence de chaque terme, et borné pour qu'un
/// copier-coller malheureux ne fasse pas dégénérer le filtrage.
/// </remarks>
public sealed class SearchMatchOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static SearchMatchOptions Default { get; } = new();

    /// <summary>Ignorer la casse. Défaut : <see langword="true"/>.</summary>
    public bool IgnoreCase { get; init; } = true;

    /// <summary>Ignorer les diacritiques : « resume » trouve « Résumé ». Défaut : <see langword="true"/>.</summary>
    public bool IgnoreDiacritics { get; init; } = true;

    /// <summary>
    /// Exiger que le terme forme un mot entier plutôt qu'un fragment. Défaut : <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Le défaut cherche le fragment parce qu'une recherche interactive se juge à la frappe :
    /// « comm » doit déjà proposer « commande ». Activer ce réglage pour un filtrage par
    /// mot-clé exact, où « chat » ne doit pas ramener « achat ».
    /// </remarks>
    public bool WholeWord { get; init; }

    /// <summary>Longueur minimale d'un terme retenu. Défaut : 1.</summary>
    /// <remarks>
    /// Porter ce seuil à 2 ou 3 écarte les termes trop courts pour discriminer quoi que ce
    /// soit, sans pour autant rejeter la requête entière : les autres termes restent actifs.
    /// </remarks>
    public int MinimumTermLength { get; init; } = 1;

    /// <summary>Nombre maximal de termes retenus dans une requête. Défaut : 12.</summary>
    /// <remarks>
    /// Une borne, pas une validation : au-delà, les termes supplémentaires sont ignorés plutôt
    /// que de faire échouer la recherche. Un texte collé par mégarde dans le champ ne doit pas
    /// coûter un parcours par mot sur chaque enregistrement.
    /// </remarks>
    public int MaximumTerms { get; init; } = 12;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <see cref="MinimumTermLength"/> est négatif, ou si <see cref="MaximumTerms"/> est
    /// nul ou négatif.
    /// </exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MinimumTermLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumTerms, 1);
    }
}
