CREATE NONCLUSTERED INDEX [IX_ValidationHistory_Last]
    ON [Amend].[ValidationHistory]([AccountNumber], [TermsId], [CreationDate] DESC)
    INCLUDE ([Status]);
