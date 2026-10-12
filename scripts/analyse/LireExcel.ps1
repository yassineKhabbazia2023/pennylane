function Import-MultiSheetExcel {
    param (
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    if (-not (Test-Path $FilePath)) {
        throw "Fichier introuvable: $FilePath"
    }

    $sheetsData = @{}
    $sheets = Get-ExcelSheetInfo -Path $FilePath 
    foreach ($sheet in $sheets.Name) {
        if ($sheet -eq "Entreprises | Dossiers" -or $sheet -eq "Utilisateurs Dirigeants") {
            $data = Import-Excel -Path $FilePath -WorksheetName $sheet
            $sheetsData[$sheet] = $data
        }
    }
    return $sheetsData
}

# Dossier racine
$rootFolder = Join-Path $PSScriptRoot "traitement"

# Fichiers Excel
$xlsxFiles = Get-ChildItem -Path $rootFolder -Recurse -Filter *.xlsx |
    Where-Object {
        -not $_.PSIsContainer -and
        ($_.DirectoryName -ne $rootFolder)
    }

# Fichiers CSV
$csvFiles = Get-ChildItem -Path $rootFolder -Recurse -Filter *.csv |
    Where-Object {
        -not $_.PSIsContainer -and
        ($_.DirectoryName -ne $rootFolder)
    }

# Traitement des fichiers Excel
foreach ($file in $xlsxFiles) {
    $fichierExcel = $file.FullName
    $outputSqlFile = "$fichierExcel.sql"
    Remove-Item $outputSqlFile -ErrorAction SilentlyContinue

    $resultats = Import-MultiSheetExcel -FilePath $fichierExcel
    $filteredSheets = $resultats.Keys | Where-Object {
        $_ -notmatch 'Actionnaires|Instructions|mapping|raw_|Utilisateurs Comptables'
    }

    foreach ($sheet in $filteredSheets) {
        if ($sheet.Contains('entreprises')) {
            $tableName = 'company'
        } elseif ($sheet.Contains('dirigeants')) {
            $tableName = 'dirigeants'
        }
         elseif ($sheet.Contains('Dirigeants')) {
            $tableName = 'dirigeants'
        }
         elseif ($sheet.Contains('comptables')) {
            $tableName = 'collaborateurs'
        } else {
            $tableName = $sheet -replace '[^\w]', '_'
        }

        $rows = $resultats[$sheet] | Select-Object -Skip 1
        if ($rows.Count -eq 0) { continue }

        $columns = $resultats[$sheet][0].PSObject.Properties.Name
        $columnDefs = $columns | ForEach-Object { "[$_] NVARCHAR(MAX)" }
        $columnsSql = $columnDefs -join ", "

        $createSql = "IF OBJECT_ID(N'tmp.$tableName') IS NOT NULL DROP TABLE tmp.$tableName;
CREATE TABLE tmp.$tableName (
    $columnsSql
);
"
# filter on rows to return only where column lot have value Lot1
$rows= $rows | Where-Object { $_.lot -eq 'Lot1' -or $_.lot -eq 'Lot 1' -or $_.lot -eq 'Lot_1' -or $_.lot -eq 'lot1' -or $_.lot -eq 'lot 1' -or $_.lot -eq 'lot_1' -or $_.lot -eq 'LOT1' -or $_.lot -eq 'LOT 1' -or $_.lot -eq 'LOT_1' -or $_.lot -eq '1' }
        $insertSql = ""
        foreach ($row in $rows) {
            $values = @()
            foreach ($col in $columns) {
                $val = $row.$col -replace "'", "''"
                $values += "'$val'"
            }
            $line = "INSERT INTO tmp.$tableName ([{0}]) VALUES ({1});" -f ($columns -join "], ["), ($values -join ", ")
            $insertSql += $line + "`n"
        }

        Add-Content -Path $outputSqlFile -Value $createSql
        Add-Content -Path $outputSqlFile -Value $insertSql
        Add-Content -Path $outputSqlFile -Value "`nGO`n"
    }

    Write-Host "✅ Script SQL généré : $outputSqlFile"
}
# Traitement des fichiers CSV
foreach ($file in $csvFiles) {
    $file
    $csvPath = $file.FullName
    $outputSqlFile = "$csvPath.sql"
    Remove-Item $outputSqlFile -ErrorAction SilentlyContinue

    # Lecture avec encodage UTF8 explicite
    $rawContent = Get-Content -Path $csvPath -Encoding UTF8
    $data = $rawContent | ConvertFrom-Csv

    if ($data.Count -eq 0) { continue }

    $tableName = $file.BaseName -replace '[^\w]', '_'

    $columns = $data[0].PSObject.Properties.Name
    $columnDefs = $columns | ForEach-Object { "[$_] NVARCHAR(MAX)" }
    $columnsSql = $columnDefs -join ", "

    $createSql = "IF OBJECT_ID(N'tmp.$tableName') IS NOT NULL DROP TABLE tmp.$tableName;
CREATE TABLE tmp.$tableName (
    $columnsSql
);
"
    $columns

    $insertSql = ""
$stream = [System.IO.StreamWriter]::new($outputSqlFile, $false, [System.Text.Encoding]::UTF8)

foreach ($row in $data) {
    $values = foreach ($col in $columns) {
        $val = $row.$col -replace "'", "''"
        "'$val'"
    }

    $line = "INSERT INTO tmp.$tableName ([{0}]) VALUES ({1});" -f ($columns -join "], ["), ($values -join ", ")
    $stream.WriteLine($line)
}

$stream.WriteLine("GO")
$stream.Close()
    Add-Content -Path $outputSqlFile -Value $createSql
    Add-Content -Path $outputSqlFile -Value $insertSql
    Add-Content -Path $outputSqlFile -Value "`nGO`n"

    Write-Host "📄 Script SQL généré pour CSV : $outputSqlFile"
}