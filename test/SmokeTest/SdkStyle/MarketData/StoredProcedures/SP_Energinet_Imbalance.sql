CREATE PROCEDURE [MarketData].[SP_Energinet_Imbalance]
AS BEGIN
	SET NOCOUNT ON;

	DECLARE @StartTime DATETIME2 = (SELECT GETDATE());

	BEGIN TRY

		-- DROP temp tabeller hvis de findes
		IF OBJECT_ID('tempdb..#EnerginetImbalance', 'U') IS NOT NULL
		BEGIN
			DROP TABLE #EnerginetImbalance;
		END

		SELECT 
			meta.[Id]			AS 'TimeseriesIdentificationId'
			, ap.[Value]		AS 'Value'
			, ap.[TimeUTC]	    AS 'FromdateUTC'
			, DATEADD(mi, 15, ap.[TimeUTC])	AS 'TodateUTC'
		    , DATEADD(dd, -1, CAST(Meta.dbo.[UtcToLocal]( ap.[TimeUTC] ) AS DATE)) AS 'Refdate'
		INTO #EnerginetImbalance
		FROM [DSA_Energihandel].[EnerginetAPI].[IMBALANCEPRICE_STAGING] ap
		INNER JOIN [MarketData].[TimeseriesIdentification] meta 
			ON 
			'afrr_regulation'	= meta.[Product] 
			AND 
			ap.[Pricearea]		= meta.[Pricearea] 
			AND
				ISNULL(
			        NULLIF([ap].[Currency], meta.[Currency]),
					NULLIF(meta.[Currency], [ap].[Currency])
				) IS NULL
			AND 
			ap.[Direction]	= meta.[Direction] 
			AND 
			ap.[Unit]		= meta.[Unit] 
			AND 
			ap.[Type]		= meta.[Type];
		

		MERGE INTO [MarketData].[Timeseries] AS DM
			USING (SELECT
				[TimeseriesIdentificationId]
				,[Value] 
				,[FromdateUTC]   
				,[TodateUTC]  
				,[Refdate]  
				FROM #EnerginetImbalance
			) AS extract
				 ON
					[DM].[TimeseriesIdentificationId] = extract.[TimeseriesIdentificationId] 
					AND [DM].[FromdateUTC] = extract.[FromdateUTC] 
			WHEN 
				MATCHED  
			THEN 
				UPDATE
				SET
					DM.[Value]=extract.[Value],
					DM.[Refdate] = extract.[Refdate],
					DM.[ToDateUTC] = extract.[ToDateUTC],
					DM.[Updated] = CASE 
						WHEN DM.[Value]=extract.[Value] AND DM.[Refdate] = extract.[Refdate] THEN DM.[Updated] 
						ELSE @StartTime 
					END
			WHEN 
				 NOT MATCHED BY TARGET
			THEN       
				INSERT
					(
				[TimeseriesIdentificationId]
				,[Value]   
				,[FromdateUTC]  
				,[TodateUTC]  
				,[Refdate] 
				,[Updated] 
					)
				VALUES
				(
					extract.[TimeseriesIdentificationId],
					extract.[Value],
					extract.[FromdateUTC],
					extract.[TodateUTC],
					extract.[Refdate],
					@StartTime
				);
	END TRY

	BEGIN CATCH
		THROW;
	END CATCH;

	DROP TABLE #EnerginetImbalance;
END;

GO

