# Setup MicroForge : compile l'outil, publie les packages d'exemple, construit l'index.
# -Global : enregistre en plus le feed comme source NuGet au niveau utilisateur,
#           pour que les projets HORS de ce dossier voient aussi les micropackages.
param([switch]$Global, [switch]$SkipToolTests)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== Setup MicroForge ==="

$sdks = dotnet --list-sdks
if (-not $sdks) { Write-Error ".NET SDK introuvable : installer .NET 8+ d'abord."; exit 1 }
Write-Host "SDK .NET détecté."

Write-Host "Compilation de SnippetForge..."
dotnet build (Join-Path $root "tools\SnippetForge") -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Échec de compilation de SnippetForge."; exit 1 }

if (-not $SkipToolTests) {
    Write-Host "Tests de l'outil (l'outil qui impose des tests doit en avoir)..."
    dotnet test (Join-Path $root "tools\SnippetForge.Tests") --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Tests de SnippetForge en échec."; exit 1 }
}

# Modèle d'embedding local : optionnel, améliore recherche et détection de doublons.
try {
    $null = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -TimeoutSec 3
    Write-Host "Ollama détecté : recherche sémantique disponible."
} catch {
    Write-Host "Ollama absent : repli lexical déterministe (aucune installation requise)."
    Write-Host "  Pour activer la recherche sémantique : ollama serve, puis ollama pull nomic-embed-text"
}

New-Item -ItemType Directory -Force -Path (Join-Path $root "feed"), (Join-Path $root "registry") | Out-Null

# Publie chaque package source dont la version n'est pas encore dans le feed.
foreach ($pkg in Get-ChildItem (Join-Path $root "packages") -Directory) {
    $csproj = Get-ChildItem (Join-Path $pkg.FullName "src") -Filter *.csproj | Select-Object -First 1
    if (-not $csproj) { continue }
    [xml]$xml = Get-Content $csproj.FullName
    $id = ($xml.Project.PropertyGroup.PackageId | Where-Object { $_ }) | Select-Object -First 1
    $version = ($xml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
    if (Test-Path (Join-Path $root "feed\$id.$version.nupkg")) {
        Write-Host "Déjà publié : $id $version"
        continue
    }
    Write-Host "Publication de $id $version (validation + tests + pack)..."
    & (Join-Path $root "forge.ps1") publish $pkg.FullName
    if ($LASTEXITCODE -ne 0) { Write-Error "Échec de publication de $($pkg.Name)."; exit 1 }
}

Write-Host "Régénération de l'index, des contrats publics et des vecteurs..."
& (Join-Path $root "forge.ps1") index

if ($Global) {
    $sources = dotnet nuget list source
    if ($sources -notmatch "MicroForge") {
        dotnet nuget add source (Join-Path $root "feed") --name MicroForge
        Write-Host "Source NuGet globale « MicroForge » enregistrée."
    } else {
        Write-Host "Source NuGet globale « MicroForge » déjà enregistrée."
    }
}

Write-Host ""
Write-Host "=== Setup terminé ==="
Write-Host "Trouver     :  .\forge.ps1 search ""relancer un appel http qui echoue"""
Write-Host "Consulter   :  .\forge.ps1 info Micro.Flow.Retry"
Write-Host "Hygiène     :  .\forge.ps1 duplicates --all"
Write-Host "Consommer   :  .\forge.ps1 outdated demo"
Write-Host "Workflow IA : AGENT.md — Règles immuables : RULES.md"
