namespace SnippetForge;

/// <summary>
/// Génère la structure conforme d'un nouveau micropackage :
/// src/, tests/, README.md avec les sections obligatoires.
/// </summary>
public static class Scaffolder
{
    /// <summary>Crée le squelette du micropackage et retourne son dossier.</summary>
    public static string Scaffold(ForgeRoot root, string packageId, string description, IReadOnlyList<string> tags)
    {
        var packageDir = Path.Combine(root.PackagesDir, packageId);
        if (Directory.Exists(packageDir))
        {
            throw new InvalidOperationException($"Le package {packageId} existe déjà : {packageDir}");
        }

        var className = packageId.Split('.')[^1];
        var srcDir = Path.Combine(packageDir, "src");
        var testsDir = Path.Combine(packageDir, "tests");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(testsDir);

        File.WriteAllText(Path.Combine(packageDir, "README.md"), ReadmeTemplate(packageId, description));
        File.WriteAllText(Path.Combine(srcDir, $"{packageId}.csproj"), SrcProjectTemplate(packageId, description, tags));
        File.WriteAllText(Path.Combine(srcDir, $"{className}.cs"), ClassTemplate(packageId, className));
        File.WriteAllText(Path.Combine(testsDir, $"{packageId}.Tests.csproj"), TestProjectTemplate(packageId));
        File.WriteAllText(Path.Combine(testsDir, $"{className}Tests.cs"), TestClassTemplate(packageId, className));

        return packageDir;
    }

    /// <summary>
    /// Expose le gabarit aux tests : ils vérifient qu'un README scaffoldé mais non
    /// rédigé est bien refusé à la publication.
    /// </summary>
    public static string ReadmeTemplateForTests(string packageId, string description) =>
        ReadmeTemplate(packageId, description);

    private static string ReadmeTemplate(string packageId, string description) => $"""
        # {packageId}

        ## Description

        {description}

        ## Mode d'emploi

        <!-- Expliquer QUAND utiliser ce package (cas d'usage) et QUAND ne pas l'utiliser. -->
        À compléter.

        ## Paramétrage

        <!-- Tableau : paramètre | type | défaut | rôle. Documenter chaque option. -->
        | Paramètre | Type | Défaut | Rôle |
        |-----------|------|--------|------|
        | À compléter | - | - | - |

        ## Exemple

        ```csharp
        // Exemple d'appel minimal, compilable, à compléter.
        ```
        """;

    private static string SrcProjectTemplate(string packageId, string description, IReadOnlyList<string> tags) => $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <!-- Auteur, licence, symboles de débogage et attribution sont hérités de
               packages/Directory.Build.props : ne pas les redéclarer ici. -->
          <PropertyGroup>
            <PackageId>{packageId}</PackageId>
            <Version>1.0.0</Version>
            <Description>{description}</Description>
            <PackageTags>{string.Join(';', tags)}</PackageTags>
            <PackageReadmeFile>README.md</PackageReadmeFile>
          </PropertyGroup>

          <ItemGroup>
            <None Include="../README.md" Pack="true" PackagePath="\" />
            <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
          </ItemGroup>

        </Project>
        """;

    private static string ClassTemplate(string packageId, string className) => $$"""
        namespace {{packageId}};

        /// <summary>À compléter : contrat de la méthode générique, une seule responsabilité.</summary>
        public static class {{className}}
        {
            // TODO : implémenter la méthode générique unique de ce micropackage.
            // Rappels RULES.md : pas de Console, pas d'horloge/IO ambiante, tout est injecté,
            // logging via Microsoft.Extensions.Logging.ILogger si l'opération est multi-étapes.
        }
        """;

    private static string TestProjectTemplate(string packageId) => $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <IsPackable>false</IsPackable>
            <GenerateDocumentationFile>false</GenerateDocumentationFile>
            <!-- CA1707 : les noms de tests avec underscores (Cas_Attendu) sont la convention xUnit. -->
            <NoWarn>$(NoWarn);CS1591;CA1707</NoWarn>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
            <PackageReference Include="xunit" Version="2.9.2" />
            <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
          </ItemGroup>

          <ItemGroup>
            <ProjectReference Include="../src/{packageId}.csproj" />
          </ItemGroup>

        </Project>
        """;

    private static string TestClassTemplate(string packageId, string className) => $$"""
        using Xunit;

        namespace {{packageId}}.Tests;

        public sealed class {{className}}Tests
        {
            [Fact]
            public void RemplacerParDeVraisTests()
            {
                // Ce test échoue volontairement : un micropackage sans vrais tests
                // ne peut pas être publié (RULES.md).
                Assert.Fail("Écrire de vrais tests couvrant le comportement nominal et les cas limites.");
            }
        }
        """;
}
