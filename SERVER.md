# Héberger un dépôt MicroForge d'équipe

Ce document décrit l'étage « organisation » de l'architecture : un dépôt NuGet privé
où une équipe partage ses micropackages. Il a été **validé en conditions réelles**
avec BaGet en Docker — les artefacts reviennent bit à bit identiques sur un poste vierge.

## Ce qui va où

| Étage | Contenu | Portée |
|-------|---------|--------|
| **Local** — `feed/` | Copie de travail, rapide, hors ligne | Le poste |
| **Équipe** — dépôt NuGet privé | Les artefacts partagés | L'organisation |
| **nuget.org** | `MicroForge.Cli` uniquement, jamais le corpus | Public |

**Les micropackages ne vont pas sur nuget.org.** C'est un espace de noms mondial et
permanent : y déverser des identifiants génériques (`Micro.Text.Slugify`…) revient à
en priver tout le monde définitivement. Le corpus est de toute façon propre à une
organisation — le « générique » d'une équipe finance n'est pas celui d'une équipe jeux.

Le modèle est celui d'un clone Git : on travaille localement, on synchronise avec le
dépôt d'équipe. Les **sources** se partagent par Git, les **artefacts** par NuGet.

## Monter un serveur de test (BaGet, Docker)

```powershell
docker volume create microforge-baget-data

docker run -d --name microforge-baget -p 5555:80 `
  -v microforge-baget-data:/var/baget `
  -e ApiKey=votre-cle `
  -e "Storage__Type=FileSystem" -e "Storage__Path=/var/baget/packages" `
  -e "Database__Type=Sqlite" -e "Database__ConnectionString=Data Source=/var/baget/baget.db" `
  -e "Search__Type=Database" `
  loicsharma/baget:latest
```

Deux pièges rencontrés, et leur raison :

- **BaGet écoute sur le port 80**, pas 8080 — le mapping doit être `-p <externe>:80`.
- **Le volume est obligatoire** : sans `/var/baget` inscriptible, la création de la
  base SQLite échoue et le conteneur s'arrête au démarrage.

Vérifier : `curl http://localhost:5555/v3/index.json` doit renvoyer un index de
service avec une ressource `PackageBaseAddress/3.0.0`.

## Configurer la forge

```powershell
forge remote --source "https://nuget.interne/v3/index.json"
$env:MICROFORGE_API_KEY = "<votre clé>"
```

La clé n'est **jamais** écrite sur disque : seul le nom de la variable d'environnement
est enregistré, et la valeur est masquée dans toutes les sorties, y compris les
messages d'erreur du serveur.

### HTTPS obligatoire pour publier

NuGet **refuse** `push` vers une source HTTP. Un dépôt d'équipe réel doit être servi
en HTTPS — c'est la bonne pratique, la clé d'API transite dans la requête.

Pour un serveur de test local uniquement, ajouter dans le `nuget.config` :

```xml
<add key="MonServeurTest" value="http://localhost:5555/v3/index.json"
     allowInsecureConnections="true" />
```

## Le cycle

```powershell
# Publier vers l'équipe (après validation locale)
forge push Micro.Flow.Retry

# Rapatrier sur un autre poste
forge pull Micro.Flow.Retry
forge verify            # les empreintes doivent concorder
```

L'ordre est délibéré : un package est validé, testé et publié **localement** d'abord,
puis poussé. Le dépôt partagé ne reçoit que des artefacts déjà éprouvés.

Sur une source HTTP(S), `forge pull` demande un identifiant de package — le protocole
NuGet v3 n'expose pas d'inventaire complet fiable. Sur un **dossier partagé** (chemin
UNC), `forge pull` sans argument rapatrie tout.

## Amorcer un poste vierge

```powershell
git clone <votre dépôt MicroForge>
cd MicroForge
.\install.ps1                                    # outil, source NuGet, instructions IA
forge remote --source "https://nuget.interne/v3/index.json"
forge pull <chaque package voulu>
forge verify
```

Les artefacts rapatriés sont **bit à bit identiques** à ceux publiés : c'est la
garantie d'immutabilité, vérifiée par les empreintes SHA-256.

## Autres serveurs

Tout dépôt NuGet v3 convient. BaGet est le plus simple à héberger ;
**Azure Artifacts**, **GitHub Packages**, **Nexus** et **Gitea** fonctionnent de la
même façon — seule l'URL et le mode d'authentification changent.

Pour les dépôts exigeant une authentification par jeton d'accès (GitHub Packages,
Azure Artifacts), le jeton se place dans la variable d'environnement désignée par
`--api-key-var`.

## Fusionner deux dépôts

Les dépôts sont interopérables : ce sont des flux NuGet standard. Fusionner revient à
pousser les artefacts de l'un vers l'autre. Deux points d'attention :

- **Collision d'identifiants** — deux équipes ayant chacune un `Micro.Text.Slugify`
  différent ne peuvent pas cohabiter sous le même nom. Un préfixe d'organisation
  (`Contoso.Micro.Text.Slugify`) évite le problème dès le départ.
- **Doublons fonctionnels** — après fusion, lancer `forge duplicates --all` et
  déprécier les redondances en pointant vers le package conservé. C'est exactement
  le cas d'usage de `forge deprecate --replacement`.
