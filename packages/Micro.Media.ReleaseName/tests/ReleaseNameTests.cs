using Xunit;

namespace Micro.Media.ReleaseName.Tests;

public sealed class ReleaseNameTests
{
    private static readonly string[] EtiquettesTng = ["1080p", "English", "Esubs"];
    private static readonly string[] EtiquettesOffice = ["720p", "WEB-DL", "x264"];
    private static readonly string[] EtiquettesFilm = ["1080p", "WEBRip", "x265", "10bit", "AAC5.1"];
    private static readonly string[] Resolution720 = ["720p"];
    private static readonly string[] Resolution1080 = ["1080p"];

    // ==================== Épisodes : noms de release ====================

    [Fact]
    public void UnNomDeReleaseDonneSerieSaisonEpisodeEtTitre()
    {
        ReleaseInfo info = ReleaseNameParser.Parse(
            "Star.Trek.The.Next.Generation.S01E01.Encounter.At.Farpoint.Parts.1.&.2.1080p.English.Esubs.MoviezVerse.Org.mkv");

        Assert.True(info.IsEpisode);
        Assert.Equal("Star Trek The Next Generation", info.Title);
        Assert.Equal(1, info.Season);
        Assert.Equal(1, info.Episode);
        Assert.Null(info.LastEpisode);
        Assert.Equal("Encounter At Farpoint Parts 1 & 2", info.EpisodeTitle);
        Assert.Equal(EtiquettesTng, info.Tags);
    }

    [Fact]
    public void LesEtiquettesTechniquesNeFontPasUnTitreDEpisode()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("The.Office.US.S02E01E02.720p.WEB-DL.x264-GROUP.mkv");

        Assert.Equal("The Office US", info.Title);
        Assert.Equal(2, info.Season);
        Assert.Equal(1, info.Episode);
        Assert.Equal(2, info.LastEpisode);
        Assert.Null(info.EpisodeTitle);
        Assert.Equal(EtiquettesOffice, info.Tags);
    }

    [Theory]
    [InlineData("Show.S01E02-E03.mkv", 2, 3)]
    [InlineData("Show.S01E02-03.mkv", 2, 3)]
    [InlineData("Show S01 E02.mkv", 2, null)]
    [InlineData("show.s1.e2.mkv", 2, null)]
    public void LesVariantesDuMarqueurSxxEyySontReconnues(string name, int episode, int? last)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.Equal("Show", info.Title, ignoreCase: true);
        Assert.Equal(1, info.Season);
        Assert.Equal(episode, info.Episode);
        Assert.Equal(last, info.LastEpisode);
    }

    [Fact]
    public void UneResolutionColleeAuNumeroNEstPasUnSecondEpisode()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Show.S01E02-720p.mkv");

        Assert.Equal(2, info.Episode);
        Assert.Null(info.LastEpisode);
        Assert.Equal(Resolution720, info.Tags);
    }

    [Fact]
    public void LAnneeQuiPrecedeLeMarqueurEstCelleDeLaSerie()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("doctor_who_2005.s01e01.rose.mkv");

        Assert.Equal("doctor who", info.Title);
        Assert.Equal(2005, info.Year);
        Assert.Equal("rose", info.EpisodeTitle);
    }

    // ==================== Épisodes : noms rangés ====================

    [Fact]
    public void UnNomRangeGardeSesTiretsSesApostrophesEtSesPoints()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Star Trek - The Next Generation - S03E15 - Yesterday's Enterprise.mkv");

        Assert.Equal("Star Trek - The Next Generation", info.Title);
        Assert.Equal(3, info.Season);
        Assert.Equal(15, info.Episode);
        Assert.Equal("Yesterday's Enterprise", info.EpisodeTitle);
        Assert.Empty(info.Tags);
    }

    [Theory]
    [InlineData("Show Name.S01E01.720p.mkv", "Show Name", null)]
    [InlineData("Show Name - S01E01.The Title With Spaces.mkv", "Show Name", "The Title With Spaces")]
    [InlineData("Mr. Robot - S01E01 - Pilot.mkv", "Mr. Robot", "Pilot")]
    public void UnPointSeparateurColleAUnNomMixteEstRetire(string name, string title, string? episodeTitle)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.Equal(title, info.Title);
        Assert.Equal(episodeTitle, info.EpisodeTitle);
    }

    [Fact]
    public void LesPointsDeSuspensionDUnTitreRangeSontConserves()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Star Trek - The Next Generation - S07E25 - All Good Things....mkv");

        Assert.Equal("All Good Things...", info.EpisodeTitle);
    }

    [Fact]
    public void LaSaisonZeroDesigneLesEpisodesSpeciaux()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Star Trek - The Next Generation - S00E01 - Retrospective.mkv");

        Assert.Equal(0, info.Season);
        Assert.Equal(1, info.Episode);
        Assert.Equal("Retrospective", info.EpisodeTitle);
    }

    [Fact]
    public void UnNomSansSerieDonneUnTitreVide()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("S01E01 - Pilote.mkv");

        Assert.Equal(string.Empty, info.Title);
        Assert.Equal("Pilote", info.EpisodeTitle);
    }

    [Fact]
    public void LAnneeEntreParenthesesDUneSerieEstLue()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Doctor Who (2005) - S01E01 - Rose.mkv");

        Assert.Equal("Doctor Who", info.Title);
        Assert.Equal(2005, info.Year);
        Assert.Equal("Rose", info.EpisodeTitle);
    }

    [Fact]
    public void UneEtiquetteEntreParenthesesApresLeTitreDisparait()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Show - S01E01 - The Beginning (1080p).mkv");

        Assert.Equal("The Beginning", info.EpisodeTitle);
        Assert.Equal(Resolution1080, info.Tags);
    }

    [Fact]
    public void UnMotFaibleAuMilieuDUnTitreDEpisodeReste()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Friends 1x05 The One with the East German Laundry Detergent.avi");

        Assert.Equal("Friends", info.Title);
        Assert.Equal(1, info.Season);
        Assert.Equal(5, info.Episode);
        Assert.Equal("The One with the East German Laundry Detergent", info.EpisodeTitle);
    }

    [Theory]
    [InlineData("Saison 1 Episode 2 - Titre.mkv", 1, 2)]
    [InlineData("Season.01.Episode.02.mkv", 1, 2)]
    [InlineData("Saison 3 - Épisode 12.mkv", 3, 12)]
    public void LesMarqueursEnToutesLettresSontReconnus(string name, int season, int episode)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.Equal(season, info.Season);
        Assert.Equal(episode, info.Episode);
    }

    [Theory]
    [InlineData("Episode 12.mkv", 12)]
    [InlineData("Ep.05 - Titre.mkv", 5)]
    [InlineData("Serie E07.mkv", 7)]
    public void UnEpisodeSansSaisonLaisseLaSaisonAbsente(string name, int episode)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.Null(info.Season);
        Assert.Equal(episode, info.Episode);
        Assert.True(info.IsEpisode);
    }

    [Theory]
    [InlineData("Saison 2", 2, "")]
    [InlineData("Season 04", 4, "")]
    [InlineData("Star.Trek.The.Next.Generation.S02.1080p.English.Esubs.Moviesmod.Org.zip", 2, "Star Trek The Next Generation")]
    public void UneSaisonSeuleSeLitSansEpisode(string name, int season, string title)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.Equal(season, info.Season);
        Assert.Null(info.Episode);
        Assert.False(info.IsEpisode);
        Assert.Equal(title, info.Title);
        Assert.Null(info.EpisodeTitle);
    }

    // ==================== Films ====================

    [Theory]
    [InlineData("Blade.Runner.1982.Final.Cut.1080p.BluRay.x264-SPARKS.mkv", "Blade Runner", 1982)]
    [InlineData("Blade Runner 2049 (2017).mkv", "Blade Runner 2049", 2017)]
    [InlineData("2001.A.Space.Odyssey.1968.2160p.UHD.BluRay.x265-TERMiNAL.mkv", "2001 A Space Odyssey", 1968)]
    [InlineData("1917.2019.1080p.mkv", "1917", 2019)]
    [InlineData("Johnny.English.2003.mkv", "Johnny English", 2003)]
    [InlineData("Charlottes.Web.1973.1080p.mkv", "Charlottes Web", 1973)]
    [InlineData("Le Fabuleux Destin d'Amélie Poulain [2001].mkv", "Le Fabuleux Destin d'Amélie Poulain", 2001)]
    public void UnFilmDonneSonTitreEtSonAnnee(string name, string title, int year)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.False(info.IsEpisode);
        Assert.Equal(title, info.Title);
        Assert.Equal(year, info.Year);
    }

    [Fact]
    public void UnFilmSansAnneeSArreteALaPremiereEtiquetteForte()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Movie.Title.1080p.WEBRip.x265.10bit.AAC5.1.mkv");

        Assert.Equal("Movie Title", info.Title);
        Assert.Null(info.Year);
        Assert.Equal(EtiquettesFilm, info.Tags);
    }

    [Fact]
    public void UnSeulMotFaibleEnFinDeTitreEstGarde()
    {
        Assert.Equal("Johnny English", ReleaseNameParser.Parse("Johnny English.mkv").Title);
    }

    [Fact]
    public void UneTraineDeMotsFaiblesEtUnSiteSontRetires()
    {
        Assert.Equal("Titre", ReleaseNameParser.Parse("Titre.FRENCH.VOSTFR.mkv").Title);
        Assert.Equal("Titre Du Film", ReleaseNameParser.Parse("Titre.Du.Film.MoviezVerse.Org.mkv").Title);
    }

    [Fact]
    public void LeGroupeEntreCrochetsEnTeteEstIgnore()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("[Groupe] Mon Film (1999) [1080p].mkv");

        Assert.Equal("Mon Film", info.Title);
        Assert.Equal(1999, info.Year);
    }

    [Fact]
    public void UnCheminNeLitQueSonDernierSegment()
    {
        ReleaseInfo info = ReleaseNameParser.Parse(@"C:\Vidéos\Saison 1\Show - S01E03 - Trois.mkv");

        Assert.Equal("Show", info.Title);
        Assert.Equal(3, info.Episode);
    }

    [Fact]
    public void SeuleUneExtensionConnueEstRetiree()
    {
        Assert.Equal("Film", ReleaseNameParser.Parse("Film.1080p").Title);
        Assert.Equal(Resolution1080, ReleaseNameParser.Parse("Film.1080p").Tags);
        Assert.Equal("Rapport final.docx", ReleaseNameParser.Parse("Rapport final.docx").Title);
    }

    [Fact]
    public void UneResolutionNEstPasUnMarqueurCroise()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Concert 1920x1080.mkv");

        Assert.False(info.IsEpisode);
        Assert.Equal("Concert 1920x1080", info.Title);
    }

    // ==================== Réglages ====================

    [Fact]
    public void LesEtiquettesSupplementairesCoupentLeTitre()
    {
        ReleaseNameOptions options = new() { ExtraTags = ["MaCollection"] };

        ReleaseInfo info = ReleaseNameParser.Parse("Titre.MaCollection.Bonus.mkv", options);

        Assert.Equal("Titre", info.Title);
        Assert.Contains("MaCollection", info.Tags);
    }

    [Fact]
    public void LesExtensionsSontReglables()
    {
        ReleaseNameOptions options = new() { Extensions = ["zip"] };

        Assert.Equal("Archive", ReleaseNameParser.Parse("Archive.zip", options).Title);
        Assert.Equal("Film mkv", ReleaseNameParser.Parse("Film.mkv", options).Title);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesBornesDAnneeSontInclusives()
    {
        ReleaseNameOptions options = new() { MinimumYear = 1950, MaximumYear = 2026 };

        Assert.Equal(1950, ReleaseNameParser.Parse("Film.1950.mkv", options).Year);
        Assert.Equal(2026, ReleaseNameParser.Parse("Film.2026.mkv", options).Year);
        Assert.Null(ReleaseNameParser.Parse("Film.1949.mkv", options).Year);
        Assert.Null(ReleaseNameParser.Parse("Blade.Runner.2049.1080p.mkv", options).Year);
        Assert.Equal("Blade Runner 2049", ReleaseNameParser.Parse("Blade.Runner.2049.1080p.mkv", options).Title);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LaLongueurMaximaleEstInclusive()
    {
        ReleaseNameOptions options = new() { MaximumLength = 12 };

        Assert.Equal("Abcdefgh", ReleaseNameParser.Parse("Abcdefgh.mkv", options).Title);
        Assert.Throws<ArgumentException>(() => ReleaseNameParser.Parse("Abcdefghi.mkv", options));
        Assert.False(ReleaseNameParser.TryParse("Abcdefghi.mkv", out _, options));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesNumerosExtremesSontLus()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Show.S0000E9999.mkv");

        Assert.Equal(0, info.Season);
        Assert.Equal(9999, info.Episode);
    }

    [Fact]
    public void DesReglagesIncoherentsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReleaseNameOptions { MinimumYear = 2000, MaximumYear = 1999 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReleaseNameOptions { MinimumYear = 999 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReleaseNameOptions { MaximumYear = 10000 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReleaseNameOptions { MaximumLength = 0 }.Validate());
        Assert.Throws<ArgumentException>(() => new ReleaseNameOptions { Extensions = ["mkv", " "] }.Validate());
        Assert.Throws<ArgumentException>(() => new ReleaseNameOptions { ExtraTags = null! }.Validate());
    }

    [Fact]
    public void ParseValideLesReglages()
    {
        ReleaseNameOptions options = new() { MaximumLength = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => ReleaseNameParser.Parse("Film.mkv", options));
    }

    [Fact]
    public void TryParseRendFauxSurDesReglagesIncoherents()
    {
        ReleaseNameOptions options = new() { MinimumYear = 2000, MaximumYear = 1999 };

        Assert.False(ReleaseNameParser.TryParse("Film.mkv", out ReleaseInfo? info, options));
        Assert.Null(info);
    }

    [Fact]
    public void LesReglagesParDefautRetirentLesExtensionsVideoEtSousTitres()
    {
        Assert.Same(ReleaseNameOptions.DefaultExtensions, ReleaseNameOptions.Default.Extensions);
        Assert.Contains("mkv", ReleaseNameOptions.DefaultExtensions);
        Assert.Contains("srt", ReleaseNameOptions.DefaultExtensions);
        Assert.DoesNotContain("zip", ReleaseNameOptions.DefaultExtensions);

        ReleaseNameOptions.Default.Validate();
        Assert.Equal("Film", ReleaseNameParser.Parse("Film.MP4", ReleaseNameOptions.Default).Title);
        Assert.Equal("Film", ReleaseNameParser.Parse("Film.srt", ReleaseNameOptions.Default).Title);
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\0")]
    [InlineData("S01E01")]
    [InlineData("S99999999999999999999E99999999999999999999")]
    [InlineData("((((((((((((((((((((((((((((((((")]
    [InlineData("S01E01S01E01S01E01S01E01S01E01S01E01")]
    [InlineData("..................................................mkv")]
    public void TryParseNeLeveJamais(string? name)
    {
        Exception? thrown = Record.Exception(() => ReleaseNameParser.TryParse(name, out _));
        Exception? thrownWithBadOptions = Record.Exception(
            () => ReleaseNameParser.TryParse(name, out _, new ReleaseNameOptions { Extensions = null!, MaximumLength = -5 }));
        Exception? thrownTooLong = Record.Exception(
            () => ReleaseNameParser.TryParse(name + new string('x', 5000), out _));

        Assert.Null(thrown);
        Assert.Null(thrownWithBadOptions);
        Assert.Null(thrownTooLong);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void DesDemiPairesDeSubstitutionIsoleesNeFontPasLever()
    {
        // Construites en code : un attribut sérialise ses chaînes en UTF-8, qui ne porte pas de demi-paire.
        string[] names =
        [
            new string((char)0xD800, 1),
            (char)0xDC00 + "abc" + (char)0xD800,
            "Show - S01E01 - Titre" + (char)0xD83C + ".mkv",
        ];

        foreach (string name in names)
        {
            Assert.True(ReleaseNameParser.TryParse(name, out ReleaseInfo? info));
            Assert.NotNull(info.Title);
        }

        Assert.Equal(1, ReleaseNameParser.Parse(names[2]).Episode);
    }

    [Fact]
    public void TryParseRendLeMemeResultatQueParse()
    {
        const string Name = "Star.Trek.The.Next.Generation.S05E25.The.Inner.Light.1080p.mkv";

        Assert.True(ReleaseNameParser.TryParse(Name, out ReleaseInfo? info));
        Assert.Equal(ReleaseNameParser.Parse(Name).EpisodeTitle, info.EpisodeTitle);
        Assert.Equal("The Inner Light", info.EpisodeTitle);
    }

    // ==================== Aléas ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnNomNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => ReleaseNameParser.Parse(null!));
        Assert.False(ReleaseNameParser.TryParse(null, out ReleaseInfo? info));
        Assert.Null(info);
    }

    [Theory]
    [Trait("hazard", "empty-input")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void UnNomVideEstRefuse(string name)
    {
        Assert.Throws<ArgumentException>(() => ReleaseNameParser.Parse(name));
        Assert.False(ReleaseNameParser.TryParse(name, out _));
    }

    [Theory]
    [Trait("hazard", "unicode-edge")]
    [InlineData("Déjà Vu (2006).mkv", "Déjà Vu", 2006)]
    [InlineData("千と千尋の神隠し (2001).mkv", "千と千尋の神隠し", 2001)]
    [InlineData("Ménage à Trois 🎬 (1999).mkv", "Ménage à Trois 🎬", 1999)]
    public void LesTitresNonAsciiSontConservesIntacts(string name, string title, int year)
    {
        ReleaseInfo info = ReleaseNameParser.Parse(name);

        Assert.Equal(title, info.Title);
        Assert.Equal(year, info.Year);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void DesChiffresDUneAutreEcritureNeSontPasDesNumeros()
    {
        // Chiffres arabes-indiens : ٠١٢٣ — ils ne doivent ni lever ni devenir un numéro d'épisode.
        string name = "Show S" + (char)0x0661 + "E" + (char)0x0662 + (char)0x0663 + ".mkv";

        Assert.True(ReleaseNameParser.TryParse(name, out ReleaseInfo? info));
        Assert.False(info.IsEpisode);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnTitreDEpisodeAccentueRangeEstConserve()
    {
        ReleaseInfo info = ReleaseNameParser.Parse("Star Trek - The Next Generation - S03E24 - Ménage à Troi.mkv");

        Assert.Equal("Ménage à Troi", info.EpisodeTitle);
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData(".mkv")]
    [InlineData("....")]
    [InlineData("- - -")]
    [InlineData("()[]{}")]
    [InlineData("1080p.x264.mkv")]
    [InlineData("S.E.mkv")]
    [InlineData("[[[[")]
    [InlineData("\\/\\/")]
    public void UnNomSansContenuLisibleNeLevePas(string name)
    {
        Assert.True(ReleaseNameParser.TryParse(name, out ReleaseInfo? info));
        Assert.NotNull(info.Title);
        Assert.DoesNotContain(info.Title, c => c is '.' or '_');
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UneLongueSuiteDeChiffresNeDebordePas()
    {
        string name = "S" + new string('9', 400) + "E" + new string('9', 400) + ".mkv";

        Assert.True(ReleaseNameParser.TryParse(name, out ReleaseInfo? info));
        Assert.False(info.IsEpisode);
    }
}
