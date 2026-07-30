namespace SnippetForge.Metrics;

/// <summary>Volume de code d'un micropackage, en lignes non vides.</summary>
public sealed record PackageFootprint(string PackageId, int SourceLines, int TestLines, int DocumentationLines)
{
    /// <summary>Coût total d'écriture du package : code, tests et mode d'emploi.</summary>
    public int TotalLines => SourceLines + TestLines + DocumentationLines;
}

/// <summary>Projets consommateurs référençant un package donné.</summary>
public sealed record ReuseRecord(string PackageId, IReadOnlyList<string> Consumers);

/// <summary>Bilan chiffré d'un package.</summary>
public sealed record PackageSavings(
    string PackageId,
    int Consumers,
    int RegenerationsAvoided,
    int LinesAvoided,
    int InvestmentLines);

/// <summary>Bilan chiffré de la bibliothèque entière.</summary>
public sealed record SavingsReport(
    IReadOnlyList<PackageSavings> Packages,
    int InvestmentLines,
    int LinesAvoided,
    int RegenerationsAvoided)
{
    /// <summary>Estimation basse des tokens économisés.</summary>
    public int TokensAvoided => (int)(LinesAvoided * SavingsCalculator.TokensPerLine);

    /// <summary>Estimation des tokens investis dans l'écriture de la bibliothèque.</summary>
    public int TokensInvested => (int)(InvestmentLines * SavingsCalculator.TokensPerLine);

    /// <summary>Vrai quand la bibliothèque a plus fait économiser qu'elle n'a coûté.</summary>
    public bool IsProfitable => LinesAvoided > InvestmentLines;

    /// <summary>Lignes de réutilisation qu'il reste à atteindre pour rentabiliser.</summary>
    public int LinesToBreakEven => Math.Max(0, InvestmentLines - LinesAvoided);
}

/// <summary>
/// Chiffre ce que la bibliothèque fait économiser, et ce qu'elle a coûté.
///
/// Le raisonnement est délibérément conservateur. Sans MicroForge, une capacité
/// utilisée par N projets serait régénérée N fois ; avec, elle est écrite une fois
/// et réutilisée. L'économie porte donc sur <c>N - 1</c> régénérations, jamais sur N :
/// **le premier usage d'un package ne fait rien économiser, il coûte.**
///
/// Seules les lignes de <c>src/</c> comptent comme économie : une IA générant du code
/// à la volée n'aurait probablement écrit ni suite de tests ni mode d'emploi. Tests
/// et README sont comptés du côté de l'investissement uniquement.
/// </summary>
public static class SavingsCalculator
{
    /// <summary>
    /// Tokens par ligne de C#, estimation basse. Une ligne de C# fait ~40 caractères
    /// en moyenne dans ce style, et l'on compte ~3,5 caractères par token pour du code.
    /// C'est un ordre de grandeur assumé, pas une mesure.
    /// </summary>
    public const double TokensPerLine = 11.0;

    /// <summary>Croise l'empreinte des packages et leur réutilisation effective.</summary>
    public static SavingsReport Estimate(
        IReadOnlyList<PackageFootprint> footprints,
        IReadOnlyList<ReuseRecord> reuse)
    {
        ArgumentNullException.ThrowIfNull(footprints);
        ArgumentNullException.ThrowIfNull(reuse);

        var consumersById = reuse.ToDictionary(
            r => r.PackageId,
            r => r.Consumers.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            StringComparer.OrdinalIgnoreCase);

        var packages = footprints
            .Select(footprint =>
            {
                var consumers = consumersById.GetValueOrDefault(footprint.PackageId, 0);
                var avoided = Math.Max(0, consumers - 1);
                return new PackageSavings(
                    footprint.PackageId,
                    consumers,
                    avoided,
                    avoided * footprint.SourceLines,
                    footprint.TotalLines);
            })
            .OrderByDescending(p => p.LinesAvoided)
            .ThenBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SavingsReport(
            packages,
            packages.Sum(p => p.InvestmentLines),
            packages.Sum(p => p.LinesAvoided),
            packages.Sum(p => p.RegenerationsAvoided));
    }
}
