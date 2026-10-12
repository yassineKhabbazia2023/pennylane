update Amend.SignatoryAccess
set TermsId = (SELECT Id From Amend.Terms where LinkFr = 'Conditions générales en vigueur – mise à jour mai 2026 – RYDGE Conseil.pdf')
where CanAccess <> 1 and FreeTrialEndDate = '2026-10-31 00:00:00.0000000'