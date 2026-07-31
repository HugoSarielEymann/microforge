using SnippetForge.Api;
using SnippetForge.Quality;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Ces tests rejouent les deux défauts réellement passés au travers en manche 4 :
/// un TryParse qui levait, et un membre public non couvert.
/// </summary>
public sealed class ReviewAnalyzerTests
{
    private static ApiSurface Surface(params string[] members) =>
        new("Micro.Text.CompactDuration", "1.0.0", members);

    private const string ParseMember =
        "method Micro.Text.CompactDuration.CompactDurationParser.Parse(System.String) : System.TimeSpan";

    private const string TryParseMember =
        "method Micro.Text.CompactDuration.CompactDurationParser.TryParse(System.String, ref System.TimeSpan) : System.Boolean";

    [Fact]
    public void MembrePublicJamaisCiteDansLesTests_EstSignaleCommeAbsence()
    {
        var findings = ReviewAnalyzer.Analyze(Surface(ParseMember), string.Empty, "public void Test() { }");

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewSeverity.Gap, finding.Severity);
        Assert.Equal("Parse", finding.Subject);
    }

    [Fact]
    public void MembreCite_NEstPasSignaleCommeAbsence()
    {
        var findings = ReviewAnalyzer.Analyze(
            Surface(ParseMember), string.Empty, "CompactDurationParser.Parse(\"30s\");");

        Assert.DoesNotContain(findings, f => f.Severity == ReviewSeverity.Gap && f.Subject == "Parse");
    }

    [Fact]
    public void MethodeTryXxx_DeclencheUneQuestionSurLaNonLevee()
    {
        // Le défaut de la manche 4 : TryParse propageait OverflowException.
        var findings = ReviewAnalyzer.Analyze(
            Surface(TryParseMember), string.Empty, "CompactDurationParser.TryParse(\"2h\", out var r);");

        Assert.Contains(findings, f =>
            f.Category == "contrat TryXxx" &&
            f.Subject == "TryParse" &&
            f.Question.Contains("jamais", StringComparison.Ordinal));
    }

    [Fact]
    public void MethodeOrdinaire_NeDeclenchePasLaQuestionTryXxx()
    {
        var findings = ReviewAnalyzer.Analyze(Surface(ParseMember), string.Empty, "Parse(\"30s\");");

        Assert.DoesNotContain(findings, f => f.Category == "contrat TryXxx");
    }

    [Fact]
    public void ExceptionDocumenteeJamaisProvoquee_EstSignalee()
    {
        const string source = """
            /// <exception cref="FormatException">Si le format est invalide.</exception>
            /// <exception cref="ArgumentNullException">Si l'entrée est nulle.</exception>
            public static TimeSpan Parse(string input) => default;
            """;
        const string tests = "Assert.Throws<ArgumentNullException>(() => Parse(null!));";

        var findings = ReviewAnalyzer.Analyze(Surface(ParseMember), source, tests);

        Assert.Contains(findings, f => f.Category == "exception documentée" && f.Subject == "FormatException");
        Assert.DoesNotContain(findings, f => f.Subject == "ArgumentNullException");
    }

    [Fact]
    public void ExceptionDocumenteeAvecPrefixeT_EstReconnue()
    {
        const string source = """/// <exception cref="T:System.FormatException">…</exception>""";

        var findings = ReviewAnalyzer.Analyze(Surface(ParseMember), source, "Parse();");

        Assert.Contains(findings, f => f.Subject == "FormatException");
    }

    [Fact]
    public void MethodeNumeriqueSansTestAuxBornes_DeclencheUneQuestion()
    {
        const string member =
            "method Micro.Text.CompactDuration.Calc.Add(System.Int32, System.Int32) : System.Int32";

        var findings = ReviewAnalyzer.Analyze(Surface(member), string.Empty, "Calc.Add(1, 2);");

        Assert.Contains(findings, f => f.Category == "bornes numériques" && f.Subject == "Add");
    }

    [Theory]
    [InlineData("int.MaxValue")]
    [InlineData("int.MinValue")]
    [InlineData("OverflowException")]
    public void MentionDUneValeurExtreme_EteintLaQuestionDesBornes(string mention)
    {
        const string member =
            "method Micro.Text.CompactDuration.Calc.Add(System.Int32, System.Int32) : System.Int32";

        var findings = ReviewAnalyzer.Analyze(Surface(member), string.Empty, $"Calc.Add({mention}, 2);");

        Assert.DoesNotContain(findings, f => f.Category == "bornes numériques");
    }

    [Fact]
    public void ConstructeurSansParametre_NEstPasSignale()
    {
        var findings = ReviewAnalyzer.Analyze(
            Surface("ctor Micro.Text.CompactDuration.Options()"), string.Empty, "rien");

        Assert.Empty(findings);
    }

    [Fact]
    public void PackageBienCouvert_NeProduitAucuneRemarque()
    {
        const string source = """/// <exception cref="FormatException">…</exception>""";
        const string tests = """
            Assert.Throws<FormatException>(() => Parse("x"));
            Assert.False(TryParse("99999999999h", out _));
            Assert.Equal(int.MaxValue, Add(int.MaxValue, 0));
            """;

        // TryParse reste une question ouverte par construction : le motif l'exige.
        var findings = ReviewAnalyzer.Analyze(Surface(ParseMember), source, tests);

        Assert.Empty(findings);
    }

    [Fact]
    public void LesAbsencesPassentAvantLesQuestions()
    {
        var findings = ReviewAnalyzer.Analyze(
            Surface(ParseMember, TryParseMember), string.Empty, "TryParse(\"2h\", out var r);");

        Assert.Equal(ReviewSeverity.Gap, findings[0].Severity);
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ReviewAnalyzer.Analyze(null!, "", ""));
        Assert.Throws<ArgumentNullException>(() => ReviewAnalyzer.Analyze(Surface(), null!, ""));
        Assert.Throws<ArgumentNullException>(() => ReviewAnalyzer.Analyze(Surface(), "", null!));
    }
}
