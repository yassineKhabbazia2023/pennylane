param(
    [string]$targetDate = ""
)

if ([string]::IsNullOrWhiteSpace($targetDate)){
    Write-Host "Vous devez renseigner la date (targetDate) au format MM/dd/yyyy." -ForegroundColor Red
    exit 1
}

# -----------------------------------------------------------------------------
# CONFIG
# -----------------------------------------------------------------------------
# Dossier racine
$rootFolder = Join-Path $PSScriptRoot "traitement"
$expectedSheets = @(
    "Entreprises | Dossiers",
    "MATRICE MIGR PENNYLANE"
)

#En-têtes requises
$requiredHeaders = @(
    "source_id",
    "statut_migration",
    "date_activation"
)

Write-Host "Date cible :" $targetDate

$filterCondition = {
    $val = $_.date_activation
    
    if ([string]::IsNullOrWhiteSpace($val)) { return $false }

    # Extraction de la partie date
    $date = ($val -split ' ')[0]

    return $date -eq $targetDate
}

# -----------------------------------------------------------------------------
# RÉCUPÉRATION DE TOUS LES FICHIERS EXCEL DANS LES SOUS-DOSSIERS
# -----------------------------------------------------------------------------
$excelFiles = Get-ChildItem -Path $rootFolder -Recurse -Filter "*.xlsx"
Write-Host "Fichiers trouvés :" $excelFiles.Count

# -----------------------------------------------------------------------------
# TRAITEMENT DE CHAQUE FICHIER EXCEL
# -----------------------------------------------------------------------------
foreach ($file in $excelFiles) {
    Write-Host "Lecture de : $($file.Name)"

    try {
        # Vérification de la présence de la feuille Excel attendue ("Entreprises | Dossiers" ou "MATRICE MIGR PENNYLANE")
        $excelPackage = Open-ExcelPackage -Path $file.FullName
        $availableSheets = $excelPackage.Workbook.Worksheets.Name

        $sheetName = $expectedSheets |
            Where-Object { $_ -in $availableSheets } |
            Select-Object -First 1

        if (-not $sheetName) {
            throw "Aucune feuille valide trouvée. Attendu : $($expectedSheets -join ', ')"
        }

        # Import de la feuille "entreprise"
        $data = Import-Excel -Path $file.FullName -WorksheetName $sheetName

        # Recherche des entêtes manquantes
        $availableHeaders = $data | Get-Member -MemberType NoteProperty | Select-Object -ExpandProperty Name
        $missingHeaders = $requiredHeaders | Where-Object { $_ -notin $availableHeaders }

        if ($missingHeaders.Count -gt 0) {
            Write-Error "Entêtes manquantes dans $($file.Name) : $($missingHeaders -join ', ')"
            continue
        }

        # Filtrage
        $data = $data | Select-Object -Skip 1
        $filtered = $data | Where-Object $filterCondition
        $filtered = $filtered | Where-Object {
            $_.statut_migration -in @("Activer l'offre Pennylane", "Mettre en echec de migration", "Mettre en échec de migration", "Mettre en echec de mgration")
        }

        Write-Host $filtered.Count "dossiers à traiter"

        # Transformation des données -> format CSV final
        $converted = $filtered | ForEach-Object {
            # Mapping du statut
            $migrationStatus = switch ($_.statut_migration) {
                "Activer l'offre Pennylane"     { "success" }
                "Mettre en echec de migration"  { "failure" }
                "Mettre en echec de mgration"   { "failure" }
                "Mettre en échec de migration"  { "failure" }
            }
            
            [PSCustomObject]@{
                AccountNumber         = $_.source_id
                ClientEmail           = ""
                CollaboratorEmail     = ""
                MissionLeaderEmail    = ""
                AccountingExpertEmail = ""
                OfferName             = "Pennylane"
                MigrationStatus       = $migrationStatus
            }
        }

        # Détermination du chemin de sortie
        $outputCsv = Join-Path $file.DirectoryName (
            ($file.BaseName) + ".csv"
        )

        Remove-Item $outputCsv -ErrorAction SilentlyContinue

        # Export du CSV individuel
        $converted | Export-Csv -Path $outputCsv -Delimiter ";" -NoTypeInformation -Encoding UTF8 -UseQuotes Never

        Write-Host "CSV généré : $outputCsv`n"
    }
    catch {
        Write-Warning "Erreur lors de la lecture du fichier : $($file.FullName)"
        Write-Warning $_.Exception.Message
    }
}
