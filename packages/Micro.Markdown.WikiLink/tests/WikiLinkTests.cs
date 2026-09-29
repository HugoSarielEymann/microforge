using Xunit;

namespace Micro.Markdown.WikiLink.Tests;

public sealed class WikiLinkTests
{
    private static WikiLinkMatch Parse(string text, int index = 0)
    {
        Assert.True(WikiLinkParser.TryParseAt(text, index, out WikiLinkMatch? link));
        return link;
    }

    // ==================== Analyse d'un lien ====================

    [Fact]
    public void UnLienSimpleDonneSaCibleEtSaPosition()
    {
        WikiLinkMatch link = Parse("[[Carnet de bord]]");

        Assert.Equal("Carnet de bord", link.Target);
        Assert.Equal(0, link.Start);
        Assert.Equal(18, link.Length);
        Assert.Equal(2, link.TargetStart);
        Assert.Equal(14, link.TargetLength);
        Assert.False(link.IsEmbed);
        Assert.Null(link.Heading);
        Assert.Null(link.Alias);
        Assert.Equal("Carnet de bord", link.DisplayText);
    }

    [Fact]
    public void LAliasEstSepareParLaBarre()
    {
        WikiLinkMatch link = Parse("[[Carnet de bord|le carnet]]");

        Assert.Equal("Carnet de bord", link.Target);
        Assert.Equal("le carnet", link.Alias);
        Assert.Equal("le carnet", link.DisplayText);
        Assert.Equal("[[Carnet de bord|le carnet]]".IndexOf("le carnet", StringComparison.Ordinal), link.AliasStart);
        Assert.Equal(9, link.AliasLength);
    }

    [Fact]
    public void LaSectionSuitLeDiese()
    {
        WikiLinkMatch link = Parse("[[Projet#Objectifs|buts]]");

        Assert.Equal("Projet", link.Target);
        Assert.Equal("Objectifs", link.Heading);
        Assert.Equal("buts", link.Alias);
    }

    [Fact]
    public void LesSousSectionsGardentLeursDieses()
    {
        WikiLinkMatch link = Parse("[[Projet#Plan#Étape 2]]");

        Assert.Equal("Plan#Étape 2", link.Heading);
        Assert.Equal("Projet › Plan#Étape 2", link.DisplayText);
    }

    [Fact]
    public void UnBlocSeReconnaitALAccentCirconflexe()
    {
        WikiLinkMatch link = Parse("[[Projet#^a1b2c3]]");

        Assert.Equal("Projet", link.Target);
        Assert.Equal("a1b2c3", link.BlockId);
        Assert.Null(link.Heading);
        Assert.Equal("Projet › ^a1b2c3", link.DisplayText);
    }

    [Fact]
    public void UnLienVersUneSectionDeLaNoteCouranteALaCibleVide()
    {
        WikiLinkMatch link = Parse("[[#Conclusion]]");

        Assert.Equal(string.Empty, link.Target);
        Assert.Equal("Conclusion", link.Heading);
        Assert.Equal("Conclusion", link.DisplayText);
        Assert.Equal(0, link.TargetLength);
    }

    [Fact]
    public void UneIntegrationCommenceAuPointDExclamation()
    {
        WikiLinkMatch link = Parse("voir ![[schema.png]]", 5);

        Assert.True(link.IsEmbed);
        Assert.Equal("schema.png", link.Target);
        Assert.Equal(5, link.Start);
        Assert.Equal(15, link.Length);
        Assert.Equal(8, link.ContentStart);
        Assert.Equal(10, link.ContentLength);
    }

    [Fact]
    public void LesIntegrationsPeuventEtreDesactivees()
    {
        WikiLinkOptions options = new() { AllowEmbeds = false };

        Assert.False(WikiLinkParser.TryParseAt("![[image.png]]", 0, out _, options));
        Assert.True(WikiLinkParser.TryParseAt("![[image.png]]", 1, out WikiLinkMatch? link, options));
        Assert.False(link.IsEmbed);
    }

    [Fact]
    public void LesBlancsDeBordSontRetiresEtLesPositionsSuivent()
    {
        string text = "[[  Dossier/Note  |  alias  ]]";
        WikiLinkMatch link = Parse(text);

        Assert.Equal("Dossier/Note", link.Target);
        Assert.Equal("Dossier/Note", text.Substring(link.TargetStart, link.TargetLength));
        Assert.Equal("alias", text.Substring(link.AliasStart, link.AliasLength));
    }

    [Fact]
    public void UneBarreEchappeeDansUnTableauSepareAussiLAlias()
    {
        WikiLinkMatch link = Parse(@"[[Note\|alias]]");

        Assert.Equal("Note", link.Target);
        Assert.Equal("alias", link.Alias);
    }

    [Fact]
    public void UnAliasVideEstIgnore()
    {
        WikiLinkMatch link = Parse("[[Note|  ]]");

        Assert.Null(link.Alias);
        Assert.Equal(-1, link.AliasStart);
        Assert.Equal("Note", link.DisplayText);
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("[[]]")]
    [InlineData("[[   ]]")]
    [InlineData("[[|alias]]")]
    [InlineData("[[Note")]
    [InlineData("[[Note]")]
    [InlineData("[Note]]")]
    [InlineData("[[Note\n]]")]
    [InlineData("[[a [[b]]")]
    [InlineData("[[#]]")]
    [InlineData("[[#^]]")]
    public void LesFormesInvalidesSontRefusees(string text)
    {
        Assert.False(WikiLinkParser.TryParseAt(text, 0, out WikiLinkMatch? link));
        Assert.Null(link);
    }

    [Fact]
    public void LePremierDoubleCrochetFermantClotLeLien()
    {
        WikiLinkMatch link = Parse("[[a]]]");

        Assert.Equal("a", link.Target);
        Assert.Equal(5, link.Length);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LaLongueurDuContenuEstBornee()
    {
        WikiLinkOptions options = new() { MaxLength = 5 };

        Assert.True(WikiLinkParser.TryParseAt("[[abcde]]", 0, out _, options));
        Assert.False(WikiLinkParser.TryParseAt("[[abcdef]]", 0, out _, options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnePositionHorsDuTexteRendFaux()
    {
        Assert.False(WikiLinkParser.TryParseAt("[[a]]", -1, out _));
        Assert.False(WikiLinkParser.TryParseAt("[[a]]", 5, out _));
        Assert.False(WikiLinkParser.TryParseAt("[[a]]", int.MaxValue, out _));
        Assert.False(WikiLinkParser.TryParseAt("!", 0, out _));
        Assert.False(WikiLinkParser.TryParseAt("[", 0, out _));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void TryParseAtNeLevePasSurUneEntreeNulle()
    {
        Assert.False(WikiLinkParser.TryParseAt(null, 0, out _));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void TryParseAtNeLeveJamaisQuelleQueSoitLaPosition()
    {
        string[] absurdes =
        [
            "[[", "]]", "![[", "!", "[[|]]", "[[#^|]]", "[[\\|]]", "[[a\\", "[[a]", "\\[[a]]",
            "[[[[[[a]]]]]]", "![[![[a]]]]", "[[\ud83c]]", "[[a\r\n]]", new string('[', 600) + "a]]",
        ];

        foreach (string text in absurdes)
        {
            for (int index = -2; index <= text.Length + 2; index++)
            {
                Exception? erreur = Record.Exception(() => WikiLinkParser.TryParseAt(text, index, out _));
                Assert.Null(erreur);
            }
        }
    }

    [Fact]
    public void EndDesigneLaPositionQuiSuitLeLien()
    {
        WikiLinkMatch link = Parse("ab ![[c]] d", 3);

        Assert.Equal(9, link.End);
        Assert.Equal(" d", "ab ![[c]] d"[link.End..]);
    }

    [Fact]
    public void TryParseAtNeLevePasSurDesReglagesIncoherents()
    {
        Assert.False(WikiLinkParser.TryParseAt("[[a]]", 0, out _, new WikiLinkOptions { MaxLength = 0 }));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesPositionsRestentJustesApresUnEmoji()
    {
        string text = "🌌 [[Étoile 🌟 filante|✨]]";
        WikiLinkMatch link = Assert.Single(WikiLinkParser.FindAll(text));

        Assert.Equal(text.IndexOf("[[", StringComparison.Ordinal), link.Start);
        Assert.Equal("Étoile 🌟 filante", text.Substring(link.TargetStart, link.TargetLength));
        Assert.Equal("✨", link.Alias);
    }

    // ==================== Relevé dans un texte ====================

    [Fact]
    public void FindAllReleveLesLiensDansLOrdre()
    {
        string text = "Voir [[A]], puis ![[B.png]] et [[C#Titre|c]].";

        IReadOnlyList<WikiLinkMatch> links = WikiLinkParser.FindAll(text);

        Assert.Equal(["A", "B.png", "C"], links.Select(l => l.Target));
        Assert.True(links[1].IsEmbed);
        Assert.Equal("c", links[2].Alias);
    }

    [Fact]
    public void FindAllIgnoreLeCodeParDefaut()
    {
        string text = "[[Vrai]] `[[Faux]]`\n```\n[[Aussi faux]]\n```\n[[Encore vrai]]";

        IReadOnlyList<WikiLinkMatch> links = WikiLinkParser.FindAll(text);

        Assert.Equal(["Vrai", "Encore vrai"], links.Select(l => l.Target));
    }

    [Fact]
    public void FindAllPeutInclureLeCode()
    {
        WikiLinkOptions options = new() { SkipCode = false };

        Assert.Equal(2, WikiLinkParser.FindAll("[[A]] `[[B]]`", options).Count);
    }

    [Fact]
    public void UnDoubleCrochetEchappeNOuvrePasDeLien()
    {
        Assert.Empty(WikiLinkParser.FindAll(@"\[[Pas un lien]]"));
        Assert.Single(WikiLinkParser.FindAll(@"\\[[Un lien]]"));
    }

    [Fact]
    public void UnPointDExclamationEchappeLaisseUnRenvoiSimple()
    {
        WikiLinkMatch link = Assert.Single(WikiLinkParser.FindAll(@"\![[Note]]"));

        Assert.False(link.IsEmbed);
        Assert.Equal(2, link.Start);
    }

    [Fact]
    public void UnCrochetDeTropEstToleré()
    {
        WikiLinkMatch link = Assert.Single(WikiLinkParser.FindAll("[[[Note]]"));

        Assert.Equal("Note", link.Target);
        Assert.Equal(1, link.Start);
    }

    [Fact]
    public void UnLienInterieurEstRetenuQuandLExterieurNeSeFermePas()
    {
        WikiLinkMatch link = Assert.Single(WikiLinkParser.FindAll("[[a [[b]]"));

        Assert.Equal("b", link.Target);
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnTexteVideNeContientAucunLien()
    {
        Assert.Empty(WikiLinkParser.FindAll(string.Empty));
        Assert.Equal(string.Empty, WikiLinkParser.Rewrite(string.Empty, _ => "x"));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontRefusees()
    {
        Assert.Throws<ArgumentNullException>(() => WikiLinkParser.FindAll(null!));
        Assert.Throws<ArgumentNullException>(() => WikiLinkParser.Rewrite(null!, _ => null));
        Assert.Throws<ArgumentNullException>(() => WikiLinkParser.Rewrite("[[a]]", null!));
        Assert.Throws<ArgumentNullException>(() => WikiLinkParser.Format(null!));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void DesReglagesIncoherentsSontRefusesParFindAll()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WikiLinkParser.FindAll("[[a]]", new WikiLinkOptions { MaxLength = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WikiLinkOptions { MaxLength = -3 }.Validate());
        new WikiLinkOptions { MaxLength = 1 }.Validate();
        WikiLinkOptions.Default.Validate();
    }

    // ==================== Réécriture ====================

    [Fact]
    public void RewriteRemplaceLaCibleEtGardeAliasSectionEtIntegration()
    {
        string text = "[[Ancien]] [[Ancien|alias]] ![[Ancien#Titre]] [[Autre]]";

        string result = WikiLinkParser.Rewrite(text, l => l.Target == "Ancien" ? "Nouveau nom" : null);

        Assert.Equal("[[Nouveau nom]] [[Nouveau nom|alias]] ![[Nouveau nom#Titre]] [[Autre]]", result);
    }

    [Fact]
    public void RewriteConserveLesBlancsAutourDeLaCible()
    {
        Assert.Equal("[[ B | x ]]", WikiLinkParser.Rewrite("[[ A | x ]]", _ => "B"));
    }

    [Fact]
    public void RewriteNeToucheNiAuCodeNiAuxAutresLiens()
    {
        string text = "`[[A]]` [[A]] [[a]]";

        string result = WikiLinkParser.Rewrite(text, l => l.Target == "A" ? "B" : null);

        Assert.Equal("`[[A]]` [[B]] [[a]]", result);
    }

    [Fact]
    public void RewriteRendLInstanceQuandRienNeChange()
    {
        string text = "[[A]] et [[B]]";

        Assert.Same(text, WikiLinkParser.Rewrite(text, l => l.Target));
        Assert.Same(text, WikiLinkParser.Rewrite(text, _ => null));
    }

    [Fact]
    public void RewriteRefuseUneCibleQuiCasseraitLeLien()
    {
        Assert.Throws<ArgumentException>(() => WikiLinkParser.Rewrite("[[A]]", _ => "B]]C"));
        Assert.Throws<ArgumentException>(() => WikiLinkParser.Rewrite("[[A]]", _ => "B#C"));
    }

    [Fact]
    public void RewritePeutDonnerUneCibleAUnLienInterne()
    {
        Assert.Equal("[[Note#Titre]]", WikiLinkParser.Rewrite("[[#Titre]]", _ => "Note"));
    }

    // ==================== Composition ====================

    [Theory]
    [InlineData("Note", null, null, null, false, "[[Note]]")]
    [InlineData("Note", "Titre", null, null, false, "[[Note#Titre]]")]
    [InlineData("Note", null, "abc", null, false, "[[Note#^abc]]")]
    [InlineData("Note", null, "^abc", null, false, "[[Note#^abc]]")]
    [InlineData("Note", null, null, "alias", false, "[[Note|alias]]")]
    [InlineData("image.png", null, null, null, true, "![[image.png]]")]
    [InlineData("", "Titre", null, null, false, "[[#Titre]]")]
    [InlineData(" Note ", " Titre ", null, " C# ", false, "[[Note#Titre|C#]]")]
    public void FormatComposeUnLienRelisible(string target, string? heading, string? block, string? alias, bool embed, string expected)
    {
        string text = WikiLinkParser.Format(target, heading, block, alias, embed);

        Assert.Equal(expected, text);
        Assert.True(WikiLinkParser.TryParseAt(text, 0, out WikiLinkMatch? link));
        Assert.Equal(target.Trim(), link.Target);
        Assert.Equal(embed, link.IsEmbed);
    }

    [Theory]
    [InlineData("A|B", null, null)]
    [InlineData("A", "x]]", null)]
    [InlineData("A", "x|y", null)]
    [InlineData("A", null, "a\nb")]
    [InlineData("", null, null)]
    [InlineData("  ", null, "alias")]
    public void FormatRefuseCeQuiCasseraitLaSyntaxe(string target, string? heading, string? alias)
    {
        Assert.Throws<ArgumentException>(() => WikiLinkParser.Format(target, heading, null, alias));
    }

    [Theory]
    [InlineData("Note", true)]
    [InlineData("Dossier/Sous dossier/Note", true)]
    [InlineData("Été 2026 🌞", true)]
    [InlineData(" Note", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("A#B", false)]
    [InlineData("A^B", false)]
    [InlineData("A[B", false)]
    [InlineData("A|B", false)]
    [InlineData("A\nB", false)]
    public void IsValidTargetNeLeveJamais(string? target, bool expected)
    {
        Assert.Equal(expected, WikiLinkParser.IsValidTarget(target));
    }
}
