namespace Micro.Edit.History;

/// <summary>
/// Historique d'édition d'une valeur : retient les états successifs, permet de revenir en
/// arrière et de repartir en avant.
/// </summary>
/// <typeparam name="TState">
/// Nature de l'état suivi. Doit être <em>immuable</em>, ou au moins recopié avant d'être
/// enregistré : l'historique conserve les références telles quelles, et une valeur mutée
/// après coup réécrirait le passé.
/// </typeparam>
/// <remarks>
/// Le modèle est celui de l'état complet, pas celui de la commande inversible : on enregistre
/// ce que la valeur <em>est</em> après chaque changement, jamais comment on y est arrivé. C'est
/// plus coûteux en mémoire, mais ça ne peut pas diverger — une commande dont l'inverse est
/// légèrement faux corrompt tout ce qui suit, sans que rien ne le signale.
///
/// Cette classe n'est pas sûre face à des appels simultanés : elle suit une édition, et une
/// édition a un seul auteur à la fois.
/// </remarks>
public sealed class EditHistory<TState>
{
    private readonly List<TState> _past = [];
    private readonly List<TState> _future = [];
    private readonly EditHistoryOptions<TState> _options;
    private TState _current;

    /// <summary>Ouvre un historique sur un état initial.</summary>
    /// <param name="initial">État de départ, celui vers lequel la dernière annulation ramène.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <exception cref="ArgumentNullException">Si l'état initial est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public EditHistory(TState initial, EditHistoryOptions<TState>? options = null)
    {
        ArgumentNullException.ThrowIfNull(initial);

        _options = options ?? new EditHistoryOptions<TState>();
        _options.Validate();
        _current = initial;
    }

    /// <summary>État courant.</summary>
    public TState Current => _current;

    /// <summary>Une annulation est possible.</summary>
    public bool CanUndo => _past.Count > 0;

    /// <summary>Un rétablissement est possible.</summary>
    public bool CanRedo => _future.Count > 0;

    /// <summary>Nombre d'annulations encore disponibles.</summary>
    public int UndoDepth => _past.Count;

    /// <summary>Nombre de rétablissements encore disponibles.</summary>
    public int RedoDepth => _future.Count;

    /// <summary>Enregistre un nouvel état comme état courant.</summary>
    /// <param name="state">Nouvel état.</param>
    /// <returns>
    /// <see langword="true"/> si l'état a été retenu ; <see langword="false"/> s'il était égal
    /// au courant et a donc été ignoré.
    /// </returns>
    /// <exception cref="ArgumentNullException">Si l'état est nul.</exception>
    /// <remarks>
    /// Enregistrer efface le futur : après être revenu en arrière, repartir dans une autre
    /// direction abandonne la branche qu'on venait de quitter. C'est ce que fait tout éditeur,
    /// et c'est ce que l'utilisateur attend — un historique arborescent demanderait de choisir
    /// quelle branche rétablir, question qu'aucun raccourci clavier ne sait poser.
    /// </remarks>
    public bool Record(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Un état identique au courant n'apporte rien : le retenir obligerait à appuyer
        // deux fois sur « annuler » pour voir un seul changement revenir.
        if (_options.Comparer.Equals(_current, state))
        {
            return false;
        }

        _past.Add(_current);
        _future.Clear();
        _current = state;

        // La profondeur borne le passé, pas le futur : le futur est déjà borné par le passé
        // dont il est issu.
        if (_past.Count > _options.MaxDepth)
        {
            _past.RemoveRange(0, _past.Count - _options.MaxDepth);
        }

        return true;
    }

    /// <summary>Revient à l'état précédent.</summary>
    /// <param name="state">L'état restauré, ou l'état courant inchangé si rien n'était annulable.</param>
    /// <returns><see langword="true"/> si une annulation a eu lieu.</returns>
    public bool TryUndo(out TState state)
    {
        if (_past.Count == 0)
        {
            state = _current;
            return false;
        }

        _future.Add(_current);
        _current = _past[^1];
        _past.RemoveAt(_past.Count - 1);

        state = _current;
        return true;
    }

    /// <summary>Repart vers l'état suivant, après une annulation.</summary>
    /// <param name="state">L'état restauré, ou l'état courant inchangé si rien n'était rétablissable.</param>
    /// <returns><see langword="true"/> si un rétablissement a eu lieu.</returns>
    public bool TryRedo(out TState state)
    {
        if (_future.Count == 0)
        {
            state = _current;
            return false;
        }

        _past.Add(_current);
        _current = _future[^1];
        _future.RemoveAt(_future.Count - 1);

        state = _current;
        return true;
    }

    /// <summary>Repart d'un état neuf, en oubliant tout le passé et tout le futur.</summary>
    /// <param name="state">Nouvel état de départ.</param>
    /// <exception cref="ArgumentNullException">Si l'état est nul.</exception>
    /// <remarks>
    /// À appeler quand l'objet édité change d'identité — un autre fichier, un autre document.
    /// Sans cela, une annulation ramènerait l'état du document précédent dans celui qu'on
    /// vient d'ouvrir.
    /// </remarks>
    public void Reset(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _past.Clear();
        _future.Clear();
        _current = state;
    }
}
