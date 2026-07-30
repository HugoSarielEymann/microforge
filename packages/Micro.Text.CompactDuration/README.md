# Micro.Text.CompactDuration

## Description

Parse une chaîne de durée au format compact (`30s`, `5m`, `2h30m`, `1h15m30s`) en `TimeSpan`.
Supporte la combinaison heures (`h`), minutes (`m`), secondes (`s`), dans cet ordre, chacune optionnelle.
Fournit `CompactDurationParser.Parse` (lève `FormatException` si invalide) et `CompactDurationParser.TryParse` (retourne `bool`).

## Mode d'emploi

**Quand utiliser ce package :** dès qu'une configuration exprime une durée sous forme compacte et lisible
par un humain (intervalles de polling, délais de timeout, TTL de cache, périodes de retry…).
La combinaison `Nh`, `Nm`, `Ns` couvre l'essentiel des besoins de configuration sans imposer le format ISO 8601.

**Quand ne pas l'utiliser :** pour des durées au format ISO 8601 (`PT2H30M`) ou `hh:mm:ss`,
`TimeSpan.Parse` / `XmlConvert.ToTimeSpan` suffisent et ne nécessitent pas ce package.
Ne pas l'utiliser non plus pour des durées fractionnaires (millisecondes, microsecondes).

## Paramétrage

| Méthode | Paramètre | Description |
|---------|-----------|-------------|
| `Parse` | `string input` | Lève `ArgumentNullException` si null, `FormatException` si le format est invalide ou si aucune unité n'est présente. |
| `TryParse` | `string? input`, `out TimeSpan result` | Retourne `false` sans lever d'exception ; `result` vaut `TimeSpan.Zero` en cas d'échec. |

Unités reconnues (ordre obligatoire) : `h` → `m` → `s`. Chacune est optionnelle mais au moins une doit figurer.
Les valeurs doivent être des entiers non négatifs.

## Exemple

```csharp
using Micro.Text.CompactDuration;

// Parsing strict : lève FormatException si la valeur est invalide
TimeSpan interval = CompactDurationParser.Parse("2h30m");   // 02:30:00
TimeSpan timeout  = CompactDurationParser.Parse("30s");     // 00:00:30
TimeSpan long_    = CompactDurationParser.Parse("1h15m30s"); // 01:15:30

// Parsing souple depuis la configuration
string? configValue = configuration["Monitor:Interval"];
if (CompactDurationParser.TryParse(configValue, out var delay))
    await Task.Delay(delay, cancellationToken);
```