# Apply SQL migration files to a Postgres connection
param(
  [string]$ConnectionString = $env:TEST_POSTGRES_CONN
)
if (-not $ConnectionString) {
  Write-Error "TEST_POSTGRES_CONN is not set. Set env var to a Postgres connection string before running this script."
  exit 1
}
$scriptDir = Join-Path $PSScriptRoot "..\src\Logistics.Infrastructure\Migrations"
$migrations = @(
  @{ Id = "20260901185816_InitialCreate"; File = "InitialCreate.sql" },
  @{ Id = "20260901191623_AddConstraintsAndIndexes"; File = "AddConstraintsAndIndexes.sql" },
  @{ Id = "20260901191927_AddVoyageCapacitySumCheck"; File = "AddVoyageCapacitySumCheck.sql" },
  @{ Id = "20260903160050_AddBookingConfirmationProjection"; File = "AddBookingConfirmationProjection.sql" },
  @{ Id = "20260903163148_AddActiveHoldUniqueness"; File = "AddActiveHoldUniqueness.sql" }
)
foreach ($migration in $migrations) {
  $f = Join-Path $scriptDir $migration.File
  if (Test-Path $f) {
    $historyTable = psql $ConnectionString -tAc 'SELECT to_regclass(''public.__EFMigrationsHistory'')'
    $historyQuery = "SELECT 1 FROM `"__EFMigrationsHistory`" WHERE `"MigrationId`" = :'migration_id'"
    if ($historyTable.Trim() -and (psql $ConnectionString --set=migration_id=$($migration.Id) -tAc $historyQuery).Trim() -eq "1") {
      Write-Host "Skipping already applied migration $($migration.Id)"
      continue
    }
    Write-Host "Applying $f"
    psql $ConnectionString --set ON_ERROR_STOP=1 -f $f
    if ($LASTEXITCODE -ne 0) {
      throw "Failed to apply migration script: $f"
    }
  } else {
    throw "Migration file not found: $f"
  }
}
