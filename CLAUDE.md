# MicroForge

Avant de générer du code C# ici ou dans un projet voisin, lire et appliquer
**AGENT.md** dans son intégralité : c'est le workflow obligatoire (décomposer,
chercher via `forge search` avant d'écrire, réutiliser ou forger un micropackage,
publier via `forge publish`, maintenir les consommateurs via `forge outdated`).

Règles non négociables : **RULES.md** — appliquées mécaniquement par
`.\forge.ps1 validate`. Ne jamais modifier RULES.md, packages/Directory.Build.props,
ni écrire directement dans feed/ ou registry/ (sauf registry/deprecations.json, via
`forge deprecate`).
