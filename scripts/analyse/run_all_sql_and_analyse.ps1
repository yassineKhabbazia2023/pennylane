# Ce script PowerShell exécute tous les fichiers .sql trouvés dans un dossier sur deux bases différentes, puis lance analyse.ps1

$folder = Join-Path $PSScriptRoot "traitement"  # Chemin relatif au script
$sqlFiles = Get-ChildItem -Path $folder -Filter *.sql -Recurse

# Définir les deux bases à utiliser
$databases = @("daregprd01", "daaccprd01")
$server = "(localdb)\MSSQLLocalDB"

# Création du schéma tmp si nécessaire et suppression des tables temporaires
$dropTablesQuery = @"
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'tmp')
BEGIN
    EXEC('CREATE SCHEMA tmp')
END

IF OBJECT_ID('tmp.Dirigeants', 'U') IS NOT NULL DROP TABLE tmp.Dirigeants;
IF OBJECT_ID('tmp.Entreprises___Dossiers', 'U') IS NOT NULL DROP TABLE tmp.Entreprises___Dossiers;
"@


foreach ($sqlFile in $sqlFiles) {
    # Lire et nettoyer la requête SQL (supprimer les GO)
    $query = Get-Content $sqlFile.FullName -Raw
    $query = $query -replace '(?i)\bGO\b', ''
    foreach ($db in $databases) {
    Write-Host "Suppression des tables temporaires sur $db"
    $connectionString = "Server=$server;Database=$db;Trusted_Connection=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    $command = $connection.CreateCommand()
    $command.CommandText = $dropTablesQuery
    try {
        $connection.Open()
        $command.ExecuteNonQuery() | Out-Null
        $connection.Close()
        Write-Host "Tables temporaires supprimées sur $db"
    } catch {
        Write-Host "ERREUR lors de la suppression des tables sur $db - $_"
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

    foreach ($db in $databases) {
        $sqlFile.Name
        Write-Host "Execution de $($sqlFile.Name) sur $db"
        $connectionString = "Server=$server;Database=$db;Trusted_Connection=True;"
        $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $command.CommandTimeout = 600
        try {
            $connection.Open()
            $command.ExecuteNonQuery() | Out-Null
            $connection.Close()
            Write-Host "OK: $($sqlFile.Name) sur $db"
        } catch {
            Write-Host "ERREUR: $($sqlFile.Name) sur $db - $_"
            if ($connection.State -eq 'Open') { $connection.Close() }
        }
    }
    
    # Construire le nom du fichier Excel d'analyse
    $timestamp = (Get-Date).ToString("_yyyyMMdd")
    $baseName = $sqlFile.Name.Replace(".xlsx.sql", "")
    $excelFileName = "analyse_" + $baseName + $timestamp + ".xlsx"
    $excelFilePath = Join-Path $sqlFile.DirectoryName $excelFileName
    
    # Lancer analyse.ps1 avec le paramètre du fichier Excel
    $analyseScript = Join-Path (Split-Path -Parent $PSCommandPath) "analyse.ps1"
    Write-Host "Execution de analyse.ps1 avec le fichier $excelFileName..."
    & $analyseScript -fichierExcel $excelFilePath
    Write-Host "Terminé.`n"
}


