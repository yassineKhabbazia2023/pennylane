# Rattrapage des rôles Pennylane — Ticket #60787

## Contexte

Depuis juin 2025, lors de la migration RYDGE Conseil × Pennylane, le `sub_role`
de l'utilisateur signataire n'a pas été redescendu dans la plateforme (table
`Pennylane.Roles`). Ce rattrapage est **one-shot** : il corrige les 33 323 clients
impactés.

**Source de vérité :** `[auth].[ContactAuthorization] WHERE AuthorizationId = 15`
> `AuthorizationId = 15` = accès à l'outil Pennylane.

---

## Schéma des étapes

```
[Serveur Autorisation]            [Serveur Pennylane]     PowerShell + API Pennylane      [Serveur Pennylane]
        │                                  │                          │                          │
01_extract_authorized_users.sql            │                          │                          │
        │→ authorized_users.csv            │                          │                          │
                           02_extract_existing_roles.sql              │                          │
                                           │→ existing_roles.csv      │                          │
                                                    03_compute_and_fetch_roles.ps1               │
                                                    (diff en mémoire + appel API)                │
                                                                      │→ sql_output/04_update_roles_batch_NNN.sql
                                                                      │→ sql_output/04c_manual_fix_missing_accounts.sql (si SKIP)
                                                                                          (exécution manuelle SSMS — autocommit)
                                                                                                 │
                                                                             04a_verify_pennylane.sql  (Serveur Pennylane)
                                                                             04b_verify_autorisation.sql (Serveur Autorisation)
                                                                             → comparaison croisée Excel
```

---

## Étape 1 — Extraire les utilisateurs autorisés Pennylane

**Fichier :** `01_extract_authorized_users.sql` | **Base :** `auth` | **Sortie :** `authorized_users.csv`

```sql
SELECT ca.ContactId, ca.AccountId, c.Email, a.AccountNumber
FROM [auth].[ContactAuthorization] AS ca
WHERE ca.AuthorizationId = 15
```

### Procédure SSMS
1. Connecter SSMS au serveur SQL (base `auth` ou cross-base)
2. Exécuter le bloc **Estimation** (1ère requête)
3. `Tools > Options > Query Results > SQL Server > Results to Text`
   → Output format : **Comma delimited** | cocher **Include column headers**
4. `Query > Results To > Results to File` (Ctrl+Shift+F)
5. Exécuter la 2ème requête → enregistrer sous `authorized_users.csv`

Format attendu :
```
ContactId,AccountId,Email,AccountNumber
1234,5678,jean.dupont@exemple.fr,EC84393892
```

---

## Étape 2 — Extraire les rôles existants dans Pennylane

**Fichier :** `02_extract_existing_roles.sql` | **Base :** `Pennylane` | **Sortie :** `existing_roles.csv`

### Procédure SSMS
1. Connecter SSMS à la base `Pennylane`
2. Même configuration d'export que l'étape 1
3. Exécuter → enregistrer sous `existing_roles.csv`

Format attendu :
```
ContactId,AccountId,Role
1234,5678,executive
```

---

## Étape 3 — Diff + Appel API + Génération SQL (script unique)

**Fichier :** `03_compute_and_fetch_roles.ps1`

> ⚠️ Ce script s'exécute avec **PowerShell 7 sur Windows** (`pwsh.exe`).

### Ce que le script fait en une seule exécution
```
authorized_users.csv
       −  existing_roles.csv
       =  contacts à rattraper  (diff en mémoire)
              ↓
       Appel API Pennylane par compte (champ "role")
              ↓
       sql_output/04_update_roles_batch_NNN.sql
```

### Première exécution

```powershell
.\03_compute_and_fetch_roles.ps1 `
  -AuthorizedCsvPath    .\authorized_users.csv `
  -ExistingRolesCsvPath .\existing_roles.csv `
  -OutputFolder         .\sql_output `
  -ApiBaseUrl           "https://app.pennylane.com/api/firm/v3" `
  -BearerToken          "eyJhbGciOiJSUzI1NiJ9..." `
  -DelayMs              300 `
  -SqlBatchSize         500
```

### Reprise après interruption (checkpoint automatique)

```powershell
.\03_compute_and_fetch_roles.ps1 `
  -AuthorizedCsvPath    .\authorized_users.csv `
  -ExistingRolesCsvPath .\existing_roles.csv `
  -OutputFolder         .\sql_output `
  -ApiBaseUrl           "https://app.pennylane.com/api/firm/v3" `
  -BearerToken          "eyJhbGciOiJSUzI1NiJ9..." `
  -ResumeFromCheckpoint
```

### Sauvegarder le diff pour traçabilité (optionnel)
Ajouter `-SaveDiffCsv` pour écrire `users_without_role.csv` en plus des batches SQL.

### Fonctionnalités
| Fonctionnalité | Détail |
|---|---|
| **Diff en mémoire** | Script1 − Script2 calculé sans fichier intermédiaire |
| **Retry automatique** | 3 tentatives, backoff exponentiel 2s → 4s → 8s |
| **HTTP 429** | Respecte le header `Retry-After` de Pennylane |
| **HTTP 404** | Compte non trouvé → skip sans erreur |
| **Checkpoint** | `sql_output/checkpoint.json` — reprise exacte si interruption |
| **Batches SQL** | 1 fichier par `SqlBatchSize` lignes (défaut 500) |
| **Log persistant** | `sql_output/fetch_log_YYYYMMDD_HHmmss.txt` |
| **Champ API** | Lit `role` (et non `sub_role`) — conforme à l'API Pennylane v3 |
| **SKIP avec diagnostic** | Email non trouvé → log des emails disponibles dans l'API pour ce compte |
| **Mode autocommit** | Batches sans `BEGIN`/`COMMIT` — UPDATE+INSERT, validé immédiatement |

---

## Étape 4 — Appliquer les rôles en base

**Fichiers :** `sql_output/04_update_roles_batch_001.sql`, `_002.sql`, … (auto-générés)
**Base :** `Pennylane`

> ℹ️ **Mode AUTOCOMMIT** — les batches générés ne contiennent plus de `BEGIN`/`COMMIT`
> à gérer manuellement. Chaque `UPDATE`/`INSERT` est validé immédiatement.
> Plus aucun risque de transaction zombie.

### Structure d'un batch généré

```sql
-- MODE AUTOCOMMIT : chaque UPDATE/INSERT est validé immédiatement
SET XACT_ABORT ON;
WHILE @@TRANCOUNT > 0
BEGIN
    PRINT 'WARN : transaction résiduelle détectée - rollback préventif';
    ROLLBACK TRANSACTION;
END

-- email@... | EC123456
IF EXISTS (SELECT 1 FROM [Account].[Accounts]  WHERE AccountId  = ...)
   AND EXISTS (SELECT 1 FROM [Contact].[Contacts] WHERE ContactId = ...)
BEGIN
    IF EXISTS (SELECT 1 FROM [Pennylane].[Roles] WHERE ContactId = ... AND AccountId = ...)
        UPDATE [Pennylane].[Roles] SET Role = '...' WHERE ContactId = ... AND AccountId = ...;
    ELSE
        INSERT INTO [Pennylane].[Roles] (ContactId, AccountId, Role) VALUES (..., ..., '...');
END

-- Vérification finale (lecture committed)
SELECT COUNT(*) AS TotalRoles FROM [Pennylane].[Roles];
```

### Procédure (un fichier à la fois)

1. Ouvrir `04_update_roles_batch_001.sql` dans SSMS dans un **nouvel onglet dédié**
2. Vérifier le contenu
3. Exécuter (F5) — les données sont committées immédiatement
4. Vérifier le `SELECT COUNT(*)` affiché en fin d'exécution
5. Passer au batch suivant

> ⚠️ **Toujours ouvrir chaque batch dans un nouvel onglet SSMS.**
> Ne jamais lancer de requêtes de diagnostic dans le même onglet pendant l'exécution.

---

## Étape 4b — Comptes manquants (fix manuel si mismatch email)

**Fichier :** `04c_manual_fix_missing_accounts.sql` | **Base :** `Pennylane`

Si un compte autorisé n'apparaît pas dans les batches générés (ex : alias `+mod` dans
l'email non reconnu par Pennylane), le PS1 logge un avertissement :

```
SKIP : email 'aanane+mod@...' non trouvé chez Pennylane pour compte EC269067667
       Emails disponibles dans l'API : aanane@... | ...
       -> Ajouter ce compte dans 04c_manual_fix_missing_accounts.sql si nécessaire.
```

Ouvrir `04c_manual_fix_missing_accounts.sql`, vérifier/ajouter les comptes concernés,
puis exécuter dans SSMS. Le script est également en **mode autocommit**.

---

## Étape 5 — Vérification post-rattrapage

> ⚠️ Les bases **Autorisation** et **Pennylane** sont sur deux serveurs distincts.
> Il est impossible de faire des JOINs cross-serveur sans linked server.
> La vérification se fait en **deux scripts séparés** à exécuter dans deux fenêtres SSMS.

### Script 4a — `04a_verify_pennylane.sql`
**Connexion : serveur Pennylane**

Fournit :
- Total des rôles enregistrés dans `[Pennylane].[Roles]`
- Répartition par type de rôle
- Rôles invalides (résultat attendu : **0 ligne**)
- Export CSV des paires `(ContactId, AccountId, Role)` pour comparaison croisée

### Script 4b — `04b_verify_autorisation.sql`
**Connexion : serveur Autorisation**

Fournit :
- Total des autorisations Pennylane (`AuthorizationId = 15`)
- Export CSV des paires `(ContactId, AccountId, Email, AccountNumber)` pour comparaison croisée

### Comparaison croisée (Excel / Power Query)

1. Exporter les résultats de `04a` et `04b` en CSV
2. Créer une clé de jointure `ContactId|AccountId` dans chaque fichier
3. Identifier les lignes présentes dans `04b` (Autorisation) mais absentes dans `04a` (Pennylane)
4. Résultat attendu : **0 ligne manquante**

### Bilan attendu

| Indicateur | Valeur attendue |
|---|---|
| Total autorisations Pennylane (AuthorizationId=15) | X |
| Total Roles Pennylane | ≥ X |
| Utilisateurs encore sans rôle | **0** |

---

## Prérequis

| Élément | Valeur |
|---|---|
| **OS d'exécution** | Windows (PowerShell 7) |
| Token Pennylane | Portail Pennylane — droits `firm:read`, durée de vie > 3h |
| PowerShell | v7+ (`pwsh.exe`) |
| Accès BDD | Lecture `[auth].[ContactAuthorization]` + Lecture/Écriture `[Pennylane].[Roles]` |
| SSMS | Pour exécuter les scripts SQL et exporter les CSV |
| Espace disque | ~50 MB pour les fichiers SQL générés |

---

## Rôles valides

| Code | Libellé |
|---|---|
| `executive` | Dirigeant / Admin |
| `employee` | Employé |
| `internal_accountant` | Comptable interne |
| `invoicing_manager` | Gestionnaire de facturation |
| `purchasing_manager` | Gestionnaire facturation fournisseur |
| `sales_manager` | Gestionnaire facturation client |
