using Xunit;

namespace Micro.Markdown.CodeSpans.Tests;

public sealed class CodeSpansTests
{
    private static string Slice(string text, CodeSpan span) => text.Substring(span.Start, span.Length);

    // ==================== Blocs délimités ====================

    [Fact]
    public void UnBlocDelimiteParDesBackticksEstReleveAvecSesClotures()
    {
        string text = "avant\n```csharp\nvar x = 1;\n```\naprès";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal(CodeSpanKind.Fenced, span.Kind);
        Assert.Equal("```csharp\nvar x = 1;\n```", Slice(text, span));
    }

    [Fact]
    public void UnBlocDelimiteParDesTildesEstReleve()
    {
        string text = "~~~\n[[pas un lien]]\n~~~\n";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal("~~~\n[[pas un lien]]\n~~~", Slice(text, span));
    }

    [Fact]
    public void UneClotureFermanteDoitEtreAuMoinsAussiLongue()
    {
        string text = "````\n```\nencore du code\n````\nprose";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.EndsWith("encore du code\n````", Slice(text, span), StringComparison.Ordinal);
    }

    [Fact]
    public void UneClotureDeNatureDifferenteNeFermePas()
    {
        string text = "```\n~~~\n```";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal(text.Length, span.Length);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnBlocJamaisRefermeCourtJusquALaFin()
    {
        string text = "prose\n```\ncode sans fin\n`encore`";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal(CodeSpanKind.Fenced, span.Kind);
        Assert.Equal(text.Length, span.End);
    }

    [Fact]
    public void UnBlocDansUneCitationEstReconnu()
    {
        string text = "> [!note]\n> ```\n> #pas-une-etiquette\n> ```\n";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.StartsWith("```", Slice(text, span), StringComparison.Ordinal);
        Assert.Contains("#pas-une-etiquette", Slice(text, span), StringComparison.Ordinal);
    }

    [Fact]
    public void UneClotureDontLInformationContientUnBacktickEstDuCodeEnLigne()
    {
        string text = "```a``` suite";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal(CodeSpanKind.Inline, span.Kind);
        Assert.Equal("```a```", Slice(text, span));
    }

    [Fact]
    public void LesFinsDeLigneWindowsSontAcceptees()
    {
        string text = "a\r\n```\r\ncode\r\n```\r\nb `x`";

        IReadOnlyList<CodeSpan> spans = CodeSpanScanner.Scan(text);

        Assert.Equal(2, spans.Count);
        Assert.Equal("```\r\ncode\r\n```", Slice(text, spans[0]));
        Assert.Equal("`x`", Slice(text, spans[1]));
    }

    // ==================== Segments en ligne ====================

    [Fact]
    public void UnSegmentEnLigneEstReleveAvecSesBackticks()
    {
        string text = "voir `[[Note]]` ici";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal(CodeSpanKind.Inline, span.Kind);
        Assert.Equal("`[[Note]]`", Slice(text, span));
    }

    [Fact]
    public void UnSegmentDoubleContientUnBacktickSimple()
    {
        string text = "a ``x ` y`` b";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal("``x ` y``", Slice(text, span));
    }

    [Fact]
    public void UnBacktickEchappeNOuvrePasDeSegment()
    {
        Assert.Empty(CodeSpanScanner.Scan(@"prix \`5` et rien"));
    }

    [Fact]
    public void UnDoubleAntislashNEchappePasLeBacktick()
    {
        string text = @"a \\`code` b";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal("`code`", Slice(text, span));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UnBacktickOrphelinResteDeLaProse()
    {
        Assert.Empty(CodeSpanScanner.Scan("un ` seul, puis rien"));
    }

    [Fact]
    public void UnSegmentPeutCouvrirPlusieursLignesDuMemeParagraphe()
    {
        string text = "début `code\nsuite` fin";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal("`code\nsuite`", Slice(text, span));
    }

    [Fact]
    public void UneLigneVideInterromptUnSegment()
    {
        Assert.Empty(CodeSpanScanner.Scan("début `code\n\nsuite` fin"));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LeNombreDeLignesDUnSegmentEstBorne()
    {
        string troisLignes = "`a\nb\nc`";
        CodeSpanOptions deux = new() { MaxInlineLines = 2 };
        CodeSpanOptions trois = new() { MaxInlineLines = 3 };

        Assert.Empty(CodeSpanScanner.Scan(troisLignes, deux));
        Assert.Single(CodeSpanScanner.Scan(troisLignes, trois));
    }

    [Fact]
    public void LeCodeEnLigneNEstPasCherchéDansUnBlocDelimite()
    {
        string text = "```\n`dedans`\n```\n`dehors`";

        IReadOnlyList<CodeSpan> spans = CodeSpanScanner.Scan(text);

        Assert.Equal(2, spans.Count);
        Assert.Equal(CodeSpanKind.Fenced, spans[0].Kind);
        Assert.Equal("`dehors`", Slice(text, spans[1]));
    }

    // ==================== Blocs indentés ====================

    [Fact]
    public void LesBlocsIndentesSontIgnoresParDefaut()
    {
        Assert.Empty(CodeSpanScanner.Scan("liste\n\n    - sous-point [[Lien]]"));
    }

    [Fact]
    public void UnBlocIndenteApresUneLigneVideEstReleveSurDemande()
    {
        string text = "prose\n\n    code();\n    encore();\n\nprose";
        CodeSpanOptions options = new() { IncludeIndented = true };

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text, options));

        Assert.Equal(CodeSpanKind.Indented, span.Kind);
        Assert.Equal("    code();\n    encore();", Slice(text, span));
    }

    [Fact]
    public void UneLigneIndenteeQuiSuitUnParagrapheNEstPasDuCode()
    {
        CodeSpanOptions options = new() { IncludeIndented = true };

        Assert.Empty(CodeSpanScanner.Scan("prose\n    continuation", options));
    }

    [Fact]
    public void LeRelevePeutSeLimiterAuxBlocs()
    {
        string text = "```\nx\n```\n`y`";
        CodeSpanOptions options = new() { IncludeInline = false };

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text, options));

        Assert.Equal(CodeSpanKind.Fenced, span.Kind);
    }

    [Fact]
    public void LeRelevePeutSeLimiterAuxSegments()
    {
        string text = "```\nx\n```\n`y`";
        CodeSpanOptions options = new() { IncludeFenced = false };

        IReadOnlyList<CodeSpan> spans = CodeSpanScanner.Scan(text, options);

        Assert.All(spans, s => Assert.Equal(CodeSpanKind.Inline, s.Kind));
        Assert.Contains(spans, s => Slice(text, s) == "`y`");
    }

    // ==================== Positions ====================

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesPositionsSontDesIndexUtf16MemeApresUnEmoji()
    {
        string text = "🌌 étoiles `code` ✨";

        CodeSpan span = Assert.Single(CodeSpanScanner.Scan(text));

        Assert.Equal(text.IndexOf('`', StringComparison.Ordinal), span.Start);
        Assert.Equal("`code`", Slice(text, span));
    }

    [Fact]
    public void IsInsideTrouveLesPositionsCouvertes()
    {
        string text = "a `b` c `d` e";
        IReadOnlyList<CodeSpan> spans = CodeSpanScanner.Scan(text);

        Assert.True(CodeSpanScanner.IsInside(spans, text.IndexOf('b', StringComparison.Ordinal)));
        Assert.True(CodeSpanScanner.IsInside(spans, text.IndexOf('d', StringComparison.Ordinal)));
        Assert.False(CodeSpanScanner.IsInside(spans, text.IndexOf('c', StringComparison.Ordinal)));
        Assert.False(CodeSpanScanner.IsInside(spans, text.Length));
    }

    [Fact]
    public void OverlapsDetecteUnChevauchementPartiel()
    {
        string text = "abc `code` def";
        IReadOnlyList<CodeSpan> spans = CodeSpanScanner.Scan(text);

        Assert.True(CodeSpanScanner.Overlaps(spans, 2, 3));
        Assert.False(CodeSpanScanner.Overlaps(spans, 0, 4));
        Assert.False(CodeSpanScanner.Overlaps(spans, 10, 4));
    }

    [Fact]
    public void CodeSpanContainsRespecteLaBorneExclusive()
    {
        CodeSpan span = new(4, 3, CodeSpanKind.Inline);

        Assert.True(span.Contains(4));
        Assert.True(span.Contains(6));
        Assert.False(span.Contains(7));
        Assert.Equal(7, span.End);
    }

    // ==================== Entrées limites et paramétrage ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnTexteNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => CodeSpanScanner.Scan(null!));
        Assert.Throws<ArgumentNullException>(() => CodeSpanScanner.IsInside(null!, 0));
        Assert.Throws<ArgumentNullException>(() => CodeSpanScanner.Overlaps(null!, 0, 1));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnTexteVideNeContientAucuneplage()
    {
        Assert.Empty(CodeSpanScanner.Scan(string.Empty));
        Assert.False(CodeSpanScanner.IsInside([], 0));
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UneLongueurNegativeEstRefusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CodeSpanScanner.Overlaps([], 0, -1));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void LesBornesExtremesNeDebordentPas()
    {
        IReadOnlyList<CodeSpan> spans = [new CodeSpan(int.MaxValue - 2, 2, CodeSpanKind.Inline)];

        Assert.True(CodeSpanScanner.Overlaps(spans, int.MaxValue - 1, int.MaxValue));
        Assert.True(CodeSpanScanner.IsInside(spans, int.MaxValue - 1));
        Assert.False(CodeSpanScanner.IsInside(spans, int.MaxValue));
        Assert.False(CodeSpanScanner.IsInside(spans, int.MinValue));
        Assert.False(CodeSpanScanner.Overlaps(spans, int.MinValue, 0));

        CodeSpan extreme = new(int.MaxValue - 1, 5, CodeSpanKind.Fenced);
        Assert.True(extreme.Contains(int.MaxValue));
        Assert.False(extreme.Contains(-1));
    }

    [Fact]
    public void LesReglagesParDefautSontValidesEtPartages()
    {
        CodeSpanOptions.Default.Validate();

        Assert.Same(CodeSpanOptions.Default, CodeSpanOptions.Default);
        Assert.True(CodeSpanOptions.Default.IncludeFenced);
        Assert.True(CodeSpanOptions.Default.IncludeInline);
        Assert.False(CodeSpanOptions.Default.IncludeIndented);
        Assert.Equal(8, CodeSpanOptions.Default.MaxInlineLines);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void ValidateAccepteLaBorneEtRefuseEnDessous()
    {
        new CodeSpanOptions { MaxInlineLines = 1 }.Validate();

        Assert.Throws<ArgumentOutOfRangeException>(() => new CodeSpanOptions { MaxInlineLines = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CodeSpanOptions { MaxInlineLines = int.MinValue }.Validate());
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void DesOptionsIncoherentesSontRefusees()
    {
        CodeSpanOptions options = new() { MaxInlineLines = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => CodeSpanScanner.Scan("`a`", options));
    }
}
