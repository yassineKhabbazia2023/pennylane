# Update-PennylaneCompanyIds

Updates Pennylane company ids using a CSV export produced by the Pulse companies script.

## Prerequisites
- PowerShell 5.1+
- Pennylane API key with permission to update companies
- Input CSV that contains the columns `id Pennylane actuel` and `l'id Pennylane cible`

## Usage
```powershell
cd /path/to/scripts/Tasks
# Uses the latest PulseCompanies-*.csv from ..\Extracts
.\Update-PennylaneCompanyIds.ps1 -ApiKey "<pennylane-api-key>"

# Or specify a file explicitly
.\Update-PennylaneCompanyIds.ps1 -ApiKey "<pennylane-api-key>" -InputPath "C:\\path\\to\\PulseCompanies-20251217-090309.csv"
```

### Parameters
- `ApiKey` (required): Pennylane API key used for Bearer authentication, found in the app settings of pennylane web app in azure under the variable with the name `PennylaneServiceSettings__ApiKey`.
- `InputPath` (optional): Path to the `PulseCompanies-*.csv` file. If omitted, the latest file in `..\Extracts` is used.
- `ApiUrl` (optional): Base Pennylane API URL. Defaults to `https://sandbox.pennylane.com/api/firm/v3/`, found in the app settings of pennylane web app in azure under the variable with the name `PennylaneServiceSettings__ApiUrl` .

## What it does
For each row, the script:
1) Uses `id Pennylane actuel` in the URL: `.../companies/{guid}`
2) Sends the body `{ "id": "<accountNumber>" }` using `l'id Pennylane cible`

The script prints a summary with totals, updated, skipped, and failed counts.
