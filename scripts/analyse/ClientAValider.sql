IF OBJECT_ID('tempdb..#EmailsDansAccount') IS NOT NULL
    DROP TABLE #EmailsDansAccount;

IF OBJECT_ID('tempdb..#EmailsDansReg') IS NOT NULL
    DROP TABLE #EmailsDansReg;

create  table #EmailsDansAccount (
    Email nvarchar(255),
    source_id nvarchar(255)
);

insert into #EmailsDansAccount (Email, source_id)
select 
    cc.Email,
    acc.AccountNumber
from account.accounts acc
join account.Roles ro on ro.AccountId = acc.AccountId
join contact.contacts cc on ro.ContactId = cc.ContactId;

create  table #EmailsDansReg (
    Email nvarchar(255),
    source_id nvarchar(255)
);

insert into #EmailsDansReg (Email, source_id)
select 
    ro.ContactEmail,
    ro.AccountNumber
from reg.Operations rgo
join ref.Role ro on rgo.EntityId = ro.EntityId
where rgo.ProcessStatus = 'ready' and ApprovalStatus='PENDING';

select distinct
    d.email,
    d.source_id,
	'Le collaborateur doit accepter l’accès de l’utilisateur sur la plateforme' as message
from tmp.dirigeants d
where d.email != ''
  and not exists (
      select 1 from #EmailsDansAccount ea
      where ea.Email = d.email 
        and ea.source_id = d.source_id
  )
  and exists (
      select 1 from #EmailsDansReg er
      where er.Email = d.email 
        and er.source_id = d.source_id
  );

