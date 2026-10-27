$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
    Lokale ontwikkelings-DB bootstrapen (backup + Migrate())

.DESCRIPTION
    Voor use op de ontwikkelworkstation. Backup van het lokale .db-bestand en vervolgens
    Migrate() uitvoeren vanuit de projectfolder.

    Let op: dit script slaat het bestand op in dezelfde map als het script zelf.
    Pas $DbPad aan indien nodig.
#>

param(
    [string]$DbPad = "G:\Projecten\Sander Van Praet\SchoolPortfolio\SchoolPortfolio\SchoolPortfolio.db",
    [string]$ProjectMap = "G:\Projecten\Sander Van Praet\SchoolPortfolio\SchoolPortfolio"
)

if (Test-Path $DbPad) {
    $backup = "$DbPad.$((Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')).bak"
    Copy-Item -Path $DbPad -Destination $backup -Force
    Write-Output "Backup: $backup"
}

Push-Location $ProjectMap
try {
    & dotnet ef database update --no-build
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed (exit code $LASTEXITCODE)" }
}
finally {
    Pop-Location
}
