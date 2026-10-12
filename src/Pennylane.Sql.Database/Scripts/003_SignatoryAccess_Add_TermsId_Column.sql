alter table Amend.SignatoryAccess
ADD [TermsId] INT NULL
GO

ALTER TABLE Amend.SignatoryAccess
ADD FOREIGN KEY ([TermsId]) REFERENCES [Amend].[Terms]([Id])

DECLARE @juneTermsId INT, @octoberTermsId INT;
SET @juneTermsId = (SELECT TOP 1 [Id] FROM Amend.Terms WHERE [LinkFr] = 'Conditions générales en vigueur – mise à jour fevrier 2026 – RYDGE Conseil (1).pdf');
SET @octoberTermsId = (SELECT TOP 1 [Id] FROM Amend.Terms WHERE [LinkFr] = 'Conditions générales en vigueur – mise à jour fevrier 2026 – RYDGE Conseil (2).pdf');

update Amend.SignatoryAccess
set [TermsId] = @juneTermsId
where [FreeTrialEndDate] = CAST('2026-06-30' AS datetime2)

update Amend.SignatoryAccess
set [TermsId] = @octoberTermsId
where [FreeTrialEndDate] = CAST('2026-10-31' AS datetime2)
GO

alter table Amend.SignatoryAccess
alter column [TermsId] INT NOT NULL
GO