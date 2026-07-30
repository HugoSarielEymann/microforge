# Protocole de test de MicroForge

Comment vérifier que le système tient ses promesses, et surtout **comment ne pas se
mentir** en le mesurant.

---

## Ce qu'il faut savoir avant de mesurer

**Le gain est négatif au premier projet.** Forger un micropackage coûte plus cher que
d'écrire le code inline : il faut la méthode générique, des tests, un mode d'emploi.
Un protocole qui mesure un seul projet conclura, à juste titre, que MicroForge fait
perdre des tokens.

Le bénéfice n'apparaît qu'à la **réutilisation**. Toute mesure honnête porte donc sur
une **séquence de projets** partageant des besoins génériques, pas sur un projet isolé.

C'est pourquoi `forge stats` compte l'économie sur `N-1` régénérations pour `N`
consommateurs, et affiche « pas encore rentable » tant que l'investissement n'est pas
récupéré.

---

## Contrôle expérimental : éviter la contamination

Si l'agent qui teste est celui avec qui vous avez conçu le système, il connaît déjà
les packages et peut « tricher » — les citer de mémoire au lieu de les chercher.

**Tester avec un agent vierge** (autre outil, ou session neuve sans historique)
élimine ce biais. Le test devient : *l'agent, avec pour seule information les fichiers
du projet, trouve-t-il et réutilise-t-il les packages ?*

Signe que le test est valide : dans sa trace, l'agent exécute réellement
`forge search` **avant** d'écrire du code générique. S'il ne le fait jamais, le
problème est dans les instructions, pas dans la bibliothèque.

### Choisir un besoin qui mérite vraiment un package

Le premier jeu de tests a buté là-dessus : « découper en lots de 50 » semble
générique, mais `Enumerable.Chunk` existe depuis .NET 6. Un agent bien instruit
utilise la bibliothèque standard et ne forge rien — c'est le bon comportement, mais
la manche ne teste alors pas la création de package.

Pour exercer le forge, choisir un besoin **réellement absent du framework** :
parsing de durée compacte (`"5m30s"`), masquage de données sensibles dans des logs,
backoff avec jitter, fenêtre glissante de limitation de débit, comparaison de
versions applicatives.

---

## Protocole en trois manches

### Manche 1 — Établir la ligne de base (sans MicroForge)

Projet neuf, **sans** `forge init`. Demandez une fonctionnalité contenant plusieurs
besoins génériques. Par exemple :

> « Écris un service qui récupère une liste d'articles depuis une API HTTP,
> retente en cas d'échec transitoire, génère un identifiant d'URL pour chaque titre,
> et traite les articles par lots de 50. »

Relevez :

| Mesure | Comment |
|--------|---------|
| Lignes de code générique écrites | Compter à la main : retry, slug, découpage en lots |
| Tokens consommés | Selon l'outil (Claude Code : `/cost`) |
| Durée | Chronomètre |

### Manche 2 — Le même besoin, avec MicroForge

Projet neuf, `forge init .`, **même demande, mot pour mot**, agent vierge.

Attendu : l'agent cherche, trouve `Micro.Flow.Retry` et `Micro.Text.Slugify`, les
installe, et ne forge que ce qui manque (le découpage en lots). Le coût peut être
**supérieur** à la manche 1 : forger un package coûte cher. C'est normal.

### Manche 3 — Le test qui compte

Un **troisième** projet, avec une demande différente mais qui repose sur les mêmes
besoins génériques. Par exemple :

> « Écris un import CSV qui retente les écritures en base en cas d'échec,
> normalise les noms de colonnes en identifiants, et insère par lots de 200. »

Ici, l'agent ne devrait forger **aucun** package : tout existe. C'est cette manche qui
révèle le gain, et le seul chiffre qui vaut est l'écart entre manche 1 et manche 3.

```powershell
forge stats
```

## Encadrer une manche avec `forge bench`

Depuis la 0.2.0, le comptage manuel n'est plus nécessaire. Le déroulé d'une manche :

```powershell
# 1. Préparer le projet neuf, le raccorder, puis capturer l'état initial
forge init .
forge bench start manche4 --project . --prompt "« le prompt exact de la manche »"

# 2. Donner le prompt à l'agent (Copilot, Claude Code…) et le laisser travailler.
#    C'est la seule étape humaine : on ne pilote pas un agent d'éditeur par API,
#    et c'est voulu — un agent non instrumenté est un agent non biaisé.

# 3. Mesurer
forge bench report manche4
```

Le rapport donne : fichiers créés/modifiés et lignes C# nettes, micropackages
installés pendant la manche, micropackages forgés, la **trace des commandes forge**
que l'agent a réellement invoquées (tirée du journal `registry/usage.log`), et le
verdict « a cherché avant d'écrire » ou non.

C'est ce dernier point qui remplace la lecture manuelle des logs de l'éditeur : la
preuve du workflow est dans la trace d'invocations, pas dans le code produit.

### Manche 4 — déclencher la création de packages

Les manches 1 à 3 n'exercent que la moitié « réutiliser » du workflow. Cette manche
teste l'autre moitié : **l'agent sait-il forger un micropackage conforme ?**

Elle exige un besoin qui coche trois cases à la fois : générique, **absent de la
bibliothèque standard**, et absent de la forge. Projet neuf, `forge init .`, agent
vierge, prompt donné tel quel :

> Écris un moniteur de disponibilité. Il lit une configuration où les intervalles de
> vérification sont écrits en format compact (`30s`, `5m`, `2h30m`), interroge chaque
> URL avec relance en cas d'échec transitoire, et journalise un résumé dans lequel les
> jetons d'authentification présents dans les URL sont masqués.

Trois besoins génériques, dont deux introuvables :

| Besoin | Attendu |
|--------|---------|
| Format de durée compact (`2h30m`) | **Forge** — `TimeSpan.Parse` ne gère pas cette syntaxe |
| Relance sur échec transitoire | **Réutilise** `Micro.Flow.Retry` |
| Masquage de secrets dans une URL | **Forge** — rien de tel dans la BCL |

Le découpage du travail entre `System.Uri` (BCL) et le masquage lui-même est
volontairement ambigu : c'est aussi un test de l'arbitrage « générique ou pas ».

#### Ce qu'il faut vérifier ensuite

```powershell
forge list                    # deux nouveaux packages doivent apparaître
forge info <nouveau package>  # les 4 sections du README sont-elles remplies ?
forge stats                   # l'investissement a augmenté, l'économie non
```

Puis, package par package :

| Contrôle | Comment | Pourquoi |
|----------|---------|----------|
| Tests réels | `dotnet test packages\<Id>\tests -c Release` | Le test généré échoue volontairement ; s'il a été laissé tel quel, la publication n'aurait pas dû passer |
| Cas limites couverts | Lire les tests | `"0s"`, `""`, `"abc"`, valeur négative, unité inconnue |
| Aucun effet ambiant | `forge validate <Id>` | Doit rester conforme |
| README utilisable | `forge info <Id>` | L'exemple compile-t-il vraiment ? |
| Généricité | Lecture | Aucun mot du domaine « supervision » ne doit apparaître dans `src/` |

**Le piège à guetter** : un agent pressé publie un package dont le README a gardé les
« À compléter » du gabarit, ou dont les tests ne couvrent que le cas nominal. La
publication passera — le validateur vérifie la présence des sections, pas la qualité
de leur contenu. C'est une limite connue du système, et cette manche sert justement à
mesurer son ampleur.

### Manche 5 — refermer la boucle

Un dernier projet, domaine encore différent, qui réutilise ce que la manche 4 a créé :

> Écris un planificateur de tâches : il lit des règles où la périodicité est notée en
> format compact (`45s`, `1h30m`), exécute chaque tâche en réessayant si elle échoue,
> et écrit un journal où les chaînes de connexion sont masquées.

Attendu : **zéro `forge new`**, quatre packages réutilisés. C'est la manche qui
démontre l'effet cumulatif — et celle où `forge stats` doit franchir un nouveau seuil.

---

## Vérifier les garanties du système

Chacune se teste en quelques secondes.

| Garantie | Commande | Attendu |
|----------|----------|---------|
| Un package ne compile pas → refus | Casser volontairement `src/`, puis `forge publish <Id>` | Échec des tests, publication refusée |
| Version immuable | `forge publish <Id>` deux fois de suite | « version déjà publiée » |
| SemVer vérifié | Ajouter une méthode publique, `forge bump <Id> patch`, publier | « Minor exigé, Patch appliqué » → refus |
| Anti-duplication | Créer un package qui reformule un existant, publier | « QUASI-DOUBLON », refus |
| Règles opposables | Mettre `Console.WriteLine` dans `src/`, `forge validate <Id>` | « API bannie » |
| Montée sans casse | `forge outdated <projet>` | Classement SÛRE / RELECTURE |
| Rollback sur échec | `forge update <projet> --safe-only --test "exit 1"` | `.csproj` restauré à l'identique |

L'outil lui-même se vérifie par :

```powershell
dotnet test tools\SnippetForge.Tests
```

---

## Lire `forge stats` sans se tromper

```
Investi   :    384 lignes  (~4224 tokens)
Économisé :    178 lignes  (~1958 tokens)
```

- **Investi** : tout ce qui a été écrit dans la bibliothèque (code, tests, docs).
  Coût réel et unique.
- **Économisé** : lignes de `src/` qu'il n'a **pas** fallu régénérer, comptées sur
  `N-1` consommateurs.
- **~11 tokens/ligne** : un ordre de grandeur assumé, pas une facture. Pour un chiffre
  exact, comparez les tokens réellement rapportés par votre outil entre les manches 1
  et 3 — c'est la seule mesure directe.

Ce que `forge stats` **ne mesure pas** : les tokens de raisonnement de l'agent, le
coût des recherches infructueuses, ni le temps humain. Le protocole en trois manches
les capture, la commande non.

---

## Quand conclure que ça ne marche pas

Soyez prêt à ce verdict — il est informatif :

- **L'agent ne lance jamais `forge search`** → les instructions ne sont pas lues.
  Vérifier que `CLAUDE.md` ou `.github\copilot-instructions.md` existe dans le projet.
- **Il cherche mais ne trouve pas un package qui existe** → problème de recherche.
  Vérifier avec `forge search` à la main ; si le repli lexical rate la synonymie,
  installer `nomic-embed-text` (voir INSTALL.md).
- **Il trouve mais recode quand même** → les instructions manquent de fermeté, ou le
  package trouvé ne couvrait pas vraiment le besoin. Lire sa justification.
- **La bibliothèque reste non rentable après plusieurs projets** → les besoins ne se
  recoupent pas assez. C'est un résultat valide : MicroForge est utile sur des projets
  qui partagent des briques, pas sur des travaux sans recouvrement.
