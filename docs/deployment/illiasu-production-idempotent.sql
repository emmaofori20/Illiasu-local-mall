IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011000039_InitialIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011000039_InitialIdentity', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(160) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Categories_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_Categories_Slug] CHECK (LEN(LTRIM(RTRIM([Slug]))) > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE TABLE [UnitsOfMeasure] (
        [Id] uniqueidentifier NOT NULL,
        [Code] varchar(32) COLLATE Latin1_General_100_CI_AS NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Symbol] nvarchar(16) NOT NULL,
        [Kind] int NOT NULL,
        [DecimalPlaces] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_UnitsOfMeasure] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_UnitsOfMeasure_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
        CONSTRAINT [CK_UnitsOfMeasure_Kind] CHECK ([Kind] BETWEEN 0 AND 4),
        CONSTRAINT [CK_UnitsOfMeasure_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_UnitsOfMeasure_Precision] CHECK ([DecimalPlaces] BETWEEN 0 AND 6 AND ([Kind] IN (3,4) OR [DecimalPlaces] = 0)),
        CONSTRAINT [CK_UnitsOfMeasure_Symbol] CHECK (LEN(LTRIM(RTRIM([Symbol]))) > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(180) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [IsActive] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_Products_Slug] CHECK (LEN(LTRIM(RTRIM([Slug]))) > 0),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Sku] varchar(64) COLLATE Latin1_General_100_CI_AS NOT NULL,
        [Barcode] varchar(64) COLLATE Latin1_General_100_CI_AS NULL,
        [UnitOfMeasureId] uniqueidentifier NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [QuantityStep] decimal(18,6) NOT NULL,
        [AllowFractionalQuantity] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsAvailable] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ProductVariants_Id_ProductId] UNIQUE ([Id], [ProductId]),
        CONSTRAINT [AK_ProductVariants_Id_ProductId_UnitOfMeasureId] UNIQUE ([Id], [ProductId], [UnitOfMeasureId]),
        CONSTRAINT [CK_ProductVariants_Barcode] CHECK ([Barcode] IS NULL OR (LEN([Barcode]) > 0 AND DATALENGTH([Barcode]) = DATALENGTH(LTRIM(RTRIM([Barcode]))))),
        CONSTRAINT [CK_ProductVariants_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_ProductVariants_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_ProductVariants_QuantityStep] CHECK ([QuantityStep] > 0 AND ([AllowFractionalQuantity] = 1 OR [QuantityStep] = FLOOR([QuantityStep]))),
        CONSTRAINT [CK_ProductVariants_Sku] CHECK (LEN([Sku]) > 0 AND DATALENGTH([Sku]) = DATALENGTH(LTRIM(RTRIM([Sku])))),
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]),
        CONSTRAINT [FK_ProductVariants_UnitsOfMeasure_UnitOfMeasureId] FOREIGN KEY ([UnitOfMeasureId]) REFERENCES [UnitsOfMeasure] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NULL,
        [Path] nvarchar(1024) NOT NULL,
        [AltText] nvarchar(256) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProductImages_AltText] CHECK (LEN(LTRIM(RTRIM([AltText]))) > 0),
        CONSTRAINT [CK_ProductImages_DisplayOrder] CHECK ([DisplayOrder] >= 0),
        CONSTRAINT [CK_ProductImages_Path] CHECK (LEN(LTRIM(RTRIM([Path]))) > 0),
        CONSTRAINT [FK_ProductImages_ProductVariants_ProductVariantId_ProductId] FOREIGN KEY ([ProductVariantId], [ProductId]) REFERENCES [ProductVariants] ([Id], [ProductId]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE TABLE [ProductUnitConversions] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NOT NULL,
        [FromUnitOfMeasureId] uniqueidentifier NOT NULL,
        [ToUnitOfMeasureId] uniqueidentifier NOT NULL,
        [Factor] decimal(18,6) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ProductUnitConversions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProductUnitConversions_DifferentUnits] CHECK ([FromUnitOfMeasureId] <> [ToUnitOfMeasureId]),
        CONSTRAINT [CK_ProductUnitConversions_Factor] CHECK ([Factor] > 0),
        CONSTRAINT [FK_ProductUnitConversions_ProductVariants_ProductVariantId_ProductId_ToUnitOfMeasureId] FOREIGN KEY ([ProductVariantId], [ProductId], [ToUnitOfMeasureId]) REFERENCES [ProductVariants] ([Id], [ProductId], [UnitOfMeasureId]),
        CONSTRAINT [FK_ProductUnitConversions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]),
        CONSTRAINT [FK_ProductUnitConversions_UnitsOfMeasure_FromUnitOfMeasureId] FOREIGN KEY ([FromUnitOfMeasureId]) REFERENCES [UnitsOfMeasure] ([Id]),
        CONSTRAINT [FK_ProductUnitConversions_UnitsOfMeasure_ToUnitOfMeasureId] FOREIGN KEY ([ToUnitOfMeasureId]) REFERENCES [UnitsOfMeasure] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductVariantId_ProductId] ON [ProductImages] ([ProductVariantId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProductImages_ProductPrimary] ON [ProductImages] ([ProductId]) WHERE [IsPrimary] = 1 AND [ProductVariantId] IS NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProductImages_VariantPrimary] ON [ProductImages] ([ProductId], [ProductVariantId]) WHERE [IsPrimary] = 1 AND [ProductVariantId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_ProductUnitConversions_FromUnitOfMeasureId] ON [ProductUnitConversions] ([FromUnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductUnitConversions_ProductId_ProductVariantId_FromUnitOfMeasureId] ON [ProductUnitConversions] ([ProductId], [ProductVariantId], [FromUnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_ProductUnitConversions_ProductVariantId_ProductId_ToUnitOfMeasureId] ON [ProductUnitConversions] ([ProductVariantId], [ProductId], [ToUnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_ProductUnitConversions_ToUnitOfMeasureId] ON [ProductUnitConversions] ([ToUnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductVariants_Barcode] ON [ProductVariants] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_Sku] ON [ProductVariants] ([Sku]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_UnitOfMeasureId] ON [ProductVariants] ([UnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UnitsOfMeasure_Code] ON [UnitsOfMeasure] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011002028_AddCatalogueFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011002028_AddCatalogueFoundation', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings'
)
BEGIN
    CREATE TABLE [VariantStockMappings] (
        [ProductVariantId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [SoldUnitOfMeasureId] uniqueidentifier NOT NULL,
        [StockVariantId] uniqueidentifier NOT NULL,
        [BaseUnitOfMeasureId] uniqueidentifier NOT NULL,
        [UnitsPerSale] decimal(18,6) NOT NULL,
        CONSTRAINT [PK_VariantStockMappings] PRIMARY KEY ([ProductVariantId]),
        CONSTRAINT [CK_VariantStockMappings_PositiveConsumption] CHECK ([UnitsPerSale] > 0),
        CONSTRAINT [CK_VariantStockMappings_SelfOwned] CHECK ([ProductVariantId] <> [StockVariantId] OR ([UnitsPerSale] = 1 AND [SoldUnitOfMeasureId] = [BaseUnitOfMeasureId])),
        CONSTRAINT [FK_VariantStockMappings_ProductVariants_ProductVariantId_ProductId_SoldUnitOfMeasureId] FOREIGN KEY ([ProductVariantId], [ProductId], [SoldUnitOfMeasureId]) REFERENCES [ProductVariants] ([Id], [ProductId], [UnitOfMeasureId]),
        CONSTRAINT [FK_VariantStockMappings_ProductVariants_StockVariantId_ProductId_BaseUnitOfMeasureId] FOREIGN KEY ([StockVariantId], [ProductId], [BaseUnitOfMeasureId]) REFERENCES [ProductVariants] ([Id], [ProductId], [UnitOfMeasureId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VariantStockMappings_ProductVariantId_ProductId_SoldUnitOfMeasureId] ON [VariantStockMappings] ([ProductVariantId], [ProductId], [SoldUnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings'
)
BEGIN
    CREATE INDEX [IX_VariantStockMappings_StockVariantId] ON [VariantStockMappings] ([StockVariantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings'
)
BEGIN
    CREATE INDEX [IX_VariantStockMappings_StockVariantId_ProductId_BaseUnitOfMeasureId] ON [VariantStockMappings] ([StockVariantId], [ProductId], [BaseUnitOfMeasureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings'
)
BEGIN
    INSERT INTO [dbo].[VariantStockMappings]
                    ([ProductVariantId], [ProductId], [SoldUnitOfMeasureId], [StockVariantId], [BaseUnitOfMeasureId], [UnitsPerSale])
                    SELECT [Id], [ProductId], [UnitOfMeasureId], [Id], [UnitOfMeasureId], CAST(1 AS decimal(18,6))
                    FROM [dbo].[ProductVariants];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172240_AddVariantStockMappings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011172240_AddVariantStockMappings', N'8.0.31');
END;
GO

COMMIT;
GO

