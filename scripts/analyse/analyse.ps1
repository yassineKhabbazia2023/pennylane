param(
    [string]$fichierExcel = ""
)
# Vérifier si le fichier Excel existe, sinon le créer à partir du template
    $templatePath = Join-Path $PSScriptRoot "traitement\template2.xlsx"
    Write-Host "Création du fichier Excel à partir du template..."
    
    # Créer le dossier parent s'il n'existe pas
    $parentFolder = Split-Path -Parent $fichierExcel
    if (-not (Test-Path $parentFolder)) {
        New-Item -ItemType Directory -Path $parentFolder -Force | Out-Null
    }
    #
    # supprime fichierExcel avant si ça existe
    Remove-Item $fichierExcel -ErrorAction SilentlyContinue
    
    # Copier le template
    Copy-Item -Path $templatePath -Destination $fichierExcel
    Write-Host "Fichier Excel créé : $fichierExcel"

function Export-SQLToExcel {
    param (
        [string]$FichierSQL,
        [string]$FichierExcel,
        [string]$NomFeuille,
        [string]$Database = "Server=(localdb)\MSSQLLocalDB;Database=miniregprd01;Trusted_Connection=True;"
    )
$ChaineConnexion="Server=(localdb)\MSSQLLocalDB;Database=$Database;Trusted_Connection=True;"
    # Lecture de la requête SQL
    $RequêteSQL = Get-Content $FichierSQL -Raw

    # Connexion et execution
    $Connection = New-Object System.Data.SqlClient.SqlConnection
    $Connection.ConnectionString = $ChaineConnexion
    $Connection.Open()

    $Command = $Connection.CreateCommand()
    $Command.CommandText = $RequêteSQL
$Command.CommandTimeout = 600
    $Adapter = New-Object System.Data.SqlClient.SqlDataAdapter $Command
    $Table = New-Object System.Data.DataTable
    $Adapter.Fill($Table)

    $Connection.Close()
    
   # Colonnes à vérifier
$colonnesInitiales = @("name","email", "source_id","accountant_email")

# Ignorer la première ligne (en-têtes)
$tableSansEntete = $Table | Select-Object 

# Déterminer les colonnes non vides
$colonnesNonVides = $colonnesInitiales | Where-Object {
    $colonne = $_
    $tableSansEntete | Where-Object { $_.$colonne -ne $null -and $_.$colonne -ne "" }
} 

# Sélectionner uniquement les colonnes non vides
$tableNettoyee = $tableSansEntete | Select-Object -Property $colonnesNonVides



    # Export vers Excel sans les colonnes techniques
    $tableNettoyee | Export-Excel -Path $FichierExcel -WorksheetName $NomFeuille -AutoSize

    # Ajouter le texte statique avec le nombre de lignes
    $excel = Open-ExcelPackage -Path $FichierExcel
    $ws = $excel.Workbook.Worksheets[$NomFeuille]
    $rowCount = $tableNettoyee | Measure-Object | Select-Object -ExpandProperty Count
    
   $message = switch ($NomFeuille) {
    "Entreprise non existante PL" {
        @"
$rowCount entreprises ne sont pas présentes dans la plateforme RYDGE Conseil

Impact : Ces entreprises ne pourront pas être onboardées sur la plateforme Rydge Conseil
"@
    }
    "Entreprise sans dirigeant" {
        @"
$rowCount entreprises ne possèdent pas de dirigeant dans la mallette

Impact : Ces entreprises ne pourront ni être onboardées sur la plateforme Rydge Conseil ni sur Pennylane puisqu’elles n’ont pas de contact client
"@
    }
    "Client non existant PL" {
        @"
$rowCount contacts clients n'existent pas dans la plateforme RYDGE Conseil

Impact : Ces clients ne seront pas onboardés sur la plateforme RYDGE Conseil pour accéder à Pennylane
"@
    }
    "Client non signataire" {
        @"
$rowCount contacts clients qui ne sont pas signataires

Impact : Si nous onboardons ces contacts clients non-signataires sur la plateforme Rydge Conseil et donc Pennylane, ces contacts auront accès en tant qu’Admin à toutes les données de l'entreprise sur Pennylane --> Très gros risque RGPD + Risk Management
"@
    }
    "Client a valider" {
        @"
$rowCount contacts client doivent être validés dans 'Gérer les accès' par les collaborateurs sur la plateforme RYDGE Conseil.
"@
    }
    default {
        "$rowCount éléments trouvés"
    }
}
    
    # Écrire le message dans la cellule AA1
    $ws.Cells["AA1"].Value = $message
    
    # Sauvegarder et fermer le fichier Excel
    Close-ExcelPackage $excel
}

# Definir les bases de données
$regdatabase="daregprd01"
$accdatabase="daaccprd01"
# Definir les couples SQL/Feuille à executer
$exports = @(
    @{ SQL = "CompanyNotexist.sql"; Feuille = "Entreprise non existante PL";Database=$regdatabase },
    @{ SQL = "SansDirigeant.sql"; Feuille = "Entreprise sans dirigeant" ;Database=$regdatabase},
    @{ SQL = "ClientNotFound.sql"; Feuille = "Client non existant PL" ;Database=$accdatabase},
    @{ SQL = "NonSignataire.sql"; Feuille = "Client non signataire" ;Database=$accdatabase},
    @{ SQL = "ClientAValider.sql"; Feuille = "Client a valider" ;Database=$regdatabase}
)

# Repertoire des fichiers SQL
$repertoireSQL = Join-Path $PSScriptRoot ""

# Boucle d'export
foreach ($item in $exports) {
    $item.SQL
    $fichierSQL = Join-Path $repertoireSQL $item.SQL
    $nomFeuille = $item.Feuille

    Export-SQLToExcel -FichierSQL $fichierSQL -FichierExcel $fichierExcel -NomFeuille $nomFeuille -Database $item.Database
}
