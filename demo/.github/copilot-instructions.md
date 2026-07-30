<!-- microforge:begin -->
## Bibliothèque de micropackages MicroForge

Avant d'écrire du code C# **générique** (retry, parsing, formatage, validation,
découpage, mapping…), chercher d'abord dans la bibliothèque :

```
forge search "<besoin en langage naturel>"
forge info <PackageId>
```

Si un package répond au besoin : `dotnet add package <Id> --version <version exacte>`
puis le paramétrer — **ne jamais le recoder**. S'il répond presque, l'étendre de
façon rétrocompatible plutôt que d'en créer un nouveau. Sinon seulement, forger un
micropackage (`forge new`, puis `forge publish`).

Workflow complet et obligatoire : `C:\Users\hugoe\Documents\Widgets\MicroForge\AGENT.md`
Règles immuables opposables : `C:\Users\hugoe\Documents\Widgets\MicroForge\RULES.md`

Avant de monter une version de micropackage dans ce projet :
`forge outdated .` puis `forge update . --safe-only --test "<votre commande de tests>"`.
<!-- microforge:end -->
