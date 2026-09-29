# Micro.Soap.Envelope

## Description

Construit et lit des **enveloppes SOAP 1.1 et 1.2** : en-têtes, corps, et **fautes** complètes
(code, sous-code applicatif, raison, acteur ou rôle, détail XML), dans la syntaxe de chaque
version (`Client`/`Server` en 1.1, `Sender`/`Receiver` et `Subcode` en 1.2). Détecte la version
par l'espace de noms de l'enveloppe ou par le Content-Type HTTP, et extrait l'action SOAP 1.2 du
Content-Type. La lecture est durcie : **DTD interdites, aucune entité externe résolue**, taille
bornée — une enveloppe reçue du réseau ne peut ni lire un fichier local ni saturer la mémoire.

## Mode d'emploi

À utiliser pour appeler un service SOAP sans proxy généré (poster une enveloppe, lire la
réponse ou la faute), ou pour servir des opérations SOAP depuis un point de terminaison HTTP
écrit à la main : lire la requête, répondre par une enveloppe ou une faute.

**Ne pas** l'utiliser pour produire ou valider un WSDL, ni pour les extensions WS-*
(WS-Security, WS-Addressing) : il transporte les éléments d'en-tête sans les interpréter. Il ne
sérialise pas vos objets : le corps est un `XElement` que vous construisez ou lisez vous-même.

| Membre | Rôle |
|--------|------|
| `SoapEnvelope.Create(body, version, headers)` | Une enveloppe (`XDocument`) autour d'un élément de corps. |
| `SoapEnvelope.CreateFault(fault, version)` | Une enveloppe portant une faute. |
| `SoapEnvelope.Parse(xml | stream, options)` | L'enveloppe lue (`SoapMessage` : version, en-têtes, premier élément du corps, faute) ; `FormatException` sinon. |
| `SoapEnvelope.TryParse(xml, out message, out error, options)` | Même lecture, sans jamais lever. |
| `SoapEnvelope.ContentType(version, action)` | `text/xml; charset=utf-8`, ou `application/soap+xml; charset=utf-8; action="…"`. |
| `SoapEnvelope.DetectVersion(contentType)` | La version d'après le Content-Type, `null` s'il n'est pas SOAP. |
| `SoapEnvelope.ActionFromContentType(contentType)` | L'action SOAP 1.2 portée par le Content-Type. |
| `SoapEnvelope.EnvelopeNamespace(version)` | L'espace de noms de l'enveloppe. |

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `MaxCharacters` | `long` | `10000000` | Taille maximale d'un document lu, en caractères ; au-delà, `FormatException`. |

`SoapEnvelopeOptions.Validate()` refuse une taille inférieure à 1 (`ArgumentOutOfRangeException`).
Passer `null` en options prend les défauts. La construction n'a aucun paramètre.

## Exemple

```csharp
using System.Xml.Linq;
using Micro.Soap.Envelope;

XNamespace ns = "urn:mini-erp";
XDocument requete = SoapEnvelope.Create(new XElement(ns + "ConsulterUnClient", new XElement(ns + "identifiant", 42)));
using var contenu = new StringContent(requete.ToString(), System.Text.Encoding.UTF8);
contenu.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(SoapEnvelope.ContentType(SoapVersion.Soap11));

string texte = "<réponse reçue du service>";
if (SoapEnvelope.TryParse(texte, out SoapMessage? reponse, out string? motif))
{
    if (reponse.IsFault)
    {
        string raison = reponse.Fault.Reason;             // « Aucun client ne porte ce numéro. »
        bool faute_appelant = reponse.Fault.Code == SoapFaultCode.Client;
    }
    else
    {
        XElement? corps = reponse.Body;                   // <ConsulterUnClientResponse>…
    }
}

XDocument faute = SoapEnvelope.CreateFault(new SoapFault(SoapFaultCode.Client, "Aucun client ne porte ce numéro.") { SubCode = "INTROUVABLE" }, SoapVersion.Soap12);
```
