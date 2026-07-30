# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/). Versions : SemVer.

## [0.2.1] — 2026-07-31

### Corrigé

- `forge init` et `forge bench start` refusent désormais un **dossier parent**
  (plus de 3 projets ou 5 000 fichiers) : lancés sur `source\repos`, ils déposaient
  des instructions valables pour des dizaines de dépôts sans rapport et tentaient
  d'empreindre 133 000 fichiers. `--force` reste possible en connaissance de cause.

## [0.2.0] — 2026-07-31

### Ajouté

- **`forge bench`** (`start` / `report` / `list`) : encadre une manche de test avec un
  agent IA. Capture l'état du projet, puis mesure fichiers et lignes produits,
  packages réutilisés, packages forgés, et la trace des commandes forge invoquées.
- **Journal d'usage** (`registry/usage.log`) : chaque invocation est consignée —
  la preuve que l'agent cherche avant d'écrire se lit dans la trace.
- **`forge pull`** : rapatrie les artefacts du dépôt d'équipe vers le feed local
  (dossier partagé : tout ou par package ; HTTP NuGet v3 : par package). Modèle du
  clone : chaque poste travaille sur sa copie, jamais de réécriture d'un artefact
  présent.
- **`forge verify [--adopt]`** : empreintes SHA-256 des artefacts, détection du dépôt
  manuel, de la falsification et de la suppression.
- **`forge doctor`**, **`forge remote`/`push`**, **`forge use`**, **`forge --version`**.
- **SPEC.md** : la méthode, spécifiée indépendamment du langage (MUST/SHOULD/MAY).
- Distribution en **outil .NET global** (`MicroForge.Cli`, commande `forge`) —
  Windows, macOS, Linux. Remplace lanceur .cmd et manipulation du PATH.
- Licence **Apache-2.0** + NOTICE (attribution préservée sur toute redistribution).

### Modifié

- La qualité du README est contrôlée (contenu, pas seulement titres) : gabarit non
  rédigé refusé à la publication (R7 durcie).
- L'anti-duplication confronte aussi la **forme du contrat public** ; une proximité
  de description confirmée par un contrat identique devient bloquante (R10 durcie).
- Les tests des micropackages s'exécutent en configuration Release — celle de
  l'artefact livré (R6 durcie).
- Le bloc d'instructions agent est autonome (plus de renvoi vers AGENT.md pour la
  moitié « forger »).

## [0.1.0] — 2026-07-25

Première version : CLI (search/info/new/validate/publish/bump/diff/deprecate/
outdated/update/stats), feed NuGet local immuable, contrats publics extraits et
digests, incrément SemVer vérifié, anti-duplication par description (Ollama ou repli
lexical calibré), dépréciation hors artefacts, mise à jour des consommateurs validée
par leur suite de tests avec restauration, métriques conservatrices, `forge init`,
règles R1–R13 appliquées par le validateur, 2 micropackages d'exemple.
