CREATE TABLE [Pennylane].[CompanyDirectory]
(
    [CompanyId] NVARCHAR (100) NOT NULL,
    [Siren] NVARCHAR (50) NOT NULL,
    [SnapshotDate] DATETIME NOT NULL,
    CONSTRAINT [PK_CompanyDirectory] PRIMARY KEY CLUSTERED ([CompanyId] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IX_CompanyDirectory_Siren] ON [Pennylane].[CompanyDirectory]([Siren] ASC) INCLUDE([CompanyId]);
