-- =============================================================================
-- SCRIPT 4b -- Verification cote base AUTORISATION
-- Ticket  : #60787 - Rattraper les roles
-- Auteur  : RYDGE Conseil
-- Date    : 2026-05-20
--
-- A executer dans SSMS connecte a la base AUTORISATION.
-- Comparer les resultats avec ceux de 04a_verify_pennylane.sql.
-- =============================================================================

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- =============================================================================
-- 1. Total des autorisations Pennylane (source de verite)
--    AuthorizationId = 15 => acces Pennylane
-- =============================================================================
SELECT
    'Total autorisations Pennylane (AuthorizationId=15)' AS Indicateur,
    COUNT(*)                                              AS Valeur
FROM [auth].[ContactAuthorization] WITH (NOLOCK)
WHERE AuthorizationId = 15;

-- =============================================================================
-- 2. Export des paires (ContactId, AccountId) ayant AuthorizationId = 15
--    Copier le resultat en CSV et comparer avec l export du script 04a.
--    Les lignes presentes ici mais absentes dans 04a = utilisateurs sans role.
-- =============================================================================
SELECT
    ca.ContactId,
    ca.AccountId,
    c.Email,
    a.AccountNumber
FROM [auth].[ContactAuthorization] AS ca WITH (NOLOCK)
INNER JOIN [actor].[Contact]    AS c  WITH (NOLOCK) ON c.ContactId = ca.ContactId
INNER JOIN [account].[Account]    AS a  WITH (NOLOCK) ON a.AccountId = ca.AccountId
WHERE ca.AuthorizationId = 15
ORDER BY ca.ContactId, ca.AccountId;

