using SnippetForge.Consumers;
using Xunit;

namespace SnippetForge.Tests;

public sealed class AgentInstructionsWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "microforge-instructions", Guid.NewGuid().ToString("N"));

    public AgentInstructionsWriterTests() => Directory.CreateDirectory(_directory);

    private string ClaudeMdPath => Path.Combine(_directory, "CLAUDE.md");

    private const string ForgeRoot = @"C:\MicroForge";

    [Fact]
    public void BuildBlock_ProfilVerifie_ConsommeParReferenceNuGet()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot, Languages.LanguageProfiles.CSharp);

        Assert.Contains("dotnet add package", block, StringComparison.Ordinal);
        Assert.DoesNotContain("forge copy", block, StringComparison.Ordinal);
        Assert.Contains("[Trait(\"hazard\"", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBlock_ProfilDeBase_ConsommeParCopieEtAnnonceLaDegradation()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot, Languages.LanguageProfiles.Python);

        // Une instruction « dotnet add package » dans un dépôt Python enverrait
        // l'agent sur une commande qui n'existe pas.
        Assert.DoesNotContain("dotnet add package", block, StringComparison.Ordinal);
        Assert.Contains("forge copy", block, StringComparison.Ordinal);
        Assert.Contains("--language python", block, StringComparison.Ordinal);
        Assert.Contains("# hazard: <id>", block, StringComparison.Ordinal);
        Assert.Contains("profil de base", block, StringComparison.Ordinal);
        Assert.Contains("forge copied", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_ProfilDifferent_RemplaceLeBloc()
    {
        AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot, Languages.LanguageProfiles.CSharp);
        var outcome = AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot, Languages.LanguageProfiles.Python);

        Assert.Equal(InstructionsOutcome.Replaced, outcome);
        Assert.DoesNotContain("dotnet add package", File.ReadAllText(ClaudeMdPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_FichierAbsent_LeCree()
    {
        var outcome = AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);

        Assert.Equal(InstructionsOutcome.Created, outcome);
        Assert.Contains("MicroForge", File.ReadAllText(ClaudeMdPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_ReferenceLesCheminsDeLaRacine()
    {
        AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);
        var content = File.ReadAllText(ClaudeMdPath);

        Assert.Contains(Path.Combine(ForgeRoot, "AGENT.md"), content, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(ForgeRoot, "RULES.md"), content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_DeuxFois_EstIdempotent()
    {
        AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);
        var first = File.ReadAllText(ClaudeMdPath);

        var outcome = AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);

        Assert.Equal(InstructionsOutcome.Unchanged, outcome);
        Assert.Equal(first, File.ReadAllText(ClaudeMdPath));
    }

    [Fact]
    public void Ensure_FichierExistant_PreserveLeContenuDuProjet()
    {
        const string projectContent = "# Mon projet\n\nInstructions propres au projet.\n";
        File.WriteAllText(ClaudeMdPath, projectContent);

        var outcome = AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);
        var content = File.ReadAllText(ClaudeMdPath);

        Assert.Equal(InstructionsOutcome.Appended, outcome);
        Assert.StartsWith(projectContent, content, StringComparison.Ordinal);
        Assert.Contains("microforge:begin", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_RacineDeplacee_RemplaceLeBlocSansToucherAuReste()
    {
        File.WriteAllText(ClaudeMdPath, "# Mon projet\n\nAvant.\n");
        AgentInstructionsWriter.Ensure(ClaudeMdPath, @"C:\ancien\MicroForge");

        var outcome = AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);
        var content = File.ReadAllText(ClaudeMdPath);

        Assert.Equal(InstructionsOutcome.Replaced, outcome);
        Assert.Contains("# Mon projet", content, StringComparison.Ordinal);
        Assert.Contains("Avant.", content, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(ForgeRoot, "AGENT.md"), content, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\ancien\MicroForge", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_PreserveLeContenuPlaceApresLeBloc()
    {
        File.WriteAllText(ClaudeMdPath, "# Avant\n");
        AgentInstructionsWriter.Ensure(ClaudeMdPath, @"C:\ancien");
        File.AppendAllText(ClaudeMdPath, "\n## Section ajoutée après\n");

        AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);
        var content = File.ReadAllText(ClaudeMdPath);

        Assert.Contains("# Avant", content, StringComparison.Ordinal);
        Assert.Contains("## Section ajoutée après", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ensure_NInsereQuUnSeulBloc()
    {
        AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);
        AgentInstructionsWriter.Ensure(ClaudeMdPath, @"C:\autre");
        AgentInstructionsWriter.Ensure(ClaudeMdPath, ForgeRoot);

        var occurrences = File.ReadAllText(ClaudeMdPath).Split("microforge:begin").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void BuildBlock_EstDelimiteParLesMarqueurs()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.StartsWith("<!-- microforge:begin -->", block, StringComparison.Ordinal);
        Assert.EndsWith("<!-- microforge:end -->", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBlock_ImposeLaRechercheAvantEcriture()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.Contains("forge search", block, StringComparison.Ordinal);
        Assert.Contains("ne jamais le recoder", block, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Régression : les agents ne suivent pas de façon fiable un chemin de fichier
    /// cité en référence. Le premier test réel l'a montré — AGENT.md n'a jamais été
    /// ouvert, et la moitié « forger un package » du workflow n'a donc jamais été
    /// appliquée. Le bloc doit se suffire à lui-même.
    /// </summary>
    [Fact]
    public void BuildBlock_EstAutonome_ContientLeCycleDeForgeComplet()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.Contains("forge new ", block, StringComparison.Ordinal);
        Assert.Contains("forge validate", block, StringComparison.Ordinal);
        Assert.Contains("forge publish", block, StringComparison.Ordinal);
        Assert.Contains("forge bump", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBlock_DecritCeQuIlFautEcrireDansUnPackage()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.Contains("## Description", block, StringComparison.Ordinal);
        Assert.Contains("## Mode d'emploi", block, StringComparison.Ordinal);
        Assert.Contains("## Paramétrage", block, StringComparison.Ordinal);
        Assert.Contains("## Exemple", block, StringComparison.Ordinal);
        Assert.Contains("xUnit", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBlock_EnumereLesApiInterdites()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.Contains("Console.", block, StringComparison.Ordinal);
        Assert.Contains("DateTime.Now", block, StringComparison.Ordinal);
        Assert.Contains("Thread.Sleep", block, StringComparison.Ordinal);
        Assert.Contains("ILogger", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Régression : l'agent avait reçu « candidat à la création » sans consigne, et
    /// avait écrit le code en ligne faute de savoir quoi faire de ce message.
    /// </summary>
    [Fact]
    public void BuildBlock_TraiteLeCasAucunResultat()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.Contains("Aucun micropackage ne correspond", block, StringComparison.Ordinal);
        Assert.Contains("Ne pas écrire ce code en ligne", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Régression : le premier jeu de tests a vu l'agent traiter un découpage en lots
    /// comme un besoin à forger, alors qu'Enumerable.Chunk existe depuis .NET 6.
    /// </summary>
    [Fact]
    public void BuildBlock_ImposeDeVerifierLaBibliothequeStandardDabord()
    {
        var block = AgentInstructionsWriter.BuildBlock(ForgeRoot);

        Assert.Contains("Enumerable.Chunk", block, StringComparison.Ordinal);
        Assert.Contains("double le framework", block, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectTargets_CouvrentClaudeCodeEtCopilot()
    {
        var paths = AgentInstructionsWriter.ProjectTargets.Select(t => t.RelativePath).ToList();

        Assert.Contains("CLAUDE.md", paths, StringComparer.Ordinal);
        Assert.Contains(Path.Combine(".github", "copilot-instructions.md"), paths, StringComparer.Ordinal);
    }

    [Fact]
    public void Ensure_CreeLesSousDossiersManquants()
    {
        // .github/ n'existe pas dans un projet neuf : l'écriture doit le créer.
        var nested = Path.Combine(_directory, ".github", "copilot-instructions.md");

        var outcome = AgentInstructionsWriter.Ensure(nested, ForgeRoot);

        Assert.Equal(InstructionsOutcome.Created, outcome);
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Ensure_ContenuIdentiquePourTousLesAgents()
    {
        // Une seule source de vérité : les agents reçoivent exactement les mêmes
        // consignes, seul le nom de fichier change.
        var claude = Path.Combine(_directory, "CLAUDE.md");
        var copilot = Path.Combine(_directory, ".github", "copilot-instructions.md");

        AgentInstructionsWriter.Ensure(claude, ForgeRoot);
        AgentInstructionsWriter.Ensure(copilot, ForgeRoot);

        Assert.Equal(File.ReadAllText(claude), File.ReadAllText(copilot));
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData("", 2)]
    [InlineData("Claude", 1)]
    [InlineData("Copilot", 1)]
    [InlineData("Claude,Copilot", 2)]
    [InlineData("Inconnu", 0)]
    public void SelectTargets_FiltreParNomDAgent(string? filter, int expected) =>
        Assert.Equal(expected, AgentInstructionsWriter.SelectTargets(filter).Count);

    [Fact]
    public void SelectTargets_ClaudeSeul_NEcritPasLeFichierCopilot()
    {
        var target = Assert.Single(AgentInstructionsWriter.SelectTargets("Claude"));
        Assert.Equal("CLAUDE.md", target.RelativePath);
    }

    [Fact]
    public void Ensure_ArgumentsVides_LeventArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AgentInstructionsWriter.Ensure("  ", ForgeRoot));
        Assert.Throws<ArgumentException>(() => AgentInstructionsWriter.Ensure(ClaudeMdPath, "  "));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}
