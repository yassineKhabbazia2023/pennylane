# Ce script PowerShell execute tous les fichiers .sql trouves dans le dossier traitement,
# puis execute export.sql pour extraire les donnees et les sauvegarder dans un fichier CSV

$folder = Join-Path $PSScriptRoot "traitement"  # Chemin relatif au script
$sqlFiles = Get-ChildItem -Path $folder -Filter *.sql -Recurse
$exportSqlPath = Join-Path $PSScriptRoot "export.sql"

# Definir la base de donnees a utiliser
$database = "daregprd01"
$server = "(localdb)\MSSQLLocalDB"

# Verifier que le fichier export.sql existe
if (-not (Test-Path $exportSqlPath)) {
    Write-Host "ERREUR: Le fichier export.sql n'existe pas a l'emplacement: $exportSqlPath"
    exit 1
}

# Lire le contenu de export.sql
$exportQuery = Get-Content $exportSqlPath -Raw

# Creation du schema tmp si necessaire et suppression des tables temporaires
$dropTablesQuery = @"
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'tmp')
BEGIN
    EXEC('CREATE SCHEMA tmp')
END

IF OBJECT_ID('tmp.Dirigeants', 'U') IS NOT NULL DROP TABLE tmp.Dirigeants;
IF OBJECT_ID('tmp.Entreprises___Dossiers', 'U') IS NOT NULL DROP TABLE tmp.Entreprises___Dossiers;
"@

foreach ($sqlFile in $sqlFiles) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Traitement de: $($sqlFile.Name)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    # Lire et nettoyer la requete SQL (supprimer les GO)
    $query = Get-Content $sqlFile.FullName -Raw
    $query = $query -replace '(?i)\bGO\b', ''

    $connectionString = "Server=$server;Database=$database;Trusted_Connection=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString

    try {
        $connection.Open()

        # Etape 1: Suppression des tables temporaires
        Write-Host "1. Suppression des tables temporaires..." -ForegroundColor Yellow
        $command = $connection.CreateCommand()
        $command.CommandText = $dropTablesQuery
        $command.ExecuteNonQuery() | Out-Null
        Write-Host "   OK Tables temporaires supprimees" -ForegroundColor Green

        # Etape 2: Execution du fichier SQL
        Write-Host "2. Execution de $($sqlFile.Name)..." -ForegroundColor Yellow
        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $command.CommandTimeout = 600
        $command.ExecuteNonQuery() | Out-Null
        Write-Host "   OK Fichier SQL execute avec succes" -ForegroundColor Green

        # Etape 3: Execution de export.sql et recuperation des resultats
        Write-Host "3. Extraction des donnees (export.sql)..." -ForegroundColor Yellow
        $command = $connection.CreateCommand()
        $command.CommandText = $exportQuery
        $command.CommandTimeout = 600
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
        $dataTable = New-Object System.Data.DataTable
        $adapter.Fill($dataTable) | Out-Null

        # Etape 4: Generation du fichier CSV
        $csvFileName = $sqlFile.Name -replace '\.sql$', '.csv'
        $csvFilePath = Join-Path $sqlFile.DirectoryName $csvFileName

        Write-Host "4. Generation du fichier CSV: $csvFileName..." -ForegroundColor Yellow

        # Ecrire les donnees dans le fichier CSV
        $csvContent = @()
        foreach ($row in $dataTable.Rows) {
            # Les donnees sont deja formatees avec des ';' par la requete SQL
            $csvContent += $row[0]
        }

        $csvContent | Out-File -FilePath $csvFilePath -Encoding UTF8
        Write-Host "   OK Fichier CSV cree: $csvFilePath" -ForegroundColor Green
        Write-Host "   OK Nombre de lignes: $($csvContent.Count)" -ForegroundColor Green

    } catch {
        Write-Host "ERREUR lors du traitement de $($sqlFile.Name): $_" -ForegroundColor Red
    } finally {
        if ($connection.State -eq 'Open') {
            $connection.Close()
        }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Traitement termine!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
