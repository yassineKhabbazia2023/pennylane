CREATE TABLE [Pennylane].[DocumentsSyncStatus] (
    [AccountId]     INT           NOT NULL,
    [AccountNumber] VARCHAR (100) NOT NULL,
    [LastSynchro]   DATETIME      NOT NULL,
    CONSTRAINT [PK_DocumentsSynchro] PRIMARY KEY CLUSTERED ([AccountId] ASC),
    CONSTRAINT [FK_DocumentsSynchros_Accounts] FOREIGN KEY ([AccountId]) REFERENCES [Account].[Accounts] ([AccountId]) ON DELETE CASCADE
);

