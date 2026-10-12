CREATE TABLE [Pennylane].[PennylaneGedPermissions] (
    [ContactId] INT NOT NULL,
    [AccountId] INT NOT NULL,
    [GrantedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PennylaneGedPermissions_GrantedAt] DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_PennylaneGedPermissions] PRIMARY KEY CLUSTERED ([ContactId], [AccountId])
);
GO

CREATE INDEX [IX_PennylaneGedPermissions_AccountId]
ON [Pennylane].[PennylaneGedPermissions] ([AccountId]);
GO
