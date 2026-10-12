#Requires -Version 5.1

<#
.SYNOPSIS
    Met a jour l'id des companies Pennylane a partir d'un CSV.

.DESCRIPTION
    Lit un CSV PulseCompanies et effectue un PUT sur /companies/{guid}
    avec l'AccountNumber place dans le champ id.

.PARAMETER ApiKey
    Cle API Pennylane pour l'authentification Bearer.

.PARAMETER InputPath
    Chemin du fichier CSV PulseCompanies-*.csv. Si non fourni, le script prend le plus recent
    dans ..\Extracts.

.PARAMETER ApiUrl
    URL de base de l'API Pennylane (defaut: https://app.pennylane.com/api/firm/v3/).
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [Parameter(Mandatory = $false)]
    [string]$InputPath,

    [Parameter(Mandatory = $false)]
    [string]$ApiUrl = "https://sandbox.pennylane.com/api/firm/v3"
)

Write-Host "=== Update Pennylane Company Ids ===" -ForegroundColor Green
Write-Host ""

if ([string]::IsNullOrWhiteSpace($InputPath)) {
    $defaultExtractsPath = Join-Path -Path $PSScriptRoot -ChildPath "..\\Extracts"
    $latestFile = Get-ChildItem -Path $defaultExtractsPath -Filter "PulseCompanies-*.csv" -File |
        Sort-Object -Property LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $latestFile) {
        throw "No PulseCompanies-*.csv found in $defaultExtractsPath. Provide -InputPath."
    }

    $InputPath = $latestFile.FullName
}

if (-not (Test-Path $InputPath)) {
    throw "Input file not found at $InputPath"
}

Write-Host "Input file: $InputPath" -ForegroundColor Cyan

$companies = Import-Csv -Path $InputPath -Delimiter "," -Encoding UTF8
if (-not $companies -or $companies.Count -eq 0) {
    Write-Warning "No rows found in $InputPath"
    return
}

$headers = @{
    "Authorization" = "Bearer $ApiKey"
    "Content-Type"  = "application/json"
}

$total = $companies.Count
$updated = 0
$skipped = 0
$failed = 0
$failures = New-Object System.Collections.Generic.List[object]

foreach ($row in $companies) {
    $companyId = $row.'id Pennylane actuel'
    $targetId = $row."l'id Pennylane cible"

    if ([string]::IsNullOrWhiteSpace($companyId) -or [string]::IsNullOrWhiteSpace($targetId)) {
        $skipped++
        $failures.Add([PSCustomObject]@{
            CompanyId = $companyId
            TargetId  = $targetId
            Reason    = "Missing company id or target id"
        })
        continue
    }

    $url = "$($ApiUrl.TrimEnd('/'))/companies/$companyId"
    $body = @{ id = "$targetId" } | ConvertTo-Json

    try {
        Write-Host "Mise a jour de la company $companyId..." -ForegroundColor Cyan
        Invoke-RestMethod -Method Put -Uri $url -Headers $headers -Body $body -ErrorAction Stop | Out-Null
        $updated++
    }
    catch {
        $failed++
        $failures.Add([PSCustomObject]@{
            CompanyId = $companyId
            TargetId  = $targetId
            Reason    = $_.Exception.Message
        })
        Write-Error "Erreur lors de la mise a jour de la company '$companyId' avec l'id '$targetId': $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Total rows: $total" -ForegroundColor Cyan
Write-Host "Updated: $updated" -ForegroundColor Green
Write-Host "Skipped: $skipped" -ForegroundColor Yellow
Write-Host "Failed: $failed" -ForegroundColor Red

if ($failures.Count -gt 0) {
    Write-Host "Failures captured in memory for review." -ForegroundColor Yellow
}
