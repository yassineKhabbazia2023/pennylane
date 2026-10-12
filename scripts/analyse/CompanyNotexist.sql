


 select d.name,d.source_id,d.accounting_supervisor_email  from tmp.Entreprises___Dossiers d
where not exists (select 1 from account.Accounts acc where acC.AccountNumber=d.source_id)
and source_id!=''