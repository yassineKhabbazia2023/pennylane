CREATE TABLE [Contact].[Contacts]
(
	[ContactId]				INT	                NOT NULL,
	[ContactGlobalUniqueId]	UNIQUEIDENTIFIER	NULL,
	[Email]     			NVARCHAR(255)		NOT NULL,
	[Type]                  VARCHAR(20)         NOT NULL,
	[FirstName]				NVARCHAR(100)		NULL,
	[LastName]				NVARCHAR(100)		NULL,
    CONSTRAINT [C_Contact_PK] PRIMARY KEY CLUSTERED ([ContactId] ASC)
)