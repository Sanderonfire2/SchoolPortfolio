# DBInit-Check.ps1
# Quick schema health check for the production SQLite DB.
# Run BEFORE redeploy to confirm the folder is writable and the file exists.
# Expected: DB bestaat, __EFMigrationsHistory tabel aanwezig, schrijfrechten OK.

param(
    [string]$DbPad = "D:\Sites\site100024\wwwroot\SchoolPortfolio.db",
    [string]$WritableCheckDir = "D:\Sites\site100024\wwwroot"
)

$Errors = @()
$Status = @{ DbExists = $false; Tables = 0; MigrationsHistory = $false; CanWrite = $false }

if (Test-Path $DbPad) {
    $Status.DbExists = $true
    try {
        $conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$DbPad;Version=3;")
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'"
        $tables = [int]($cmd.ExecuteScalar())
        $Status.Tables = $tables
        $cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory"
        $hist = [int]($cmd.ExecuteScalar())
        $Status.MigrationsHistory = ($hist -gt 0)
        $conn.Close()
        $conn.Dispose()
    }
    catch {
        $Errors += "SQLite connect/query failed: $_"
    }
}
else {
    $Status.DbExists = $false
    $Status.Tables = 0
}

# Write-access test
$testFile = Join-Path $WritableCheckDir ".write-test-tmp"
try {
    if (-not (Test-Path $testFile)) {
        Set-Content -Path $testFile -Value "" -ErrorAction Stop
        Remove-Item $testFile -Force
        $Status.CanWrite = $true
    }
}
catch {
    $Status.CanWrite = $false
    $Errors += "Schrijftest mislukt: $_"
}

$Status | ConvertTo-Json
if ($Errors.Count -gt 0) {
    Write-Warning "Fouten: $($Errors -join '; ')"
}

if (-not $Status.DbExists) {
    Write-Warning "DB-bestand ontbreekt: $DbPad"
}
if (-not $Status.MigrationsHistory) {
    Write-Warning "__EFMigrationsHistory ontbreekt of is leeg — migraties zijn niet gestart / niet succesvol afgerond."
}
