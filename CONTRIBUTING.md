# Contribuer à MicroForge

## Ajouter ou faire évoluer un micropackage

C'est le cas de contribution le plus courant, et il n'y a **qu'un seul chemin** —
le même pour un humain et pour une IA (R12) :

```bash
forge search "<le besoin>"        # d'abord vérifier qu'il n'existe pas
forge new Micro.<Domaine>.<Action> --description "…" --tags "a;b;c"
# implémenter src/, écrire les tests, rédiger le README (4 sections)
forge validate Micro.<Domaine>.<Action>
forge publish  Micro.<Domaine>.<Action>
```

Le validateur applique [RULES.md](RULES.md) mécaniquement. Si la publication est
refusée, le message dit quoi corriger — ne jamais contourner, jamais écrire
directement dans `feed/`.

Pour modifier un package existant : éditer `packages/<Id>/`, `forge bump` au niveau
que le contrat exige (`forge diff` pour le connaître), republier.

## Modifier l'outil (SnippetForge)

- Toute fonctionnalité nouvelle arrive **avec ses tests** — l'outil qui impose des
  tests aux packages en a lui-même 337+.
- Les corrections de bugs ajoutent un test de régression qui documente le scénario
  (voir `ForgeRootResolutionTests`, `ReadmeQualityTests` pour le style).
- `dotnet test tools/SnippetForge.Tests` doit être vert avant tout commit.
- Une entrée dans [CHANGELOG.md](CHANGELOG.md) pour tout changement visible.
- La version de l'outil (`tools/SnippetForge/SnippetForge.csproj`) suit SemVer ;
  un changement du format des fichiers de `registry/` incrémente aussi
  `ForgeVersion.RegistryFormat`.

## Immuables

`RULES.md`, `SPEC.md` (hors errata), `AGENT.md` et `packages/Directory.Build.props`
ne se modifient pas dans le cours normal des contributions. Une évolution de ces
fichiers est un changement de la méthode elle-même : elle se discute d'abord.

## Licence des contributions

Le projet est sous [Apache-2.0](LICENSE). En soumettant une contribution, vous
acceptez qu'elle soit distribuée sous cette licence (section 5 de la licence).
Le fichier [NOTICE](NOTICE) est conservé sur toute redistribution.
