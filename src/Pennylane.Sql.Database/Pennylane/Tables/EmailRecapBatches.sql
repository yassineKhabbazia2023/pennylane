CREATE TABLE [Pennylane].[EmailRecapBatches] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [RecapDate] DATETIME2 NOT NULL,
    [GeneratedAt] DATETIME2 NOT NULL,
    [SendingStartedAt] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,
    [Status] INT NOT NULL CONSTRAINT [DF_EmailRecapBatches_Status] DEFAULT 0,
    [TotalItems] INT NOT NULL CONSTRAINT [DF_EmailRecapBatches_TotalItems] DEFAULT 0,
    [SentCount] INT NOT NULL CONSTRAINT [DF_EmailRecapBatches_SentCount] DEFAULT 0,
    [FailedCount] INT NOT NULL CONSTRAINT [DF_EmailRecapBatches_FailedCount] DEFAULT 0,
    [ErrorMessage] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_EmailRecapBatches] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [IX_EmailRecapBatches_RecapDate]
ON [Pennylane].[EmailRecapBatches] ([RecapDate]);
GO

CREATE INDEX [IX_EmailRecapBatches_Status]
ON [Pennylane].[EmailRecapBatches] ([Status]);
GO
