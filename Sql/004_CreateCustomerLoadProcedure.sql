USE [bc];
GO

CREATE OR ALTER PROCEDURE [dbo].[LoadCustomers]
    @JsonPayload nvarchar(max),
    @ExtractedAt datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF ISJSON(@JsonPayload) <> 1 THROW 50020, 'Invalid Customers JSON.', 1;

    CREATE TABLE #S
    (
        [SystemId] uniqueidentifier NOT NULL PRIMARY KEY,
        [CustomerNo] nvarchar(20) NOT NULL,
        [CustomerName] nvarchar(100) NULL,
        [CorpGroup] nvarchar(25) NULL,
        [Address] nvarchar(100) NULL,
        [City] nvarchar(30) NULL,
        [PostCode] nvarchar(20) NULL,
        [SystemCreatedAt] datetime2(7) NOT NULL,
        [SystemModifiedAt] datetime2(7) NOT NULL,
        [SystemCreatedBy] uniqueidentifier NULL,
        [SystemModifiedBy] uniqueidentifier NULL
    );

    INSERT #S
    SELECT *
    FROM OPENJSON(@JsonPayload)
    WITH
    (
        [SystemId] uniqueidentifier '$.systemId',
        [CustomerNo] nvarchar(20) '$.customerNo',
        [CustomerName] nvarchar(100) '$.customerName',
        [CorpGroup] nvarchar(25) '$.corpGroup',
        [Address] nvarchar(100) '$.address',
        [City] nvarchar(30) '$.city',
        [PostCode] nvarchar(20) '$.postCode',
        [SystemCreatedAt] datetime2(7) '$.systemCreatedAt',
        [SystemModifiedAt] datetime2(7) '$.systemModifiedAt',
        [SystemCreatedBy] uniqueidentifier '$.systemCreatedBy',
        [SystemModifiedBy] uniqueidentifier '$.systemModifiedBy'
    );

    DECLARE @A TABLE ([Action] nvarchar(10) NOT NULL);
    MERGE [bc].[dbo].[Customers] WITH (HOLDLOCK) AS T
    USING #S AS S ON T.[SystemId] = S.[SystemId]
    WHEN MATCHED AND
    (
        T.[CustomerNo] <> S.[CustomerNo] OR
        ISNULL(T.[CustomerName],N'') <> ISNULL(S.[CustomerName],N'') OR
        ISNULL(T.[CorpGroup],N'') <> ISNULL(S.[CorpGroup],N'') OR
        ISNULL(T.[Address],N'') <> ISNULL(S.[Address],N'') OR
        ISNULL(T.[City],N'') <> ISNULL(S.[City],N'') OR
        ISNULL(T.[PostCode],N'') <> ISNULL(S.[PostCode],N'') OR
        T.[SystemCreatedAt] <> S.[SystemCreatedAt] OR
        T.[SystemModifiedAt] <> S.[SystemModifiedAt] OR
        ISNULL(CONVERT(nvarchar(36),T.[SystemCreatedBy]),N'') <> ISNULL(CONVERT(nvarchar(36),S.[SystemCreatedBy]),N'') OR
        ISNULL(CONVERT(nvarchar(36),T.[SystemModifiedBy]),N'') <> ISNULL(CONVERT(nvarchar(36),S.[SystemModifiedBy]),N'')
    )
    THEN UPDATE SET
        [CustomerNo]=S.[CustomerNo],[CustomerName]=S.[CustomerName],
        [CorpGroup]=S.[CorpGroup],[Address]=S.[Address],[City]=S.[City],
        [PostCode]=S.[PostCode],[SystemCreatedAt]=S.[SystemCreatedAt],
        [SystemModifiedAt]=S.[SystemModifiedAt],[SystemCreatedBy]=S.[SystemCreatedBy],
        [SystemModifiedBy]=S.[SystemModifiedBy],[ExtractedAt]=@ExtractedAt
    WHEN NOT MATCHED THEN INSERT
    ([SystemId],[CustomerNo],[CustomerName],[CorpGroup],[Address],[City],[PostCode],
     [SystemCreatedAt],[SystemModifiedAt],[SystemCreatedBy],[SystemModifiedBy],[ExtractedAt])
    VALUES
    (S.[SystemId],S.[CustomerNo],S.[CustomerName],S.[CorpGroup],S.[Address],S.[City],S.[PostCode],
     S.[SystemCreatedAt],S.[SystemModifiedAt],S.[SystemCreatedBy],S.[SystemModifiedBy],@ExtractedAt)
    OUTPUT $action INTO @A;

    SELECT
        (SELECT COUNT(*) FROM #S),
        (SELECT COUNT(*) FROM @A WHERE [Action]=N'INSERT'),
        (SELECT COUNT(*) FROM @A WHERE [Action]=N'UPDATE'),
        (SELECT MAX([SystemModifiedAt]) FROM #S);
END;
GO
