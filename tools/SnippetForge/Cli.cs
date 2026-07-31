using System.Diagnostics;

namespace SnippetForge;

/// <summary>Utilitaires partagés par les commandes : lecture d'arguments, sortie, sous-processus.</summary>
public static class Cli
{
    /// <summary>Lit un argument positionnel obligatoire.</summary>
    /// <exception cref="ArgumentException">S'il est absent.</exception>
    public static string RequireArg(string[] args, int position, string name) =>
        args.Length > position && !args[position].StartsWith("--", StringComparison.Ordinal)
            ? args[position]
            : throw new ArgumentException($"Argument manquant : {name}.");

    /// <summary>Lit un argument positionnel optionnel.</summary>
    public static string? Arg(string[] args, int position) =>
        args.Length > position && !args[position].StartsWith("--", StringComparison.Ordinal) ? args[position] : null;

    /// <summary>Lit la valeur d'une option nommée (« --nom valeur »).</summary>
    public static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Lit la valeur d'une option nommée obligatoire.</summary>
    /// <exception cref="ArgumentException">Si elle est absente.</exception>
    public static string RequireOption(string[] args, string name, string explanation) =>
        Option(args, name) ?? throw new ArgumentException($"{name} est obligatoire : {explanation}");

    /// <summary>Indique la présence d'un drapeau.</summary>
    public static bool Flag(string[] args, string name) => args.Contains(name, StringComparer.Ordinal);

    /// <summary>Découpe une liste séparée par des points-virgules.</summary>
    public static string[] SplitList(string? value) =>
        (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Tronque un texte multiligne pour un affichage sur une ligne.</summary>
    public static string Truncate(string text, int max)
    {
        var flat = text.ReplaceLineEndings(" ");
        return flat.Length <= max ? flat : flat[..max] + "…";
    }

    /// <summary>Écrit un message d'erreur et retourne le code de sortie 1.</summary>
    public static int Fail(string message)
    {
        Console.Error.WriteLine($"ERREUR : {message}");
        return 1;
    }

    /// <summary>
    /// Exécute un processus et capture sa sortie combinée.
    ///
    /// Les deux flux sont lus **simultanément** : les lire l'un après l'autre expose
    /// à un interblocage si le processus remplit le tampon du second pendant qu'on
    /// attend la fin du premier — et, en pratique, à une sortie vide qui masque la
    /// vraie cause de l'échec.
    /// </summary>
    public static bool Run(string file, string arguments, string workingDir, out string output)
    {
        var info = new ProcessStartInfo(file, arguments)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(info)!;

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var combined = string.Concat(standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
        output = string.IsNullOrWhiteSpace(combined)
            ? $"(aucune sortie ; code de sortie {process.ExitCode})"
            : combined;

        return process.ExitCode == 0;
    }

    /// <summary>Exécute une ligne de commande arbitraire via le shell, en affichant sa sortie.</summary>
    public static bool RunShell(string commandLine, string workingDir, out string output)
    {
        var isWindows = OperatingSystem.IsWindows();
        var info = new ProcessStartInfo(
            isWindows ? "cmd.exe" : "/bin/sh",
            isWindows ? $"/c {commandLine}" : $"-c \"{commandLine.Replace("\"", "\\\"", StringComparison.Ordinal)}\"")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(info)!;
        output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    /// <summary>Retourne les dernières lignes d'une sortie, pour un rapport d'erreur lisible.</summary>
    public static string Tail(string output, int lines) =>
        string.Join('\n', output.Split('\n').TakeLast(lines));
}
