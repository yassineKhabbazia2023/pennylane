-- =============================================
-- Table: ValidationHistory
-- Description: Historique append-only des accept/refus (insert uniquement)
-- Règles: CreationDate = UTCNow à l'insertion, append-only
-- =============================================

CREATE TABLE [Amend].[ValidationHistory]
(
    [Id]                      BIGINT IDENTITY(1, 1) NOT NULL,
    [AccountNumber]           VARCHAR(20)           NOT NULL,
    [TermsId]                 INT                   NOT NULL,
    [Status]                  BIT                   NOT NULL, -- true=accepted, false=refused
    [CreationDate]            DATETIME2(7)          NOT NULL
        CONSTRAINT [DF_ValidationHistory_CreationDate] DEFAULT SYSUTCDATETIME(),
    [Email]                   NVARCHAR(255)         NULL,
    [LastName]                NVARCHAR(200)         NULL,
    [FirstName]               NVARCHAR(200)         NULL,
    [PlanName]                NVARCHAR(200)         NULL,
    [UserCount]               INT                   NULL,
    [PlanPriceExclTax]        DECIMAL(18,2)         NULL,
    [CompliancePriceExclTax]  DECIMAL(18,2)         NULL,

    CONSTRAINT [PK_ValidationHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ValidationHistory_Terms]
        FOREIGN KEY ([TermsId])
        REFERENCES [Amend].[Terms]([Id]),
    CONSTRAINT [FK_ValidationHistory_SignatoryAccess]
        FOREIGN KEY ([AccountNumber])
        REFERENCES [Amend].[SignatoryAccess]([AccountNumber])
);
