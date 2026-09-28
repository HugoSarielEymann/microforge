using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Micro.Media.ReleaseName;

/// <summary>
/// Lit le nom d'un fichier vidéo — nom de release (« Show.S01E02.Title.1080p.WEB-DL.x264 »)
/// ou nom rangé (« Show - S01E02 - Title.mkv », « Film (1982).mkv ») — et en tire le titre,
/// l'année, la saison, le numéro et le titre d'épisode.
/// </summary>
/// <remarks>
/// <para>
/// Marqueurs d'épisode reconnus, par ordre de préséance : <c>S01E02</c> (et <c>S01E02E03</c>,
/// <c>S01E02-E03</c>, <c>S01.E02</c>), « Season 1 Episode 2 » / « Saison 1 Épisode 2 »,
/// <c>1x02</c>, puis un épisode seul (« Episode 12 », <c>E12</c>) et une saison seule
/// (<c>S02</c>, « Saison 2 »), utile pour un dossier ou une archive de saison.
/// </para>
/// <para>
/// Deux familles d'étiquettes techniques. Les <b>fortes</b> (résolution, source, codecs vidéo
/// et audio : <c>1080p</c>, <c>BluRay</c>, <c>x264</c>, <c>DDP5.1</c>…) ne se rencontrent pas
/// dans un vrai titre : la première rencontrée coupe le titre. Les <b>faibles</b> (langues,
/// sous-titres, éditions : <c>FRENCH</c>, <c>English</c>, <c>PROPER</c>, <c>WEB</c>…) peuvent
/// appartenir à un titre (« Johnny English », « Charlotte's Web ») : elles ne coupent qu'en fin
/// de nom, quand au moins deux s'y suivent, ou qu'un nom de site les accompagne.
/// </para>
/// <para>
/// Fonction pure et déterministe : ni horloge, ni fichier, ni culture ambiante. Les chiffres
/// lus sont des chiffres ASCII ; un chiffre d'une autre écriture n'est pas un numéro.
/// </para>
/// </remarks>
public static partial class ReleaseNameParser
{
    private const string TrimAround = " ._-–—,;:";
    private const RegexOptions Flags = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private static readonly char[] PathSeparators = ['/', (char)0x5C];
    private static readonly char[] AroundChars = TrimAround.ToCharArray();

    // Ce qui peut suivre l'année d'une série avant son numéro d'épisode : « Show (2005) - S01E01 ».
    private static readonly char[] YearTrailerChars = (TrimAround + ")]}").ToCharArray();

    // Les mêmes, sans le point : un titre écrit avec des espaces garde ses points (« Things... »).
    private static readonly char[] CleanChars = [' ', '_', '-', (char)0x2013, (char)0x2014, ',', ';', ':', (char)0x09];
    private static readonly Regex[] MarkerPatterns = [SeasonEpisode(), SeasonEpisodeWords(), Cross(), EpisodeOnly(), SeasonOnly()];

    /// <summary>Analyse un nom de fichier vidéo.</summary>
    /// <param name="fileName">
    /// Nom du fichier, avec ou sans extension. Un chemin est accepté : seul son dernier
    /// segment est lu.
    /// </param>
    /// <param name="options">Réglages ; <see cref="ReleaseNameOptions.Default"/> si omis.</param>
    /// <returns>Ce que le nom dit de son contenu ; jamais <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="fileName"/> est nul.</exception>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="fileName"/> est vide ou blanc, s'il dépasse
    /// <see cref="ReleaseNameOptions.MaximumLength"/>, ou si les réglages sont incohérents
    /// (voir <see cref="ReleaseNameOptions.Validate"/>).
    /// </exception>
    public static ReleaseInfo Parse(string fileName, ReleaseNameOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        options ??= ReleaseNameOptions.Default;
        options.Validate();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Le nom de fichier est vide.", nameof(fileName));
        }

        if (fileName.Length > options.MaximumLength)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Le nom de fichier dépasse {options.MaximumLength} caractères."),
                nameof(fileName));
        }

        return ParseCore(fileName, options);
    }

    /// <summary>Analyse un nom de fichier vidéo sans jamais lever.</summary>
    /// <param name="fileName">Nom du fichier, avec ou sans extension, ou chemin.</param>
    /// <param name="info">Le résultat, ou <see langword="null"/> en cas d'échec.</param>
    /// <param name="options">Réglages ; <see cref="ReleaseNameOptions.Default"/> si omis.</param>
    /// <returns>
    /// <see langword="false"/> si le nom est nul, vide, blanc ou trop long, ou si les réglages
    /// sont incohérents ; <see langword="true"/> sinon.
    /// </returns>
    public static bool TryParse(
        [NotNullWhen(true)] string? fileName,
        [NotNullWhen(true)] out ReleaseInfo? info,
        ReleaseNameOptions? options = null)
    {
        info = null;
        options ??= ReleaseNameOptions.Default;
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > options.MaximumLength || !options.IsValid())
        {
            return false;
        }

        info = ParseCore(fileName, options);
        return true;
    }

    private static ReleaseInfo ParseCore(string fileName, ReleaseNameOptions options)
    {
        string name = StripExtension(LastSegment(fileName).Trim(), options.Extensions);
        Match group = LeadingGroup().Match(name);
        if (group.Success)
        {
            name = name[group.Length..];
        }

        Marker? marker = FindMarker(name);
        if (marker is { } found)
        {
            return FromMarker(name, found, options);
        }

        int limit = FirstStrongTag(name, 0, options) ?? name.Length;
        Match? year = LastYear(name, limit, options);
        string titleText = year is null ? TrimTrailingNoise(name[..limit]) : name[..year.Index];
        int noiseFrom = year is null ? titleText.Length : year.Index + year.Length;

        return new ReleaseInfo
        {
            Title = Clean(titleText),
            Year = year is null ? null : ParseNumber(year.Value),
            Tags = CollectTags(name, noiseFrom, options),
        };
    }

    private static ReleaseInfo FromMarker(string name, Marker marker, ReleaseNameOptions options)
    {
        string head = name[..marker.Index];
        string tail = name[(marker.Index + marker.Length)..];

        int? year = null;
        Match? headYear = LastYear(head, head.Length, options);
        if (headYear is not null && head[(headYear.Index + headYear.Length)..].AsSpan().TrimStart(YearTrailerChars).IsEmpty)
        {
            year = ParseNumber(headYear.Value);
            head = head[..headYear.Index];
        }

        int cut = FirstStrongTag(tail, 0, options) ?? tail.Length;
        string episodeText = marker.Episode is null ? string.Empty : TrimTrailingNoise(tail[..cut]);
        int noiseFrom = marker.Episode is null ? 0 : episodeText.Length;

        return new ReleaseInfo
        {
            Title = Clean(head),
            Year = year,
            Season = marker.Season,
            Episode = marker.Episode,
            LastEpisode = marker.LastEpisode,
            EpisodeTitle = Clean(episodeText) is { Length: > 0 } title ? title : null,
            Tags = CollectTags(tail, noiseFrom, options),
        };
    }

    // ==================== Marqueurs d'épisode ====================

    private readonly record struct Marker(int Index, int Length, int? Season, int? Episode, int? LastEpisode);

    private static Marker? FindMarker(string name)
    {
        foreach (Regex regex in MarkerPatterns)
        {
            Match match = regex.Match(name);
            if (!match.Success)
            {
                continue;
            }

            int? season = match.Groups["s"].Success ? ParseNumber(match.Groups["s"].Value) : null;
            int? episode = match.Groups["e"].Success ? ParseNumber(match.Groups["e"].Value) : null;
            int? last = match.Groups["l"].Success ? ParseNumber(match.Groups["l"].Value) : null;
            return new Marker(match.Index, match.Length, season, episode, last > episode ? last : null);
        }

        return null;
    }

    // ==================== Étiquettes ====================

    private static int? FirstStrongTag(string text, int from, ReleaseNameOptions options)
    {
        int? first = null;
        Match strong = StrongTag().Match(text, from);
        if (strong.Success)
        {
            first = strong.Index;
        }

        foreach (string extra in options.ExtraTags)
        {
            int index = IndexOfWord(text, extra.Trim(), from);
            if (index >= 0 && (first is null || index < first))
            {
                first = index;
            }
        }

        return first;
    }

    private static List<string> CollectTags(string text, int from, ReleaseNameOptions options)
    {
        List<(int Index, int Length)> spans = [];
        foreach (Match match in StrongTag().Matches(text, from))
        {
            spans.Add((match.Index, match.Length));
        }

        foreach (Match match in WeakTag().Matches(text, from))
        {
            spans.Add((match.Index, match.Length));
        }

        foreach (string extra in options.ExtraTags)
        {
            string word = extra.Trim();
            for (int index = IndexOfWord(text, word, from); index >= 0; index = IndexOfWord(text, word, index + word.Length))
            {
                spans.Add((index, word.Length));
            }
        }

        spans.Sort((a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : b.Length.CompareTo(a.Length));
        List<string> tags = [];
        int end = -1;
        foreach ((int index, int length) in spans)
        {
            if (index >= end)
            {
                tags.Add(text.Substring(index, length));
                end = index + length;
            }
        }

        return tags;
    }

    /// <summary>
    /// Retire la traîne d'étiquettes faibles et de sites d'un titre (« Title.English.Esubs »),
    /// à condition qu'il y en ait au moins deux, ou un site : un seul mot faible peut être un
    /// vrai mot du titre (« Johnny English »).
    /// </summary>
    private static string TrimTrailingNoise(string text)
    {
        string current = text;
        int removed = 0;
        bool site = false;
        while (true)
        {
            string trimmed = current.TrimEnd(AroundChars);
            Match siteMatch = TrailingSite().Match(trimmed);
            if (siteMatch.Success && siteMatch.Index > 0)
            {
                current = trimmed[..siteMatch.Index];
                removed++;
                site = true;
                continue;
            }

            Match weak = TrailingWeakTag().Match(trimmed);
            if (weak.Success && weak.Index > 0)
            {
                current = trimmed[..weak.Index];
                removed++;
                continue;
            }

            return removed >= 2 || site ? current : text;
        }
    }

    // ==================== Nettoyage ====================

    /// <summary>
    /// Met un fragment de nom en forme de titre. Un fragment sans espace est un nom de release :
    /// ses points et soulignés séparent des mots. Un fragment avec espaces garde ses points
    /// (« Mr. Robot », « All Good Things... »).
    /// </summary>
    private static string Clean(string text)
    {
        bool dotted = !text.Trim().Contains(' ', StringComparison.Ordinal);
        StringBuilder builder = new(text.Length);
        foreach (char c in text)
        {
            builder.Append(c == '_' || (dotted && c == '.') ? ' ' : c);
        }

        string result = EmptyBrackets().Replace(builder.ToString(), " ");
        result = Whitespace().Replace(result, " ");

        string previous;
        do
        {
            previous = result;
            result = result.Trim(CleanChars);

            // Un point seul en bordure est un séparateur resté collé (« Show Name.S01E01 ») ; des
            // points de suspension en fin de titre sont du titre (« All Good Things... »).
            if (result.StartsWith('.'))
            {
                result = result.TrimStart('.');
            }

            if (result.EndsWith('.') && !result.EndsWith("..", StringComparison.Ordinal))
            {
                result = result[..^1];
            }

            if (result.Length > 0 && "([{".Contains(result[^1], StringComparison.Ordinal))
            {
                result = result[..^1];
            }

            if (result.Length > 0 && ")]}".Contains(result[0], StringComparison.Ordinal))
            {
                result = result[1..];
            }

            if (result.Length > 0 && ")]}".Contains(result[^1], StringComparison.Ordinal) && !result.Contains(Opener(result[^1]), StringComparison.Ordinal))
            {
                result = result[..^1];
            }

            if (result.Length > 0 && "([{".Contains(result[0], StringComparison.Ordinal) && !result.Contains(Closer(result[0]), StringComparison.Ordinal))
            {
                result = result[1..];
            }
        }
        while (result != previous);

        return result;
    }

    private static char Opener(char closer) => closer switch { ')' => '(', ']' => '[', _ => '{' };

    private static char Closer(char opener) => opener switch { '(' => ')', '[' => ']', _ => '}' };

    // ==================== Utilitaires ====================

    private static string LastSegment(string path)
    {
        int slash = path.LastIndexOfAny(PathSeparators);
        return slash >= 0 ? path[(slash + 1)..] : path;
    }

    private static string StripExtension(string name, IReadOnlyCollection<string> extensions)
    {
        int dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            return name;
        }

        string extension = name[(dot + 1)..];
        return extensions.Any(known => string.Equals(known.Trim().TrimStart('.'), extension, StringComparison.OrdinalIgnoreCase))
            ? name[..dot]
            : name;
    }

    private static Match? LastYear(string text, int limit, ReleaseNameOptions options)
    {
        Match? last = null;
        foreach (Match match in Year().Matches(text))
        {
            if (match.Index == 0 || match.Index >= limit)
            {
                continue;
            }

            int year = ParseNumber(match.Value);
            if (year >= options.MinimumYear && year <= options.MaximumYear)
            {
                last = match;
            }
        }

        return last;
    }

    private static int IndexOfWord(string text, string word, int from)
    {
        if (word.Length == 0)
        {
            return -1;
        }

        for (int index = text.IndexOf(word, from, StringComparison.OrdinalIgnoreCase); index >= 0; index = text.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase))
        {
            bool startsWord = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            int after = index + word.Length;
            bool endsWord = after >= text.Length || !char.IsLetterOrDigit(text[after]);
            if (startsWord && endsWord)
            {
                return index;
            }

            if (index + 1 >= text.Length)
            {
                break;
            }
        }

        return -1;
    }

    /// <summary>Lit un nombre de un à quatre chiffres ASCII (les expressions n'en capturent pas d'autres).</summary>
    private static int ParseNumber(string digits)
    {
        int value = 0;
        foreach (char c in digits)
        {
            value = (value * 10) + (c - '0');
        }

        return value;
    }

    // ==================== Expressions ====================

    [GeneratedRegex(@"^\s*\[[^\]]{1,64}\]\s*", Flags)]
    private static partial Regex LeadingGroup();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])S(?<s>[0-9]{1,4})[ ._-]?E(?<e>[0-9]{1,4})(?:(?:[ ._-]?E|-)(?<l>[0-9]{1,4}))?(?![\p{L}\p{N}])", Flags)]
    private static partial Regex SeasonEpisode();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:season|saison|series|s[eé]rie|temporada|staffel|stagione)[ ._-]*(?<s>[0-9]{1,4})[ ._,-]*(?:episode|[eé]pisode|episodio|folge|ep)[ ._-]*(?<e>[0-9]{1,4})(?![\p{L}\p{N}])", Flags)]
    private static partial Regex SeasonEpisodeWords();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?<s>[0-9]{1,2})x(?<e>[0-9]{2,3})(?:-(?:[0-9]{1,2}x)?(?<l>[0-9]{2,3}))?(?![\p{L}\p{N}])", Flags)]
    private static partial Regex Cross();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:(?:[eé]pisode|episodio|folge|ep)[ ._-]?(?<e>[0-9]{1,4})|E(?<e>[0-9]{2,4}))(?![\p{L}\p{N}])", Flags)]
    private static partial Regex EpisodeOnly();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:S(?<s>[0-9]{1,2})|(?:season|saison|temporada|staffel|stagione)[ ._-]*(?<s>[0-9]{1,4}))(?![\p{L}\p{N}])", Flags)]
    private static partial Regex SeasonOnly();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[0-9]{4}(?![\p{L}\p{N}])", Flags)]
    private static partial Regex Year();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?:[0-9]{3,4}[pi]|[48]k|uhd|fhd|qhd"
        + @"|blu-?ray|bd-?rip|br-?rip|bd-?remux|remux|web-?dl|web-?rip|hdtv|pdtv|sdtv|dvd-?rip|dvd-?scr|dvd[59]|hd-?rip|hd-?cam|tv-?rip|vhs-?rip"
        + @"|amzn|dsnp|hmax|atvp|pcok|pmtp|hulu|nf"
        + @"|[xh]\.?26[45]|hevc|avc|xvid|divx|av1|vp9|10-?bit|8-?bit|hdr(?:10(?:\+|plus)?)?|dovi|dolby[ ._-]?vision"
        + @"|e?ac-?3(?:[ ._-]?[1257]\.[01])?|aac(?:[ ._-]?[1257]\.[01])?|ddp?[ ._-]?[257]\.[01]|ddp|dd\+|dts(?:-?(?:hd|x|ma|es))?|truehd|atmos|flac|mp3|[268]ch"
        + @")(?![\p{L}\p{N}])",
        Flags)]
    private static partial Regex StrongTag();

    private const string WeakAlternatives =
        @"multi|vff|vfq|vfi|vf2?|vostfr|vost|truefrench|french|english|eng|e-?subs?|subs?|subbed|dubbed|dual(?:-audio)?|hindi|spanish|german|italian"
        + @"|proper|repack|rerip|extended|unrated|remastered|limited|internal|complete|imax|web|dvd|dv|sdr|dd|[257]\.[01]|hc";

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:" + WeakAlternatives + @")(?![\p{L}\p{N}])", Flags)]
    private static partial Regex WeakTag();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:" + WeakAlternatives + @")$", Flags)]
    private static partial Regex TrailingWeakTag();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:www\.)?[\p{L}\p{N}-]{1,64}\.(?:org|com|net|info|xyz|club|lol|cc|ws|vip|top|site|online|biz)$", Flags)]
    private static partial Regex TrailingSite();

    [GeneratedRegex(@"[(\[{]\s*[)\]}]", Flags)]
    private static partial Regex EmptyBrackets();

    [GeneratedRegex(@"\s+", Flags)]
    private static partial Regex Whitespace();
}
