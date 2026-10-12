-- =============================================================================
-- SCRIPT 4a -- Verification cote base PENNYLANE
-- Ticket  : #60787 - Rattraper les roles
-- Auteur  : RYDGE Conseil
-- Date    : 2026-05-20
--
-- A executer dans SSMS connecte a la base PENNYLANE.
-- Comparer les resultats avec ceux de 04b_verify_registry.sql.
-- =============================================================================

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- =============================================================================
-- 1. Total des roles enregistres
-- =============================================================================
SELECT
    'Total Roles Pennylane' AS Indicateur,
    COUNT(*)                AS Valeur
FROM [Pennylane].[Roles] WITH (NOLOCK);

-- =============================================================================
-- 2. Repartition des roles (distribution)
-- =============================================================================
SELECT
    r.Role      AS SubRole,
    COUNT(*)    AS NombreContacts
FROM [Pennylane].[Roles] AS r WITH (NOLOCK)
GROUP BY r.Role
ORDER BY NombreContacts DESC;

-- =============================================================================
-- 3. Roles avec valeur invalide (hors liste officielle)
--    Resultat attendu : 0 ligne
-- =============================================================================
SELECT
    r.ContactId,
    r.AccountId,
    r.Role AS RoleInvalide
FROM [Pennylane].[Roles] AS r WITH (NOLOCK)
WHERE r.Role NOT IN (
    'executive',
    'employee',
    'internal_accountant',
    'invoicing_manager',
    'purchasing_manager',
    'sales_manager'
)
ORDER BY r.Role;

-- =============================================================================
-- 4. Export des paires (ContactId, AccountId) pour comparaison croisee
--    Copier le resultat en CSV et comparer avec l export du script 04b.
-- =============================================================================
SELECT
    r.ContactId,
    r.AccountId,
    r.Role
FROM [Pennylane].[Roles] AS r WITH (NOLOCK)
ORDER BY r.ContactId, r.AccountId;

