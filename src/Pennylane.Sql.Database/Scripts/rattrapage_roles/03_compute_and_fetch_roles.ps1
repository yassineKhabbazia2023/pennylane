<#
.SYNOPSIS
    SCRIPT 3 - Calcul du diff + recuperation des roles Pennylane + generation SQL.

.DESCRIPTION
    Ticket : #60787 - Rattraper les roles
    Auteur  : RYDGE Conseil
    Date    : 2026-05-20

    Ce script fusionne les etapes 3 et 4 :
      1. Lit authorized_users.csv  (Script SQL 1 : ContactAuthorization WHERE AuthorizationId=15)
      2. Lit existing_roles.csv    (Script SQL 2 : Pennylane.Roles)
      3. Calcule le diff en memoire → contacts autorises SANS role
      4. Appelle l API Pennylane pour chaque compte pour recuperer le role
      5. Genere des fichiers SQL decoupes en batches prets a executer dans SSMS

    ROBUSTESSE :
      - Retry automatique avec backoff exponentiel (3 tentatives par appel)
      - Gestion HTTP 429 avec respect du Retry-After header
      - Checkpoint / reprise : si le script est interrompu, il repart ou il s est arrete
      - Ecriture SQL en streaming (pas de stockage memoire des 33K lignes)
      - Decoupage SQL en batches (defaut 500 lignes) pour eviter les timeouts SSMS
      - Log persistant horodate de toutes les operations
      - Barre de progression avec estimation du temps restant

.PARAMETER AuthorizedCsvPath
    Chemin vers authorized_users.csv (produit par 01_extract_authorized_users.sql).
    Defaut : .\authorized_users.csv

.PARAMETER ExistingRolesCsvPath
    Chemin vers existing_roles.csv (produit par 02_extract_existing_roles.sql).
    Defaut : .\existing_roles.csv

.PARAMETER OutputFolder
    Dossier de sortie pour les fichiers SQL generes.
    Defaut : .\sql_output

.PARAMETER ApiBaseUrl
    URL de base de l API Pennylane (sans slash final).
    Exemple : https://app.pennylane.com/api/firm/v3

.PARAMETER BearerToken
    Token Bearer pour appeler l API Pennylane.

.PARAMETER DelayMs
    Delai en millisecondes entre chaque appel API.
    Defaut : 300

.PARAMETER SqlBatchSize
    Nombre maximum de lignes par fichier SQL genere.
    Defaut : 500

.PARAMETER MaxRetries
    Nombre maximum de tentatives par appel API.
    Defaut : 3

.PARAMETER TimeoutSec
    Timeout en secondes pour chaque appel HTTP.
    Defaut : 30

.PARAMETER ResumeFromCheckpoint
    Si present, reprend depuis le checkpoint existant.

.PARAMETER DiffCsvPath
    Chemin du fichier diff intermediaire (toujours ecrit sur disque).
    Permet la reprise sans recalculer le diff ni relancer les scripts SQL.
    Defaut : .\sql_output\users_without_role.csv

.EXAMPLE
    # Premiere execution
    .\03_compute_and_fetch_roles.ps1 `
        -AuthorizedCsvPath    .\authorized_users.csv `
        -ExistingRolesCsvPath .\existing_roles.csv `
        -OutputFolder         .\sql_output `
        -ApiBaseUrl           "https://app.pennylane.com/api/firm/v3" `
        -BearerToken          "eyJhbGciOiJSUzI1NiJ9..." `
        -DelayMs              300

    # Reprise apres interruption
    .\03_compute_and_fetch_roles.ps1 `
        -AuthorizedCsvPath    .\authorized_users.csv `
        -ExistingRolesCsvPath .\existing_roles.csv `
        -OutputFolder         .\sql_output `
        -ApiBaseUrl           "https://app.pennylane.com/api/firm/v3" `
        -BearerToken          "eyJhbGciOiJSUzI1NiJ9..." `
        -ResumeFromCheckpoint
    # Note : si sql_output\users_without_role.csv existe deja, le diff n est
    #        pas recalcule (les CSV SQL n ont pas besoin d etre re-exportes).
#>

param(
    [string] $AuthorizedCsvPath    = ".\authorized_users.csv",
    [string] $ExistingRolesCsvPath = ".\existing_roles.csv",
    [string] $OutputFolder         = ".\sql_output",
    [string] $DiffCsvPath          = "",           # vide = auto : sql_output\users_without_role.csv
    [string] $ApiBaseUrl           = "https://app.pennylane.com/api/firm/v3",
    [string] $BearerToken,
    [int]    $DelayMs              = 300,
    [int]    $SqlBatchSize         = 500,
    [int]    $MaxRetries           = 3,
    [int]    $TimeoutSec           = 30,
    [switch] $ResumeFromCheckpoint
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Forcer l encodage UTF-8 dans la console Windows
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding           = [System.Text.Encoding]::UTF8

# Resoudre les chemins en absolu (evite le decalage PWD/.NET avec -File)
$AuthorizedCsvPath    = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($AuthorizedCsvPath)
$ExistingRolesCsvPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ExistingRolesCsvPath)
$OutputFolder         = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputFolder)

# Chemin du diff : toujours dans le dossier de sortie pour cohérence
if (-not $DiffCsvPath) {
    $DiffCsvPath = Join-Path $OutputFolder "users_without_role.csv"
}
else {
    $DiffCsvPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DiffCsvPath)
}

# ---------------------------------------------------------------------------
# Validation des parametres obligatoires
# ---------------------------------------------------------------------------
if (-not $BearerToken) {
    throw "Le parametre -BearerToken est obligatoire."
}

# Creer le dossier de sortie si inexistant
if (-not (Test-Path $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder | Out-Null
}

$CheckpointPath = Join-Path $OutputFolder "checkpoint.json"
$LogPath        = Join-Path $OutputFolder "fetch_log_$([datetime]::Now.ToString('yyyyMMdd_HHmmss')).txt"

# ---------------------------------------------------------------------------
# Fonctions utilitaires
# ---------------------------------------------------------------------------
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $line = "[$([datetime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))] [$Level] $Message"
    Write-Host $line
    Add-Content -Path $LogPath -Value $line -Encoding UTF8
}

function Import-CsvNormalized {
    <#
    .SYNOPSIS
        Import-Csv robuste : gere BOM, espaces, TAB/virgule ET l absence de ligne d en-tete.
        Si le 1er champ de la 1ere ligne est numerique → pas de header → utilise FallbackHeaders.
    #>
    param(
        [string]   $Path,
        [string[]] $FallbackHeaders = @()   # utilise si fichier sans header (1ere ligne = donnees)
    )

    $lines = Get-Content -Path $Path -Encoding UTF8
    if ($lines.Count -eq 0) { return @() }

    # Suppression BOM par cast direct (regex \uFEFF non fiable dans toutes les versions PS)
    $firstLine = $lines[0].TrimStart([char]0xFEFF).Trim()

    # Detection du separateur
    $delimiter = if ($firstLine -match '\t') { "`t" } else { ',' }

    # Valeurs de la 1ere ligne
    $firstValues = ($firstLine -split [regex]::Escape($delimiter)) |
                   ForEach-Object { $_.Trim().Trim('"').Trim() }

    # Detecter si la 1ere ligne est un header ou des donnees
    # Heuristique : si le 1er champ est purement numerique → c est une donnee (pas un nom de colonne)
    $firstFieldIsData = ($firstValues[0] -match '^\d+$') -or ($firstValues[0] -match '^-?\d+(\.\d+)?$')

    if ($firstFieldIsData) {
        if ($FallbackHeaders.Count -eq 0) {
            throw "Le fichier '$Path' n a pas de ligne d en-tete et aucun FallbackHeaders n a ete fourni.`nPremiere ligne detectee : $firstLine`n→ Cocher 'Include column headers' dans SSMS avant d exporter."
        }
        $headers   = $FallbackHeaders
        $dataLines = $lines | Where-Object { $_.Trim() -ne '' }
        Write-Log "      [CSV] $(Split-Path $Path -Leaf) : PAS DE HEADER | headers injectes : $($headers -join ', ') | $($dataLines.Count) lignes" "WARN"
    }
    else {
        $headers   = $firstValues
        $dataLines = $lines[1..($lines.Count - 1)] | Where-Object { $_.Trim() -ne '' }
        Write-Log "      [CSV] $(Split-Path $Path -Leaf) | Sep:$(if($delimiter -eq "`t"){'TAB'}else{'VIRGULE'}) | Colonnes : $($headers -join ', ') | $($dataLines.Count) lignes"
    }

    if ($dataLines.Count -eq 0) { return @() }

    return ($dataLines | ConvertFrom-Csv -Header $headers -Delimiter $delimiter)
}

function Invoke-PennylaneApi {
    param(
        [string]    $Url,
        [hashtable] $Headers,
        [int]       $MaxRetries,
        [int]       $TimeoutSec,
        [string]    $AccountNumber
    )
    $attempt    = 0
    $backoffSec = 2
    while ($attempt -lt $MaxRetries) {
        $attempt++
        try {
            return Invoke-RestMethod -Uri $Url -Headers $Headers -Method Get `
                -TimeoutSec $TimeoutSec -ErrorAction Stop
        }
        catch {
            if ($_.Exception.Response) {
                $statusCode = [int]$_.Exception.Response.StatusCode
                if ($statusCode -eq 429) {
                    $retryAfterHeader = $_.Exception.Response.Headers["Retry-After"]
                    $retryAfterSec    = if ($retryAfterHeader) { [int]$retryAfterHeader } else { 60 }
                    Write-Log "HTTP 429 sur $AccountNumber - attente $retryAfterSec s (tentative $attempt/$MaxRetries)" "WARN"
                    Start-Sleep -Seconds $retryAfterSec
                    continue
                }
                if ($statusCode -eq 401 -or $statusCode -eq 403) {
                    Write-Log "HTTP $statusCode sur $AccountNumber - Token invalide ou expire. Arret." "ERROR"
                    throw
                }
                if ($statusCode -eq 404) {
                    Write-Log "HTTP 404 sur $AccountNumber - compte non trouve dans Pennylane." "WARN"
                    return "NOT_FOUND"   # sentinel distinct de $null (= erreur reseau)
                }
            }
            if ($attempt -ge $MaxRetries) {
                Write-Log "Echec definitif sur $AccountNumber apres $MaxRetries tentatives : $_" "ERROR"
                return $null
            }
            Write-Log "Erreur sur $AccountNumber (tentative $attempt/$MaxRetries) : $_ - retry dans $backoffSec s" "WARN"
            Start-Sleep -Seconds $backoffSec
            $backoffSec = $backoffSec * 2
        }
    }
    return $null
}

function Open-SqlBatch {
    param([string]$FilePath, [int]$BatchNumber)
    if (Test-Path $FilePath) {
        try { Remove-Item $FilePath -Force -ErrorAction Stop }
        catch { throw "Impossible de supprimer '$FilePath' : fichier verrouille. Fermer dans SSMS puis relancer." }
    }
    $writer = [System.IO.StreamWriter]::new($FilePath, $false, [System.Text.Encoding]::UTF8)
    $writer.WriteLine("-- ============================================================")
    $writer.WriteLine("-- Mise a jour des roles Pennylane - Batch $BatchNumber")
    $writer.WriteLine("-- Genere le : $([datetime]::Now.ToString('yyyy-MM-dd HH:mm'))")
    $writer.WriteLine("-- Ticket : #60787")
    $writer.WriteLine("-- IMPORTANT : Verifier les donnees avant execution en PROD")
    $writer.WriteLine("-- IMPORTANT : Ouvrir dans un NOUVEL onglet SSMS dedie")
    $writer.WriteLine("-- MODE AUTOCOMMIT : chaque UPDATE/INSERT est valide immediatement,")
    $writer.WriteLine("--                  aucune transaction a gerer manuellement.")
    $writer.WriteLine("-- ============================================================")
    $writer.WriteLine("")
    $writer.WriteLine("-- Securite : fermer toute transaction residuelle avant execution")
    $writer.WriteLine("SET XACT_ABORT ON;")
    $writer.WriteLine("WHILE @@TRANCOUNT > 0")
    $writer.WriteLine("BEGIN")
    $writer.WriteLine("    PRINT 'WARN : transaction residuelle detectee - rollback preventif';")
    $writer.WriteLine("    ROLLBACK TRANSACTION;")
    $writer.WriteLine("END")
    $writer.WriteLine("")
    return $writer
}

function Close-SqlBatch {
    param([System.IO.StreamWriter]$Writer, [int]$RowsInBatch)
    $Writer.WriteLine("")
    $Writer.WriteLine("-- ============================================================")
    $Writer.WriteLine("-- Verification finale ($RowsInBatch ligne(s) traitee(s) dans ce batch)")
    $Writer.WriteLine("-- Lecture committed : si la ligne apparait ici, elle est bien en base.")
    $Writer.WriteLine("-- ============================================================")
    $Writer.WriteLine("SELECT COUNT(*) AS TotalRoles FROM [Pennylane].[Roles];")
    $Writer.Flush()
    $Writer.Close()
}

# ===========================================================================
# ETAPE 1 : Diff (calcul ou chargement depuis disque)
# ===========================================================================
Write-Log "=== Demarrage rattrapage roles Pennylane ==="
Write-Log "Dossier sortie  : $OutputFolder"
Write-Log "Diff CSV        : $DiffCsvPath"

# Si le diff existe deja sur disque (reprise ou pre-calcul) → charger directement
# Cela evite de re-exporter les CSV depuis SSMS en cas de reprise.
if (Test-Path $DiffCsvPath) {
    Write-Log "[1/2] Diff deja present sur disque → chargement depuis $DiffCsvPath" "WARN"
    $usersToProcess = [System.Collections.Generic.List[PSObject]](
        Import-CsvNormalized -Path $DiffCsvPath `
            -FallbackHeaders @('ContactId','AccountId','Email','AccountNumber')
    )
    Write-Log "      -> $($usersToProcess.Count) utilisateurs charges depuis le disque."
}
else {
    # Verifier que les CSV sources existent
    if (-not (Test-Path $AuthorizedCsvPath)) {
        throw "Fichier introuvable : $AuthorizedCsvPath`n-> Executer d abord 01_extract_authorized_users.sql dans SSMS."
    }
    if (-not (Test-Path $ExistingRolesCsvPath)) {
        throw "Fichier introuvable : $ExistingRolesCsvPath`n-> Executer d abord 02_extract_existing_roles.sql dans SSMS."
    }

    Write-Log "[1/2] Calcul du diff en memoire..."
    Write-Log "      Lecture de authorized_users.csv : $AuthorizedCsvPath"
    # FallbackHeaders = ordre des colonnes du SELECT dans 01_extract_authorized_users.sql
    # SELECT ca.ContactId, ca.AccountId, c.Email, acc.AccountNumber
    $authorizedUsers = Import-CsvNormalized -Path $AuthorizedCsvPath `
                           -FallbackHeaders @('ContactId','AccountId','Email','AccountNumber')
    Write-Log "      -> $($authorizedUsers.Count) lignes autorisees."

    Write-Log "      Lecture de existing_roles.csv   : $ExistingRolesCsvPath"
    # FallbackHeaders = ordre des colonnes du SELECT dans 02_extract_existing_roles.sql
    # SELECT [AccountId], [ContactId], [Role]   ← AccountId est EN PREMIER dans la requete
    $existingRoles = Import-CsvNormalized -Path $ExistingRolesCsvPath `
                         -FallbackHeaders @('AccountId','ContactId','Role')
    Write-Log "      -> $($existingRoles.Count) roles existants."

    # Index HashSet sur (ContactId|AccountId) pour lookup O(1)
    $existingKeys = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    # Detecter dynamiquement les noms de colonnes (insensible a la casse + BOM residuel)
    $firstRow = $existingRoles | Select-Object -First 1
    if ($null -eq $firstRow) {
        Write-Log "      [WARN] existing_roles.csv est vide - aucun role existant." "WARN"
    }
    else {
        $propNames  = $firstRow.PSObject.Properties.Name
        $colContact = $propNames | Where-Object { $_ -match 'ContactId' } | Select-Object -First 1
        $colAccount = $propNames | Where-Object { $_ -match 'AccountId' } | Select-Object -First 1

        if (-not $colContact -or -not $colAccount) {
            throw "Colonnes ContactId/AccountId introuvables dans existing_roles.csv.`nColonnes detectees : $($propNames -join ', ')`nVerifier le separateur et l encodage du fichier."
        }
        Write-Log "      [CSV] Colonnes detectees dans existing_roles : ContactId='$colContact' AccountId='$colAccount'"

        foreach ($row in $existingRoles) {
            $cid = $row.$colContact
            $aid = $row.$colAccount
            if ($cid -and $aid) {
                $existingKeys.Add("$($cid.Trim())|$($aid.Trim())") | Out-Null
            }
        }
    }
    # Liberer la memoire - plus besoin du tableau existingRoles
    $existingRoles = $null
    [System.GC]::Collect()

    $usersToProcess = [System.Collections.Generic.List[PSObject]]::new()

    # Detecter dynamiquement les noms de colonnes dans authorized_users
    $firstAuth  = $authorizedUsers | Select-Object -First 1
    $authProps  = $firstAuth.PSObject.Properties.Name
    $colAuthContact = $authProps | Where-Object { $_ -match 'ContactId' } | Select-Object -First 1
    $colAuthAccount = $authProps | Where-Object { $_ -match 'AccountId' } | Select-Object -First 1

    if (-not $colAuthContact -or -not $colAuthAccount) {
        throw "Colonnes ContactId/AccountId introuvables dans authorized_users.csv.`nColonnes detectees : $($authProps -join ', ')`nVerifier le separateur et l encodage du fichier."
    }
    Write-Log "      [CSV] Colonnes detectees dans authorized_users : ContactId='$colAuthContact' AccountId='$colAuthAccount'"

    foreach ($user in $authorizedUsers) {
        $cid = $user.$colAuthContact
        $aid = $user.$colAuthAccount
        if ($cid -and $aid -and -not $existingKeys.Contains("$($cid.Trim())|$($aid.Trim())")) {
            $usersToProcess.Add($user)
        }
    }
    # Liberer la memoire - plus besoin du tableau complet
    $authorizedUsers = $null
    $existingKeys    = $null
    [System.GC]::Collect()

    Write-Log "      -> A rattraper : $($usersToProcess.Count) utilisateurs."

    if ($usersToProcess.Count -eq 0) {
        Write-Log "Aucun utilisateur sans role detecte. Rien a faire." "WARN"
        exit 0
    }

    # Ecriture du diff sur disque (TOUJOURS - tracabilite + reprise)
    Write-Log "[2/2] Ecriture du diff sur disque : $DiffCsvPath"
    $usersToProcess | Export-Csv -Path $DiffCsvPath -NoTypeInformation -Delimiter "," -Encoding UTF8
    Write-Log "      -> Fichier ecrit ($($usersToProcess.Count) lignes)."
}

# ===========================================================================
# ETAPE 2 : Appel API Pennylane + generation SQL
# ===========================================================================
$headers   = @{ Authorization = "Bearer $BearerToken" }
$byAccount = $usersToProcess | Group-Object AccountNumber
Write-Log "Comptes distincts a interroger : $($byAccount.Count)"
Write-Log "Batch SQL size  : $SqlBatchSize | Retry max : $MaxRetries | Timeout : $TimeoutSec s | Delai : $DelayMs ms"

# Checkpoint - reprise
$processedAccounts = [System.Collections.Generic.HashSet[string]]::new()
if ($ResumeFromCheckpoint -and (Test-Path $CheckpointPath)) {
    $checkpoint = Get-Content $CheckpointPath | ConvertFrom-Json
    foreach ($acc in $checkpoint.ProcessedAccounts) { $processedAccounts.Add($acc) | Out-Null }
    Write-Log "Reprise depuis checkpoint : $($processedAccounts.Count) comptes deja traites." "WARN"
}
elseif (Test-Path $CheckpointPath) {
    Remove-Item $CheckpointPath -Force
}

$totalFound          = 0
$totalMissing        = 0   # email non matche (compte trouve dans Pennylane, email inconnu)
$totalNotInPennylane = 0   # HTTP 404 - compte inexistant dans Pennylane
$totalErrors         = 0   # erreurs reseau / API reelles (5xx, timeout, etc.)
$batchNumber   = 1
$rowsInBatch   = 0
$accountsDone  = $processedAccounts.Count
$accountsTotal = $byAccount.Count
$startTime     = [datetime]::Now

# Continuer la numerotation des batches en cas de reprise
if ($ResumeFromCheckpoint) {
    $existingBatches = Get-ChildItem $OutputFolder -Filter "04_update_roles_batch_*.sql" -ErrorAction SilentlyContinue
    if ($existingBatches) {
        $batchNumber = ($existingBatches | ForEach-Object {
            if ($_.Name -match '_batch_(\d+)\.sql$') { [int]$Matches[1] }
        } | Measure-Object -Maximum).Maximum + 1
    }
}

$currentBatchPath   = Join-Path $OutputFolder ("04_update_roles_batch_{0:D3}.sql" -f $batchNumber)
$currentBatchWriter = Open-SqlBatch -FilePath $currentBatchPath -BatchNumber $batchNumber

foreach ($group in $byAccount) {
    $accountNumber     = $group.Name
    $contactsInAccount = $group.Group
    $accountsDone++

    if ($processedAccounts.Contains($accountNumber)) { continue }

    # Progression + ETA
    $elapsed    = ([datetime]::Now - $startTime).TotalSeconds
    $ratePerSec = if ($elapsed -gt 0) { ($accountsDone - $processedAccounts.Count) / $elapsed } else { 0 }
    $remaining  = $accountsTotal - $accountsDone
    $etaSec     = if ($ratePerSec -gt 0) { [int]($remaining / $ratePerSec) } else { 0 }
    $etaStr     = if ($etaSec -gt 3600)  { "{0}h{1}m" -f [int]($etaSec/3600), [int](($etaSec%3600)/60) }
                  elseif ($etaSec -gt 60) { "{0}m{1}s" -f [int]($etaSec/60), ($etaSec%60) }
                  else { "${etaSec}s" }
    $pct = [int](($accountsDone / $accountsTotal) * 100)

    Write-Progress -Activity "Rattrapage roles Pennylane" `
        -Status "$accountsDone/$accountsTotal - $accountNumber - ETA : $etaStr" `
        -PercentComplete $pct

    Write-Log "[$accountsDone/$accountsTotal] Compte $accountNumber ($($contactsInAccount.Count) contact(s)) | ETA $etaStr"

    # Pagination API
    $page         = 1
    $totalPages   = 1
    $companyUsers = [System.Collections.Generic.Dictionary[string,string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    do {
        $url      = "$ApiBaseUrl/companies/$accountNumber/users?per_page=500&page=$page"
        $response = Invoke-PennylaneApi -Url $url -Headers $headers `
                        -MaxRetries $MaxRetries -TimeoutSec $TimeoutSec `
                        -AccountNumber $accountNumber

        if ($null -eq $response) {
            $totalErrors++   # erreur reseau / timeout / 5xx
            break
        }
        if ($response -eq "NOT_FOUND") {
            $totalNotInPennylane++   # HTTP 404 - compte absent de Pennylane
            break
        }

        foreach ($u in $response.users) {
            # The /companies/{id}/users endpoint returns "role" (e.g. "executive", "sales_manager").
            # This field contains the sub_role value — there is no separate "sub_role" field on this endpoint.
            $roleProp = $u.PSObject.Properties['role']
            if ($u.email -and $roleProp -and $roleProp.Value -and -not $companyUsers.ContainsKey($u.email)) {
                $companyUsers[$u.email] = $roleProp.Value
            }
        }

        $totalPages = $response.total_pages
        $page++
        if ($DelayMs -gt 0 -and $page -le $totalPages) { Start-Sleep -Milliseconds $DelayMs }

    } while ($page -le $totalPages)

    # Generation SQL
    foreach ($contact in $contactsInAccount) {
        $email     = $contact.Email.Trim()
        $contactId = [long]$contact.ContactId
        $accountId = [long]$contact.AccountId

        if ($companyUsers.ContainsKey($email)) {
            $roleSafe = $companyUsers[$email].Replace("'", "''")

            if ($rowsInBatch -ge $SqlBatchSize) {
                Close-SqlBatch -Writer $currentBatchWriter -RowsInBatch $rowsInBatch
                Write-Log "Batch $batchNumber ferme ($rowsInBatch lignes) -> $currentBatchPath"
                $batchNumber++
                $rowsInBatch        = 0
                $currentBatchPath   = Join-Path $OutputFolder ("04_update_roles_batch_{0:D3}.sql" -f $batchNumber)
                $currentBatchWriter = Open-SqlBatch -FilePath $currentBatchPath -BatchNumber $batchNumber
            }

            $currentBatchWriter.WriteLine("-- $email | $accountNumber")
            $currentBatchWriter.WriteLine("IF EXISTS (SELECT 1 FROM [Account].[Accounts]  WHERE AccountId  = $accountId)")
            $currentBatchWriter.WriteLine("   AND EXISTS (SELECT 1 FROM [Contact].[Contacts] WHERE ContactId = $contactId)")
            $currentBatchWriter.WriteLine("BEGIN")
            $currentBatchWriter.WriteLine("    -- UPDATE si la ligne existe deja")
            $currentBatchWriter.WriteLine("    UPDATE [Pennylane].[Roles]")
            $currentBatchWriter.WriteLine("    SET    Role = '$roleSafe'")
            $currentBatchWriter.WriteLine("    WHERE  ContactId = $contactId AND AccountId = $accountId;")
            $currentBatchWriter.WriteLine("    -- INSERT si la ligne n existait pas")
            $currentBatchWriter.WriteLine("    IF @@ROWCOUNT = 0")
            $currentBatchWriter.WriteLine("        INSERT INTO [Pennylane].[Roles] (ContactId, AccountId, Role)")
            $currentBatchWriter.WriteLine("        VALUES ($contactId, $accountId, '$roleSafe');")
            $currentBatchWriter.WriteLine("END")
            $currentBatchWriter.WriteLine("")
            $rowsInBatch++
            $totalFound++
        }
        else {
            # Construire la liste des emails disponibles dans l API pour ce compte (aide au diagnostic)
            $availableEmails = ($companyUsers.Keys | Sort-Object) -join " | "
            Write-Log "  SKIP : email '$email' non trouve chez Pennylane pour compte $accountNumber" "WARN"
            Write-Log "         Emails disponibles dans l API : $availableEmails" "WARN"
            Write-Log "         -> Ajouter ce compte dans 04c_manual_fix_missing_accounts.sql si necessaire." "WARN"
            $totalMissing++
        }
    }

    # Checkpoint
    $processedAccounts.Add($accountNumber) | Out-Null
    @{ ProcessedAccounts = @($processedAccounts) } | ConvertTo-Json -Compress |
        Set-Content -Path $CheckpointPath -Encoding UTF8

    if ($DelayMs -gt 0) { Start-Sleep -Milliseconds $DelayMs }
}

# Fermer le dernier batch
if ($rowsInBatch -gt 0) {
    Close-SqlBatch -Writer $currentBatchWriter -RowsInBatch $rowsInBatch
    Write-Log "Batch $batchNumber ferme ($rowsInBatch lignes) -> $currentBatchPath"
}
else {
    $currentBatchWriter.Close()
    Remove-Item $currentBatchPath -Force
}

Write-Progress -Activity "Rattrapage roles Pennylane" -Completed

# ---------------------------------------------------------------------------
# Resume final
# ---------------------------------------------------------------------------
$totalDuration = [datetime]::Now - $startTime

Write-Log "=== RESUME FINAL ==="
Write-Log "  Duree totale                : $($totalDuration.ToString('hh\:mm\:ss'))"
Write-Log "  Roles recuperes             : $totalFound"
Write-Log "  NON dans Pennylane (HTTP 404): $totalNotInPennylane  (comptes absents de Pennylane - test/REC)"
Write-Log "  Email non matche            : $totalMissing  (compte existe mais email BDD != email Pennylane)"
Write-Log "  Erreurs API (reseau/5xx)    : $totalErrors"
Write-Log "  Batches SQL                 : $($batchNumber - $(if ($rowsInBatch -eq 0) {1} else {0}))"
Write-Log "  Fichiers dans               : $OutputFolder"
Write-Log "  Log complet                 : $LogPath"

if ($totalErrors -gt 0) {
    Write-Log "[!] $totalErrors erreurs API reelles (reseau/5xx) - consulter le log." "WARN"
}
if ($totalNotInPennylane -gt 0) {
    Write-Log "[i] $totalNotInPennylane comptes absents de Pennylane (HTTP 404) - comptes test/REC probables." "WARN"
}

if ($totalErrors -eq 0 -and (Test-Path $CheckpointPath)) {
    Remove-Item $CheckpointPath -Force
    Write-Log "Checkpoint supprime (execution complete sans erreur)."
}

Write-Host ""
Write-Host "Fichiers SQL prets dans : $OutputFolder" -ForegroundColor Green
Write-Host "Executer les fichiers 04_update_roles_batch_*.sql dans SSMS par ordre numerique." -ForegroundColor Green

