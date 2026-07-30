namespace SnippetForge.Tests;

/// <summary>
/// Racine MicroForge jetable, créée dans un dossier temporaire et supprimée à la fin
/// du test. Permet d'exercer les commandes qui écrivent sur disque sans toucher au
/// feed réel.
/// </summary>
public sealed class TempForge : IDisposable
{
    private TempForge(string path, ForgeRoot root)
    {
        Path = path;
        Root = root;
    }

    /// <summary>Chemin absolu de la racine temporaire.</summary>
    public string Path { get; }

    /// <summary>Racine résolue par l'outil.</summary>
    public ForgeRoot Root { get; }

    /// <summary>
    /// Crée une racine temporaire valide (RULES.md + feed/ + registry/ + packages/).
    ///
    /// Utilise <see cref="ForgeRoot.At"/> plutôt que la variable d'environnement
    /// MICROFORGE_ROOT : celle-ci est globale au processus, et xUnit exécute les
    /// classes de tests en parallèle — la muter provoquerait des échecs aléatoires.
    /// </summary>
    public static TempForge Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "microforge-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(System.IO.Path.Combine(path, "feed"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "registry"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "packages"));
        File.WriteAllText(System.IO.Path.Combine(path, "RULES.md"), "# Règles de test");

        return new TempForge(path, ForgeRoot.At(path));
    }

    /// <summary>Écrit un micropackage source minimal et retourne son dossier.</summary>
    public string WritePackage(
        string id,
        string version = "1.0.0",
        string? description = null,
        string tags = "alpha;beta;gamma",
        string? readme = null,
        string? sourceCode = null,
        string? testCode = null)
    {
        var directory = System.IO.Path.Combine(Path, "packages", id);
        var src = System.IO.Path.Combine(directory, "src");
        var tests = System.IO.Path.Combine(directory, "tests");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tests);

        description ??= $"Description suffisamment longue pour le package {id} et ses cas d'usage.";

        File.WriteAllText(System.IO.Path.Combine(directory, "README.md"), readme ?? DefaultReadme(id));
        File.WriteAllText(System.IO.Path.Combine(src, $"{id}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>{id}</PackageId>
                <Version>{version}</Version>
                <Description>{description}</Description>
                <PackageTags>{tags}</PackageTags>
                <PackageReadmeFile>README.md</PackageReadmeFile>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(System.IO.Path.Combine(src, "Entry.cs"), sourceCode ?? "namespace Sample; public static class Entry { }");
        File.WriteAllText(System.IO.Path.Combine(tests, "EntryTests.cs"), testCode ?? "public class EntryTests { [Fact] public void Works() { } }");

        return directory;
    }

    /// <summary>README complet, satisfaisant les contrôles de contenu de ReadmeQuality.</summary>
    public static string DefaultReadme(string id) => $$"""
        # {{id}}

        ## Description

        Capacité générique de test, décrite avec assez de détail pour être trouvée par
        le moteur de recherche de la bibliothèque.

        ## Mode d'emploi

        Utiliser cette capacité lorsque le besoin correspond exactement au contrat décrit
        ci-dessus ; ne pas l'utiliser pour un cas particulier au projet appelant.

        ## Paramétrage

        | Paramètre | Type | Défaut | Rôle |
        |-----------|------|--------|------|
        | `value` | `string` | — | Valeur d'entrée à traiter par la méthode. |

        ## Exemple

        ```csharp
        using {{id}};

        var resultat = Entry.Run("valeur");
        ```
        """;

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}
