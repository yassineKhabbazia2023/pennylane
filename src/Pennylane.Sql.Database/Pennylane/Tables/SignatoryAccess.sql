CREATE TABLE [Amend].[SignatoryAccess]
(
	[AccountNumber]           VARCHAR(20)       NOT NULL,
    [Email]                   NVARCHAR(255)     NULL,
    [LastName]                NVARCHAR(200)     NULL,
    [FirstName]               NVARCHAR(200)     NULL,
    [PlanName]                NVARCHAR(200)     NULL,
    [UserCount]               INT               NULL,
    [PlanPriceExclTax]        DECIMAL(18,2)     NULL,
    [CompliancePriceExclTax]  DECIMAL(18,2)     NULL,
    [CanAccess]               BIT               NULL ,
    [CreationDate]            DATETIME2(7)      NOT NULL
        CONSTRAINT [DF_SignatoryAccess_CreationDate] DEFAULT (SYSUTCDATETIME()),
    [UpdatedDate]             DATETIME2(7)      NULL,
    [FreeTrialEndDate]        DATETIME2(7)      NULL,
    [TermsId]                 INT               NOT NULL,
    [Wave]                    NVARCHAR(50)      NULL,
    CONSTRAINT [PK_SignatoryAccess] PRIMARY KEY CLUSTERED ([AccountNumber] ASC),
    CONSTRAINT [FK_SignatoryAccess_Terms] FOREIGN KEY ([TermsId]) REFERENCES [Amend].[Terms]([Id])
)
