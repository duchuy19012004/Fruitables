BEGIN TRANSACTION;
GO

ALTER TABLE [Orders] ADD [DeliveredAtUtc] datetime2 NULL;
GO

CREATE TABLE [ReturnPolicies] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Scope] int NOT NULL,
    [CategoryId] int NULL,
    [ProductId] int NULL,
    [Reason] int NOT NULL,
    [ClaimWindowHours] int NOT NULL,
    [EvidenceRequired] bit NOT NULL,
    [AllowPartialRefund] bit NOT NULL,
    [AllowFullRefund] bit NOT NULL,
    [AllowReplacement] bit NOT NULL,
    [AllowStoreCredit] bit NOT NULL,
    [AllowRestock] bit NOT NULL,
    [IsEligible] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [Version] int NOT NULL,
    [EffectiveFromUtc] datetime2 NOT NULL,
    [EffectiveToUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ReturnPolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReturnPolicies_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnPolicies_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReturnRequests] (
    [Id] int NOT NULL IDENTITY,
    [ReturnNumber] nvarchar(32) NOT NULL,
    [IdempotencyKey] nvarchar(64) NOT NULL,
    [OrderId] int NOT NULL,
    [UserId] int NOT NULL,
    [Status] int NOT NULL,
    [Resolution] int NOT NULL,
    [PolicyVersion] int NOT NULL,
    [SubmittedAtUtc] datetime2 NOT NULL,
    [ClaimDeadlineAtUtc] datetime2 NOT NULL,
    [ReviewDueAtUtc] datetime2 NOT NULL,
    [EvidenceDueAtUtc] datetime2 NULL,
    [ReviewedAtUtc] datetime2 NULL,
    [ResolvedAtUtc] datetime2 NULL,
    [ReviewerId] int NULL,
    [CustomerNote] nvarchar(2000) NULL,
    [InternalNote] nvarchar(2000) NULL,
    [DecisionReason] nvarchar(1000) NULL,
    [MerchantFault] bit NOT NULL,
    [ShippingFeeApproved] bit NOT NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ReturnRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReturnRequests_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnRequests_Users_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReturnEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [ReturnRequestId] int NOT NULL,
    [Type] int NOT NULL,
    [FromStatus] int NULL,
    [ToStatus] int NULL,
    [ActorUserId] int NULL,
    [Note] nvarchar(1000) NULL,
    [DataJson] nvarchar(4000) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ReturnEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReturnEvents_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReturnRequestItems] (
    [Id] int NOT NULL IDENTITY,
    [ReturnRequestId] int NOT NULL,
    [OrderItemId] int NOT NULL,
    [ReturnPolicyId] int NULL,
    [RequestedQuantity] int NOT NULL,
    [ApprovedQuantity] int NOT NULL,
    [Reason] int NOT NULL,
    [RequestedResolution] int NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [NetPaidAmountSnapshot] decimal(12,2) NOT NULL,
    [RequestedAmount] decimal(12,2) NOT NULL,
    [ApprovedAmount] decimal(12,2) NOT NULL,
    [PolicyVersionSnapshot] int NOT NULL,
    [ClaimWindowHoursSnapshot] int NOT NULL,
    [EvidenceRequiredSnapshot] bit NOT NULL,
    [ClaimDeadlineAtUtcSnapshot] datetime2 NOT NULL,
    CONSTRAINT [PK_ReturnRequestItems] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ReturnRequestItems_ApprovedQuantity] CHECK ([ApprovedQuantity] >= 0 AND [ApprovedQuantity] <= [RequestedQuantity]),
    CONSTRAINT [CK_ReturnRequestItems_RequestedQuantity] CHECK ([RequestedQuantity] > 0),
    CONSTRAINT [FK_ReturnRequestItems_OrderItems_OrderItemId] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnRequestItems_ReturnPolicies_ReturnPolicyId] FOREIGN KEY ([ReturnPolicyId]) REFERENCES [ReturnPolicies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnRequestItems_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [InventoryDispositions] (
    [Id] int NOT NULL IDENTITY,
    [ReturnRequestItemId] int NOT NULL,
    [Quantity] int NOT NULL,
    [Disposition] int NOT NULL,
    [InspectorUserId] int NOT NULL,
    [Notes] nvarchar(1000) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryDispositions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_InventoryDispositions_Quantity] CHECK ([Quantity] > 0),
    CONSTRAINT [FK_InventoryDispositions_ReturnRequestItems_ReturnRequestItemId] FOREIGN KEY ([ReturnRequestItemId]) REFERENCES [ReturnRequestItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryDispositions_Users_InspectorUserId] FOREIGN KEY ([InspectorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Refunds] (
    [Id] int NOT NULL IDENTITY,
    [ReturnRequestId] int NOT NULL,
    [ReturnRequestItemId] int NULL,
    [OrderId] int NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Method] int NOT NULL,
    [Status] int NOT NULL,
    [IdempotencyKey] nvarchar(64) NOT NULL,
    [TransactionReference] nvarchar(128) NULL,
    [TransferEvidenceStorageKey] nvarchar(128) NULL,
    [FailureReason] nvarchar(1000) NULL,
    [CreatedByUserId] int NOT NULL,
    [ProcessedByUserId] int NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [ProcessedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Refunds_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [FK_Refunds_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Refunds_ReturnRequestItems_ReturnRequestItemId] FOREIGN KEY ([ReturnRequestItemId]) REFERENCES [ReturnRequestItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Refunds_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Refunds_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Refunds_Users_ProcessedByUserId] FOREIGN KEY ([ProcessedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReturnEvidences] (
    [Id] int NOT NULL IDENTITY,
    [ReturnRequestId] int NOT NULL,
    [ReturnRequestItemId] int NULL,
    [UploadedByUserId] int NOT NULL,
    [OriginalFileName] nvarchar(255) NOT NULL,
    [StorageKey] nvarchar(128) NOT NULL,
    [MimeType] nvarchar(100) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Sha256Checksum] nvarchar(64) NOT NULL,
    [ScanStatus] int NOT NULL,
    [UploadedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ReturnEvidences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReturnEvidences_ReturnRequestItems_ReturnRequestItemId] FOREIGN KEY ([ReturnRequestItemId]) REFERENCES [ReturnRequestItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnEvidences_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReturnEvidences_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_InventoryDispositions_InspectorUserId] ON [InventoryDispositions] ([InspectorUserId]);
GO

CREATE INDEX [IX_InventoryDispositions_ReturnRequestItemId_CreatedAtUtc] ON [InventoryDispositions] ([ReturnRequestItemId], [CreatedAtUtc]);
GO

CREATE INDEX [IX_Refunds_CreatedByUserId] ON [Refunds] ([CreatedByUserId]);
GO

CREATE UNIQUE INDEX [IX_Refunds_IdempotencyKey] ON [Refunds] ([IdempotencyKey]);
GO

CREATE INDEX [IX_Refunds_OrderId] ON [Refunds] ([OrderId]);
GO

CREATE INDEX [IX_Refunds_ProcessedByUserId] ON [Refunds] ([ProcessedByUserId]);
GO

CREATE INDEX [IX_Refunds_ReturnRequestId] ON [Refunds] ([ReturnRequestId]);
GO

CREATE INDEX [IX_Refunds_ReturnRequestItemId] ON [Refunds] ([ReturnRequestItemId]);
GO

CREATE INDEX [IX_Refunds_Status_CreatedAtUtc] ON [Refunds] ([Status], [CreatedAtUtc]);
GO

CREATE UNIQUE INDEX [IX_Refunds_TransactionReference] ON [Refunds] ([TransactionReference]) WHERE [TransactionReference] IS NOT NULL;
GO

CREATE INDEX [IX_ReturnEvents_ActorUserId] ON [ReturnEvents] ([ActorUserId]);
GO

CREATE INDEX [IX_ReturnEvents_ReturnRequestId_CreatedAtUtc] ON [ReturnEvents] ([ReturnRequestId], [CreatedAtUtc]);
GO

CREATE INDEX [IX_ReturnEvidences_ReturnRequestId] ON [ReturnEvidences] ([ReturnRequestId]);
GO

CREATE INDEX [IX_ReturnEvidences_ReturnRequestItemId] ON [ReturnEvidences] ([ReturnRequestItemId]);
GO

CREATE UNIQUE INDEX [IX_ReturnEvidences_StorageKey] ON [ReturnEvidences] ([StorageKey]);
GO

CREATE INDEX [IX_ReturnEvidences_UploadedByUserId] ON [ReturnEvidences] ([UploadedByUserId]);
GO

CREATE INDEX [IX_ReturnPolicies_CategoryId] ON [ReturnPolicies] ([CategoryId]);
GO

CREATE INDEX [IX_ReturnPolicies_ProductId] ON [ReturnPolicies] ([ProductId]);
GO

CREATE INDEX [IX_ReturnPolicies_Scope_ProductId_CategoryId_Reason_IsActive] ON [ReturnPolicies] ([Scope], [ProductId], [CategoryId], [Reason], [IsActive]);
GO

CREATE INDEX [IX_ReturnPolicies_Version_EffectiveFromUtc] ON [ReturnPolicies] ([Version], [EffectiveFromUtc]);
GO

CREATE INDEX [IX_ReturnRequestItems_OrderItemId] ON [ReturnRequestItems] ([OrderItemId]);
GO

CREATE INDEX [IX_ReturnRequestItems_ReturnPolicyId] ON [ReturnRequestItems] ([ReturnPolicyId]);
GO

CREATE INDEX [IX_ReturnRequestItems_ReturnRequestId_OrderItemId] ON [ReturnRequestItems] ([ReturnRequestId], [OrderItemId]);
GO

CREATE INDEX [IX_ReturnRequests_OrderId_Status] ON [ReturnRequests] ([OrderId], [Status]);
GO

CREATE UNIQUE INDEX [IX_ReturnRequests_ReturnNumber] ON [ReturnRequests] ([ReturnNumber]);
GO

CREATE INDEX [IX_ReturnRequests_ReviewerId] ON [ReturnRequests] ([ReviewerId]);
GO

CREATE INDEX [IX_ReturnRequests_Status_ReviewDueAtUtc] ON [ReturnRequests] ([Status], [ReviewDueAtUtc]);
GO

CREATE UNIQUE INDEX [IX_ReturnRequests_UserId_IdempotencyKey] ON [ReturnRequests] ([UserId], [IdempotencyKey]);
GO

CREATE INDEX [IX_ReturnRequests_UserId_SubmittedAtUtc] ON [ReturnRequests] ([UserId], [SubmittedAtUtc]);
GO


DECLARE @now datetime2 = '2026-07-27T00:00:00Z';
DECLARE @reasons TABLE (Reason int, EvidenceRequired bit, IsEligible bit);
INSERT INTO @reasons VALUES
(0,1,1),(1,1,1),(2,1,1),(3,1,1),(4,1,1),(5,1,1),(6,1,1),(7,1,1),(8,1,1),(9,0,0),(10,0,1);

INSERT INTO ReturnPolicies
(Name, Scope, CategoryId, ProductId, Reason, ClaimWindowHours, EvidenceRequired,
 AllowPartialRefund, AllowFullRefund, AllowReplacement, AllowStoreCredit,
 AllowRestock, IsEligible, IsActive, Version, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT CONCAT('Default v1 - ', Reason), 0, NULL, NULL, Reason, 24, EvidenceRequired,
       IsEligible, IsEligible, IsEligible, IsEligible, 0, IsEligible, 1, 1, @now, NULL, @now
FROM @reasons;

INSERT INTO ReturnPolicies
(Name, Scope, CategoryId, ProductId, Reason, ClaimWindowHours, EvidenceRequired,
 AllowPartialRefund, AllowFullRefund, AllowReplacement, AllowStoreCredit,
 AllowRestock, IsEligible, IsActive, Version, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT CONCAT('Perishable 12h v1 - ', c.Id, ' - ', r.Reason), 1, c.Id, NULL, r.Reason, 12, r.EvidenceRequired,
       r.IsEligible, r.IsEligible, r.IsEligible, r.IsEligible, 0, r.IsEligible, 1, 1, @now, NULL, @now
FROM Categories c CROSS JOIN @reasons r
WHERE LOWER(c.Slug) IN ('rau-la','rau-thom','qua-mong','nam','nam-tuoi','dau-tay')
   OR LOWER(c.Slug) LIKE 'rau-la-%' OR LOWER(c.Slug) LIKE 'rau-thom-%'
   OR LOWER(c.Slug) LIKE '%qua-mong%' OR LOWER(c.Slug) LIKE '%berry%' OR LOWER(c.Slug) LIKE 'nam-%';

INSERT INTO ReturnPolicies
(Name, Scope, CategoryId, ProductId, Reason, ClaimWindowHours, EvidenceRequired,
 AllowPartialRefund, AllowFullRefund, AllowReplacement, AllowStoreCredit,
 AllowRestock, IsEligible, IsActive, Version, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT CONCAT('Sealed dry 7d v1 - ', c.Id, ' - ', r.Reason), 1, c.Id, NULL, r.Reason, 168, r.EvidenceRequired,
       r.IsEligible, r.IsEligible, r.IsEligible, r.IsEligible, 1, r.IsEligible, 1, 1, @now, NULL, @now
FROM Categories c CROSS JOIN @reasons r
WHERE LOWER(c.Slug) IN ('hang-kho','thuc-pham-kho','do-kho','hang-dong-goi')
   OR LOWER(c.Slug) LIKE '%-kho' OR LOWER(c.Slug) LIKE '%dong-goi%' OR LOWER(c.Slug) LIKE '%nguyen-seal%';

DECLARE @permission TABLE (Name nvarchar(100), Description nvarchar(500));
INSERT INTO @permission VALUES
('returns.view',N'Xem hàng đợi khiếu nại'),('returns.review',N'Yêu cầu bằng chứng và xem xét'),
('returns.approve',N'Duyệt khiếu nại'),('returns.reject',N'Từ chối khiếu nại'),
('returns.refund',N'Ghi nhận hoàn tiền'),('returns.override_policy',N'Override policy và hoàn kho');
INSERT INTO Permissions (Name, Description, Module, CreatedAt)
SELECT p.Name,p.Description,'returns',@now FROM @permission p
WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name=p.Name);

INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='SuperAdmin' AND p.Module='returns'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='Admin' AND p.Name IN ('returns.view','returns.review','returns.approve','returns.reject','returns.refund')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
INSERT INTO RolePermissions(RoleId, PermissionId, AssignedAt, AssignedByAdminId)
SELECT r.Id,p.Id,@now,NULL FROM Roles r CROSS JOIN Permissions p
WHERE r.Name='CustomerSupport' AND p.Name IN ('returns.view','returns.review','returns.reject')
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260727151845_AddReturnClaimsFoundation', N'8.0.11');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ReturnEvidences] ADD [IsInternal] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260727153529_ProtectInternalReturnEvidence', N'8.0.11');
GO

COMMIT;
GO
