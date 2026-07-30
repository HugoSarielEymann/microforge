# La méthode MicroForge — spécification

Version 1.0 — Hugo Eymann, 2026.
Implémentation de référence : ce dépôt (C#/.NET, NuGet).

Cette spécification est **indépendante du langage**. Elle décrit une méthode que tout
écosystème doté d'un gestionnaire de paquets peut appliquer : npm, PyPI, Cargo, Maven…
Les mots MUST (« doit »), SHOULD (« devrait ») et MAY (« peut ») s'entendent au sens
de la RFC 2119.

## 1. Problème

Les IA génératrices de code réécrivent sans cesse les mêmes capacités génériques —
relance, normalisation, parsing, découpage. Chaque régénération coûte des tokens,
de l'énergie, et produit un code non testé qui diverge d'un projet à l'autre.

## 2. Principe

Chaque capacité générique est écrite **une seule fois**, sous forme de **micropackage**
versionné, testé et documenté, publié dans un dépôt de paquets interrogeable. Avant
d'écrire du code générique, un agent **cherche** ; il ne forge un micropackage que si
la recherche échoue, et le consomme ensuite comme n'importe quelle dépendance. La
bibliothèque croît comme sous-produit du travail normal.

Le point décisif n'est pas l'idée de réutilisation — elle est aussi vieille que les
bibliothèques — mais son **opposabilité à un agent** : chaque règle est appliquée par
l'outillage, qui refuse la publication non conforme. Un agent ne « devrait pas »
publier un doublon : il ne le **peut** pas.

## 3. Le micropackage

Un micropackage MUST :

- **M1** — porter une seule responsabilité, exposée par un point d'entrée unique,
  avec un identifiant de la forme `<Préfixe>.<Domaine>.<Action>` ;
- **M2** — être pur : tout effet non déterministe (horloge, aléa, délai, E/S,
  journalisation) est injecté par l'appelant, jamais ambiant ;
- **M3** — embarquer ses tests, couvrant le cas nominal, les cas limites et les
  erreurs de paramétrage ; la publication MUST échouer si les tests échouent ;
- **M4** — embarquer un mode d'emploi structuré : description, quand l'utiliser et
  quand ne pas l'utiliser, tableau exhaustif des options, exemple exécutable. Le
  contenu MUST être contrôlé (pas seulement la présence des sections) : un gabarit
  non rédigé est refusé ;
- **M5** — ne contenir aucun terme du domaine métier du projet qui l'a fait naître.

Le mode d'emploi n'est pas décoratif : c'est **l'interface de décision** d'un agent.
Un package sans mode d'emploi utilisable est invisible en pratique.

## 4. Le dépôt

- **D1** — Un artefact publié est **immuable** : jamais modifié, jamais supprimé.
  Toute évolution est une nouvelle version.
- **D2** — Le dépôt MUST enregistrer une empreinte de chaque artefact publié, et
  savoir détecter un artefact déposé hors du circuit de validation, modifié, ou
  supprimé. Sur un dépôt partagé, les artefacts SHOULD être signés.
- **D3** — Un package obsolète se **déprécie** : la déclaration (raison, versions
  visées, remplaçant) vit hors des artefacts, est réversible, et n'invalide aucun
  build existant.
- **D4** — Tout l'état dérivé (index de recherche, contrats, vecteurs) MUST être
  régénérable depuis les artefacts. Seules les décisions humaines (dépréciations)
  sont irremplaçables.
- **D5** — Le dépôt MAY être répliqué : un poste travaille sur sa copie locale et se
  synchronise avec un dépôt d'équipe (modèle du clone).

## 5. Le contrat public et les versions

- **V1** — À chaque publication, l'outil MUST extraire le **contrat public** de
  l'artefact (types et signatures visibles par un consommateur) et le résumer en une
  empreinte (« digest »). Deux versions au même digest sont interchangeables à la
  compilation.
- **V2** — L'incrément de version sémantique MUST être vérifié contre la différence
  de contrat, pas seulement déclaré : contrat identique → patch suffit ; ajouts
  seuls → mineur exigé ; tout retrait ou modification → majeur exigé. Un incrément
  insuffisant MUST faire échouer la publication.
- **V3** — Les tests MUST porter sur le binaire livré (même configuration de build
  que l'artefact) : on ne valide pas un artefact et on n'en livre pas un autre.
- **V4** — Les consommateurs MUST épingler des versions exactes. L'outil SHOULD
  classer les montées : « sûre » (même majeur, aucun retrait de contrat — la
  compilation ne peut pas casser) ou « à relire ». Une montée sûre garantit la
  compilation, pas le comportement : l'application effective MUST être validée par
  la suite de tests du consommateur, avec restauration automatique en cas d'échec.

## 6. L'anti-duplication

- **A1** — Avant publication, le candidat MUST être comparé à tous les packages
  publiés par au moins deux signaux indépendants : la **description** (similarité
  lexicale ou sémantique) et la **forme du contrat public** (signatures, identifiants
  propres au package effacés).
- **A2** — Une similarité au-delà du seuil de blocage MUST faire échouer la
  publication, avec indication du package existant à réutiliser ou étendre. Une
  dérogation explicite MAY exister ; elle SHOULD rester exceptionnelle et tracée.
- **A3** — Les seuils MUST être calibrés par espace de comparaison : les scores d'un
  modèle sémantique et ceux d'une projection lexicale ne sont pas commensurables.
- **A4** — La forme du contrat seule MUST NOT déclencher un refus : elle confirme une
  proximité suspectée, elle ne la crée pas.

## 7. Le workflow de l'agent

Pour toute demande de code, l'agent MUST procéder dans cet ordre :

1. **Bibliothèque standard d'abord.** Si le langage couvre le besoin, l'utiliser.
   Un micropackage qui double le framework est une dette.
2. **Chercher** dans la bibliothèque, en langage naturel. Si un package répond :
   l'installer à version exacte et le paramétrer, **jamais le recoder**. S'il répond
   presque : l'étendre de façon rétrocompatible.
3. **Forger** le manquant : concevoir la capacité générique (options avec défauts,
   effets injectés, documentation), la tester, la documenter, la publier. Traiter
   chaque refus de publication, jamais le contourner.
4. **Consommer** le package forgé via le gestionnaire de paquets — le projet ne
   contient jamais de copie du code générique.

Les instructions données à l'agent MUST être **autonomes** — auto-suffisantes, sans
renvoi vers un fichier externe pour une partie du workflow. (Constat d'expérience :
un renvoi n'est pas suivi de façon fiable, et la moitié du workflow n'est alors
jamais appliquée.)

## 8. La mesure

- **S1** — L'outil SHOULD journaliser ses invocations : la preuve que le workflow est
  suivi se lit dans la trace (« l'agent a-t-il cherché avant d'écrire ? »), pas dans
  le code produit.
- **S2** — Toute mesure d'économie MUST être conservatrice : un package utilisé par
  N projets économise N−1 régénérations, jamais N — le premier usage est un coût.
  Seul le code que l'agent aurait effectivement généré compte comme économie ; les
  tests et la documentation du package comptent comme investissement.
- **S3** — L'évaluation SHOULD comparer des séquences de projets (avec/sans la
  méthode), pas un projet isolé : le gain n'apparaît qu'à la réutilisation.

## 9. Symétrie humain/machine

Humains et agents passent par le même circuit : mêmes règles, même validateur, mêmes
refus. Aucun acteur n'écrit directement dans le dépôt.

---

*Cette spécification est publiée sous licence Apache 2.0 (voir LICENSE et NOTICE).
Toute œuvre dérivée doit conserver l'attribution. Les implémentations indépendantes
de la méthode sont libres et encouragées ; les citer est demandé par courtoisie
(section 6 de la licence pour l'usage du nom).*
