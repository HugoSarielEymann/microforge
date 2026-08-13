using Xunit;

namespace Micro.Edit.History.Tests;

public sealed class HistoryTests
{
    private sealed class LongueurComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x?.Length == y?.Length;

        public int GetHashCode(string obj) => obj.Length;
    }

    // ==================== Cas nominal ====================

    [Fact]
    public void UnHistoriqueNeufNAniPasseNiFutur()
    {
        EditHistory<string> histoire = new("a");

        Assert.Equal("a", histoire.Current);
        Assert.False(histoire.CanUndo);
        Assert.False(histoire.CanRedo);
        Assert.Equal(0, histoire.UndoDepth);
        Assert.Equal(0, histoire.RedoDepth);
    }

    [Fact]
    public void EnregistrerAvanceLEtatCourant()
    {
        EditHistory<string> histoire = new("a");

        Assert.True(histoire.Record("b"));
        Assert.Equal("b", histoire.Current);
        Assert.True(histoire.CanUndo);
        Assert.Equal(1, histoire.UndoDepth);
    }

    [Fact]
    public void AnnulerPuisRetablirRevientAuMemeEtat()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");
        histoire.Record("c");

        Assert.True(histoire.TryUndo(out string apresAnnulation));
        Assert.Equal("b", apresAnnulation);
        Assert.Equal("b", histoire.Current);

        Assert.True(histoire.TryRedo(out string apresRetablissement));
        Assert.Equal("c", apresRetablissement);
        Assert.Equal("c", histoire.Current);
    }

    [Fact]
    public void AnnulerPlusieursFoisRemonteDansLOrdre()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");
        histoire.Record("c");
        histoire.Record("d");

        histoire.TryUndo(out _);
        histoire.TryUndo(out _);

        Assert.Equal("b", histoire.Current);
        Assert.Equal(2, histoire.RedoDepth);
    }

    [Fact]
    public void RetablirPlusieursFoisRedescendDansLOrdre()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");
        histoire.Record("c");
        histoire.TryUndo(out _);
        histoire.TryUndo(out _);

        histoire.TryRedo(out _);
        Assert.Equal("b", histoire.Current);

        histoire.TryRedo(out _);
        Assert.Equal("c", histoire.Current);
        Assert.False(histoire.CanRedo);
    }

    [Fact]
    public void LaDerniereAnnulationRamenALEtatInitial()
    {
        EditHistory<string> histoire = new("depart");
        histoire.Record("suite");

        Assert.True(histoire.TryUndo(out string etat));
        Assert.Equal("depart", etat);
        Assert.False(histoire.CanUndo);
    }

    // ==================== Le futur s'efface ====================

    [Fact]
    public void EnregistrerApresUneAnnulationAbandonneLaBrancheQuittee()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");
        histoire.Record("c");
        histoire.TryUndo(out _);

        Assert.True(histoire.CanRedo);
        histoire.Record("autre");

        Assert.False(histoire.CanRedo);
        Assert.Equal("autre", histoire.Current);
    }

    [Fact]
    public void UnEnregistrementIgnoreNEffacePasLeFutur()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");
        histoire.TryUndo(out _);

        // « a » est déjà l'état courant : rien n'a changé, le futur doit survivre.
        Assert.False(histoire.Record("a"));
        Assert.True(histoire.CanRedo);
    }

    // ==================== Rien à annuler ====================

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void AnnulerSansPasseNeChangeRien()
    {
        EditHistory<string> histoire = new("a");

        Assert.False(histoire.TryUndo(out string etat));
        Assert.Equal("a", etat);
        Assert.Equal("a", histoire.Current);
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void RetablirSansFuturNeChangeRien()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");

        Assert.False(histoire.TryRedo(out string etat));
        Assert.Equal("b", etat);
        Assert.Equal("b", histoire.Current);
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void LesTentativesNeLeventJamais()
    {
        EditHistory<string> histoire = new("a");

        // Un TryXxx ne lève pas, même appelé en boucle sur un historique épuisé.
        for (int i = 0; i < 5; i++)
        {
            Assert.False(histoire.TryUndo(out _));
            Assert.False(histoire.TryRedo(out _));
        }
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void LesTentativesNeLeventJamais_MemeDansUneSuiteAberrante()
    {
        // Le contrat TryXxx est absolu : quelle que soit la suite d'appels — au-delà des
        // deux bouts, à ras de la profondeur, autour d'une reprise — aucune tentative ne
        // lève. L'appelant branche ces méthodes sur un raccourci clavier ; une exception
        // remonterait dans la boucle de messages.
        EditHistory<string> histoire = new("a", new EditHistoryOptions<string> { MaxDepth = 2 });

        Exception? echec = Record.Exception(() =>
        {
            for (int tour = 0; tour < 3; tour++)
            {
                for (int i = 0; i < 6; i++)
                {
                    histoire.TryUndo(out _);
                    histoire.TryRedo(out _);
                    histoire.TryUndo(out _);
                }

                histoire.Record($"etat {tour}");
                histoire.TryRedo(out _);
                histoire.Reset("reprise");
                histoire.TryUndo(out _);
                histoire.TryRedo(out _);
            }
        });

        Assert.Null(echec);
    }

    // ==================== Dédoublonnage ====================

    [Fact]
    public void EnregistrerLEtatCourantEstIgnore()
    {
        EditHistory<string> histoire = new("a");

        Assert.False(histoire.Record("a"));
        Assert.False(histoire.CanUndo);
        Assert.Equal(0, histoire.UndoDepth);
    }

    [Fact]
    public void UnComparateurSurMesureDecideDeLEgalite()
    {
        EditHistory<string> histoire = new("abc", new EditHistoryOptions<string>
        {
            Comparer = new LongueurComparer(),
        });

        // Même longueur : pour ce comparateur, rien n'a changé.
        Assert.False(histoire.Record("xyz"));
        Assert.Equal("abc", histoire.Current);

        Assert.True(histoire.Record("abcd"));
        Assert.Equal("abcd", histoire.Current);
    }

    [Fact]
    public void UnEtatDejaVuMaisPasCourantEstBienRetenu()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");

        // « a » n'est plus l'état courant : y revenir est un vrai changement.
        Assert.True(histoire.Record("a"));
        Assert.Equal(2, histoire.UndoDepth);
    }

    // ==================== Profondeur ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void AuDelaDeLaProfondeurLesPlusAnciensSontOublies()
    {
        EditHistory<int> histoire = new(0, new EditHistoryOptions<int> { MaxDepth = 3 });

        for (int i = 1; i <= 10; i++)
        {
            histoire.Record(i);
        }

        Assert.Equal(3, histoire.UndoDepth);

        histoire.TryUndo(out _);
        histoire.TryUndo(out _);
        histoire.TryUndo(out _);

        Assert.Equal(7, histoire.Current);
        Assert.False(histoire.CanUndo);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneProfondeurDeUnNeRetientQueLePasImmediat()
    {
        EditHistory<int> histoire = new(0, new EditHistoryOptions<int> { MaxDepth = 1 });
        histoire.Record(1);
        histoire.Record(2);

        Assert.Equal(1, histoire.UndoDepth);
        Assert.True(histoire.TryUndo(out int etat));
        Assert.Equal(1, etat);
        Assert.False(histoire.CanUndo);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ExactementALaProfondeurRienNEstOublie()
    {
        EditHistory<int> histoire = new(0, new EditHistoryOptions<int> { MaxDepth = 3 });
        histoire.Record(1);
        histoire.Record(2);
        histoire.Record(3);

        Assert.Equal(3, histoire.UndoDepth);

        histoire.TryUndo(out _);
        histoire.TryUndo(out _);
        histoire.TryUndo(out _);

        Assert.Equal(0, histoire.Current);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneProfondeurNulleOuNegativeEstRefusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EditHistoryOptions<int> { MaxDepth = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EditHistory<int>(0, new EditHistoryOptions<int> { MaxDepth = -2 }));
    }

    // ==================== Reprise ====================

    [Fact]
    public void ReprendreOublieLePasseEtLeFutur()
    {
        EditHistory<string> histoire = new("a");
        histoire.Record("b");
        histoire.TryUndo(out _);

        histoire.Reset("autre document");

        Assert.Equal("autre document", histoire.Current);
        Assert.False(histoire.CanUndo);
        Assert.False(histoire.CanRedo);
    }

    // ==================== Entrées nulles ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnEtatInitialNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => new EditHistory<string>(null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnEtatEnregistreNulEstRefuse()
    {
        EditHistory<string> histoire = new("a");

        Assert.Throws<ArgumentNullException>(() => histoire.Record(null!));
        Assert.Throws<ArgumentNullException>(() => histoire.Reset(null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnComparateurNulEstRefuse()
    {
        EditHistoryOptions<string> options = new() { Comparer = null! };

        Assert.Throws<ArgumentNullException>(() => options.Validate());
        Assert.Throws<ArgumentNullException>(() => new EditHistory<string>("a", options));
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        EditHistoryOptions<string> defauts = new();
        defauts.Validate();

        Assert.Equal(100, defauts.MaxDepth);
        Assert.Same(EqualityComparer<string>.Default, defauts.Comparer);
    }

    // ==================== Suites d'opérations ====================

    [Fact]
    public void UneLongueSuiteDOperationsResteCoherente()
    {
        EditHistory<int> histoire = new(0);

        for (int i = 1; i <= 50; i++)
        {
            histoire.Record(i);
        }

        for (int i = 0; i < 20; i++)
        {
            histoire.TryUndo(out _);
        }

        Assert.Equal(30, histoire.Current);

        for (int i = 0; i < 20; i++)
        {
            histoire.TryRedo(out _);
        }

        Assert.Equal(50, histoire.Current);
        Assert.False(histoire.CanRedo);
        Assert.Equal(50, histoire.UndoDepth);
    }
}
