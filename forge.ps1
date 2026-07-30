# Wrapper du CLI SnippetForge. Usage : .\forge.ps1 <commande> [arguments]
$toolProject = Join-Path $PSScriptRoot "tools\SnippetForge"
$dll = Join-Path $toolProject "bin\Release\net8.0\SnippetForge.dll"

if (-not (Test-Path $dll)) {
    Write-Host "Compilation de SnippetForge..."
    dotnet build $toolProject -c Release --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Échec de compilation de SnippetForge."
        exit 1
    }
}

$env:MICROFORGE_ROOT = $PSScriptRoot
dotnet $dll @args
exit $LASTEXITCODE
