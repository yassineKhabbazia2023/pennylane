-- ============================================================================
-- Script: Update-SignatoryAccess-TermsId.sql
-- Description: Met à jour les TermsId des SignatoryAccess en fonction de la
--              FreeTrialEndDate lorsque de nouveaux Terms sont ajoutés.
-- Usage: Exécuter ce script après l'ajout de nouveaux Terms dans la table
--        [Amend].[Terms] pour associer les SignatoryAccess aux bons TermsId.
-- ============================================================================

-- ============================================================================
-- CONFIGURATION DES DATES
-- Modifier les valeurs ci-dessous selon les nouvelles dates de fin de gratuité
-- ============================================================================

DECLARE @FreeTrialEndDate_1 DATE = '2026-06-30';
DECLARE @FreeTrialEndDate_2 DATE = '2026-10-31';

-- ============================================================================
-- RECUPERATION DYNAMIQUE DES TERMSID DEPUIS LA TABLE TERMS
-- Mapping basé sur la colonne LinkFr :
--   (1).pdf => 2026-06-30
--   (2).pdf => 2026-10-31
-- ============================================================================

DECLARE @TermsId_20260630 INT;
DECLARE @TermsId_20261031 INT;

SELECT @TermsId_20260630 = Id
FROM [Amend].[Terms]
WHERE LinkFr LIKE '%mars 2026%RYDGE Conseil (1).pdf';

SELECT @TermsId_20261031 = Id
FROM [Amend].[Terms]
WHERE LinkFr LIKE '%mars 2026%RYDGE Conseil (2).pdf';

-- Affichage des TermsId récupérés
SELECT
    @TermsId_20260630 AS TermsId_20260630,
    @TermsId_20261031 AS TermsId_20261031;

-- ============================================================================
-- VERIFICATION AVANT EXECUTION (optionnel)
-- Affiche les lignes qui seront impactées
-- ============================================================================

SELECT
    AccountNumber,
    Email,
    FreeTrialEndDate,
    TermsId AS CurrentTermsId,
    CASE
        WHEN CAST(FreeTrialEndDate AS DATE) = @FreeTrialEndDate_1 THEN @TermsId_20260630
        WHEN CAST(FreeTrialEndDate AS DATE) = @FreeTrialEndDate_2 THEN @TermsId_20261031
        ELSE TermsId
    END AS NewTermsId,
    CanAccess
FROM [Amend].[SignatoryAccess]
WHERE CanAccess = 0
  AND (
      CAST(FreeTrialEndDate AS DATE) = @FreeTrialEndDate_1
      OR CAST(FreeTrialEndDate AS DATE) = @FreeTrialEndDate_2
  );

-- ============================================================================
-- MISE A JOUR DES TERMSID
-- ============================================================================

UPDATE [Amend].[SignatoryAccess]
SET
    TermsId =
        CASE
            WHEN CAST(FreeTrialEndDate AS DATE) = @FreeTrialEndDate_1 THEN @TermsId_20260630
            WHEN CAST(FreeTrialEndDate AS DATE) = @FreeTrialEndDate_2 THEN @TermsId_20261031
            ELSE TermsId
        END
WHERE CanAccess = 0;

-- ============================================================================
-- VERIFICATION APRES EXECUTION
-- ============================================================================

SELECT
    TermsId,
    CAST(FreeTrialEndDate AS DATE) AS FreeTrialEndDate,
    COUNT(*) AS NombreLignes
FROM [Amend].[SignatoryAccess]
WHERE CanAccess = 0
GROUP BY TermsId, CAST(FreeTrialEndDate AS DATE)
ORDER BY FreeTrialEndDate;
