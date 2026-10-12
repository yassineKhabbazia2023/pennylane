# Export-PulseCompanies

Automates the lookup of Pennylane companies in the Pulse accounts database and produces two timestamped CSV exports.

## Prerequisites
- PowerShell 5.1+
- Input extract `PennylaneCompanies.csv` available (default location: `..\Extracts\PennylaneCompanies.csv` relative to the script)
- For RELEASE mode: access to Azure SQL `sqldbcegpulseaccitg0101` using `Authentication="Active Directory Default"`
- For TEST mode: `..\DataSource\accounts.csv` exists (sample file provided)

## Usage
```powershell
cd /path/to/scripts/Tasks
# Release mode (default)
.\Export-PulseCompanies.ps1 -Mode RELEASE -ManagedIdentityUserId "<client-id>"

# Test mode (local CSV as accounts source)
.\Export-PulseCompanies.ps1 -Mode TEST

# Optional overrides
.\Export-PulseCompanies.ps1 -Mode TEST -InputPath "C:\\temp\\PennylaneCompanies.csv" -OutputDirectory "C:\\temp\\exports"
```

### Parameters
- `Mode` (optional): `TEST` or `RELEASE`. Defaults to `RELEASE`.
- `ManagedIdentityUserId` (required in RELEASE): Client ID used for Azure SQL authentication; not used in TEST mode.
- `InputPath` (optional): Path to the Pennylane extract CSV. Defaults to `..\Extracts\PennylaneCompanies.csv` relative to the script.
- `OutputDirectory` (optional): Folder where results are written. Defaults to `..\Extracts` relative to the script.

## Steps to use the script and obtain the extract
Use a real extract from the accounts database to build `accounts.csv` for TEST mode:
1) Connect to the accounts database and run:
```sql
SELECT  [AccountId]
      ,[AccountNumber]
      ,[LegalName]
      ,[Siret]
FROM [account].[Account]
WHERE IsActive = 1
```
2) Export the result as CSV with headers (in SSMS: `Ctrl + A`, then `Click droite` then `Save Results as`).
3) Paste into a file named `accounts.csv` under `..\DataSource\`.
4) Run the script in TEST mode:
```powershell
cd /path/to/scripts/Tasks
.\Export-PulseCompanies.ps1 -Mode TEST
```

## Outputs
- `PulseCompanies-<timestamp>.csv`: Matches found with columns `SIREN`, `Raison sociale`, `id Pennylane actuel`, `l'id Pennylane cible (AccountNumber)`.
- `PulseCompaniesNotFound-<timestamp>.csv`: Rows not found with columns `Id`, `RegNo`, `Name`.

Each run writes new files (timestamped `yyyyMMdd-HHmmss`). The script prints the number of accounts extracted and the locations of both CSVs.
