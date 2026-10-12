CREATE TABLE [offer].[Subscriptions] (
    [Id]               INT           IDENTITY (1, 1)    NOT NULL,
    [OfferName]        VARCHAR(200)                     NOT NULL,
    [AccountId]        INT                              NOT NULL,
    CONSTRAINT [PK_Offer_Subscription] PRIMARY KEY CLUSTERED ([Id] ASC)
);