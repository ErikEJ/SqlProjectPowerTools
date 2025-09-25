CREATE TABLE [MarketData].[Timeseries] (
    [Value]                      NUMERIC (18, 2) NOT NULL,
    [FromdateUTC]                DATETIME2 (0)   NOT NULL,
    [TodateUTC]                  DATETIME2 (0)   NOT NULL,
    [Refdate]                    DATE            NOT NULL,
    [TimeseriesIdentificationID] INT             NOT NULL,
    [Updated]                    DATETIME2 (2)   NOT NULL,
    CONSTRAINT [PK_Prices] PRIMARY KEY CLUSTERED ([TimeseriesIdentificationID] ASC, [FromdateUTC] ASC, [TodateUTC] ASC, [Refdate] ASC)
);


GO

