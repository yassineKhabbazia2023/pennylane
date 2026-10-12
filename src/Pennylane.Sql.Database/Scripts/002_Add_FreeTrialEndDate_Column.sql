alter table Amend.SignatoryAccess
ADD [FreeTrialEndDate] DATETIME2 NULL
GO

update Amend.SignatoryAccess
set [FreeTrialEndDate] = CONVERT(datetime2, '30/06/2026', 103)
where [FreeTrialEndDate] is null
GO

alter table Amend.SignatoryAccess
alter column [FreeTrialEndDate] DATETIME2 NOT NULL
GO