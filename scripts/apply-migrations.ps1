# Apply SQL migration files to a Postgres connection
param(
  [string]$ConnectionString = $env:TEST_POSTGRES_CONN
)
if (-not $ConnectionString) {
  Write-Error "TEST_POSTGRES_CONN is not set. Set env var to a Postgres connection string before running this script."
  exit 1
}
$scriptDir = Join-Path $PSScriptRoot "..\src\Logistics.Infrastructure\Migrations"
$files = @("InitialCreate.sql", "AddConstraintsAndIndexes.sql", "AddVoyageCapacitySumCheck.sql") | ForEach-Object { Join-Path $scriptDir $_ }
foreach ($f in $files) {
  if (Test-Path $f) {
    Write-Host "Applying $f"
    psql $ConnectionString -f $f
  } else {
    Write-Host "Migration file not found: $f"
  }
}
