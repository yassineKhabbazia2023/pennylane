CREATE TABLE [Pennylane].[Companies]
(
    [Id] NVARCHAR (100) NOT NULL,
    [FirmId] NVARCHAR (50) NOT NULL,
    [RegNo] NVARCHAR (50) NULL,
    [Name] NVARCHAR (255) NOT NULL,
    [SaasPlan] NVARCHAR (100) NULL,
    [SupplierInvoiceTransmissionEmail] NVARCHAR (255) NULL,
    [CustomerInvoiceTransmissionEmail] NVARCHAR (255) NULL,
    [InternalId] NVARCHAR (100) NULL,
    [LastSyncDate] DATETIME NULL,
    [CreatedAt] DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Companies] PRIMARY KEY CLUSTERED ([Id] ASC)
);

GO
CREATE NONCLUSTERED INDEX [IX_Companies_FirmId]
    ON [Pennylane].[Companies]([FirmId] ASC)
    INCLUDE([Id], [Name]);

GO
CREATE NONCLUSTERED INDEX [IX_Companies_InternalId]
    ON [Pennylane].[Companies]([InternalId] ASC)
    WHERE [InternalId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_Companies_LastSyncDate]
    ON [Pennylane].[Companies]([LastSyncDate] ASC)
    WHERE [LastSyncDate] IS NOT NULL;
