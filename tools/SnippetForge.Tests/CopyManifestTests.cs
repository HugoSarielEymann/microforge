using SnippetForge.Copy;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Le manifeste est ce qui distingue une copie d'un copier-coller : sans lui, le code
/// copié devient orphelin et le corpus retrouve le problème qu'il existe pour résoudre.
/// </summary>
public sealed class CopyManifestTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "microforge-copy", Guid.NewGuid().ToString("N"));

    public CopyManifestTests() => Directory.CreateDirectory(_project);

    private CopiedPackage Copy(string relativePath, string content)
    {
        var full = Path.Combine(_project, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);

        return new CopiedPackage(
            "Micro.A.One", "1.0.0", Path.GetDirectoryName(relativePath)!, DateTime.UtcNow,
            new Dictionary<string, string>(StringComparer.Ordinal) { [relativePath] = CopyManifest.Hash(full) });
    }

    [Fact]
    public void FichierIntact_EstReconnuConforme()
    {
        var package = Copy(Path.Combine("MicroForge", "a.cs"), "public class A { }");

        var état = Assert.Single(CopyManifest.Inspect(_project, package));
        Assert.Equal(CopiedFileState.Untouched, état.State);
    }

    [Fact]
    public void FichierRetouche_EstDetecte()
    {
        var relative = Path.Combine("MicroForge", "a.cs");
        var package = Copy(relative, "public class A { }");
        File.WriteAllText(Path.Combine(_project, relative), "public class A { /* retouché */ }");

        Assert.Equal(CopiedFileState.Modified, CopyManifest.Inspect(_project, package)[0].State);
    }

    [Fact]
    public void FichierSupprime_EstDetecte()
    {
        var relative = Path.Combine("MicroForge", "a.cs");
        var package = Copy(relative, "public class A { }");
        File.Delete(Path.Combine(_project, relative));

        Assert.Equal(CopiedFileState.Absent, CopyManifest.Inspect(_project, package)[0].State);
    }

    [Fact]
    public void ChangementDeFinDeLigne_NEstPasUneModification()
    {
        // Un éditeur qui convertit LF en CRLF ne modifie pas le code : le signaler
        // noierait les vraies divergences.
        var relative = Path.Combine("MicroForge", "a.cs");
        var package = Copy(relative, "ligne1\nligne2\n");
        File.WriteAllText(Path.Combine(_project, relative), "ligne1\r\nligne2\r\n");

        Assert.Equal(CopiedFileState.Untouched, CopyManifest.Inspect(_project, package)[0].State);
    }

    [Fact]
    public void EnregistrementPuisRelecture_ConserveLaProvenance()
    {
        var package = Copy(Path.Combine("MicroForge", "a.cs"), "public class A { }");

        var manifest = CopyManifest.Load(_project);
        manifest.Record(package);
        manifest.Save();

        var relu = CopyManifest.Load(_project).Find("Micro.A.One");
        Assert.NotNull(relu);
        Assert.Equal("1.0.0", relu!.Version);
        Assert.Single(relu.FileHashes);
    }

    [Fact]
    public void ProjetSansManifeste_EstVide() =>
        Assert.Empty(CopyManifest.Load(_project).Entries);

    [Fact]
    public void ManifesteCorrompu_RepartVide()
    {
        var path = CopyManifest.PathFor(_project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ pas du JSON");

        Assert.Empty(CopyManifest.Load(_project).Entries);
    }

    [Fact]
    public void Remove_RetireLePackage()
    {
        var manifest = CopyManifest.Load(_project);
        manifest.Record(Copy(Path.Combine("MicroForge", "a.cs"), "x"));

        Assert.True(manifest.Remove("Micro.A.One"));
        Assert.Null(manifest.Find("Micro.A.One"));
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CopyManifest.PathFor("  "));
        Assert.Throws<ArgumentException>(() => CopyManifest.Hash("  "));
        Assert.Throws<ArgumentNullException>(() => CopyManifest.Inspect(_project, null!));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_project, recursive: true);
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}
