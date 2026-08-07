[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [ValidateSet("Expand", "Preflight", "Backfill", "Contract")]
    [string]$Phase = "Preflight",

    [string]$BackupPath,
    [switch]$ConfirmContract,
    [string]$ExpectedDatabaseName,
    [string]$ProjectPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPath = if ($ProjectPath) { (Resolve-Path $ProjectPath).Path } else { Join-Path $repoRoot "Fruitables.csproj" }

function Get-ConnectionValue([string]$name) {
    $match = [regex]::Match($ConnectionString, "(?i)(?:^|;)\s*$name\s*=\s*([^;]*)")
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        throw "Connection string must contain $name."
    }
    return $match.Groups[1].Value.Trim()
}

$server = Get-ConnectionValue "(?:Server|Data Source)"
$database = Get-ConnectionValue "(?:Database|Initial Catalog)"
$trusted = $ConnectionString -match "(?i)(?:Trusted_Connection|Integrated Security)\s*=\s*(true|sspi|yes)"
if (-not $trusted) {
    throw "This safety script only accepts Windows-integrated SQL Server connections."
}

$sqlcmd = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
$sqlcmdPath = $sqlcmd.Source
if (-not $sqlcmdPath) {
    $sqlcmdPath = @(
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE",
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $sqlcmdPath) { throw "sqlcmd.exe was not found." }

function Invoke-Sql([string]$sql) {
    $arguments = @("-S", $server, "-d", $database, "-E", "-C", "-b", "-h", "-1", "-W", "-r", "1", "-Q", $sql)
    $output = @(& $sqlcmdPath @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($output.Count -gt 0) { $output | ForEach-Object { Write-Host $_ } }
    if ($exitCode -ne 0) {
        throw "sqlcmd failed with exit code $exitCode."
    }
    return $output
}

function Invoke-Dotnet([string[]]$arguments) {
    $output = @(& dotnet @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($output.Count -gt 0) { $output | ForEach-Object { Write-Host $_ } }
    if ($exitCode -ne 0) {
        throw "dotnet command failed with exit code $exitCode."
    }
}

function Invoke-AppCommand([string[]]$arguments) {
    $oldConnection = $env:ConnectionStrings__DefaultConnection
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    try {
        Invoke-Dotnet (@("run", "--project", $ProjectPath, "--no-build", "--") + $arguments)
    }
    finally {
        if ($null -eq $oldConnection) { Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue }
        else { $env:ConnectionStrings__DefaultConnection = $oldConnection }
    }
}

function Invoke-EfCommand([string[]]$arguments) {
    $oldDesignConnection = $env:FRUITABLES_DESIGN_CONNECTION
    $env:FRUITABLES_DESIGN_CONNECTION = $ConnectionString
    try {
        Invoke-Dotnet (@("ef") + $arguments)
    }
    finally {
        if ($null -eq $oldDesignConnection) { Remove-Item Env:FRUITABLES_DESIGN_CONNECTION -ErrorAction SilentlyContinue }
        else { $env:FRUITABLES_DESIGN_CONNECTION = $oldDesignConnection }
    }
}

function Invoke-ExpandPreflight {
    $sql = @"
SET NOCOUNT ON;
DECLARE @issues TABLE (Message nvarchar(4000) NOT NULL);
DECLARE @expected TABLE (Name sysname PRIMARY KEY);

INSERT @expected VALUES
('Users'),('Roles'),('Addresses'),('Categories'),('Products'),('ProductVariants'),('Carts'),
('Orders'),('OrderItems'),('Reviews'),('Settings'),('ChatSessions'),('KnowledgeChunks'),('OutboxMessages'),
('CartItems'),('ChatMessages'),('ComboAuditLogs'),('ComboItems'),('ContactMessages'),('Coupons'),
('Faqs'),('OrderNotes'),('OrderStatusHistories'),('PriceSchedules'),('ProductImages'),('ProductLogs'),
('ProductTagMapping'),('RbacAuditLogs'),('Refunds'),('ReturnEvents'),('ReturnEvidence'),('ReviewHelpfuls'),
('ReviewReports'),('ReviewSentimentAspects'),('RolePermissions'),('SearchHotKeywords'),('SePayTransactions'),
('Testimonials'),('UserAccountLogs'),('UserRoleMappings'),('Wishlists'),('CartGroups'),('ProductTags'),
('ReturnRequestItems'),('ReviewSentiments'),('Permissions'),('Combos'),('ReturnRequests');

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    INSERT @issues VALUES (N'Missing dbo.__EFMigrationsHistory.');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260805091804_AddDeliveredAtUtc')
    INSERT @issues VALUES (N'Baseline migration 20260805091804_AddDeliveredAtUtc is not applied.');
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId IN (N'20260806212020_AddAggregateJsonSchema', N'20260806224359_AddConsolidationIdentityAndPaymentStatus', N'20260807023646_ContractAggregateSchema'))
    INSERT @issues VALUES (N'An aggregate-schema migration is already applied or partially applied.');
INSERT @issues SELECT N'Missing baseline table: ' + e.Name FROM @expected e WHERE OBJECT_ID(N'dbo.' + e.Name, N'U') IS NULL;
INSERT @issues
SELECT N'Unexpected baseline table: ' + t.name
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND t.name NOT IN (N'__EFMigrationsHistory', N'sysdiagrams')
  AND NOT EXISTS (SELECT 1 FROM @expected e WHERE e.Name = t.name);
DECLARE @actualCount int = (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = N'dbo' AND name NOT IN (N'__EFMigrationsHistory', N'sysdiagrams'));
IF @actualCount <> (SELECT COUNT(*) FROM @expected)
    INSERT @issues VALUES (N'Baseline schema table count is ' + CONVERT(nvarchar(20), @actualCount) + N'; expected ' + CONVERT(nvarchar(20), (SELECT COUNT(*) FROM @expected)) + N'.');
SELECT N'EXPAND_PREFLIGHT|' + CASE WHEN EXISTS (SELECT 1 FROM @issues) THEN N'FAIL' ELSE N'PASS' END;
SELECT N'ISSUE|' + Message FROM @issues ORDER BY Message;
"@

    $output = Invoke-Sql $sql
    $issues = @($output | Where-Object { $_.ToString().TrimStart().StartsWith("ISSUE|") })
    if ($issues.Count -gt 0) {
        throw "Expand preflight failed for $database with $($issues.Count) issue(s)."
    }
    Write-Host "Expand preflight passed for $database."
}

function Invoke-Preflight {
    $dbLiteral = $database.Replace("'", "''")
    $sql = @"
SET NOCOUNT ON;
DECLARE @issues TABLE (Message nvarchar(4000) NOT NULL);
DECLARE @target TABLE (Name sysname PRIMARY KEY);
DECLARE @legacy TABLE (Name sysname PRIMARY KEY);
DECLARE @requiredColumns TABLE (TableName sysname, ColumnName sysname);
DECLARE @requiredMigrations TABLE (MigrationId nvarchar(150) PRIMARY KEY);

INSERT @target VALUES
('Users'),('Roles'),('Addresses'),('Categories'),('Products'),('ProductVariants'),
('Carts'),('Orders'),('OrderItems'),('Payments'),('Promotions'),('Reviews'),('Returns'),
('Settings'),('ContentEntries'),('ChatSessions'),('KnowledgeChunks'),('AuditLogs'),('OutboxMessages');

INSERT @legacy VALUES
('CartItems'),('ChatMessages'),('ComboAuditLogs'),('ComboItems'),('ContactMessages'),('Coupons'),
('Faqs'),('OrderNotes'),('OrderStatusHistories'),('PriceSchedules'),('ProductImages'),('ProductLogs'),
('ProductTagMapping'),('RbacAuditLogs'),('Refunds'),('ReturnEvents'),('ReturnEvidence'),('ReviewHelpfuls'),
('ReviewReports'),('ReviewSentimentAspects'),('RolePermissions'),('SearchHotKeywords'),('SePayTransactions'),
('Testimonials'),('UserAccountLogs'),('UserRoleMappings'),('Wishlists'),('CartGroups'),('ProductTags'),
('ReturnRequestItems'),('ReviewSentiments'),('Permissions'),('Combos'),('ReturnRequests');

INSERT @requiredColumns VALUES
('Products','AssetRevision'),('Products','ImagesJson'),('Products','TagsJson'),
('Users','RoleIdsJson'),('Users','WishlistJson'),('Users','RowVersion'),
('Roles','PermissionsJson'),('Roles','RowVersion'),('Carts','LinesJson'),('Carts','RowVersion'),
('Orders','StatusHistoryJson'),('Orders','NotesJson'),('Orders','RowVersion'),
('Payments','ProviderEventStatus'),('Payments','RowVersion'),('Promotions','PayloadJson'),('Promotions','RowVersion'),
('Reviews','MetadataJson'),('Reviews','RowVersion'),('Returns','DetailsJson'),('Returns','RowVersion'),
('ContentEntries','PayloadJson'),('ContentEntries','RowVersion'),('ChatSessions','MessagesJson'),('ChatSessions','RowVersion'),
('AuditLogs','SourceId'),('AuditLogs','SourceType'),('OutboxMessages','IdempotencyKey');

INSERT @requiredMigrations VALUES
('20260806212020_AddAggregateJsonSchema'),
('20260806224359_AddConsolidationIdentityAndPaymentStatus');

IF DB_NAME() <> N'$dbLiteral'
    INSERT @issues VALUES (N'Connected database name does not match the requested database.');
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    INSERT @issues VALUES (N'Missing dbo.__EFMigrationsHistory.');
IF EXISTS (SELECT 1 FROM @requiredMigrations m WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = m.MigrationId))
    INSERT @issues SELECT N'Migration not applied: ' + m.MigrationId FROM @requiredMigrations m WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = m.MigrationId);
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260807023646_ContractAggregateSchema')
    INSERT @issues VALUES (N'ContractAggregateSchema is already applied; use post-cutover verification instead.');

INSERT @issues SELECT N'Missing target table: ' + t.Name FROM @target t WHERE OBJECT_ID(N'dbo.' + t.Name, N'U') IS NULL;
INSERT @issues SELECT N'Missing legacy source table: ' + l.Name FROM @legacy l WHERE OBJECT_ID(N'dbo.' + l.Name, N'U') IS NULL;
INSERT @issues SELECT N'Missing target column: ' + c.TableName + N'.' + c.ColumnName FROM @requiredColumns c WHERE COL_LENGTH(N'dbo.' + c.TableName, c.ColumnName) IS NULL;

INSERT @issues
SELECT N'Unexpected business table: ' + t.name
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND t.name NOT IN (N'__EFMigrationsHistory', N'sysdiagrams')
  AND NOT EXISTS (SELECT 1 FROM @target x WHERE x.Name = t.name)
  AND NOT EXISTS (SELECT 1 FROM @legacy x WHERE x.Name = t.name);

DECLARE @expectedCount int = (SELECT COUNT(*) FROM @target) + (SELECT COUNT(*) FROM @legacy);
DECLARE @actualCount int = (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = N'dbo' AND name NOT IN (N'__EFMigrationsHistory', N'sysdiagrams'));
IF @actualCount <> @expectedCount
    INSERT @issues VALUES (N'Expanded schema table count is ' + CONVERT(nvarchar(20), @actualCount) + N'; expected ' + CONVERT(nvarchar(20), @expectedCount) + N'.');

SELECT N'PREFLIGHT|' + CASE WHEN EXISTS (SELECT 1 FROM @issues) THEN N'FAIL' ELSE N'PASS' END;
SELECT N'ISSUE|' + Message FROM @issues ORDER BY Message;
"@

    $output = Invoke-Sql $sql
    $issues = @($output | Where-Object { $_.ToString().TrimStart().StartsWith("ISSUE|") })
    if ($issues.Count -gt 0) {
        throw "Safe preflight failed for $database with $($issues.Count) issue(s)."
    }
    Write-Host "Safe preflight passed for $database. Expanded schema is ready for backfill/cutover."
}

function Backup-And-Verify {
    if ([string]::IsNullOrWhiteSpace($BackupPath)) { throw "-BackupPath is required for $Phase." }
    $backupLiteral = $BackupPath.Replace("'", "''")
    Invoke-Sql "BACKUP DATABASE [$database] TO DISK = N'$backupLiteral' WITH COPY_ONLY, CHECKSUM, INIT, STATS = 10;"
    Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$backupLiteral' WITH CHECKSUM;"
    Write-Host "Verified backup: $BackupPath"
}

if ($Phase -eq "Expand") {
    Invoke-ExpandPreflight
    Backup-And-Verify
    Invoke-EfCommand @(
        "database", "update", "20260806224359_AddConsolidationIdentityAndPaymentStatus",
        "--project", $ProjectPath,
        "--startup-project", $ProjectPath,
        "--no-build"
    )
    Invoke-Preflight
    Write-Host "Expand phase completed. Additive schema is ready for backfill."
    exit 0
}

if ($Phase -eq "Preflight") {
    Invoke-Preflight
    exit 0
}

Invoke-Preflight
Backup-And-Verify

if ($Phase -eq "Backfill") {
    Invoke-AppCommand @("--database-consolidation-backfill")
    Invoke-AppCommand @("--database-consolidation-backfill", "--apply")
    Invoke-AppCommand @("--database-consolidation-verify")
    Write-Host "Backfill phase completed. Contract migration was not applied."
    exit 0
}

if (-not $ConfirmContract) { throw "Contract phase requires -ConfirmContract." }
if ($ExpectedDatabaseName -ne $database) {
    throw "Contract phase requires -ExpectedDatabaseName '$database'."
}

Invoke-AppCommand @("--database-consolidation-verify")
Invoke-EfCommand @(
    "database", "update", "ContractAggregateSchema",
    "--project", $ProjectPath,
    "--startup-project", $ProjectPath,
    "--no-build"
)
Invoke-AppCommand @("--database-consolidation-verify")
Write-Host "Contract phase completed and post-cutover verification passed."
