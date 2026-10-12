-- =============================================
-- Table: Terms
-- Description: Source des liens CGU à afficher
-- Règles: CreationDate = UTCNow à l'insertion
-- =============================================

CREATE TABLE [Amend].[Terms]
(
    [Id]           INT IDENTITY(1, 1) NOT NULL,
    [LinkFr]       NVARCHAR(255)      NOT NULL,
    [CreationDate] DATETIME2(7)       NOT NULL 
        CONSTRAINT [DF_Terms_CreationDate] DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Terms] PRIMARY KEY CLUSTERED ([Id] ASC)
);
