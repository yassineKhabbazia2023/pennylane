-- =============================================
-- Script: 004_ValidationHistory_Add_SourceColumns
-- Description: Rattrape les données existantes depuis SignatoryAccess
-- =============================================

-- Rattrapage : copier les données depuis SignatoryAccess pour les historiques existants
UPDATE vh
SET vh.[Email]                  = sa.[Email],
    vh.[LastName]               = sa.[LastName],
    vh.[FirstName]              = sa.[FirstName],
    vh.[PlanName]               = sa.[PlanName],
    vh.[UserCount]              = sa.[UserCount],
    vh.[PlanPriceExclTax]       = sa.[PlanPriceExclTax],
    vh.[CompliancePriceExclTax] = sa.[CompliancePriceExclTax]
FROM [Amend].[ValidationHistory] vh
INNER JOIN [Amend].[SignatoryAccess] sa ON vh.[AccountNumber] = sa.[AccountNumber]
GO

-- Rattrapage : remettre CanAccess à NULL pour les lignes sans historique et sans mise à jour
-- (lignes importées mais jamais touchées par un utilisateur)
UPDATE sa
SET sa.[CanAccess] = NULL
FROM [Amend].[SignatoryAccess] sa
WHERE sa.[UpdatedDate] IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM [Amend].[ValidationHistory] vh
      WHERE vh.[AccountNumber] = sa.[AccountNumber]
  )
GO
