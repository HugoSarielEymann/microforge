# Licence, attribution et usage commercial

## En bref

MicroForge est sous **[Apache-2.0](LICENSE)**. Vous pouvez l'utiliser, le modifier,
le redistribuer et le commercialiser — y compris au sein d'un produit propriétaire
fermé, sans rien devoir ni demander.

**Une seule obligation : conserver la mention d'auteur.** La section 4(d) de la
licence impose de reproduire le fichier [NOTICE](NOTICE) dans toute redistribution,
y compris d'une version modifiée. C'est peu, et c'est non négociable.

## Pourquoi Apache-2.0 plutôt qu'une licence protectrice

Le choix a été délibéré, après examen de l'AGPL et des licences à seuil.

**L'AGPL n'aurait rien protégé ici.** Son copyleft se déclenche quand on *distribue*
le logiciel ou qu'on l'*expose comme service*. Or MicroForge est un outil de
développement : une entreprise qui s'en sert en interne pour construire ses produits
ne distribue rien, et n'aurait donc jamais eu à acheter quoi que ce soit. La
contrainte sans le bénéfice.

**Pire, elle aurait été toxique pour les micropackages.** Ceux-là sont réellement
distribués : ils partent dans le binaire du produit de l'utilisateur. Sous AGPL, tout
produit consommant `Micro.Flow.Retry` serait devenu AGPL. Aucune entreprise ne prend
ce risque — l'adoption serait morte, exactement l'inverse du but recherché.

**Les licences à seuil de chiffre d'affaires** (PolyForm, BSL) génèrent du revenu mais
sortent du champ open source, ce qui coûte en adoption et en confiance. À ce stade —
un outil qui fonctionne, pas encore un produit — l'adoption vaut plus que la
protection.

## Ce qui reste réservé

La licence Apache-2.0 couvre **ce dépôt, en l'état**. Elle ne concède aucun droit sur
du code futur.

Une éventuelle couche entreprise — registre partagé avec gouvernance, politiques
d'organisation, journal d'audit, métriques inter-équipes, intégration SSO — n'existe
pas encore et n'a pas vocation à être publiée sous cette licence. C'est le modèle
dit *open-core* : le noyau est libre, la couche organisationnelle ne l'est pas.

Publier le noyau aujourd'hui n'entame donc en rien cette possibilité.

## La méthode, distincte de l'implémentation

[SPEC.md](SPEC.md) décrit la méthode indépendamment du langage. Elle est librement
implémentable ailleurs — npm, PyPI, Cargo — et ces implémentations sont encouragées.

Citer l'origine est demandé par courtoisie ; c'est une obligation de la section 4(c)
dès lors qu'une implémentation dérive de ce code ou de cette documentation. Une
réécriture indépendante à partir de la seule lecture de la méthode ne crée, elle,
aucune obligation juridique — mais la citation reste la moindre des choses.

## Marque

Le nom « MicroForge » ne fait l'objet d'aucun dépôt de marque. La section 6 de la
licence Apache-2.0 ne concède pas l'usage du nom de l'auteur ni du projet au-delà de
ce qui est nécessaire pour en décrire l'origine.

## Contributions

Voir [CLA.md](CLA.md). Toute contribution est acceptée sous Apache-2.0, avec cession
des droits nécessaires pour préserver la possibilité d'une licence différente sur les
évolutions futures — sans quoi cette porte se refermerait dès la première
contribution externe.

---

*Ce document explique une intention ; il n'a pas valeur juridique. En cas de doute,
c'est le texte de [LICENSE](LICENSE) qui fait foi.*
