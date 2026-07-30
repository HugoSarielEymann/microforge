namespace SnippetForge;

/// <summary>Niveau d'incrément SemVer.</summary>
public enum BumpLevel
{
    /// <summary>Aucun changement de contrat : correctif seul.</summary>
    Patch = 0,

    /// <summary>Ajout rétrocompatible.</summary>
    Minor = 1,

    /// <summary>Rupture de contrat.</summary>
    Major = 2,
}

/// <summary>Comparaison SemVer minimaliste (majeur.mineur.patch[-prerelease]).</summary>
public static class SemVerLite
{
    /// <summary>Vérifie qu'une chaîne est une version SemVer valide.</summary>
    public static bool IsValid(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var core = version.Split('-', 2)[0];
        var parts = core.Split('.');
        return parts.Length == 3 && parts.All(p => int.TryParse(p, out var n) && n >= 0);
    }

    /// <summary>Compare deux versions ; une prerelease est inférieure à la version finale.</summary>
    public static int Compare(string left, string right)
    {
        var (leftCore, leftPre) = Split(left);
        var (rightCore, rightPre) = Split(right);

        for (var i = 0; i < 3; i++)
        {
            var diff = leftCore[i].CompareTo(rightCore[i]);
            if (diff != 0)
            {
                return diff;
            }
        }

        if (leftPre is null && rightPre is null) return 0;
        if (leftPre is null) return 1;
        if (rightPre is null) return -1;
        return string.CompareOrdinal(leftPre, rightPre);
    }

    /// <summary>Retourne la plus haute version d'une liste, ou null si la liste est vide.</summary>
    public static string? Latest(IEnumerable<string> versions) =>
        versions.OrderByDescending(v => v, Comparer<string>.Create(Compare)).FirstOrDefault();

    /// <summary>
    /// Détermine le niveau d'incrément effectivement appliqué entre deux versions.
    /// Retourne null si <paramref name="candidate"/> n'est pas strictement supérieure à
    /// <paramref name="previous"/>.
    /// </summary>
    public static BumpLevel? BumpBetween(string previous, string candidate)
    {
        if (Compare(candidate, previous) <= 0)
        {
            return null;
        }

        var (previousCore, _) = Split(previous);
        var (candidateCore, _) = Split(candidate);

        if (candidateCore[0] > previousCore[0]) return BumpLevel.Major;
        if (candidateCore[1] > previousCore[1]) return BumpLevel.Minor;
        return BumpLevel.Patch;
    }

    /// <summary>Incrémente une version au niveau demandé, en remettant à zéro les rangs inférieurs.</summary>
    public static string Bump(string version, BumpLevel level)
    {
        var (core, _) = Split(version);
        return level switch
        {
            BumpLevel.Major => $"{core[0] + 1}.0.0",
            BumpLevel.Minor => $"{core[0]}.{core[1] + 1}.0",
            _ => $"{core[0]}.{core[1]}.{core[2] + 1}",
        };
    }

    /// <summary>Indique si deux versions partagent le même numéro majeur (compatibilité de contrat).</summary>
    public static bool SameMajor(string left, string right) => Split(left).Core[0] == Split(right).Core[0];

    private static (int[] Core, string? Prerelease) Split(string version)
    {
        var dash = version.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? version : version[..dash];
        var pre = dash < 0 ? null : version[(dash + 1)..];
        var parts = core.Split('.');
        var numbers = new int[3];
        for (var i = 0; i < 3 && i < parts.Length; i++)
        {
            _ = int.TryParse(parts[i], out numbers[i]);
        }

        return (numbers, pre);
    }
}
