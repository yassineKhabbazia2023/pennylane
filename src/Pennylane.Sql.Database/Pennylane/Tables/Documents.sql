CREATE TABLE [Pennylane].[Documents] (
    [Id]        BIGINT        NOT NULL,
    [AccountId] INT           NOT NULL,
    [Name]      NVARCHAR (260) NOT NULL,
    [Path]      NVARCHAR (100) NOT NULL,
    [Url]       NVARCHAR (max) NOT NULL,
    [CreatedAt] DATETIME      NOT NULL,
    [UpdatedAt] DATETIME      NOT NULL,
    CONSTRAINT [PK_Documents] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Documents_Accounts] FOREIGN KEY ([AccountId]) REFERENCES [Account].[Accounts] ([AccountId]) ON DELETE CASCADE
);




GO
CREATE NONCLUSTERED INDEX [Account_Documents]
    ON [Pennylane].[Documents]([AccountId] ASC)
    INCLUDE([Id]);

