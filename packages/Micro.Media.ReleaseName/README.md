# Micro.Media.ReleaseName

## Description

Lit le nom d'un fichier vidéo et dit ce qu'il contient : un **film** (titre, année) ou un
**épisode** (série, saison, numéro, titre d'épisode), débarrassé de ses étiquettes techniques.
Il accepte aussi bien un nom de release
(`Star.Trek.The.Next.Generation.S01E01.Encounter.At.Farpoint.1080p.English.Esubs.MoviezVerse.Org.mkv`)
qu'un nom rangé (`Star Trek - The Next Generation - S03E15 - Yesterday's Enterprise.mkv`,
`Blade Runner 2049 (2017).mkv`). Fonction pure et déterministe : ni horloge, ni fichier, ni
culture ambiante.

Ce qui justifie un package plutôt que trois expressions régulières :

- **Deux familles d'étiquettes.** Les *fortes* (`1080p`, `BluRay`, `WEB-DL`, `x264`, `HEVC`,
  `DDP5.1`, `Atmos`…) n'appartiennent jamais à un titre : la première coupe le titre. Les
  *faibles* (`FRENCH`, `English`, `VOSTFR`, `PROPER`, `WEB`…) peuvent en faire partie
  (« Johnny English », « Charlotte's Web ») : elles ne coupent qu'en fin de nom, quand au moins
  deux s'y suivent ou qu'un nom de site (`MoviezVerse.Org`) les accompagne.
- **Les points d'un nom de release séparent des mots, ceux d'un nom rangé non** :
  « Mr. Robot » et « All Good Things... » gardent les leurs. La décision se prend fragment par
  fragment (la série, le titre d'épisode), pas sur le nom entier.
- **L'année est la dernière plausible avant les étiquettes**, jamais la première du nom :
  « 2001 A Space Odyssey 1968 » et « 1917 (2019) » gardent leur titre numérique.
- **Aucun faux épisode** : `1920x1080` n'est pas « saison 19, épisode 20 », `S01E02-720p`
  n'est pas un double épisode, et un chiffre d'une autre écriture (arabe-indien, par exemple)
  n'est jamais lu comme un numéro.

Marqueurs reconnus, par ordre de préséance : `S01E02` (et `S01E02E03`, `S01E02-E03`,
`S01E02-03`, `S01.E02`, `S01 E02`), « Season 1 Episode 2 » / « Saison 1 Épisode 2 »,
`1x02`, un épisode seul (« Episode 12 », `Ep.05`, `E07`) et une saison seule (`S02`,
« Saison 2 ») — utile pour un dossier ou une archive de saison.

## Mode d'emploi

**Quand l'utiliser** — pour ranger une vidéothèque : regrouper des épisodes par série et par
saison, afficher un titre propre au lieu d'un nom de release, retrouver l'année d'un film,
lire le numéro d'une saison dans le nom de son dossier. Deux points d'entrée sur
`ReleaseNameParser` :

- `Parse(fileName, options)` — lève sur un nom nul (`ArgumentNullException`), vide, blanc ou
  trop long, ou sur des réglages incohérents (`ArgumentException`).
- `TryParse(fileName, out info, options)` — ne lève **jamais** : rend `false` dans ces mêmes
  cas.

Un chemin complet est accepté : seul son dernier segment est lu. Une information absente du
nom reste `null` : « Episode 12 » n'a pas de saison, et c'est à l'appelant de la tirer du
dossier (« Saison 2 ») s'il le souhaite — en analysant ce nom de dossier avec le même package.

**Quand ne pas l'utiliser** — pour identifier une œuvre de façon certaine : le package lit ce
que le nom dit, il ne consulte aucune base (TMDB, TVDB). Il ne reconnaît pas les
numérotations absolues d'anime (« Titre - 012 »), ni les épisodes datés
(« Show.2024.03.15 ») : ces noms rendent un titre sans épisode. Sans horloge, il ne sait pas
quelle année est « future » : « Blade.Runner.2049.1080p » donne l'année 2049 tant que
`MaximumYear` n'est pas posé à l'année courante plus un.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Extensions` | `IReadOnlyCollection<string>` | `DefaultExtensions` (mkv, mp4, m4v, avi, mov, wmv, webm, ts, m2ts, mts, mpg, mpeg, flv, ogv, ogm, 3gp, divx, vob, iso, rmvb, srt, ass, ssa, sub, idx, vtt, nfo) | Extensions retirées de la fin du nom, sans le point, casse ignorée. Seule une extension connue est retirée : le dernier segment de `Film.1080p` n'en est pas une. |
| `MinimumYear` | `int` | `1888` | Plus petite année lue comme année de sortie (1000 à 9999). |
| `MaximumYear` | `int` | `2099` | Plus grande année lue comme année de sortie (de `MinimumYear` à 9999). Poser l'année courante plus un pour garder « 2049 » dans le titre de « Blade.Runner.2049 ». |
| `MaximumLength` | `int` | `1024` | Longueur maximale du nom analysé ; au-delà, `Parse` lève et `TryParse` rend `false`. |
| `ExtraTags` | `IReadOnlyCollection<string>` | vide | Mots supplémentaires traités comme étiquettes fortes (mot entier, casse ignorée) : ils coupent le titre. Pour un site ou un groupe récurrent d'une collection. |

`Validate()` lève `ArgumentOutOfRangeException` sur une année ou une longueur hors bornes, et
`ArgumentException` sur une liste nulle ou contenant un élément blanc. `Parse` l'appelle ;
`TryParse` fait le même contrôle sans lever.

Le résultat, `ReleaseInfo` :

| Propriété | Type | Contenu |
|-----------|------|---------|
| `Title` | `string` | Titre du film ou de la série ; vide si le nom n'en porte pas (« S01E01 - Pilote.mkv »). |
| `Year` | `int?` | Année de sortie ou de début de la série. |
| `Season` | `int?` | Saison ; 0 pour les épisodes spéciaux. |
| `Episode` | `int?` | Numéro d'épisode. |
| `LastEpisode` | `int?` | Dernier épisode d'un fichier double (`S01E01E02`), sinon `null`. |
| `EpisodeTitle` | `string?` | Titre de l'épisode, s'il suit le numéro. |
| `Tags` | `IReadOnlyList<string>` | Étiquettes techniques écartées, dans l'ordre et l'écriture du nom. |
| `IsEpisode` | `bool` | Un numéro d'épisode a été lu. |

## Exemple

```csharp
using Micro.Media.ReleaseName;

ReleaseInfo episode = ReleaseNameParser.Parse(
    "Star.Trek.The.Next.Generation.S03E15.Yesterdays.Enterprise.1080p.English.Esubs.MoviezVerse.Org.mkv");
// episode.Title        == "Star Trek The Next Generation"
// episode.Season       == 3, episode.Episode == 15
// episode.EpisodeTitle == "Yesterdays Enterprise"
// episode.Tags         == ["1080p", "English", "Esubs"]

ReleaseNameOptions options = new() { MaximumYear = 2027 };
ReleaseInfo film = ReleaseNameParser.Parse("Blade.Runner.2049.2017.2160p.UHD.BluRay.x265.mkv", options);
// film.Title == "Blade Runner 2049", film.Year == 2017, film.IsEpisode == false

if (ReleaseNameParser.TryParse("Saison 2", out ReleaseInfo? dossier) && dossier.Season is int saison)
{
    // saison == 2 : le numéro d'une saison se lit aussi dans le nom de son dossier.
}
```
