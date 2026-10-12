CREATE TABLE [Pennylane].[EmailRecapItems] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [BatchId] INT NOT NULL,
    [ContactId] INT NOT NULL,
    [ContactEmail] NVARCHAR(255) NOT NULL,
    [ContactName] NVARCHAR(255) NULL,
    [TotalDocuments] INT NOT NULL CONSTRAINT [DF_EmailRecapItems_TotalDocuments] DEFAULT 0,
    [AccountCount] INT NOT NULL CONSTRAINT [DF_EmailRecapItems_AccountCount] DEFAULT 0,
    [PayloadJson] NVARCHAR(MAX) NOT NULL,
    [Status] INT NOT NULL CONSTRAINT [DF_EmailRecapItems_Status] DEFAULT 0,
    [SentAt] DATETIME2 NULL,
    [ErrorMessage] NVARCHAR(MAX) NULL,
    [RetryCount] INT NOT NULL CONSTRAINT [DF_EmailRecapItems_RetryCount] DEFAULT 0,
    CONSTRAINT [PK_EmailRecapItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EmailRecapItems_EmailRecapBatches]
        FOREIGN KEY ([BatchId]) REFERENCES [Pennylane].[EmailRecapBatches] ([Id])
        ON DELETE CASCADE
);
GO

CREATE INDEX [IX_EmailRecapItems_BatchId]
ON [Pennylane].[EmailRecapItems] ([BatchId]);
GO

CREATE INDEX [IX_EmailRecapItems_Status]
ON [Pennylane].[EmailRecapItems] ([Status]);
GO
