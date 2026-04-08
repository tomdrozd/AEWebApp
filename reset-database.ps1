# Reset Database Script for Activity Explorer
# This script will drop and recreate the database with the new schema

Write-Host "Activity Explorer Database Reset Script" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Database name
$databaseName = "ActivityExplorer"
$connectionString = "Server=(localdb)\mssqllocaldb;Database=$databaseName;Trusted_Connection=True;"

Write-Host "This script will:" -ForegroundColor Yellow
Write-Host "1. Drop the existing database: $databaseName" -ForegroundColor Yellow
Write-Host "2. The database will be recreated with the new schema when you run the application" -ForegroundColor Yellow
Write-Host ""

$confirmation = Read-Host "Are you sure you want to reset the database? All existing data will be lost! (yes/no)"

if ($confirmation -ne "yes") {
    Write-Host "Database reset cancelled." -ForegroundColor Red
    exit
}

try {
    # Use SqlCmd to drop the database
    Write-Host "Dropping database $databaseName..." -ForegroundColor Yellow
    
    $dropScript = @"
USE master;
GO
IF EXISTS (SELECT name FROM sys.databases WHERE name = '$databaseName')
BEGIN
    ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$databaseName];
END
GO
"@

    # Write the script to a temp file
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    Set-Content -Path $tempFile -Value $dropScript
    
    # Execute using sqlcmd
    sqlcmd -S "(localdb)\mssqllocaldb" -i $tempFile -E
    
    # Clean up temp file
    Remove-Item $tempFile -Force
    
    Write-Host "Database dropped successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Run the backend application: cd backend\ActivityExplorer.API && dotnet run" -ForegroundColor White
    Write-Host "2. The database will be automatically created with the new schema" -ForegroundColor White
    Write-Host "3. Use the Sync button in the UI or call POST /api/activities/sync to fetch new data" -ForegroundColor White
    
} catch {
    Write-Host "Error dropping database: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative manual steps:" -ForegroundColor Yellow
    Write-Host "1. Open SQL Server Management Studio or Azure Data Studio" -ForegroundColor White
    Write-Host "2. Connect to: (localdb)\mssqllocaldb" -ForegroundColor White
    Write-Host "3. Right-click on database 'ActivityExplorerDB' and select 'Delete'" -ForegroundColor White
    Write-Host "4. Check 'Close existing connections' and click OK" -ForegroundColor White
}