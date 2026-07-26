BEGIN TRANSACTION;
GO

UPDATE [Products]
SET [PriceRevision] = 1
WHERE [PriceRevision] <= 0;

UPDATE [ProductVariants]
SET [PriceRevision] = 1
WHERE [PriceRevision] <= 0;

UPDATE [PriceSchedules]
SET [Revision] = 1
WHERE [Revision] <= 0;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'PriceRevision');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Products] ADD DEFAULT 1 FOR [PriceRevision];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductVariants]') AND [c].[name] = N'PriceRevision');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ProductVariants] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [ProductVariants] ADD DEFAULT 1 FOR [PriceRevision];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PriceSchedules]') AND [c].[name] = N'Revision');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [PriceSchedules] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [PriceSchedules] ADD DEFAULT 1 FOR [Revision];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260726112800_NormalizePriceRevisionDefaults', N'8.0.11');
GO

COMMIT;
GO

