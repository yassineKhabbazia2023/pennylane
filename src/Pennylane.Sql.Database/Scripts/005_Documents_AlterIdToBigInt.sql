-- =============================================
-- Script: 005_Documents_AlterIdToBigInt
-- Description: Migre la colonne Id de [Pennylane].[Documents] de INT à BIGINT
--              pour supporter les IDs Pennylane dépassant la capacité d'un Int32.
-- =============================================

ALTER TABLE [Pennylane].[Documents] DROP CONSTRAINT [PK_Documents];
GO

ALTER TABLE [Pennylane].[Documents] ALTER COLUMN [Id] BIGINT NOT NULL;
GO

ALTER TABLE [Pennylane].[Documents] ADD CONSTRAINT [PK_Documents] PRIMARY KEY CLUSTERED ([Id] ASC);
GO
