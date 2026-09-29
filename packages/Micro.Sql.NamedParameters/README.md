# Micro.Sql.NamedParameters

## Description

Relève les paramètres nommés d'une requête SQL — `@nom` par défaut, `:nom` et `$nom` sur
demande — sans l'exécuter et sans la comprendre. Il sait seulement où le texte cesse d'être du
code : chaînes `'…'` (quote doublée comprise) et `E'…'` de PostgreSQL, identifiants délimités
`"…"`, `[…]` et `` `…` ``, commentaires `--` et `/* */` (imbriqués ou non), chaînes dollar
`$$…$$` et `$corps$…$corps$`. Il écarte aussi les faux amis : `@@IDENTITY`, `a::int`,
`contact@domaine.fr`. Fonction pure, déterministe, sans dépendance.

## Mode d'emploi

À utiliser quand une requête est **écrite par quelqu'un d'autre** (un utilisateur, un fichier
de configuration, un éditeur de requêtes) et qu'il faut savoir, avant de l'exécuter, quelles
valeurs lier : proposer une case par paramètre, vérifier qu'aucun ne manque, typer chacun.
Les valeurs voyagent ensuite dans des `DbParameter`, à part du texte : c'est ce qui rend la
requête non injectable.

**Ne pas** l'utiliser pour valider du SQL (il ne détecte aucune erreur de syntaxe), ni pour
réécrire une requête en y collant des valeurs — ce serait précisément l'injection que les
paramètres évitent. Une requête mal fermée (chaîne ou commentaire jamais refermé) ne lève
pas : ce qui suit l'ouverture est ignoré, comme le moteur l'ignorerait.

| Membre | Rôle |
|--------|------|
| `SqlParameterScanner.Names(sql, options)` | Noms, dans l'ordre de première apparition, sans doublon ni préfixe. |
| `SqlParameterScanner.Scan(sql, options)` | Chaque occurrence (`SqlParameterToken` : nom, préfixe, position, longueur), doublons compris — pour surligner ou réécrire la requête. |

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Prefixes` | `string` | `"@"` | Préfixes qui ouvrent un paramètre : toute combinaison de `@`, `:` et `$`. |
| `DollarQuotedStrings` | `bool` | `true` | Ignorer le contenu des chaînes dollar de PostgreSQL. Incompatible avec le préfixe `$`. |
| `NestedBlockComments` | `bool` | `true` | Imbriquer `/* … */` comme SQL Server et PostgreSQL ; `false` pour SQLite et MySQL. |
| `NameComparer` | `StringComparer` | `Ordinal` | Décide que deux noms désignent le même paramètre dans `Names`. |

`SqlParameterScanOptions.Validate()` refuse un préfixe vide ou inconnu, et `$` avec les chaînes
dollar (`ArgumentException`), un `Prefixes` ou un `NameComparer` nul (`ArgumentNullException`).
Passer `null` en options prend les défauts.

## Exemple

```csharp
using Micro.Sql.NamedParameters;

const string requete = """
    -- @commentaire n'est pas un paramètre
    SELECT id, nom FROM clients
    WHERE ville = @ville AND nom <> 'x@y' AND id > @depuis
    """;

IReadOnlyList<string> noms = SqlParameterScanner.Names(requete);
// → ["ville", "depuis"]

// PostgreSQL avec :nom, sans confondre les conversions « :: ».
var postgres = new SqlParameterScanOptions { Prefixes = "@:" };
IReadOnlyList<string> autres = SqlParameterScanner.Names("SELECT a::int FROM t WHERE b = :b", postgres);
// → ["b"]

foreach (SqlParameterToken occurrence in SqlParameterScanner.Scan(requete))
{
    // occurrence.Start, occurrence.Length : pour surligner @ville et @depuis dans un éditeur.
}
```
