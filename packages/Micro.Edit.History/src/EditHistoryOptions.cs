namespace Micro.Edit.History;

/// <summary>Réglages d'un <see cref="EditHistory{TState}"/>.</summary>
/// <typeparam name="TState">Nature de l'état suivi.</typeparam>
public sealed class EditHistoryOptions<TState>
{
    /// <summary>Nombre d'annulations conservées. Défaut : 100.</summary>
    /// <remarks>
    /// Au-delà, les états les plus anciens sont oubliés. Une profondeur infinie retiendrait
    /// indéfiniment des états dont chacun peut peser lourd ; cent pas en arrière couvrent
    /// largement ce qu'un humain se rappelle avoir fait.
    /// </remarks>
    public int MaxDepth { get; init; } = 100;

    /// <summary>
    /// Comparateur décidant que deux états sont le même. Défaut : celui du type.
    /// </summary>
    /// <remarks>
    /// Sert à ignorer un enregistrement qui ne change rien. Un éditeur qui enregistre à chaque
    /// frappe repasse souvent par un état déjà atteint — un caractère tapé puis effacé — et
    /// sans ce filtre l'annulation semblerait ne rien faire.
    /// </remarks>
    public IEqualityComparer<TState> Comparer { get; init; } = EqualityComparer<TState>.Default;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxDepth"/> est nul ou négatif.</exception>
    /// <exception cref="ArgumentNullException">Si <see cref="Comparer"/> est nul.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDepth, 1);
        ArgumentNullException.ThrowIfNull(Comparer);
    }
}
