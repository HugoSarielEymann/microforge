using Xunit;

namespace Micro.Schema.SampleShape.Tests;

public sealed class SampleShapeTests
{
    private static ShapeNode Champ(ShapeNode racine, string nom)
        => Assert.Single(racine.Children, c => c.Name == nom);

    // ==================== JSON : natures ====================

    [Fact]
    public void JsonPlat_ClasseChaqueNature()
    {
        ShapeNode racine = SampleShapeReader.FromJson(
            """
            {
              "nom": "Dupont",
              "age": 42,
              "solde": 12.5,
              "actif": true,
              "cree": "2026-08-05T14:30:00Z"
            }
            """);

        Assert.Equal(ShapeKind.Structure, racine.Kind);
        Assert.Equal(ShapeKind.Text, Champ(racine, "nom").Kind);
        Assert.Equal(ShapeKind.WholeNumber, Champ(racine, "age").Kind);
        Assert.Equal(ShapeKind.FractionalNumber, Champ(racine, "solde").Kind);
        Assert.Equal(ShapeKind.Boolean, Champ(racine, "actif").Kind);
        Assert.Equal(ShapeKind.Timestamp, Champ(racine, "cree").Kind);
    }

    [Fact]
    public void JsonImbrique_DescendDansLesObjets()
    {
        ShapeNode racine = SampleShapeReader.FromJson(
            """{ "client": { "adresse": { "rue": "des Lilas", "numero": 12 } } }""");

        ShapeNode rue = Champ(Champ(Champ(racine, "client"), "adresse"), "rue");

        Assert.Equal(ShapeKind.Text, rue.Kind);
        Assert.Equal(ShapeKind.WholeNumber, Champ(Champ(Champ(racine, "client"), "adresse"), "numero").Kind);
    }

    [Fact]
    public void JsonTableau_MarqueLaRepetition()
    {
        ShapeNode racine = SampleShapeReader.FromJson("""{ "lignes": [ { "libelle": "A" } ] }""");
        ShapeNode lignes = Champ(racine, "lignes");

        Assert.True(lignes.IsRepeated);
        Assert.Equal(ShapeKind.Structure, lignes.Kind);
        Assert.Equal(ShapeKind.Text, Champ(lignes, "libelle").Kind);
    }

    [Fact]
    public void JsonTableauDeValeursSimples_EstUneListeDeCetteNature()
    {
        ShapeNode racine = SampleShapeReader.FromJson("""{ "codes": [1, 2, 3] }""");
        ShapeNode codes = Champ(racine, "codes");

        Assert.True(codes.IsRepeated);
        Assert.Equal(ShapeKind.WholeNumber, codes.Kind);
        Assert.Empty(codes.Children);
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void JsonTableauVide_NeDitRienDeSesElements()
    {
        ShapeNode codes = Champ(SampleShapeReader.FromJson("""{ "codes": [] }"""), "codes");

        Assert.True(codes.IsRepeated);
        Assert.Equal(ShapeKind.Unknown, codes.Kind);
    }

    [Fact]
    public void JsonTableauHeterogene_ReunitLesEntrees()
    {
        // La première entrée est incomplète : les suivantes doivent compléter la forme.
        ShapeNode lignes = Champ(
            SampleShapeReader.FromJson("""{ "lignes": [ { "a": 1 }, { "b": "x" } ] }"""),
            "lignes");

        Assert.Equal(2, lignes.Children.Count);
        Assert.Equal(ShapeKind.WholeNumber, Champ(lignes, "a").Kind);
        Assert.Equal(ShapeKind.Text, Champ(lignes, "b").Kind);
    }

    [Fact]
    public void JsonTableauMelangeantEntierEtVirgule_DonneUnNombreAVirgule()
    {
        Assert.Equal(
            ShapeKind.FractionalNumber,
            Champ(SampleShapeReader.FromJson("""{ "montants": [1, 2.5] }"""), "montants").Kind);
    }

    [Fact]
    public void JsonTableauMelangeantDesNaturesEtrangeres_RetombeSurDuTexte()
    {
        Assert.Equal(
            ShapeKind.Text,
            Champ(SampleShapeReader.FromJson("""{ "melange": [1, true] }"""), "melange").Kind);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void JsonNul_NeDitRienDeLaNature()
    {
        Assert.Equal(
            ShapeKind.Unknown,
            Champ(SampleShapeReader.FromJson("""{ "inconnu": null }"""), "inconnu").Kind);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void JsonTableauCommencantParNul_EstDecritParLesSuivants()
    {
        Assert.Equal(
            ShapeKind.WholeNumber,
            Champ(SampleShapeReader.FromJson("""{ "codes": [null, 7] }"""), "codes").Kind);
    }

    [Fact]
    public void JsonRacineNonObjet_EstAcceptee()
    {
        ShapeNode racine = SampleShapeReader.FromJson("""[ { "a": 1 } ]""");

        Assert.True(racine.IsRepeated);
        Assert.Equal("racine", racine.Name);
        Assert.Equal(ShapeKind.Structure, racine.Kind);
    }

    [Fact]
    public void JsonCleEnDouble_RetientLaPremiere()
    {
        ShapeNode racine = SampleShapeReader.FromJson("""{ "a": 1, "a": "x" }""");

        Assert.Equal(ShapeKind.WholeNumber, Assert.Single(racine.Children).Kind);
    }

    [Fact]
    public void JsonUnNombreExponentielEstUnNombreAVirgule()
    {
        Assert.Equal(
            ShapeKind.FractionalNumber,
            Champ(SampleShapeReader.FromJson("""{ "x": 1e3 }"""), "x").Kind);
    }

    [Fact]
    public void LaDetectionDesInstantsPeutEtreCoupee()
    {
        SampleShapeOptions sansDates = new() { DetectTimestamps = false };

        Assert.Equal(
            ShapeKind.Text,
            Champ(SampleShapeReader.FromJson("""{ "d": "2026-08-05" }""", sansDates), "d").Kind);
    }

    [Fact]
    public void UneChaineQuiNEstPasUnInstantResteDuTexte()
    {
        ShapeNode racine = SampleShapeReader.FromJson("""{ "a": "5", "b": "2026", "c": "05/08/2026" }""");

        Assert.Equal(ShapeKind.Text, Champ(racine, "a").Kind);
        Assert.Equal(ShapeKind.Text, Champ(racine, "b").Kind);
        Assert.Equal(ShapeKind.Text, Champ(racine, "c").Kind);
    }

    // ==================== JSON : échecs ====================

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void JsonIllisible_Leve()
    {
        FormatException echec = Assert.Throws<FormatException>(
            () => SampleShapeReader.FromJson("{ ceci n'est pas du json"));

        Assert.Contains("illisible", echec.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void TryFromJson_SurDuJsonIllisible_RendLeMotif()
    {
        Assert.False(SampleShapeReader.TryFromJson("{ oups", out ShapeNode? forme, out string? motif));

        Assert.Null(forme);
        Assert.NotNull(motif);
        Assert.NotEmpty(motif);
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void TryFromJson_SurDuVide_EchouePoliment()
    {
        Assert.False(SampleShapeReader.TryFromJson("", out _, out string? motif));
        Assert.False(SampleShapeReader.TryFromJson("   ", out _, out _));
        Assert.False(SampleShapeReader.TryFromJson(null, out _, out _));
        Assert.NotNull(motif);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void LesTentativesNeLeventJamais()
    {
        // Le contrat TryXxx est absolu : nul, vide, tronqué, absurde, démesuré — aucune
        // de ces entrées ne doit lever. Ces méthodes sont appelées à chaque frappe dans un
        // champ de saisie, où l'entrée est invalide l'essentiel du temps.
        string?[] agressions =
        [
            null, "", "   ", "{", "]", "\0", "{\"a\":", "<xml/>", "nan", "-", "0x1F",
            new string('[', 500), new string('{', 500), "\uFFFF", "<a", "<a></b>",
            "\"\"", "{\"a\":\"\\uZZZZ\"}", "<?xml version=\"9.9\"?><a/>",
        ];

        Exception? echec = Record.Exception(() =>
        {
            foreach (string? agression in agressions)
            {
                SampleShapeReader.TryFromJson(agression, out _, out _);
                SampleShapeReader.TryFromXml(agression, out _, out _);

                // Y compris avec des réglages eux-mêmes invalides : le motif d'échec doit
                // ressortir par le canal normal, pas par une exception.
                SampleShapeReader.TryFromJson(agression, out _, out _, new SampleShapeOptions { MaxDepth = 0 });
                SampleShapeReader.TryFromXml(agression, out _, out _, new SampleShapeOptions { MaxDepth = 0 });
            }
        });

        Assert.Null(echec);
    }

    // ==================== Identité d'une forme ====================

    [Fact]
    public void DeuxFormesIdentiquesSontEgalesEtPartagentLeurEmpreinte()
    {
        ShapeNode gauche = ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.Text)]);
        ShapeNode droite = ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.Text)]);

        // Les enfants sont deux listes distinctes : sans égalité structurelle, deux lectures
        // d'un même document ne seraient jamais égales.
        Assert.NotSame(gauche.Children, droite.Children);
        Assert.Equal(gauche, droite);
        Assert.True(gauche.Equals(droite));
        Assert.Equal(gauche.GetHashCode(), droite.GetHashCode());
    }

    [Fact]
    public void ChaqueTraitDistingueDeuxFormes()
    {
        ShapeNode reference = ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.Text)]);

        Assert.NotEqual(reference, ShapeNode.Structure("autre", [ShapeNode.Leaf("a", ShapeKind.Text)]));
        Assert.NotEqual(reference, ShapeNode.Structure("r", [ShapeNode.Leaf("b", ShapeKind.Text)]));
        Assert.NotEqual(reference, ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.WholeNumber)]));
        Assert.NotEqual(reference, ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.Text)], isRepeated: true));
        Assert.NotEqual(reference, ShapeNode.Structure("r", []));
        Assert.False(reference.Equals(null));
    }

    [Fact]
    public void LOrdreDesChampsCompte()
    {
        // L'ordre est celui du document : deux structures aux mêmes champs dans un autre
        // ordre ne décrivent pas le même document, et l'affichage doit le refléter.
        ShapeNode[] champs = [ShapeNode.Leaf("a", ShapeKind.Text), ShapeNode.Leaf("b", ShapeKind.Text)];

        Assert.NotEqual(
            ShapeNode.Structure("r", champs),
            ShapeNode.Structure("r", [champs[1], champs[0]]));
    }

    [Fact]
    public void LEmpreinteResteStableEntreDeuxAppels()
    {
        ShapeNode forme = ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.Text)]);

        Assert.Equal(forme.GetHashCode(), forme.GetHashCode());
    }

    [Fact]
    public void UneFormeSeDecomposeEtSeDecrit()
    {
        ShapeNode forme = ShapeNode.Leaf("montant", ShapeKind.FractionalNumber, isRepeated: true);

        (string nom, ShapeKind nature, bool repete, IReadOnlyList<ShapeNode> enfants) = forme;

        Assert.Equal("montant", nom);
        Assert.Equal(ShapeKind.FractionalNumber, nature);
        Assert.True(repete);
        Assert.Empty(enfants);

        Assert.Contains("montant", forme.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UneFormeSeRecopieEnChangeantUnTrait()
    {
        ShapeNode origine = ShapeNode.Leaf("a", ShapeKind.Text);

        Assert.Equal(ShapeNode.Leaf("a", ShapeKind.Text, isRepeated: true), origine with { IsRepeated = true });
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnEchantillonNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => SampleShapeReader.FromJson(null!));
        Assert.Throws<ArgumentNullException>(() => SampleShapeReader.FromXml(null!));
        Assert.Throws<ArgumentNullException>(() => SampleShapeWriter.ToJson(null!));
        Assert.Throws<ArgumentNullException>(() => SampleShapeWriter.ToXml(null!));
    }

    // ==================== XML ====================

    [Fact]
    public void XmlPlat_PrendLeNomDeLaRacineEtClasseSesChamps()
    {
        ShapeNode racine = SampleShapeReader.FromXml(
            """
            <Client>
              <nom>Dupont</nom>
              <actif>false</actif>
              <cree>2026-08-05T14:30:00Z</cree>
            </Client>
            """);

        Assert.Equal("Client", racine.Name);
        Assert.Equal(ShapeKind.Structure, racine.Kind);
        Assert.Equal(ShapeKind.Text, Champ(racine, "nom").Kind);
        Assert.Equal(ShapeKind.Timestamp, Champ(racine, "cree").Kind);
    }

    [Fact]
    public void XmlElementRepete_EstUneListe()
    {
        ShapeNode racine = SampleShapeReader.FromXml(
            """
            <Commande>
              <ligne><libelle>A</libelle></ligne>
              <ligne><libelle>B</libelle></ligne>
            </Commande>
            """);

        ShapeNode ligne = Champ(racine, "ligne");

        Assert.True(ligne.IsRepeated);
        Assert.Equal(ShapeKind.Structure, ligne.Kind);
    }

    [Fact]
    public void XmlElementUnique_NEstPasUneListe()
    {
        Assert.False(Champ(SampleShapeReader.FromXml("<A><b>x</b></A>"), "b").IsRepeated);
    }

    [Fact]
    public void XmlAttributs_DeviennentDesChamps()
    {
        ShapeNode racine = SampleShapeReader.FromXml("""<Client id="7"><nom>Dupont</nom></Client>""");

        Assert.Equal(2, racine.Children.Count);
        Assert.Equal(ShapeKind.Text, Champ(racine, "id").Kind);
    }

    [Fact]
    public void XmlAttributs_PeuventEtreIgnores()
    {
        SampleShapeOptions sansAttributs = new() { ReadXmlAttributes = false };

        ShapeNode racine = SampleShapeReader.FromXml(
            """<Client id="7"><nom>Dupont</nom></Client>""", sansAttributs);

        Assert.Single(racine.Children);
    }

    [Fact]
    public void XmlUnElementLEmportSurUnAttributDeMemeNom()
    {
        ShapeNode racine = SampleShapeReader.FromXml(
            """<Client nom="attribut"><nom><partie>x</partie></nom></Client>""");

        ShapeNode nom = Champ(racine, "nom");

        // L'élément peut porter une structure, l'attribut non : c'est l'élément qui décrit.
        Assert.Equal(ShapeKind.Structure, nom.Kind);
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void XmlElementVide_NeDitRienDeSaNature()
    {
        Assert.Equal(ShapeKind.Unknown, Champ(SampleShapeReader.FromXml("<A><b/></A>"), "b").Kind);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void XmlIllisible_Leve()
    {
        Assert.Throws<FormatException>(() => SampleShapeReader.FromXml("<a><b></a>"));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void XmlAvecUneEntiteExterne_NeVaRienChercher()
    {
        // Une DTD est refusée : un échantillon collé peut venir de n'importe où, et une
        // entité externe irait lire un fichier local.
        const string attaque = """
            <?xml version="1.0"?>
            <!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <root>&xxe;</root>
            """;

        Assert.Throws<FormatException>(() => SampleShapeReader.FromXml(attaque));
    }

    [Fact]
    public void XmlEspacesDeNoms_SontIgnores()
    {
        ShapeNode racine = SampleShapeReader.FromXml(
            """<c:Client xmlns:c="urn:exemple"><c:nom>Dupont</c:nom></c:Client>""");

        Assert.Equal("Client", racine.Name);
        Assert.Equal("nom", Assert.Single(racine.Children).Name);
    }

    // ==================== Écriture ====================

    [Fact]
    public void EcrireEnJson_MontreLaForme()
    {
        ShapeNode forme = ShapeNode.Structure("racine",
        [
            ShapeNode.Leaf("nom", ShapeKind.Text),
            ShapeNode.Leaf("age", ShapeKind.WholeNumber),
            ShapeNode.Structure("lignes", [ShapeNode.Leaf("libelle", ShapeKind.Text)], isRepeated: true),
        ]);

        string json = SampleShapeWriter.ToJson(forme);

        Assert.Contains("\"nom\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lignes\"", json, StringComparison.Ordinal);
        Assert.Contains("[", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EcrireEnJson_NEchappePasLesAccents()
    {
        string json = SampleShapeWriter.ToJson(
            ShapeNode.Structure("r", [ShapeNode.Leaf("numéro de série", ShapeKind.Text)]));

        Assert.Contains("numéro de série", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EcrireEnXml_RepeteLesElementsDUneListe()
    {
        string xml = SampleShapeWriter.ToXml(ShapeNode.Structure("Commande",
        [
            ShapeNode.Structure("ligne", [ShapeNode.Leaf("libelle", ShapeKind.Text)], isRepeated: true),
        ]));

        Assert.Equal(2, xml.Split("<ligne>", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void EcrireEnXml_NettoieUnNomImpossible()
    {
        // « prix TTC » est une clé JSON légitime et un nom d'élément XML impossible.
        string xml = SampleShapeWriter.ToXml(
            ShapeNode.Structure("Facture", [ShapeNode.Leaf("prix TTC", ShapeKind.FractionalNumber)]));

        Assert.DoesNotContain("prix TTC", xml, StringComparison.Ordinal);
        ShapeNode relu = SampleShapeReader.FromXml(xml);
        Assert.Single(relu.Children);
    }

    // ==================== Aller-retour ====================

    [Fact]
    public void AllerRetourJson_ConserveLaForme()
    {
        ShapeNode origine = ShapeNode.Structure("racine",
        [
            ShapeNode.Leaf("nom", ShapeKind.Text),
            ShapeNode.Leaf("age", ShapeKind.WholeNumber),
            ShapeNode.Leaf("solde", ShapeKind.FractionalNumber),
            ShapeNode.Leaf("actif", ShapeKind.Boolean),
            ShapeNode.Leaf("cree", ShapeKind.Timestamp),
            ShapeNode.Leaf("codes", ShapeKind.WholeNumber, isRepeated: true),
            ShapeNode.Structure("adresse", [ShapeNode.Leaf("rue", ShapeKind.Text)]),
            ShapeNode.Structure("lignes", [ShapeNode.Leaf("libelle", ShapeKind.Text)], isRepeated: true),
        ]);

        ShapeNode relu = SampleShapeReader.FromJson(SampleShapeWriter.ToJson(origine));

        Assert.Equal(origine, relu);
    }

    [Fact]
    public void AllerRetourXml_ConserveLaForme()
    {
        ShapeNode origine = ShapeNode.Structure("Client",
        [
            ShapeNode.Leaf("nom", ShapeKind.Text),
            ShapeNode.Leaf("cree", ShapeKind.Timestamp),
            ShapeNode.Structure("adresse", [ShapeNode.Leaf("rue", ShapeKind.Text)]),
            ShapeNode.Structure("lignes", [ShapeNode.Leaf("libelle", ShapeKind.Text)], isRepeated: true),
        ]);

        ShapeNode relu = SampleShapeReader.FromXml(SampleShapeWriter.ToXml(origine));

        Assert.Equal(origine, relu);
    }

    [Fact]
    public void AllerRetourXml_LesNombresRedeviennentDuTexte()
    {
        // XML ne porte pas de type : un entier écrit « 0 » se relit comme du texte. La perte
        // est assumée et documentée — c'est ce qui distingue les deux formats.
        ShapeNode origine = ShapeNode.Structure("A", [ShapeNode.Leaf("n", ShapeKind.WholeNumber)]);

        ShapeNode relu = SampleShapeReader.FromXml(SampleShapeWriter.ToXml(origine));

        Assert.Equal(ShapeKind.Text, Champ(relu, "n").Kind);
    }

    [Fact]
    public void UnEchantillonSansIndentationSeRelitPareil()
    {
        SampleShapeOptions compact = new() { Indent = false };
        ShapeNode origine = ShapeNode.Structure("racine", [ShapeNode.Leaf("a", ShapeKind.Text)]);

        Assert.Equal(origine, SampleShapeReader.FromJson(SampleShapeWriter.ToJson(origine, compact)));
    }

    // ==================== Bornes ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LaProfondeurEstBornee()
    {
        SampleShapeOptions courte = new() { MaxDepth = 2 };

        ShapeNode racine = SampleShapeReader.FromJson(
            """{ "a": { "b": { "c": { "d": 1 } } } }""", courte);

        ShapeNode b = Champ(Champ(racine, "a"), "b");
        Assert.Empty(b.Children);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnDocumentTresImbriqueNeFaitPasDeborderLaPile()
    {
        string profond = new string('[', 400) + new string(']', 400);

        Assert.False(SampleShapeReader.TryFromJson(profond, out _, out _));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LeNombreDeChampsEstBorne()
    {
        SampleShapeOptions courte = new() { MaxFieldsPerObject = 2 };
        string json = "{" + string.Join(",", Enumerable.Range(0, 10).Select(i => $"\"c{i}\":1")) + "}";

        Assert.Equal(2, SampleShapeReader.FromJson(json, courte).Children.Count);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void DesBornesInvalidesSontRefusees()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleShapeOptions { MaxDepth = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SampleShapeOptions { MaxFieldsPerObject = 0 }.Validate());

        // Une occurrence unique ne se relirait plus comme une liste.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SampleShapeOptions { RepeatedSampleCount = 1 }.Validate());
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void DesTextesDExempleNulsSontRefuses()
    {
        Assert.Throws<ArgumentNullException>(() => new SampleShapeOptions { TextSample = null! }.Validate());
        Assert.Throws<ArgumentNullException>(() => new SampleShapeOptions { RootName = null! }.Validate());
        Assert.Throws<ArgumentNullException>(
            () => new SampleShapeOptions { TimestampSample = null! }.Validate());
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        SampleShapeOptions.Default.Validate();

        Assert.Equal("racine", SampleShapeOptions.Default.RootName);
        Assert.Equal(24, SampleShapeOptions.Default.MaxDepth);
        Assert.True(SampleShapeOptions.Default.DetectTimestamps);
        Assert.True(SampleShapeOptions.Default.ReadXmlAttributes);
        Assert.Equal(2, SampleShapeOptions.Default.RepeatedSampleCount);
    }

    // ==================== Fusion et construction ====================

    [Fact]
    public void Fusionner_ReunitLesChampsDesDeuxCotes()
    {
        ShapeNode gauche = ShapeNode.Structure("r", [ShapeNode.Leaf("a", ShapeKind.WholeNumber)]);
        ShapeNode droite = ShapeNode.Structure("r", [ShapeNode.Leaf("b", ShapeKind.Text)]);

        ShapeNode fusion = SampleShapeReader.Merge(gauche, droite);

        Assert.Equal(2, fusion.Children.Count);
    }

    [Fact]
    public void Fusionner_UneNatureConnueLEmporteSurLInconnue()
    {
        Assert.Equal(
            ShapeKind.Text,
            SampleShapeReader.Merge(
                ShapeNode.Leaf("a", ShapeKind.Unknown),
                ShapeNode.Leaf("a", ShapeKind.Text)).Kind);
    }

    [Fact]
    public void Fusionner_UneStructureEtUneValeurSimpleDonnentDuTexte()
    {
        ShapeNode fusion = SampleShapeReader.Merge(
            ShapeNode.Structure("a", [ShapeNode.Leaf("x", ShapeKind.Text)]),
            ShapeNode.Leaf("a", ShapeKind.WholeNumber));

        Assert.Equal(ShapeKind.Text, fusion.Kind);
        Assert.Empty(fusion.Children);
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void Fusionner_UneFormeNulleEstRefusee()
    {
        ShapeNode forme = ShapeNode.Leaf("a", ShapeKind.Text);

        Assert.Throws<ArgumentNullException>(() => SampleShapeReader.Merge(null!, forme));
        Assert.Throws<ArgumentNullException>(() => SampleShapeReader.Merge(forme, null!));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void ConstruireUnObjetSansEnfantsEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => ShapeNode.Structure("a", null!));
    }

    [Fact]
    public void CompterParcourtToutLArbre()
    {
        ShapeNode forme = ShapeNode.Structure("r",
        [
            ShapeNode.Leaf("a", ShapeKind.Text),
            ShapeNode.Structure("b", [ShapeNode.Leaf("c", ShapeKind.Text)]),
        ]);

        Assert.Equal(4, forme.Count);
        Assert.Equal(1, ShapeNode.Leaf("seul", ShapeKind.Text).Count);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnDocumentAccentueTraverseLAllerRetour()
    {
        // JSON ne nomme pas sa racine : c'est RootName qui la renomme à la relecture.
        SampleShapeOptions nomme = new() { RootName = "Réservation" };

        ShapeNode origine = ShapeNode.Structure("Réservation",
        [
            ShapeNode.Leaf("numéro", ShapeKind.WholeNumber),
            ShapeNode.Leaf("créé", ShapeKind.Timestamp),
        ]);

        Assert.Equal(origine, SampleShapeReader.FromJson(SampleShapeWriter.ToJson(origine), nomme));
    }

    [Fact]
    public void JsonNeNommePasSaRacine_ElleReprendLeNomDeReglage()
    {
        ShapeNode origine = ShapeNode.Structure("Réservation", [ShapeNode.Leaf("a", ShapeKind.Text)]);

        Assert.Equal("racine", SampleShapeReader.FromJson(SampleShapeWriter.ToJson(origine)).Name);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnNomHorsAsciiEstAccepteEnXml()
    {
        ShapeNode origine = ShapeNode.Structure("Réservation", [ShapeNode.Leaf("numéro", ShapeKind.Text)]);

        Assert.Equal(origine, SampleShapeReader.FromXml(SampleShapeWriter.ToXml(origine)));
    }
}
