namespace SnippetForge;

/// <summary>
/// Lignes de commande de build partagées par la validation et la publication.
///
/// Elles sont centralisées ici pour une raison précise : les tests et l'empaquetage
/// doivent porter sur la **même configuration**. Tester en Debug puis livrer du
/// Release reviendrait à valider un binaire et à en publier un autre — ce qui ruine
/// la garantie « le package récupéré est celui qui a été prouvé ».
/// </summary>
public static class BuildCommands
{
    /// <summary>Configuration unique utilisée pour tester ET empaqueter.</summary>
    public const string Configuration = "Release";

    /// <summary>Arguments de <c>dotnet test</c> pour le dossier de tests d'un package.</summary>
    public static string TestArguments(string testsDirectory = "tests") =>
        $"test {testsDirectory} -c {Configuration} --nologo -v quiet";

    /// <summary>Arguments de <c>dotnet pack</c> vers un dossier de sortie.</summary>
    public static string PackArguments(string outputDirectory, string sourceDirectory = "src") =>
        $"pack {sourceDirectory} -c {Configuration} -o \"{outputDirectory}\" --nologo -v quiet";
}
