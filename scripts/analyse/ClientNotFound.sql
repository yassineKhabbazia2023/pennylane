  select  email,source_id from tmp.Dirigeants d
where email !=''
and   
(
(
not  exists (
select 1 from account.Account acc
join account.Role ro on ro.AccountId=acc.AccountId
join actor.Contact cc on ro.ContactId=cc.ContactId
where cc.Email=d.email and acc.AccountNumber=d.source_id  and ro.IsSignatory=1)
and  exists (
select 1 from account.Account acc
join account.Role ro on ro.AccountId=acc.AccountId
join actor.Contact cc on ro.ContactId=cc.ContactId
where cc.Email=d.email and acc.AccountNumber=d.source_id  ))
or
(
not  exists (
select 1 from account.Account acc
join account.Role ro on ro.AccountId=acc.AccountId
join actor.Contact cc on ro.ContactId=cc.ContactId
where cc.Email=d.email and acc.AccountNumber=d.source_id)))

