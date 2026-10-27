param(
    [string]$DbPath = "D:\Sites\site100024\wwwroot\SchoolPortfolio.db"
)

$ErrorActionPreference = 'Stop'

if ([System.IO.File]::Exists($DbPath)) {
    Write-Host "DB already exists at $DbPath - skipping bootstrap."
    exit 0
}

Write-Host "Bootstrapping fresh SQLite DB at $DbPath ..."

$provider = "Microsoft.Data.Sqlite.SqliteConnection"
$connStr = "Data Source=$DbPath"

# Use dotnet to run a tiny embedded script that creates schema + seeds.
# Fallback: if you prefer pure SQL, paste the DDL below manually.
Write-Host "NOTE: Replace this stub with the real CREATE TABLE / INSERT statements"
Write-Host "from import-definitief.mjs (or your EF initial migration SQL) before deploying."
Write-Host "Done."
