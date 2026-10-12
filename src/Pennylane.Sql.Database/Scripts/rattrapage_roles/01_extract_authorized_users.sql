-- =============================================================================
-- SCRIPT 1 — Extraction des utilisateurs autorisés Pennylane (AuthorizationId = 15)
-- Ticket : #60787 - Rattraper les rôles
-- Auteur  : RYDGE Conseil
-- Date    : 2026-05-20
--
-- Source   : [auth].[ContactAuthorization] WHERE AuthorizationId = 15
-- Sortie   : authorized_users.csv
--
-- ⚠️ Ce script est à exécuter sur la base AUTH (ou depuis un contexte
--    permettant les requêtes cross-base sur le même serveur SQL).
-- =============================================================================

SET LOCK_TIMEOUT 10000;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- =============================================================================
-- Estimation du volume (exécuter d'abord)
-- =============================================================================
SELECT COUNT(*) AS NombreUtilisateursAutorises
FROM [auth].[ContactAuthorization] WITH (NOLOCK)
WHERE AuthorizationId = 15;

-- =============================================================================
-- Export principal → authorized_users.csv
-- Dans SSMS : Query > Results To > Results to File (Ctrl+Shift+F)
--
-- ⚠️ Tools > Options > Query Results > SQL Server > Results to Text
--    → Output format : Comma delimited
--    → cocher "Include column headers in the result set"
-- =============================================================================
SELECT
    ca.ContactId,
    ca.AccountId,
    c.Email,
    a.AccountNumber
FROM
    [auth].[ContactAuthorization]   AS ca  WITH (NOLOCK)
    INNER JOIN [actor].Contact c   WITH (NOLOCK) ON c.ContactId = ca.ContactId
    INNER JOIN [account].[Account] AS a   WITH (NOLOCK) ON a.AccountId = ca.AccountId
WHERE
    ca.AuthorizationId = 15
  AND c.Email IS NOT NULL
  AND c.Email <> ''
ORDER BY
    a.AccountNumber,
    c.Email
OPTION (MAXDOP 4);

-- =============================================================================
-- Format CSV attendu :
--   ContactId,AccountId,Email,AccountNumber
--   1234,5678,jean.dupont@exemple.fr,EC84393892
-- =============================================================================

