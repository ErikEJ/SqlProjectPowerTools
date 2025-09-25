CREATE TABLE [MarketData].[TimeseriesIdentification] (
    [Id]              BIGINT        IDENTITY (1, 1) NOT NULL,
    [Product]         VARCHAR (25)  NOT NULL,
    [Pricearea]       VARCHAR (25)  NOT NULL,
    [Currency]        VARCHAR (8)   NULL,
    [Unit]            VARCHAR (8)   NOT NULL,
    [Commodity]       VARCHAR (8)   NULL,
    [Type]            VARCHAR (50)  NULL,
    [Exchange]        VARCHAR (50)  NULL,
    [Supplier]        VARCHAR (50)  NULL,
    [Production_unit] VARCHAR (50)  NULL,
    [Description]     VARCHAR (100) NULL,
    [Direction]       VARCHAR (8)   NULL,
    [Property]        VARCHAR (20)  NULL,
    [Updated]         DATETIME2 (2) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

