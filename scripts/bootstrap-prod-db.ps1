$ErrorActionPreference = "Stop"
$PSDefaultParameterValues['*:ErrorAction'] = "Stop"

<#
.SYNOPSIS
    Bootstrapt de productiedatabase: backup + EF Migrate().

.DESCRIPTION
    Dit script is bedoeld voor éénmalig gebruik op de IIS-server (bijv. via RDP of een
    beheer-agent) vóór de eerste aanvraag na een nieuwe deploy zonder bestaande DB.

    Het vereist dat dotnet met ASP.NET Core 10.0 (of hoger) beschikbaar is in de Path
    van de run-account, en dat de app-folder de volledige publish-output bevat met het
    projectbestand SchoolPortfolio.csproj.

    Standaard pad: de folder die het script zelf bevat + "\..\..\wwwroot" t.o.v. projectmap.
    Aanpassen via $DbPad en $ProjectMap.
#>

param(
    [string]$DbPad = "D:\Sites\site100024\wwwroot\SchoolPortfolio.db",
    [string]$ProjectMap = "D:\Sites\site100024\wwwroot\SchoolPortfolio"
)

if (-not (Test-Path $DbPad)) {
    Write-Output "Geen bestaande DB, zal een lege aanmaken via EF."
}
else {
    $backup = "$DbPad.$((Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')).bak"
    Copy-Item -Path $DbPad -Destination $backup -Force
    Write-Output "Backup gemaakt: $backup"
}

Push-Location $ProjectMap
try {
    & dotnet ef database update --no-build
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed (exit code $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

# Controleer of de __EFMigrationsHistory tabel nu aanwezig is
if (Test-Path $DbPad) {
    try {
        $conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$DbPad;Version=3;")
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory"
        $count = [int]($cmd.ExecuteScalar())
        Write-Output "__EFMigrationsHistory telt $count toegepaste migraties."
        $conn.Close()
        $conn.Dispose()
    }
    catch {
        Write-Warning "Controle __EFMigrationsHistory mislukt: $_"
    }
}
