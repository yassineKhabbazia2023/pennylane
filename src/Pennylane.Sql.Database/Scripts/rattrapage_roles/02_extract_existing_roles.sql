-- =============================================================================
-- SCRIPT 2 — Extraction des contacts ayant déjà un rôle Pennylane
-- Ticket : #60787 - Rattraper les rôles
-- Auteur  : RYDGE Conseil
-- Date    : 2026-05-20
--
-- Source   : [Pennylane].[Roles]
-- Sortie   : existing_roles.csv
--
-- ⚠️ Ce script est à exécuter sur la base PENNYLANE.
-- =============================================================================

SET LOCK_TIMEOUT 10000;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- =============================================================================
-- Estimation du volume (exécuter d'abord)
-- =============================================================================
SELECT COUNT(*) AS NombreRolesExistants
FROM [Pennylane].[Roles] WITH (NOLOCK);

-- =============================================================================
-- Export principal → existing_roles.csv
-- Dans SSMS : Query > Results To > Results to File (Ctrl+Shift+F)
--
-- ⚠️ Tools > Options > Query Results > SQL Server > Results to Text
--    → Output format : Comma delimited
--    → cocher "Include column headers in the result set"
-- =============================================================================
SELECT
    [ContactId],
    [AccountId],
    [Role]
FROM [Pennylane].[Roles] WITH (NOLOCK)
ORDER BY
    [AccountId],
    [ContactId]
OPTION (MAXDOP 4);

-- =============================================================================
-- Format CSV attendu :
--   ContactId,AccountId,Role
--   1234,5678,executive
-- =============================================================================

