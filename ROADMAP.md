# Feuille de route

État au 2026-07-31, version 0.2.1. Ordonnée par ce qui bloque réellement une mise à
disposition publique.

## Fait

- Workflow complet éprouvé en conditions réelles avec GitHub Copilot (manches 1 à 4) :
  recherche avant écriture, réutilisation, **création de micropackages par l'agent**,
  refus de validation traités et non contournés.
- Barrière SemVer vérifiée sur un cas réel : `patch` refusé pour un ajout de membre.
- Dépréciation, `forge outdated`, `forge update --safe-only` avec restauration.
- `forge review` : prépare la relecture de ce que le validateur ne peut pas juger.
- Partage d'équipe (`remote` / `push` / `pull`), intégrité (`verify`), mesure (`bench`).
- Outil .NET global, licence Apache-2.0 + NOTICE, SPEC.md agnostique du langage.

## Bloquant pour une release

1. **Publier le dépôt.** Sans publication : pas d'antériorité opposable, pas
   d'installation par un tiers, pas de retour. Préalable à tout le reste.
2. **Publier `MicroForge.Cli` sur nuget.org**, pour que
   `dotnet tool install -g MicroForge.Cli` suffise. Aujourd'hui un tiers doit cloner
   et compiler.
3. **Faire tourner la CI au moins une fois.** Le workflow existe mais n'a jamais été
   exécuté : sans dépôt distant, il est théorique.
4. **Vérifier macOS/Linux.** Le CLI cible net8.0 et devrait être portable — jamais
   confirmé. La CI Ubuntu répond immédiatement.

## Crédibilité

5. **Démo reproductible** : état initial figé, prompts, résultats attendus, script de
   vérification. Ce qui convainc en cinq minutes, mieux qu'un README.
6. **Grossir le corpus.** Quatre packages. Le système n'a jamais affronté une
   bibliothèque de cinquante, où la recherche devient difficile et les quasi-doublons
   fréquents — c'est là qu'Ollama deviendra nécessaire.

## Reporté (rappels)

7. **Multi-langage.** SPEC.md est écrit ; une implémentation npm / PyPI / Cargo est un
   projet en soi. Trois voies possibles : portage complet, cœur commun avec adaptateurs
   par écosystème, ou spécification + implémentations de référence. La troisième donne
   le plus de portée pour le moins de travail.

8. **Analyseur Roslyn** à la place des expressions régulières pour les API bannies.
   Aujourd'hui `using C = System.Console;` passe au travers. Un vrai analyseur,
   distribué dans `Directory.Build.props`, appliquerait la règle jusque dans l'IDE.

9. **Signature des artefacts.** Le registre d'empreintes (`forge verify`) détecte
   l'accident et le dépôt manuel, pas un adversaire : qui peut écrire dans `registry/`
   peut y inscrire l'empreinte d'un artefact falsifié. Sur un dépôt partagé, c'est la
   signature NuGet qui joue ce rôle.

## Écarté après examen

- **Un test par exception documentée** : proposé après la manche 4, mais **n'aurait
  attrapé aucun des deux défauts réels** (`OverflowException` n'était pas documentée,
  et le trou d'`UrlSanitizer` était une omission fonctionnelle). Remplacé par
  `forge review`, qui pose les questions au lieu d'ajouter une règle inopérante.
