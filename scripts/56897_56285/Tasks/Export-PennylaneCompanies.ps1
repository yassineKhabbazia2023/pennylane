#Requires -Version 5.1

<#
.SYNOPSIS
    Exporte les companies Pennylane dont l'ID est un GUID vers un fichier CSV.

.DESCRIPTION
    Ce script recupere toutes les companies depuis l'API Pennylane,
    filtre celles dont l'ID est un GUID valide, et exporte leurs informations
    (Id, RegNo, Name) dans un fichier CSV.

.PARAMETER ApiUrl
    URL de base de l'API Pennylane (defaut: https://sandbox.pennylane.com/api/firm/v3/)

.PARAMETER ApiKey
    Cle API pour l'authentification Bearer

.PARAMETER OutputPath
    Chemin du fichier CSV de sortie (defaut: PennylaneCompanies.csv)

.EXAMPLE
    .\Export-PennylaneCompanies.ps1 -ApiKey "votre_api_key"

.EXAMPLE
    .\Export-PennylaneCompanies.ps1 -ApiKey "votre_api_key" -OutputPath "C:\exports\companies.csv"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ApiUrl = "https://sandbox.pennylane.com/api/firm/v3/",
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "PennylaneCompanies.csv"
)

# Pattern pour valider un GUID
$guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"

function Test-IsGuid {
    param([string]$Value)
    return $Value -match $guidPattern
}

function Get-PennylaneCompanies {
    param(
        [string]$BaseUrl,
        [string]$Token
    )

    $allCompanies = @()
    $currentPage = 1

    $headers = @{
        "Authorization" = "Bearer $Token"
        "Content-Type"  = "application/json"
    }

    do {
        $url = "$($BaseUrl.TrimEnd('/'))/" + "companies?page=$currentPage&per_page=500"

        Write-Host "Recuperation de la page $currentPage..." -ForegroundColor Cyan

        try {
            $response = Invoke-RestMethod -Uri $url -Headers $headers -Method Get -ErrorAction Stop

            $pageCompanies = @()

            if ($response.companies) {
                $pageCompanies = $response.companies
            }
            elseif ($response -is [array]) {
                $pageCompanies = $response
            }

            # Si la liste est vide, on a atteint la fin
            if ($pageCompanies.Count -eq 0) {
                Write-Host "Liste vide recue, fin de la pagination." -ForegroundColor Yellow
                break
            }

            $allCompanies += $pageCompanies
            Write-Host "  -> $($pageCompanies.Count) companies recuperees (total: $($allCompanies.Count))" -ForegroundColor Gray

            $currentPage++
        }
        catch {
            Write-Error "Erreur lors de la recuperation des companies: $_"
            throw
        }
    } while ($true)

    return $allCompanies
}

# Script principal
Write-Host "=== Export des Companies Pennylane ===" -ForegroundColor Green
Write-Host ""

try {
    # Recuperation de toutes les companies
    Write-Host "Connexion a l'API Pennylane..." -ForegroundColor Yellow
    $companies = Get-PennylaneCompanies -BaseUrl $ApiUrl -Token $ApiKey

    Write-Host "Total companies recuperees: $($companies.Count)" -ForegroundColor Green

    # Filtrage des companies avec un ID GUID
    $guidCompanies = @()

    foreach ($company in $companies) {
        $companyId = $company.id

        if (Test-IsGuid -Value $companyId) {
            $guidCompanies += [PSCustomObject]@{
                Id        = $company.id
                RegNo     = $company.reg_no
                Name      = $company.name
                TradeName = $company.trade_name
            }
        }
    }

    Write-Host "Companies avec ID GUID: $($guidCompanies.Count)" -ForegroundColor Green

    if ($guidCompanies.Count -gt 0) {
        # Export vers CSV
        $guidCompanies | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8 -Delimiter ";"
        Write-Host ""
        Write-Host "Export termine avec succes!" -ForegroundColor Green
        Write-Host "Fichier: $OutputPath" -ForegroundColor Cyan
        Write-Host "Nombre d'enregistrements: $($guidCompanies.Count)" -ForegroundColor Cyan
    }
    else {
        Write-Host ""
        Write-Warning "Aucune company avec un ID GUID n'a ete trouvee."
    }
}
catch {
    Write-Error "Une erreur s'est produite: $_"
    exit 1
}
