CREATE TABLE [Account].[Accounts] (
    [AccountId]               INT  NOT NULL,
    [AccountGlobalUniqueId]   UNIQUEIDENTIFIER NULL,
    [AccountNumber]           VARCHAR (20)    NOT NULL,
    [ModifiedBy]              VARCHAR (100) NULL,
    [CreatedBy]                VARCHAR (100) NULL,
    [LegalName]               NVARCHAR (255)   NOT NULL default '',
    CONSTRAINT [C_Account_PK] PRIMARY KEY CLUSTERED ([AccountId] ASC),
    CONSTRAINT [UQ_Account_AccountGlobalUniqueId] UNIQUE NONCLUSTERED ([AccountGlobalUniqueId] ASC)
);