-- Index pour récupérer rapidement le dernier Terms créé
CREATE NONCLUSTERED INDEX [IX_Terms_CreationDate] 
    ON [Amend].[Terms] ([CreationDate] DESC);
