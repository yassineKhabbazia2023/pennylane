SELECT 
    'AccountNumber;ClientEmail;CollaboratorEmail;MissionLeaderEmail;AccountingExpertEmail;OfferName;MigrationStatus'
UNION ALL
SELECT 
    d.source_id + ';' +
    ISNULL(di.email, '') + ';' +
    ISNULL(d.accountant_email, '') + ';' +
    ISNULL(d.accounting_supervisor_email, '') + ';' +
    ISNULL(d.accounting_manager_email, '') + ';' +
    'Pennylane;in_progress'
FROM 
    [tmp].[Entreprises___Dossiers] d
JOIN 
    [tmp].[dirigeants] di 
    ON di.source_id = d.source_id
