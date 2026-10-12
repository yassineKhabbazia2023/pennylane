
 select distinct d.name,d.source_id,d.accounting_supervisor_email  from tmp.Entreprises___Dossiers d
where not exists (
select 1 from tmp.dirigeants di where di.source_id=d.source_id and di.email!='' and di.email not like '%null.%'and di.email not like '%nul.%'
)
and d.source_id!=''