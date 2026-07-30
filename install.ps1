# Installateur unique de MicroForge.
#
# C'est la SEULE commande à lancer. Il enchaîne :
#
#   0. setup.ps1 — prépare la bibliothèque               → -SkipSetup
#   1. installe « forge » comme outil .NET global        → -SkipTool
#   2. mémorise la racine pour toutes les exécutions     → (avec l'étape 1)
#   3. déclare le feed comme source NuGet machine        → -SkipNuGetSource
#   4. instructions IA globales (~/.claude/CLAUDE.md)    → -SkipGlobalAgent
#
# Ce script fonctionne aussi sous PowerShell 7 sur macOS et Linux : l'outil est un
# « dotnet tool », il n'y a plus ni lanceur .cmd ni manipulation du PATH.
#
# Rien n'est écrasé sans être signalé, et relancer le script est sans effet de bord.
# Utiliser -WhatIf pour voir ce qui serait fait sans rien modifier.

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$SkipSetup,
    [switch]$SkipTool,
    [switch]$SkipNuGetSource,
    [switch]$SkipGlobalAgent
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== Installation de MicroForge ==="
Write-Host "Racine : $root"
Write-Host ""

# --- 0. Préparation de la bibliothèque --------------------------------------
# N'écrit rien hors du dossier MicroForge.
if (-not $SkipSetup) {
    if ($PSCmdlet.ShouldProcess("la bibliothèque MicroForge", "Compiler, tester et publier (setup.ps1)")) {
        & (Join-Path $root "setup.ps1")
        if ($LASTEXITCODE -ne 0) { Write-Error "setup.ps1 a échoué : installation interrompue."; exit 1 }
        Write-Host ""
    }
} else {
    Write-Host "[0/4] Préparation de la bibliothèque ignorée (-SkipSetup)."
}

# --- 1 et 2. Outil global et racine mémorisée --------------------------------
if (-not $SkipTool) {
    if ($PSCmdlet.ShouldProcess("l'outil global « forge »", "Empaqueter et installer")) {
        $toolOutput = Join-Path $root ".artifacts\tool"
        dotnet pack (Join-Path $root "tools\SnippetForge") -c Release -o $toolOutput --nologo -v quiet | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error "Échec de l'empaquetage de l'outil."; exit 1 }

        # update installe si absent, met à jour sinon : idempotent dans les deux cas.
        dotnet tool update --global --add-source $toolOutput MicroForge.Cli | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error "Échec de l'installation de l'outil global."; exit 1 }

        $version = (dotnet tool list --global | Select-String "microforge.cli").ToString().Trim()
        Write-Host "[1/4] Outil global installé : $version"
        Write-Host "      Commande « forge », disponible dans tout nouveau terminal."

        & dotnet tool run forge use $root 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            # L'outil global n'est pas encore sur le PATH de CETTE session : on écrit
            # directement le fichier que « forge use » aurait produit.
            $configDir = Join-Path $env:USERPROFILE ".microforge"
            New-Item -ItemType Directory -Force -Path $configDir | Out-Null
            Set-Content -Path (Join-Path $configDir "root") -Value $root -NoNewline
        }
        Write-Host "[2/4] Racine mémorisée : « forge » fonctionne depuis n'importe quel dossier."
    }
} else {
    Write-Host "[1/4] Outil global ignoré (-SkipTool)."
    Write-Host "[2/4] Racine mémorisée ignorée."
}

# --- 3. Source NuGet machine -------------------------------------------------
if (-not $SkipNuGetSource) {
    $feed = Join-Path $root "feed"

    # « dotnet nuget list source » fusionne les nuget.config depuis le dossier courant.
    # Interrogé depuis MicroForge, il verrait le nuget.config local et conclurait à tort
    # que la source est enregistrée globalement. On interroge donc depuis un dossier neutre.
    Push-Location $env:USERPROFILE
    try {
        $sources = dotnet nuget list source 2>&1 | Out-String
    } finally {
        Pop-Location
    }

    if ($sources -match [regex]::Escape($feed)) {
        Write-Host "[3/4] Source NuGet « MicroForge » déjà enregistrée."
    } else {
        if ($PSCmdlet.ShouldProcess("source NuGet MicroForge", "Enregistrer $feed")) {
            Push-Location $env:USERPROFILE
            try {
                if ($sources -match "MicroForge") { dotnet nuget remove source MicroForge | Out-Null }
                dotnet nuget add source $feed --name MicroForge | Out-Null
            } finally {
                Pop-Location
            }
            Write-Host "[3/4] Source NuGet « MicroForge » enregistrée → $feed"
        }
    }
} else {
    Write-Host "[3/4] Source NuGet globale ignorée (-SkipNuGetSource)."
}

# --- 4. Instructions agent globales ------------------------------------------
if (-not $SkipGlobalAgent) {
    $claudeDir = Join-Path $env:USERPROFILE ".claude"

    if ($PSCmdlet.ShouldProcess((Join-Path $claudeDir "CLAUDE.md"), "Insérer le bloc MicroForge")) {
        New-Item -ItemType Directory -Force -Path $claudeDir | Out-Null
        # Seul Claude Code lit ce dossier : inutile d'y déposer un fichier Copilot.
        & (Join-Path $root "forge.ps1") init $claudeDir --no-nuget-config --agents "Claude" | Out-Null
        Write-Host "[4/4] Bloc MicroForge inséré dans $claudeDir\CLAUDE.md"
        Write-Host "      Toute session Claude Code, dans n'importe quel projet, le chargera."
    }
} else {
    Write-Host "[4/4] CLAUDE.md global ignoré (-SkipGlobalAgent)."
}

Write-Host ""
Write-Host "=== Installation terminée ==="
Write-Host ""
Write-Host "Il reste UNE commande, dans chaque projet où vous voulez la forge."
Write-Host "Ouvrir un NOUVEAU terminal, puis :"
Write-Host ""
Write-Host "    cd <votre projet>"
Write-Host "    forge init ."
Write-Host ""
Write-Host "Désinstallation : dotnet tool uninstall --global MicroForge.Cli,"
Write-Host "  dotnet nuget remove source MicroForge, suppression de ~/.microforge,"
Write-Host "  et des blocs microforge dans les fichiers d'instructions."
