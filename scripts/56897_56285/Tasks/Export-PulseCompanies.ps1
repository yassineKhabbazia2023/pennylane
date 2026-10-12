#Requires -Version 5.1

<#
.SYNOPSIS
    Exporte les correspondances entre les companies Pennylane et les comptes Pulse.

.DESCRIPTION
    Lit un extract Pennylane (Id, RegNo, Name), interroge la base comptes Pulse avec
    une authentification Azure AD Managed Identity, et genere deux CSV horodates:
    - correspondances trouvees (SIREN, Raison sociale, id Pennylane actuel, id Pennylane cible).
    - lignes sans correspondance.

.PARAMETER ManagedIdentityUserId
    Client ID de l'identite managee a utiliser pour l'authentification Azure AD SQL.

.PARAMETER Mode
    Mode d'execution: TEST ou RELEASE.

.PARAMETER InputPath
    Chemin du fichier d'extract PennylaneCompanies.csv.

.PARAMETER OutputDirectory
    Dossier ou ecrire les CSV generes.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ManagedIdentityUserId,

    [Parameter(Mandatory = $false)]
    [ValidateSet("TEST", "RELEASE")]
    [string]$Mode = "RELEASE",

    [Parameter(Mandatory = $false)]
    [string]$InputPath = (Join-Path -Path $PSScriptRoot -ChildPath "..\\Extracts\\PennylaneCompanies.csv"),

    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = (Join-Path -Path $PSScriptRoot -ChildPath "..\\Extracts")
)

# Aligne avec les scripts de reference: preparation des chemins et de la connexion SQL
if (-not $PSScriptRoot) {
    $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

# Chemin des donnees de test
$dataSourcePath = Join-Path -Path $PSScriptRoot -ChildPath "..\\DataSource\\accounts.csv"

# Connexion SQL construite comme dans les scripts de reference
$connectionString = "Server=tcp:sqlcegpulseaccitg0101.database.windows.net,1433;" + `
    "Initial Catalog=sqldbcegpulseaccitg0101;" + `
    "Encrypt=True;" + `
    "TrustServerCertificate=False;" + `
    "Connection Timeout=30;" + `
    "Authentication=""Active Directory Default"";"

$resolvedInputPath = [System.IO.Path]::GetFullPath($InputPath)
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

Write-Host "=== Export Pulse Companies ===" -ForegroundColor Green
Write-Host ""

if (-not (Test-Path $resolvedInputPath)) {
    throw "Input file not found at $resolvedInputPath"
}

if (-not (Test-Path $resolvedOutputDirectory)) {
    Write-Host "Output directory not found. Creating $resolvedOutputDirectory" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputFile = Join-Path -Path $resolvedOutputDirectory -ChildPath "PulseCompanies-$timestamp.csv"
$notFoundFile = Join-Path -Path $resolvedOutputDirectory -ChildPath "PulseCompaniesNotFound-$timestamp.csv"

$companies = Import-Csv -Path $resolvedInputPath -Delimiter "," -Encoding UTF8
if (-not $companies -or $companies.Count -eq 0) {
    Write-Warning "No rows found in $resolvedInputPath"
    return
}

$foundCount = 0
$notFoundCount = 0
$processed = 0
$total = $companies.Count

$foundHeaders = @("SIREN", "Raison sociale", "id Pennylane actuel", "l'id Pennylane cible")
$notFoundHeaders = @("Id", "RegNo", "Name")
$foundHeaderLine = '"' + ($foundHeaders -join '","') + '"'
$notFoundHeaderLine = '"' + ($notFoundHeaders -join '","') + '"'

Set-Content -Path $outputFile -Value $foundHeaderLine -Encoding UTF8
Set-Content -Path $notFoundFile -Value $notFoundHeaderLine -Encoding UTF8

if ($Mode -eq "RELEASE") {
    Write-Host "Connecting to accounts database..." -ForegroundColor Yellow

    # Verifie la presence d'Invoke-SqlCmd comme dans les scripts de reference
    if (-not (Get-Command -Name Invoke-SqlCmd -ErrorAction SilentlyContinue)) {
        throw "Invoke-SqlCmd is required. Install/import the SqlServer module."
    }
}
else {
    Write-Host "Running in TEST mode (local accounts.csv)..." -ForegroundColor Yellow
    if (-not (Test-Path $dataSourcePath)) {
        throw "Data source file not found at $dataSourcePath"
    }
    $accounts = Import-Csv -Path $dataSourcePath -Delimiter "," -Encoding UTF8
}

foreach ($company in $companies) {
    $processed++
    $percent = [int](($processed / $total) * 100)
    Write-Progress -Activity "Processing companies" -Status "$processed / $total" -PercentComplete $percent

    # Echappe les quotes pour conserver le style de construction de requete des scripts de reference
    $safeRegNo = ($company.RegNo).Replace("'", "''")
    $safeName = ($company.Name).Replace("'", "''")

    if ($Mode -eq "RELEASE") {
        $query = @"
SELECT [AccountId], [AccountNumber], [LegalName], [Siret]
FROM [account].[Account]
WHERE Siret LIKE '$safeRegNo%' AND LegalName = '$safeName';
"@

        try {
            $result = Invoke-SqlCmd -ConnectionString $connectionString -Query $query -ErrorAction Stop
        }
        catch {
            Write-Error "Failed during SQL execution for RegNo '$($company.RegNo)': $_"
            throw
        }

        if ($result -and $result.Count -gt 0) {
            foreach ($row in $result) {
                [PSCustomObject]@{
                    "SIREN" = $company.RegNo
                    "Raison sociale" = $row.LegalName
                    "id Pennylane actuel" = $company.Id
                    "l'id Pennylane cible" = $row.AccountNumber
                } | Export-Csv -Path $outputFile -NoTypeInformation -Append -Encoding UTF8
                $foundCount++
            }
        }
        else {
            [PSCustomObject]@{
                "Id" = $company.Id
                "RegNo" = $company.RegNo
                "Name" = $company.Name
            } | Export-Csv -Path $notFoundFile -NoTypeInformation -Append -Encoding UTF8
            $notFoundCount++
        }
    }
    else {
        $matches = $accounts |
            Where-Object { $_.Siret -like "$($company.RegNo)*" -and $_.LegalName -eq $company.Name }

        if ($matches) {
            foreach ($match in $matches) {
                [PSCustomObject]@{
                    "SIREN" = $company.RegNo
                    "Raison sociale" = $match.LegalName
                    "id Pennylane actuel" = $company.Id
                    "l'id Pennylane cible" = $match.AccountNumber
                } | Export-Csv -Path $outputFile -NoTypeInformation -Append -Encoding UTF8
                $foundCount++
            }
        }
        else {
            [PSCustomObject]@{
                "Id" = $company.Id
                "RegNo" = $company.RegNo
                "Name" = $company.Name
            } | Export-Csv -Path $notFoundFile -NoTypeInformation -Append -Encoding UTF8
            $notFoundCount++
        }
    }

    if ($processed % 25 -eq 0 -or $processed -eq $total) {
        Write-Host "Processed $processed/$total | Found: $foundCount | Not found: $notFoundCount" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Accounts extracted: $foundCount" -ForegroundColor Green
Write-Host "Not found: $notFoundCount" -ForegroundColor Yellow
Write-Host "Found file: $outputFile" -ForegroundColor Cyan
Write-Host "Not found file: $notFoundFile" -ForegroundColor Cyan
