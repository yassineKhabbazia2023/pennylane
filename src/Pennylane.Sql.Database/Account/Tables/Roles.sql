CREATE TABLE [Account].[Roles]
(
	[AccountId]			INT					NOT NULL,
	[ContactId]			INT					NOT NULL,
	[IsSignatory]		    BIT		NULL,
	CONSTRAINT [C_Role_PK] PRIMARY KEY CLUSTERED ([ContactId] ASC, [AccountId] ASC),
	CONSTRAINT [C_Account_Role_FK] FOREIGN KEY ([AccountId]) REFERENCES [Account].[Accounts] ([AccountId]),
	CONSTRAINT [C_Account_Contact_FK] FOREIGN KEY ([ContactId]) REFERENCES [Contact].[Contacts] ([ContactId]), 
    CONSTRAINT [C_Role_AccountId_ContactId] UNIQUE ([AccountId], [ContactId])
)