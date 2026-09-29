# Micro.Text.EditSpan

## Description

Situe la zone qui diffère entre deux versions d'un texte — le plus long préfixe commun, puis
le plus long suffixe commun restant — et permet de n'appliquer que cette différence. La zone
ne coupe jamais une paire de substitution (émoji) ni un saut de ligne `\r\n`. Fonction pure,
en temps linéaire.

## Mode d'emploi

Utiliser ce package pour mettre à jour un contrôle d'édition sans le réinitialiser : appliquer
une annulation ou un rétablissement, recharger un fichier modifié par un autre programme,
insérer le résultat d'une réécriture. Remplacer seulement la zone modifiée préserve le curseur,
le défilement et la mise en forme de tout le reste, là où remplacer le texte entier remettrait
tout à zéro — et coûterait cher sur un long document.

| Membre | Rôle |
|--------|------|
| `TextEditSpan.Between(before, after, options)` | Zone modifiée : `Start`, `RemovedLength`, `InsertedLength`. |
| `EditSpan.InsertedText(after)` | Le texte qui remplace la zone. |
| `TextEditSpan.Apply(before, span, inserted)` | Reconstruit la nouvelle version : l'aller-retour est exact. |
| `EditSpan.IsEmpty` | Les deux versions sont identiques. |

**Ne pas** l'utiliser comme outil de **comparaison** à afficher : la zone rendue est unique et
englobe toutes les modifications, même éloignées — pour montrer des différences ligne à ligne,
prendre un vrai algorithme de différence (Myers). Ne pas non plus s'en servir pour fusionner
deux modifications concurrentes.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `KeepUnitsWhole` | `bool` | `true` | Élargir la zone plutôt que couper une paire de substitution ou un `\r\n`. |

`EditSpanOptions.Validate()` n'a rien à refuser : aucun réglage ne peut être incohérent.
`Apply` refuse une zone qui sort du texte ou une longueur insérée qui ne correspond pas
(`ArgumentOutOfRangeException`).

## Exemple

```csharp
using Micro.Text.EditSpan;

string affiche = "Voir [[Plan]] demain.";
string relu = "Voir [[Checklist]] demain.";

EditSpan zone = TextEditSpan.Between(affiche, relu);
// zone.Start = 7, zone.RemovedLength = 4 : seul « Plan » est remplacé.
string remplacement = zone.InsertedText(relu);   // « Checklist »

string verifie = TextEditSpan.Apply(affiche, zone, remplacement);   // == relu
```
