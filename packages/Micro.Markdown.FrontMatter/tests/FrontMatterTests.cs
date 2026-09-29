using Xunit;

namespace Micro.Markdown.FrontMatter.Tests;

public sealed class FrontMatterTests
{
    // ==================== Repérage ====================

    [Fact]
    public void UnEnTeteRefermeEstSepareDuCorps()
    {
        string text = "---\ntitre: Plan\n---\n# Corps";

        FrontMatterBlock block = FrontMatterReader.Read(text);

        Assert.True(block.Exists);
        Assert.Equal("titre: Plan", block.Content);
        Assert.Equal(4, block.ContentStart);
        Assert.Equal(11, block.ContentLength);
        Assert.Equal("# Corps", text[block.BodyStart..]);
        Assert.Equal(block.Length, block.BodyStart);
    }

    [Fact]
    public void LesFinsDeLigneWindowsSontAcceptees()
    {
        string text = "---\r\na: 1\r\nb: 2\r\n---\r\ncorps";

        FrontMatterBlock block = FrontMatterReader.Read(text);

        Assert.Equal("a: 1\r\nb: 2", block.Content);
        Assert.Equal("corps", text[block.BodyStart..]);
        Assert.Equal(["1"], block.GetValues("a"));
        Assert.Equal(["2"], block.GetValues("b"));
    }

    [Fact]
    public void UnEnTeteVideExiste()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\n---\ncorps");

        Assert.True(block.Exists);
        Assert.Equal(string.Empty, block.Content);
        Assert.Empty(block.Properties);
        Assert.Equal(8, block.BodyStart);
    }

    [Fact]
    public void UnEnTeteQuiFinitLeDocumentEstAccepte()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\na: 1\n---");

        Assert.True(block.Exists);
        Assert.Equal(12, block.BodyStart);
    }

    [Fact]
    public void LesTroisPointsFermentAussiLEnTete()
    {
        Assert.True(FrontMatterReader.Read("---\na: 1\n...\ncorps").Exists);
        Assert.False(FrontMatterReader.Read("---\na: 1\n...\ncorps", new FrontMatterOptions { AllowDotsClosing = false }).Exists);
    }

    [Fact]
    public void UneMarqueDOrdreDesOctetsEstToleree()
    {
        FrontMatterBlock block = FrontMatterReader.Read("﻿---\na: 1\n---\n");

        Assert.True(block.Exists);
        Assert.Equal(5, block.ContentStart);
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("---\na: 1\n")]
    [InlineData("---")]
    [InlineData("--- titre\na: 1\n---")]
    [InlineData("----\na: 1\n----")]
    [InlineData("\n---\na: 1\n---")]
    [InlineData("texte\n---\na: 1\n---")]
    [InlineData("# Titre")]
    public void SansEnTeteRefermeEnOuvertureIlNYAPasDEnTete(string text)
    {
        FrontMatterBlock block = FrontMatterReader.Read(text);

        Assert.False(block.Exists);
        Assert.Same(FrontMatterBlock.None, block);
        Assert.Equal(0, block.BodyStart);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LaRechercheDeLaFermetureEstBornee()
    {
        string text = "---\n" + new string('a', 20) + "\n---\n";

        Assert.True(FrontMatterReader.Read(text, new FrontMatterOptions { MaxLength = 21 }).Exists);
        Assert.False(FrontMatterReader.Read(text, new FrontMatterOptions { MaxLength = 20 }).Exists);
    }

    [Fact]
    public void TryLocateDonneLesPositionsSansLireLesProprietes()
    {
        Assert.True(FrontMatterReader.TryLocate("---\na: 1\n---\nx", out int start, out int length, out int body));
        Assert.Equal(4, start);
        Assert.Equal(4, length);
        Assert.Equal(13, body);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void TryLocateNeLeveJamais()
    {
        Assert.False(FrontMatterReader.TryLocate(null, out _, out _, out _));
        Assert.False(FrontMatterReader.TryLocate("---\n---", out _, out _, out _, new FrontMatterOptions { MaxLength = 0 }));

        foreach (string text in new[] { "", "-", "--", "---", "---\n", "---\r", "---\r\n---", "﻿", "﻿---\n..." })
        {
            Assert.Null(Record.Exception(() => FrontMatterReader.TryLocate(text, out _, out _, out _)));
        }
    }

    // ==================== Propriétés ====================

    [Fact]
    public void LesScalairesPerdentLeursGuillemets()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\na: simple\nb: \"double \\\" guillemet\"\nc: 'l''apostrophe'\nd: 42\n---\n");

        Assert.Equal("simple", block.Find("a")!.Value);
        Assert.Equal("double \" guillemet", block.Find("b")!.Value);
        Assert.Equal("l'apostrophe", block.Find("c")!.Value);
        Assert.Equal("42", block.Find("d")!.Value);
        Assert.All(block.Properties, p => Assert.Equal(FrontMatterValueKind.Scalar, p.Kind));
    }

    [Fact]
    public void UneListeEnLigneEstDecoupee()
    {
        FrontMatterProperty tags = FrontMatterReader.Read("---\ntags: [projet, \"idée, à creuser\", 'x']\n---").Find("tags")!;

        Assert.Equal(FrontMatterValueKind.List, tags.Kind);
        Assert.Equal(["projet", "idée, à creuser", "x"], tags.Values);
    }

    [Fact]
    public void UneListeEnLignePeutCourirSurPlusieursLignes()
    {
        FrontMatterProperty tags = FrontMatterReader.Read("---\ntags: [a,\n  b,\n  c]\nautre: 1\n---").Find("tags")!;

        Assert.Equal(["a", "b", "c"], tags.Values);
    }

    [Fact]
    public void UneListeATiretsEstLue()
    {
        string text = "---\naliases:\n  - Plan\n  - \"Plan de vol\"\n  # commentaire\n  - [[Carnet]]\nstatut: brouillon\n---";

        FrontMatterBlock block = FrontMatterReader.Read(text);
        FrontMatterProperty aliases = block.Find("aliases")!;

        Assert.Equal(FrontMatterValueKind.List, aliases.Kind);
        Assert.Equal(["Plan", "Plan de vol", "[[Carnet]]"], aliases.Values);
        Assert.Equal("brouillon", block.Find("statut")!.Value);
    }

    [Fact]
    public void LesTiretsPeuventEtreAuNiveauDeLaCle()
    {
        FrontMatterProperty tags = FrontMatterReader.Read("---\ntags:\n- a\n- b\n---").Find("tags")!;

        Assert.Equal(["a", "b"], tags.Values);
    }

    [Fact]
    public void UneCleSansValeurEstVide()
    {
        FrontMatterProperty statut = FrontMatterReader.Read("---\nstatut:\nautre: x\n---").Find("statut")!;

        Assert.Equal(FrontMatterValueKind.Empty, statut.Kind);
        Assert.Empty(statut.Values);
        Assert.Null(statut.Value);
    }

    [Fact]
    public void UnTexteLitteralGardeSesLignes()
    {
        FrontMatterProperty resume = FrontMatterReader.Read("---\nresume: |\n  Ligne un\n  Ligne deux\nfin: 1\n---").Find("resume")!;

        Assert.Equal(FrontMatterValueKind.Text, resume.Kind);
        Assert.Equal("Ligne un\nLigne deux", resume.Value);
    }

    [Fact]
    public void UnTexteReplieJointSesLignesParDesEspaces()
    {
        FrontMatterProperty resume = FrontMatterReader.Read("---\nresume: >-\n  Ligne un\n  suite\n\n  Paragraphe\n---").Find("resume")!;

        Assert.Equal("Ligne un suite\n\nParagraphe", resume.Value);
    }

    [Fact]
    public void UneStructureImbriqueeEstRendueTelleQuelle()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\nauteur:\n  nom: Hugo\n  ville: Paris\ncarte: {x: 1}\nobjets:\n  - nom: a\n---");

        FrontMatterProperty auteur = block.Find("auteur")!;
        Assert.Equal(FrontMatterValueKind.Complex, auteur.Kind);
        Assert.Empty(auteur.Values);
        Assert.Equal("  nom: Hugo\n  ville: Paris", auteur.RawValue);
        Assert.Equal(FrontMatterValueKind.Complex, block.Find("carte")!.Kind);
        Assert.Equal(FrontMatterValueKind.Complex, block.Find("objets")!.Kind);
    }

    [Fact]
    public void LesCommentairesSontRetires()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\n# en tête\na: valeur # note\nb: \"x # pas un commentaire\"\nc: page.html#ancre\n---");

        Assert.Equal("valeur", block.Find("a")!.Value);
        Assert.Equal("x # pas un commentaire", block.Find("b")!.Value);
        Assert.Equal("page.html#ancre", block.Find("c")!.Value);
    }

    [Fact]
    public void UnScalaireNuPeutContinuerSurLesLignesIndentees()
    {
        Assert.Equal("un long titre", FrontMatterReader.Read("---\ntitre: un long\n  titre\n---").Find("titre")!.Value);
    }

    [Fact]
    public void UneCleEntreGuillemetsEstLue()
    {
        Assert.Equal("x", FrontMatterReader.Read("---\n\"clé: bizarre\": x\n---").Find("clé: bizarre")!.Value);
    }

    [Fact]
    public void UneLigneSansDeuxPointsSuivisDUnBlancNEstPasUneCle()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\nhttp://exemple.fr\na: 1\n---");

        FrontMatterProperty a = Assert.Single(block.Properties);
        Assert.Equal("a", a.Key);
    }

    [Fact]
    public void LesNumerosDeLigneComptentDepuisLeDebutDuDocument()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\na: 1\n\nb: 2\n---");

        Assert.Equal(1, block.Find("a")!.Line);
        Assert.Equal(3, block.Find("b")!.Line);
    }

    [Fact]
    public void LaCasseDesClesEstIgnoreeParDefaut()
    {
        string text = "---\nTags: [a]\n---";

        Assert.Equal(["a"], FrontMatterReader.Read(text).GetValues("tags"));
        Assert.Empty(FrontMatterReader.Read(text, new FrontMatterOptions { IgnoreKeyCase = false }).GetValues("tags"));
    }

    [Fact]
    public void UneCleRepeteeRendLaDerniereValeur()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\na: 1\na: 2\n---");

        Assert.Equal(2, block.Properties.Count);
        Assert.Equal("2", block.Find("a")!.Value);
    }

    [Fact]
    public void UneProprieteAbsenteRendUneListeVide()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\na: 1\n---");

        Assert.Null(block.Find("absente"));
        Assert.Empty(block.GetValues("absente"));
        Assert.Empty(FrontMatterBlock.None.GetValues("a"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesValeursUnicodeSontConservees()
    {
        FrontMatterBlock block = FrontMatterReader.Read("---\ntitre: Étoile 🌟 filante\ntags: [日本語, «guillemets»]\n---");

        Assert.Equal("Étoile 🌟 filante", block.Find("titre")!.Value);
        Assert.Equal(["日本語", "«guillemets»"], block.GetValues("tags"));
    }

    // ==================== Entrées limites et paramétrage ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontRefusees()
    {
        Assert.Throws<ArgumentNullException>(() => FrontMatterReader.Read(null!));
        Assert.Throws<ArgumentNullException>(() => FrontMatterReader.Read("---\n---").Find(null!));
        Assert.Throws<ArgumentNullException>(() => FrontMatterReader.Read("---\n---").GetValues(null!));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnDocumentVideNAPasDEnTete()
    {
        FrontMatterBlock block = FrontMatterReader.Read(string.Empty);

        Assert.False(block.Exists);
        Assert.Empty(block.Properties);
        Assert.Equal(0, block.ContentLength);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void DesReglagesIncoherentsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FrontMatterReader.Read("---\n---", new FrontMatterOptions { MaxLength = 2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrontMatterOptions { MaxLength = -1 }.Validate());
        new FrontMatterOptions { MaxLength = 3 }.Validate();
        FrontMatterOptions.Default.Validate();
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnYamlBancalNeFaitPasEchouerLaLecture()
    {
        string text = "---\n: sans clé\n\"non fermée: x\ntags: [a, b\nliste:\n  - \n  -\nx: \"ouvert\n---";

        FrontMatterBlock block = FrontMatterReader.Read(text);

        Assert.True(block.Exists);
        Assert.Equal(FrontMatterValueKind.Complex, block.Find("tags")!.Kind);
        Assert.Empty(block.Find("liste")!.Values);
        Assert.Equal("\"ouvert", block.Find("x")!.Value);
    }
}
