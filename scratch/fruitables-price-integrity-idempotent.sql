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
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Image] nvarchar(255) NULL,
        [ParentId] int NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [ContactMessages] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Type] int NOT NULL,
        [Value] decimal(10,2) NOT NULL,
        [MinOrderAmount] decimal(10,2) NOT NULL,
        [MaxUses] int NULL,
        [UsedCount] int NOT NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [ProductTags] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [Slug] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ProductTags] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Settings] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(max) NULL,
        [Group] nvarchar(50) NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [Password] nvarchar(255) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Avatar] nvarchar(255) NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Slug] nvarchar(255) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ShortDescription] nvarchar(500) NULL,
        [Price] decimal(10,2) NOT NULL,
        [SalePrice] decimal(10,2) NULL,
        [Unit] nvarchar(20) NOT NULL,
        [Weight] decimal(10,2) NULL,
        [CountryOrigin] nvarchar(100) NULL,
        [Quality] nvarchar(50) NULL,
        [StockQuantity] int NOT NULL,
        [MinOrderQuantity] int NOT NULL,
        [IsFeatured] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Addresses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CompanyName] nvarchar(255) NULL,
        [AddressLine] nvarchar(500) NOT NULL,
        [City] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [Postcode] nvarchar(20) NOT NULL,
        [IsDefault] bit NOT NULL,
        CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Addresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [SessionId] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(10,2) NOT NULL,
        [ShippingFee] decimal(10,2) NOT NULL,
        [Discount] decimal(10,2) NOT NULL,
        [Total] decimal(10,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [PaymentStatus] int NOT NULL,
        [ShippingMethod] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Testimonials] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [Name] nvarchar(100) NOT NULL,
        [Profession] nvarchar(100) NULL,
        [Avatar] nvarchar(255) NULL,
        [Content] nvarchar(max) NOT NULL,
        [Rating] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Testimonials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Testimonials_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ImageUrl] nvarchar(255) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [ProductTagMapping] (
        [ProductsId] int NOT NULL,
        [TagsId] int NOT NULL,
        CONSTRAINT [PK_ProductTagMapping] PRIMARY KEY ([ProductsId], [TagsId]),
        CONSTRAINT [FK_ProductTagMapping_ProductTags_TagsId] FOREIGN KEY ([TagsId]) REFERENCES [ProductTags] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductTagMapping_Products_ProductsId] FOREIGN KEY ([ProductsId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [UserId] int NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [Wishlists] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ProductId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Wishlists] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Wishlists_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Wishlists_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] int NOT NULL IDENTITY,
        [CartId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [OrderAddresses] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Type] int NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [CompanyName] nvarchar(255) NULL,
        [AddressLine] nvarchar(500) NOT NULL,
        [City] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NOT NULL,
        [Postcode] nvarchar(20) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        CONSTRAINT [PK_OrderAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderAddresses_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [ProductName] nvarchar(255) NOT NULL,
        [Quantity] int NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [Total] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Addresses_UserId] ON [Addresses] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_CartItems_CartId] ON [CartItems] ([CartId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductId] ON [CartItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]) WHERE [UserId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_OrderAddresses_OrderId] ON [OrderAddresses] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_ProductTagMapping_TagsId] ON [ProductTagMapping] ([TagsId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Reviews_ProductId] ON [Reviews] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settings_Key] ON [Settings] ([Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Testimonials_UserId] ON [Testimonials] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE INDEX [IX_Wishlists_ProductId] ON [Wishlists] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wishlists_UserId_ProductId] ON [Wishlists] ([UserId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251216123549_AddUserRoleAndStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251216123549_AddUserRoleAndStatus', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217022755_SeedAdminUsers'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Avatar', N'CreatedAt', N'Email', N'FirstName', N'IsActive', N'LastLoginAt', N'LastName', N'Password', N'Phone', N'Role', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] ON;
    EXEC(N'INSERT INTO [Users] ([Id], [Avatar], [CreatedAt], [Email], [FirstName], [IsActive], [LastLoginAt], [LastName], [Password], [Phone], [Role], [UpdatedAt])
    VALUES (1, NULL, ''2024-01-01T00:00:00.0000000Z'', N''admin@fruitables.com'', N''Admin'', CAST(1 AS bit), NULL, N''User'', N''$2a$11$lA/jMR6h6Qga83lrdc0xd.Fx1TLBOiefaI1vAvCcVTjhYFqTYisHO'', NULL, 1, ''2024-01-01T00:00:00.0000000Z''),
    (2, NULL, ''2024-01-01T00:00:00.0000000Z'', N''superadmin@fruitables.com'', N''Super'', CAST(1 AS bit), NULL, N''Admin'', N''$2a$11$lA/jMR6h6Qga83lrdc0xd.Fx1TLBOiefaI1vAvCcVTjhYFqTYisHO'', NULL, 2, ''2024-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Avatar', N'CreatedAt', N'Email', N'FirstName', N'IsActive', N'LastLoginAt', N'LastName', N'Password', N'Phone', N'Role', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217022755_SeedAdminUsers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251217022755_SeedAdminUsers', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217051246_AddCategorySortOrderAndTimestamps'
)
BEGIN
    ALTER TABLE [Categories] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217051246_AddCategorySortOrderAndTimestamps'
)
BEGIN
    ALTER TABLE [Categories] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217051246_AddCategorySortOrderAndTimestamps'
)
BEGIN
    ALTER TABLE [Categories] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217051246_AddCategorySortOrderAndTimestamps'
)
BEGIN
    ALTER TABLE [Categories] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217051246_AddCategorySortOrderAndTimestamps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251217051246_AddCategorySortOrderAndTimestamps', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217060607_AddCategorySoftDelete'
)
BEGIN
    ALTER TABLE [Categories] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217060607_AddCategorySoftDelete'
)
BEGIN
    ALTER TABLE [Categories] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217060607_AddCategorySoftDelete'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251217060607_AddCategorySoftDelete', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    ALTER TABLE [Products] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    ALTER TABLE [Products] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    ALTER TABLE [Products] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE TABLE [ProductLogs] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NULL,
        [AdminId] int NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Details] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductLogs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ProductLogs_Users_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [SKU] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [SalePrice] decimal(10,2) NULL,
        [StockQuantity] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE INDEX [IX_ProductLogs_AdminId] ON [ProductLogs] ([AdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE INDEX [IX_ProductLogs_CreatedAt] ON [ProductLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE INDEX [IX_ProductLogs_ProductId] ON [ProductLogs] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_SKU] ON [ProductVariants] ([SKU]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251217153420_AddProductVariantAndLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251217153420_AddProductVariantAndLog', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    ALTER TABLE [Addresses] DROP CONSTRAINT [FK_Addresses_Users_UserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'City');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [City];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'CompanyName');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [CompanyName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'Country');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [Country];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    EXEC sp_rename N'[Addresses].[Postcode]', N'Phone', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'UserId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Addresses] ALTER COLUMN [UserId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    ALTER TABLE [Addresses] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    ALTER TABLE [Addresses] ADD [FullName] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    ALTER TABLE [Addresses] ADD CONSTRAINT [FK_Addresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218042228_UpdateAddressModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218042228_UpdateAddressModel', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218043544_MakeCountryPostcodeNullable'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderAddresses]') AND [c].[name] = N'Postcode');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [OrderAddresses] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [OrderAddresses] ALTER COLUMN [Postcode] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218043544_MakeCountryPostcodeNullable'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderAddresses]') AND [c].[name] = N'Email');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [OrderAddresses] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [OrderAddresses] ALTER COLUMN [Email] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218043544_MakeCountryPostcodeNullable'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderAddresses]') AND [c].[name] = N'Country');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [OrderAddresses] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [OrderAddresses] ALTER COLUMN [Country] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218043544_MakeCountryPostcodeNullable'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderAddresses]') AND [c].[name] = N'City');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [OrderAddresses] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [OrderAddresses] ALTER COLUMN [City] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218043544_MakeCountryPostcodeNullable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218043544_MakeCountryPostcodeNullable', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218044717_AddAddressReferenceToOrder'
)
BEGIN
    ALTER TABLE [Orders] ADD [AddressId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218044717_AddAddressReferenceToOrder'
)
BEGIN
    ALTER TABLE [Orders] ADD [ShippingSnapshot] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218044717_AddAddressReferenceToOrder'
)
BEGIN
    CREATE INDEX [IX_Orders_AddressId] ON [Orders] ([AddressId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218044717_AddAddressReferenceToOrder'
)
BEGIN
    ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_Addresses_AddressId] FOREIGN KEY ([AddressId]) REFERENCES [Addresses] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218044717_AddAddressReferenceToOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218044717_AddAddressReferenceToOrder', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218061504_RemoveOrderAddressTable'
)
BEGIN
    DROP TABLE [OrderAddresses];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218061504_RemoveOrderAddressTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218061504_RemoveOrderAddressTable', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218063029_AddOrderStatusHistoryAndRowVersion'
)
BEGIN
    ALTER TABLE [Orders] ADD [RowVersion] rowversion NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218063029_AddOrderStatusHistoryAndRowVersion'
)
BEGIN
    CREATE TABLE [OrderStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [AdminId] int NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistories_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderStatusHistories_Users_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218063029_AddOrderStatusHistoryAndRowVersion'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_AdminId] ON [OrderStatusHistories] ([AdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218063029_AddOrderStatusHistoryAndRowVersion'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_CreatedAt] ON [OrderStatusHistories] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218063029_AddOrderStatusHistoryAndRowVersion'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218063029_AddOrderStatusHistoryAndRowVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218063029_AddOrderStatusHistoryAndRowVersion', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218090522_AddGoogleIdToUser'
)
BEGIN
    ALTER TABLE [Users] ADD [GoogleId] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218090522_AddGoogleIdToUser'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [GoogleId] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218090522_AddGoogleIdToUser'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [GoogleId] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218090522_AddGoogleIdToUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218090522_AddGoogleIdToUser', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218094936_MergeFirstNameLastNameToName'
)
BEGIN
    ALTER TABLE [Users] ADD [Name] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218094936_MergeFirstNameLastNameToName'
)
BEGIN
    UPDATE Users SET Name = CONCAT(FirstName, ' ', LastName)
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218094936_MergeFirstNameLastNameToName'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Name');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Users] ALTER COLUMN [Name] nvarchar(200) NOT NULL;
    ALTER TABLE [Users] ADD DEFAULT N'' FOR [Name];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218094936_MergeFirstNameLastNameToName'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'FirstName');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Users] DROP COLUMN [FirstName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218094936_MergeFirstNameLastNameToName'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'LastName');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Users] DROP COLUMN [LastName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218094936_MergeFirstNameLastNameToName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218094936_MergeFirstNameLastNameToName', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219005507_AddCancelReasonToOrder'
)
BEGIN
    ALTER TABLE [Orders] ADD [CancelReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219005507_AddCancelReasonToOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251219005507_AddCancelReasonToOrder', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219010725_AddOrderHistoryIndexes'
)
BEGIN
    DROP INDEX [IX_Orders_UserId] ON [Orders];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219010725_AddOrderHistoryIndexes'
)
BEGIN
    CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219010725_AddOrderHistoryIndexes'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219010725_AddOrderHistoryIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251219010725_AddOrderHistoryIndexes', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    CREATE TABLE [OrderStatusAuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [AdminId] int NOT NULL,
        [AdminName] nvarchar(100) NOT NULL,
        [AdminEmail] nvarchar(255) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [OldOrderStatus] int NOT NULL,
        [OldPaymentStatus] int NOT NULL,
        [NewOrderStatus] int NOT NULL,
        [NewPaymentStatus] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        CONSTRAINT [PK_OrderStatusAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusAuditLogs_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    CREATE TABLE [AuditLogAttachments] (
        [Id] int NOT NULL IDENTITY,
        [AuditLogId] int NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] bigint NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogAttachments_OrderStatusAuditLogs_AuditLogId] FOREIGN KEY ([AuditLogId]) REFERENCES [OrderStatusAuditLogs] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogAttachments_AuditLogId] ON [AuditLogAttachments] ([AuditLogId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    CREATE INDEX [IX_OrderStatusAuditLogs_AdminId] ON [OrderStatusAuditLogs] ([AdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    CREATE INDEX [IX_OrderStatusAuditLogs_CreatedAt] ON [OrderStatusAuditLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    CREATE INDEX [IX_OrderStatusAuditLogs_OrderId] ON [OrderStatusAuditLogs] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251219160938_AddOrderStatusAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251219160938_AddOrderStatusAuditLog', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD [CurrentLockType] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD [LockExpiresAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD [LockReason] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD [LockViolationType] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD [LockedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD [LockedByAdminId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    CREATE TABLE [UserAccountLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [AdminId] int NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [LockType] int NULL,
        [ViolationType] nvarchar(200) NULL,
        [Reason] nvarchar(1000) NULL,
        [ExpiresAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IpAddress] nvarchar(50) NULL,
        [UserAgent] nvarchar(500) NULL,
        CONSTRAINT [PK_UserAccountLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAccountLogs_Users_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserAccountLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [CurrentLockType] = NULL, [LockExpiresAt] = NULL, [LockReason] = NULL, [LockViolationType] = NULL, [LockedAt] = NULL, [LockedByAdminId] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [CurrentLockType] = NULL, [LockExpiresAt] = NULL, [LockReason] = NULL, [LockViolationType] = NULL, [LockedAt] = NULL, [LockedByAdminId] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    CREATE INDEX [IX_Users_LockedByAdminId] ON [Users] ([LockedByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    CREATE INDEX [IX_UserAccountLogs_AdminId] ON [UserAccountLogs] ([AdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    CREATE INDEX [IX_UserAccountLogs_CreatedAt] ON [UserAccountLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    CREATE INDEX [IX_UserAccountLogs_UserId] ON [UserAccountLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_LockedByAdminId] FOREIGN KEY ([LockedByAdminId]) REFERENCES [Users] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221011803_AddUserAccountLogAndLockFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251221011803_AddUserAccountLogAndLockFields', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    DELETE FROM Addresses
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'AddressLine');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [AddressLine];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [DistrictCode] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [DistrictName] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [ProvinceCode] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [ProvinceName] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [StreetAddress] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [UpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [WardCode] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [WardName] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222112527_ReplaceAddressLineWithStructuredFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251222112527_ReplaceAddressLineWithStructuredFields', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Module] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE TABLE [RbacAuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [Action] nvarchar(50) NOT NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [EntityId] int NOT NULL,
        [ChangedByAdminId] int NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [OldValue] nvarchar(2000) NULL,
        [NewValue] nvarchar(2000) NULL,
        CONSTRAINT [PK_RbacAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RbacAuditLogs_Users_ChangedByAdminId] FOREIGN KEY ([ChangedByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedByAdminId] int NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Users_AssignedByAdminId] FOREIGN KEY ([AssignedByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE TABLE [UserRoleMappings] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedByAdminId] int NULL,
        CONSTRAINT [PK_UserRoleMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoleMappings_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoleMappings_Users_AssignedByAdminId] FOREIGN KEY ([AssignedByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoleMappings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_Permissions_Module] ON [Permissions] ([Module]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Name] ON [Permissions] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_RbacAuditLogs_ChangedAt] ON [RbacAuditLogs] ([ChangedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_RbacAuditLogs_ChangedByAdminId] ON [RbacAuditLogs] ([ChangedByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_RbacAuditLogs_EntityType_EntityId] ON [RbacAuditLogs] ([EntityType], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_AssignedByAdminId] ON [RolePermissions] ([AssignedByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_RoleId] ON [RolePermissions] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions] ([RoleId], [PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_UserRoleMappings_AssignedByAdminId] ON [UserRoleMappings] ([AssignedByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_UserRoleMappings_RoleId] ON [UserRoleMappings] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE INDEX [IX_UserRoleMappings_UserId] ON [UserRoleMappings] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoleMappings_UserId_RoleId] ON [UserRoleMappings] ([UserId], [RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'Description', N'IsActive', N'CreatedAt', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Name], [Description], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES (N''Customer'', N''Khách hàng thông thường'', CAST(1 AS bit), ''2024-01-01T00:00:00.0000000Z'', ''2024-01-01T00:00:00.0000000Z''),
    (N''Admin'', N''Quản trị viên'', CAST(1 AS bit), ''2024-01-01T00:00:00.0000000Z'', ''2024-01-01T00:00:00.0000000Z''),
    (N''SuperAdmin'', N''Quản trị viên cấp cao'', CAST(1 AS bit), ''2024-01-01T00:00:00.0000000Z'', ''2024-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'Description', N'IsActive', N'CreatedAt', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'Description', N'Module', N'CreatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] ON;
    EXEC(N'INSERT INTO [Permissions] ([Name], [Description], [Module], [CreatedAt])
    VALUES (N''products.view'', N''Xem danh sách và chi tiết sản phẩm'', N''products'', ''2024-01-01T00:00:00.0000000Z''),
    (N''products.create'', N''Tạo sản phẩm mới'', N''products'', ''2024-01-01T00:00:00.0000000Z''),
    (N''products.update'', N''Cập nhật sản phẩm'', N''products'', ''2024-01-01T00:00:00.0000000Z''),
    (N''products.delete'', N''Xóa sản phẩm'', N''products'', ''2024-01-01T00:00:00.0000000Z''),
    (N''products.manage_inventory'', N''Quản lý tồn kho'', N''products'', ''2024-01-01T00:00:00.0000000Z''),
    (N''orders.view_all'', N''Xem tất cả đơn hàng'', N''orders'', ''2024-01-01T00:00:00.0000000Z''),
    (N''orders.view_own'', N''Chỉ xem đơn hàng của mình'', N''orders'', ''2024-01-01T00:00:00.0000000Z''),
    (N''orders.create'', N''Tạo đơn hàng mới'', N''orders'', ''2024-01-01T00:00:00.0000000Z''),
    (N''orders.update_status'', N''Cập nhật trạng thái đơn hàng'', N''orders'', ''2024-01-01T00:00:00.0000000Z''),
    (N''orders.cancel'', N''Hủy đơn hàng'', N''orders'', ''2024-01-01T00:00:00.0000000Z''),
    (N''orders.refund'', N''Xử lý hoàn tiền'', N''orders'', ''2024-01-01T00:00:00.0000000Z''),
    (N''users.view'', N''Xem danh sách và chi tiết người dùng'', N''users'', ''2024-01-01T00:00:00.0000000Z''),
    (N''users.create'', N''Tạo người dùng mới'', N''users'', ''2024-01-01T00:00:00.0000000Z''),
    (N''users.update'', N''Cập nhật thông tin người dùng'', N''users'', ''2024-01-01T00:00:00.0000000Z''),
    (N''users.lock'', N''Khóa tài khoản người dùng'', N''users'', ''2024-01-01T00:00:00.0000000Z''),
    (N''users.unlock'', N''Mở khóa tài khoản người dùng'', N''users'', ''2024-01-01T00:00:00.0000000Z''),
    (N''users.delete'', N''Xóa người dùng'', N''users'', ''2024-01-01T00:00:00.0000000Z''),
    (N''reviews.view'', N''Xem tất cả đánh giá'', N''reviews'', ''2024-01-01T00:00:00.0000000Z''),
    (N''reviews.create'', N''Tạo đánh giá'', N''reviews'', ''2024-01-01T00:00:00.0000000Z''),
    (N''reviews.moderate'', N''Kiểm duyệt đánh giá'', N''reviews'', ''2024-01-01T00:00:00.0000000Z''),
    (N''reviews.delete'', N''Xóa đánh giá'', N''reviews'', ''2024-01-01T00:00:00.0000000Z''),
    (N''settings.view'', N''Xem cài đặt hệ thống'', N''settings'', ''2024-01-01T00:00:00.0000000Z''),
    (N''settings.update'', N''Cập nhật cài đặt hệ thống'', N''settings'', ''2024-01-01T00:00:00.0000000Z''),
    (N''dashboard.view'', N''Xem dashboard quản trị'', N''dashboard'', ''2024-01-01T00:00:00.0000000Z''),
    (N''dashboard.view_statistics'', N''Xem thống kê chi tiết'', N''dashboard'', ''2024-01-01T00:00:00.0000000Z''),
    (N''system.manage'', N''Quản lý toàn bộ hệ thống'', N''system'', ''2024-01-01T00:00:00.0000000Z''),
    (N''system.view_logs'', N''Xem nhật ký hệ thống'', N''system'', ''2024-01-01T00:00:00.0000000Z''),
    (N''system.manage_rbac'', N''Quản lý vai trò và quyền hạn'', N''system'', ''2024-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'Description', N'Module', N'CreatedAt') AND [object_id] = OBJECT_ID(N'[Permissions]'))
        SET IDENTITY_INSERT [Permissions] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN

                    INSERT INTO RolePermissions (RoleId, PermissionId, AssignedAt)
                    SELECT 1, Id, GETUTCDATE()
                    FROM Permissions
                    WHERE Name IN ('products.view', 'orders.view_own', 'orders.create', 'reviews.create')
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN

                    INSERT INTO RolePermissions (RoleId, PermissionId, AssignedAt)
                    SELECT 2, Id, GETUTCDATE()
                    FROM Permissions
                    WHERE Name != 'system.manage'
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN

                    INSERT INTO RolePermissions (RoleId, PermissionId, AssignedAt)
                    SELECT 3, Id, GETUTCDATE()
                    FROM Permissions
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219030531_AddRbacTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260219030531_AddRbacTables', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] DROP CONSTRAINT [FK_Reviews_Users_UserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reviews]') AND [c].[name] = N'Comment');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Reviews] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [Reviews] ALTER COLUMN [Comment] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [DeletedByAdminId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [HelpfulCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [HiddenAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [HiddenByAdminId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [HiddenReason] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [IsHidden] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [IsVerifiedPurchase] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [ReportCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD [UpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Products] ADD [AverageRating] decimal(3,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Products] ADD [ReviewCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE TABLE [ReviewReports] (
        [Id] int NOT NULL IDENTITY,
        [ReviewId] int NOT NULL,
        [ReportedByUserId] int NOT NULL,
        [Reason] int NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [HandledByAdminId] int NULL,
        [HandledAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReviewReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewReports_Reviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [Reviews] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReviewReports_Users_HandledByAdminId] FOREIGN KEY ([HandledByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReviewReports_Users_ReportedByUserId] FOREIGN KEY ([ReportedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_Reviews_CreatedAt] ON [Reviews] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_Reviews_DeletedByAdminId] ON [Reviews] ([DeletedByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_Reviews_HiddenByAdminId] ON [Reviews] ([HiddenByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Reviews_ProductId_Status_IsHidden] ON [Reviews] ([ProductId], [Status], [IsHidden]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_Reviews_Rating] ON [Reviews] ([Rating]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_Reviews_Status_IsHidden_IsDeleted] ON [Reviews] ([Status], [IsHidden], [IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_ReviewReports_CreatedAt] ON [ReviewReports] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_ReviewReports_HandledByAdminId] ON [ReviewReports] ([HandledByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReviewReports_ReportedByUserId_ReviewId] ON [ReviewReports] ([ReportedByUserId], [ReviewId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_ReviewReports_ReviewId] ON [ReviewReports] ([ReviewId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    CREATE INDEX [IX_ReviewReports_Status] ON [ReviewReports] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD CONSTRAINT [FK_Reviews_Users_DeletedByAdminId] FOREIGN KEY ([DeletedByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD CONSTRAINT [FK_Reviews_Users_HiddenByAdminId] FOREIGN KEY ([HiddenByAdminId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    ALTER TABLE [Reviews] ADD CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132304_AddReviewEnhancements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260220132304_AddReviewEnhancements', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302065448_AddResetPasswordFieldsToUser'
)
BEGIN
    ALTER TABLE [Users] ADD [ResetPasswordToken] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302065448_AddResetPasswordFieldsToUser'
)
BEGIN
    ALTER TABLE [Users] ADD [ResetPasswordTokenExpiresAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302065448_AddResetPasswordFieldsToUser'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [ResetPasswordToken] = NULL, [ResetPasswordTokenExpiresAt] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302065448_AddResetPasswordFieldsToUser'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [ResetPasswordToken] = NULL, [ResetPasswordTokenExpiresAt] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302065448_AddResetPasswordFieldsToUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260302065448_AddResetPasswordFieldsToUser', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305044954_AddReviewHelpful'
)
BEGIN
    CREATE TABLE [ReviewHelpfuls] (
        [Id] int NOT NULL IDENTITY,
        [ReviewId] int NOT NULL,
        [UserId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReviewHelpfuls] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewHelpfuls_Reviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [Reviews] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReviewHelpfuls_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305044954_AddReviewHelpful'
)
BEGIN
    CREATE INDEX [IX_ReviewHelpfuls_ReviewId] ON [ReviewHelpfuls] ([ReviewId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305044954_AddReviewHelpful'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReviewHelpfuls_UserId_ReviewId] ON [ReviewHelpfuls] ([UserId], [ReviewId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305044954_AddReviewHelpful'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305044954_AddReviewHelpful', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305145612_ReplaceAuditLogWithOrderNote'
)
BEGIN
    DROP TABLE [AuditLogAttachments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305145612_ReplaceAuditLogWithOrderNote'
)
BEGIN
    DROP TABLE [OrderStatusAuditLogs];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305145612_ReplaceAuditLogWithOrderNote'
)
BEGIN
    CREATE TABLE [OrderNotes] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [AdminId] int NOT NULL,
        [AdminName] nvarchar(100) NOT NULL,
        [Content] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderNotes_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305145612_ReplaceAuditLogWithOrderNote'
)
BEGIN
    CREATE INDEX [IX_OrderNotes_CreatedAt] ON [OrderNotes] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305145612_ReplaceAuditLogWithOrderNote'
)
BEGIN
    CREATE INDEX [IX_OrderNotes_OrderId] ON [OrderNotes] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305145612_ReplaceAuditLogWithOrderNote'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305145612_ReplaceAuditLogWithOrderNote', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260325141903_AddCouponMinQuantityAndCartCoupon'
)
BEGIN
    ALTER TABLE [Coupons] ADD [MinQuantity] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260325141903_AddCouponMinQuantityAndCartCoupon'
)
BEGIN
    ALTER TABLE [Carts] ADD [CouponCode] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260325141903_AddCouponMinQuantityAndCartCoupon'
)
BEGIN
    ALTER TABLE [Carts] ADD [CouponDiscount] decimal(10,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260325141903_AddCouponMinQuantityAndCartCoupon'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260325141903_AddCouponMinQuantityAndCartCoupon', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'DistrictCode');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [DistrictCode];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'DistrictName');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [DistrictName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'WardCode');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [Addresses] DROP COLUMN [WardCode];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    EXEC sp_rename N'[Addresses].[WardName]', N'CommuneName', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'ProvinceCode');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [Addresses] ALTER COLUMN [ProvinceCode] nvarchar(20) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    ALTER TABLE [Addresses] ADD [CommuneCode] nvarchar(20) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606130104_SwitchToTwoLevelAddress'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606130104_SwitchToTwoLevelAddress', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703034517_AddGhnAddressFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [GhnDistrictId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703034517_AddGhnAddressFields'
)
BEGIN
    ALTER TABLE [Addresses] ADD [GhnWardCode] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260703034517_AddGhnAddressFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260703034517_AddGhnAddressFields', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PaymentCode] nvarchar(16) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    CREATE TABLE [SePayTransactions] (
        [Id] int NOT NULL IDENTITY,
        [SePayTransactionId] bigint NOT NULL,
        [OrderId] int NULL,
        [PaymentCode] nvarchar(16) NULL,
        [TransferAmount] decimal(10,2) NOT NULL,
        [ReferenceCode] nvarchar(100) NULL,
        [Status] int NOT NULL,
        [Message] nvarchar(500) NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SePayTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SePayTransactions_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_PaymentCode] ON [Orders] ([PaymentCode]) WHERE [PaymentCode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    CREATE INDEX [IX_SePayTransactions_OrderId] ON [SePayTransactions] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    CREATE INDEX [IX_SePayTransactions_PaymentCode] ON [SePayTransactions] ([PaymentCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SePayTransactions_SePayTransactionId] ON [SePayTransactions] ([SePayTransactionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704090350_AddSePayPayment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260704090350_AddSePayPayment', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE TABLE [ChatSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastMessageAt] datetime2 NOT NULL,
        [Source] nvarchar(20) NULL,
        CONSTRAINT [PK_ChatSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE TABLE [Faqs] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Faqs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE TABLE [KnowledgeChunks] (
        [Id] bigint NOT NULL IDENTITY,
        [SourceType] int NOT NULL,
        [SourceId] nvarchar(64) NOT NULL,
        [Title] nvarchar(300) NULL,
        [Content] nvarchar(max) NOT NULL,
        [EmbeddingJson] nvarchar(max) NOT NULL,
        [ContentHash] nvarchar(64) NOT NULL,
        [IsActive] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_KnowledgeChunks] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [SessionId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [MetaJson] nvarchar(max) NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_ChatSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [ChatSessions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SessionId] ON [ChatMessages] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_ChatSessions_LastMessageAt] ON [ChatSessions] ([LastMessageAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_ChatSessions_UserId] ON [ChatSessions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_Faqs_Category] ON [Faqs] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_Faqs_IsActive] ON [Faqs] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_KnowledgeChunks_IsActive] ON [KnowledgeChunks] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    CREATE INDEX [IX_KnowledgeChunks_SourceType_SourceId] ON [KnowledgeChunks] ([SourceType], [SourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712005131_AddChatRagTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712005131_AddChatRagTables', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712011305_SeedChatFaqs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Body', N'Category', N'CreatedAt', N'IsActive', N'Title', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Faqs]'))
        SET IDENTITY_INSERT [Faqs] ON;
    EXEC(N'INSERT INTO [Faqs] ([Id], [Body], [Category], [CreatedAt], [IsActive], [Title], [UpdatedAt])
    VALUES (1, N''Phí vận chuyển được tính theo khu vực: nội thành (zone 1), các tỉnh lân cận (zone 2) và các tỉnh xa (zone 3). Đơn hàng đạt ngưỡng miễn phí ship sẽ được miễn phí vận chuyển. Chi tiết phí hiển thị khi bạn chọn địa chỉ giao hàng ở bước thanh toán.'', N''shipping'', ''2026-07-01T00:00:00.0000000Z'', CAST(1 AS bit), N''Phí vận chuyển như thế nào?'', ''2026-07-01T00:00:00.0000000Z''),
    (2, N''Fruitables hỗ trợ thanh toán qua SePay QR khi checkout. Sau khi đặt hàng, bạn quét mã QR để chuyển khoản; hệ thống tự xác nhận thanh toán khi nhận được giao dịch.'', N''payment'', ''2026-07-01T00:00:00.0000000Z'', CAST(1 AS bit), N''Thanh toán bằng cách nào?'', ''2026-07-01T00:00:00.0000000Z''),
    (3, N''Rau củ tươi nên bảo quản trong tủ lạnh (ngăn mát), để trong túi hoặc hộp thoáng khí, tránh để gần trái cây chín. Dùng sớm trong vài ngày để giữ độ tươi ngon tốt nhất.'', N''product-care'', ''2026-07-01T00:00:00.0000000Z'', CAST(1 AS bit), N''Bảo quản rau củ tươi như thế nào?'', ''2026-07-01T00:00:00.0000000Z''),
    (4, N''Bạn có thể xem giờ làm việc và thông tin liên hệ (điện thoại, email, địa chỉ) trên trang Liên hệ hoặc phần chân trang website. Chúng tôi sẵn sàng hỗ trợ trong khung giờ làm việc đã công bố.'', N''hours'', ''2026-07-01T00:00:00.0000000Z'', CAST(1 AS bit), N''Giờ làm việc và liên hệ?'', ''2026-07-01T00:00:00.0000000Z''),
    (5, N''Đăng nhập tài khoản, vào mục Lịch sử đơn hàng để xem trạng thái, chi tiết và theo dõi đơn. Bạn cần đăng nhập để xem các đơn gắn với tài khoản của mình.'', N''order'', ''2026-07-01T00:00:00.0000000Z'', CAST(1 AS bit), N''Làm sao để kiểm tra đơn hàng?'', ''2026-07-01T00:00:00.0000000Z''),
    (6, N''Nếu sản phẩm bị lỗi hoặc không đúng mô tả, vui lòng liên hệ CSKH trong vòng 24 giờ kể từ khi nhận hàng để được hỗ trợ đổi trả. Giữ nguyên bao bì và chụp ảnh minh chứng nếu có.'', N''return'', ''2026-07-01T00:00:00.0000000Z'', CAST(1 AS bit), N''Chính sách đổi trả như thế nào?'', ''2026-07-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Body', N'Category', N'CreatedAt', N'IsActive', N'Title', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Faqs]'))
        SET IDENTITY_INSERT [Faqs] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712011305_SeedChatFaqs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712011305_SeedChatFaqs', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712100443_AddSearchHotKeywords'
)
BEGIN
    CREATE TABLE [SearchHotKeywords] (
        [Id] int NOT NULL IDENTITY,
        [Text] nvarchar(100) NOT NULL,
        [NormalizedText] nvarchar(100) NOT NULL,
        [Weight] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SearchHotKeywords] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712100443_AddSearchHotKeywords'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'IsActive', N'NormalizedText', N'Text', N'Weight') AND [object_id] = OBJECT_ID(N'[SearchHotKeywords]'))
        SET IDENTITY_INSERT [SearchHotKeywords] ON;
    EXEC(N'INSERT INTO [SearchHotKeywords] ([Id], [CreatedAt], [IsActive], [NormalizedText], [Text], [Weight])
    VALUES (1, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''tao'', N''táo'', 100),
    (2, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''cam'', N''cam'', 90),
    (3, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''nho'', N''nho'', 80),
    (4, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''dau'', N''dâu'', 80),
    (5, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''rau cu'', N''rau củ'', 95),
    (6, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''trai cay'', N''trái cây'', 95),
    (7, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''combo'', N''combo'', 85),
    (8, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''tao fuji'', N''táo fuji'', 70),
    (9, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''chuoi'', N''chuối'', 70),
    (10, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''bo'', N''bơ'', 70),
    (11, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''xoai'', N''xoài'', 70),
    (12, ''2026-07-12T00:00:00.0000000Z'', CAST(1 AS bit), N''nuoc ep'', N''nước ép'', 60)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'IsActive', N'NormalizedText', N'Text', N'Weight') AND [object_id] = OBJECT_ID(N'[SearchHotKeywords]'))
        SET IDENTITY_INSERT [SearchHotKeywords] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712100443_AddSearchHotKeywords'
)
BEGIN
    CREATE INDEX [IX_SearchHotKeywords_IsActive] ON [SearchHotKeywords] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712100443_AddSearchHotKeywords'
)
BEGIN
    CREATE INDEX [IX_SearchHotKeywords_NormalizedText] ON [SearchHotKeywords] ([NormalizedText]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712100443_AddSearchHotKeywords'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712100443_AddSearchHotKeywords', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_CartItems_CartId'
          AND object_id = OBJECT_ID(N'[dbo].[CartItems]')
    )
        DROP INDEX [IX_CartItems_CartId] ON [dbo].[CartItems];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [ProductVariantId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [VariantName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [VariantSKU] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ALTER TABLE [CartItems] ADD [ProductVariantId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    CREATE TABLE [PriceSchedules] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ProductVariantId] int NULL,
        [DiscountType] int NOT NULL,
        [Value] decimal(10,2) NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [EndsAt] datetimeoffset NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedByAdminId] int NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PriceSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceSchedules_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PriceSchedules_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PriceSchedules_Users_CreatedByAdminId] FOREIGN KEY ([CreatedByAdminId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

    INSERT INTO PriceSchedules
        (ProductId, ProductVariantId, DiscountType, Value, StartsAt, EndsAt,
         IsCancelled, CreatedByAdminId, CreatedAt, UpdatedAt)
    SELECT p.Id, NULL, 0, p.SalePrice, @now, NULL, 0, NULL, @now, @now
    FROM Products p
    WHERE p.SalePrice IS NOT NULL AND p.SalePrice >= 0 AND p.SalePrice < p.Price
      AND NOT EXISTS (
          SELECT 1 FROM ProductVariants v WHERE v.ProductId = p.Id AND v.IsActive = 1
      );

    INSERT INTO PriceSchedules
        (ProductId, ProductVariantId, DiscountType, Value, StartsAt, EndsAt,
         IsCancelled, CreatedByAdminId, CreatedAt, UpdatedAt)
    SELECT ProductId, Id, 0, SalePrice, @now, NULL, 0, NULL, @now, @now
    FROM ProductVariants
    WHERE SalePrice IS NOT NULL AND SalePrice >= 0 AND SalePrice < Price;

    -- A product-level legacy sale cannot remain product-level once active
    -- variants exist. Apply it as a fallback to variants that do not have
    -- their own valid legacy sale, while preserving variant-specific sales.
    INSERT INTO PriceSchedules
        (ProductId, ProductVariantId, DiscountType, Value, StartsAt, EndsAt,
         IsCancelled, CreatedByAdminId, CreatedAt, UpdatedAt)
    SELECT v.ProductId, v.Id, 0, p.SalePrice, @now, NULL, 0, NULL, @now, @now
    FROM ProductVariants v
    INNER JOIN Products p ON p.Id = v.ProductId
    WHERE v.IsActive = 1
      AND p.SalePrice IS NOT NULL AND p.SalePrice >= 0 AND p.SalePrice < v.Price
      AND NOT (v.SalePrice IS NOT NULL AND v.SalePrice >= 0 AND v.SalePrice < v.Price);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductVariants]') AND [c].[name] = N'SalePrice');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [ProductVariants] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [ProductVariants] DROP COLUMN [SalePrice];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'SalePrice');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [Products] DROP COLUMN [SalePrice];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ;WITH Totals AS (
        SELECT CartId, ProductId, MIN(Id) AS KeepId, SUM(Quantity) AS TotalQuantity
        FROM CartItems
        GROUP BY CartId, ProductId
    )
    UPDATE item SET Quantity = totals.TotalQuantity
    FROM CartItems item
    INNER JOIN Totals totals ON item.Id = totals.KeepId;

    ;WITH Ranked AS (
        SELECT Id, ROW_NUMBER() OVER (PARTITION BY CartId, ProductId ORDER BY Id) AS RowNumber
        FROM CartItems
    )
    DELETE FROM Ranked WHERE RowNumber > 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductVariantId] ON [OrderItems] ([ProductVariantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CartItems_CartId_ProductId_ProductVariantId] ON [CartItems] ([CartId], [ProductId], [ProductVariantId]) WHERE [ProductVariantId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CartItems_CartId_ProductId_NoVariant] ON [CartItems] ([CartId], [ProductId]) WHERE [ProductVariantId] IS NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductVariantId] ON [CartItems] ([ProductVariantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    CREATE INDEX [IX_PriceSchedules_CreatedByAdminId] ON [PriceSchedules] ([CreatedByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    CREATE INDEX [IX_PriceSchedules_ProductId_ProductVariantId_StartsAt] ON [PriceSchedules] ([ProductId], [ProductVariantId], [StartsAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    CREATE INDEX [IX_PriceSchedules_ProductVariantId] ON [PriceSchedules] ([ProductVariantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ALTER TABLE [CartItems] ADD CONSTRAINT [FK_CartItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    ALTER TABLE [OrderItems] ADD CONSTRAINT [FK_OrderItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716123416_AddPriceSchedulingAndVariantPurchasing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716123416_AddPriceSchedulingAndVariantPurchasing', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    CREATE TABLE [Combos] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(255) NOT NULL,
        [Slug] nvarchar(255) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ImageUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Combos] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    CREATE TABLE [ComboItems] (
        [Id] int NOT NULL IDENTITY,
        [ComboId] int NOT NULL,
        [ProductId] int NOT NULL,
        [ProductVariantId] int NULL,
        [Quantity] int NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ComboItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComboItems_Combos_ComboId] FOREIGN KEY ([ComboId]) REFERENCES [Combos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ComboItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ComboItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    CREATE INDEX [IX_ComboItems_ComboId_SortOrder] ON [ComboItems] ([ComboId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    CREATE INDEX [IX_ComboItems_ProductId] ON [ComboItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    CREATE INDEX [IX_ComboItems_ProductVariantId] ON [ComboItems] ([ProductVariantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Combos_Slug] ON [Combos] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717122652_AddMealCombo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717122652_AddMealCombo', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD [PriceRevision] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [Products] ADD [PriceRevision] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [PriceSchedules] ADD [CancellationReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [PriceSchedules] ADD [CancelledAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [PriceSchedules] ADD [CancelledByAdminId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [PriceSchedules] ADD [Revision] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    CREATE INDEX [IX_PriceSchedules_CancelledByAdminId] ON [PriceSchedules] ([CancelledByAdminId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    ALTER TABLE [PriceSchedules] ADD CONSTRAINT [FK_PriceSchedules_Users_CancelledByAdminId] FOREIGN KEY ([CancelledByAdminId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726084628_AddPriceIntegrityHardening'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726084628_AddPriceIntegrityHardening', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726090319_AddOrderItemPriceSnapshots'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [BasePrice] decimal(10,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726090319_AddOrderItemPriceSnapshots'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [PriceScheduleId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726090319_AddOrderItemPriceSnapshots'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [PromotionDiscount] decimal(10,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726090319_AddOrderItemPriceSnapshots'
)
BEGIN
    UPDATE OrderItems
    SET BasePrice = Price,
        PromotionDiscount = 0
    WHERE BasePrice = 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726090319_AddOrderItemPriceSnapshots'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726090319_AddOrderItemPriceSnapshots', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726112800_NormalizePriceRevisionDefaults'
)
BEGIN
    UPDATE [Products]
    SET [PriceRevision] = 1
    WHERE [PriceRevision] <= 0;

    UPDATE [ProductVariants]
    SET [PriceRevision] = 1
    WHERE [PriceRevision] <= 0;

    UPDATE [PriceSchedules]
    SET [Revision] = 1
    WHERE [Revision] <= 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726112800_NormalizePriceRevisionDefaults'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'PriceRevision');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [Products] ADD DEFAULT 1 FOR [PriceRevision];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726112800_NormalizePriceRevisionDefaults'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductVariants]') AND [c].[name] = N'PriceRevision');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [ProductVariants] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [ProductVariants] ADD DEFAULT 1 FOR [PriceRevision];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726112800_NormalizePriceRevisionDefaults'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PriceSchedules]') AND [c].[name] = N'Revision');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [PriceSchedules] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [PriceSchedules] ADD DEFAULT 1 FOR [Revision];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726112800_NormalizePriceRevisionDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726112800_NormalizePriceRevisionDefaults', N'8.0.11');
END;
GO

COMMIT;
GO

