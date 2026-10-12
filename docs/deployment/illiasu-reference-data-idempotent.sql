-- Generated offline from AppRoles and UnitReferenceData.Definitions.
-- Apply ONLY after the reviewed schema script, to the confirmed dbo target.
-- No users, credentials or schema changes. Existing definitions are preserved.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock @Resource = 'ILLiasu.DatabaseProvisioning',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
    IF @lockResult < 0 THROW 50002, 'Provisioning lock unavailable.', 1;
    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
        THROW 50003, 'Apply reviewed schema migrations before reference data.', 1;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261011000039_InitialIdentity') THROW 50003, 'Required migration history missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation') THROW 50003, 'Required migration history missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings') THROW 50003, 'Required migration history missing.', 1;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'ADMIN')
        INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(nvarchar(36), NEWID()), N'Admin', N'ADMIN', CONVERT(nvarchar(36), NEWID()));
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'INVENTORYMANAGER')
        INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(nvarchar(36), NEWID()), N'InventoryManager', N'INVENTORYMANAGER', CONVERT(nvarchar(36), NEWID()));
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'SALESSTAFF')
        INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(nvarchar(36), NEWID()), N'SalesStaff', N'SALESSTAFF', CONVERT(nvarchar(36), NEWID()));
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = N'CUSTOMER')
        INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (CONVERT(nvarchar(36), NEWID()), N'Customer', N'CUSTOMER', CONVERT(nvarchar(36), NEWID()));
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'PCS')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'PCS', N'Piece', N'PCS', 0, 0, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'PACK')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'PACK', N'Pack', N'PACK', 1, 0, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'CTN')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'CTN', N'Carton', N'CTN', 2, 0, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'TRAY')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'TRAY', N'Tray', N'TRAY', 1, 0, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'BAG')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'BAG', N'Bag', N'BAG', 1, 0, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'BTL')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'BTL', N'Bottle', N'BTL', 0, 0, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'KG')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'KG', N'Kilogram', N'KG', 3, 3, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'G')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'G', N'Gram', N'G', 3, 3, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'L')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'L', N'Litre', N'L', 4, 3, 1);
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UnitsOfMeasure] WHERE [Code] = N'ML')
        INSERT INTO [dbo].[UnitsOfMeasure] ([Id], [Code], [Name], [Symbol], [Kind], [DecimalPlaces], [IsActive]) VALUES (NEWID(), N'ML', N'Millilitre', N'ML', 4, 3, 1);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
