# Database consolidation rollback

## Pre-contract backup

1. Take a full SQL Server backup before `--database-consolidation-backfill --apply`.
2. Store backup path and timestamp in the release ticket.

## Safe commands

Do not run `dotnet ef database update` against a live database until the preflight passes. Use the guarded script from the repository root; it refuses schema drift and requires an explicit database-name confirmation for the destructive phase:

```powershell
$cs = 'Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True'

# Backup + verify backup + apply only the additive migrations. No JSON backfill or drops.
powershell -File .\scripts\database-consolidation-safe-cutover.ps1 `
  -ConnectionString $cs -Phase Expand `
  -BackupPath 'D:\SqlBackups\Fruitables-pre-expand.bak'

# Read-only. Must pass after Expand.
powershell -File .\scripts\database-consolidation-safe-cutover.ps1 `
  -ConnectionString $cs -Phase Preflight

# Backup + verify backup + dry-run + apply backfill + verify. No table drops.
powershell -File .\scripts\database-consolidation-safe-cutover.ps1 `
  -ConnectionString $cs -Phase Backfill `
  -BackupPath 'D:\SqlBackups\Fruitables-pre-consolidation.bak'

# Backup again, verify, require the exact database name, apply contract, verify again.
powershell -File .\scripts\database-consolidation-safe-cutover.ps1 `
  -ConnectionString $cs -Phase Contract `
  -BackupPath 'D:\SqlBackups\Fruitables-pre-contract.bak' `
  -ConfirmContract -ExpectedDatabaseName 'FruitablesDb'
```

The script only accepts Windows-integrated SQL Server connections. The backup path must be writable by the SQL Server service, not merely by the client. Preflight requires the 19 target tables, all approved legacy source tables, required JSON columns, additive migration history, and no unexpected tables. It reports and stops on the current drifted database instead of guessing a baseline.

Dry-run writes nothing. Apply is idempotent. Verify is read-only and must exit 0 before contract migration. The verification gate also checks 19 business tables, `ISJSON`, relational reconciliation, provider idempotency, and JSON size limits (cart 64 KiB, review 32 KiB, return 256 KiB, chat 512 KiB).

## Expand window

Tasks 1-8 keep legacy tables registered. Dual-write mirrors keep cart/order/RBAC/return/chat consumers working while JSON aggregates become source of truth for new writes.

## Rollback trigger

Rollback if verify reports required mismatches, invalid JSON, or production smoke fails after apply.

## Rollback order

1. Stop writers.
2. Restore pre-contract backup (or pre-apply backup if contract not shipped).
3. Redeploy previous application build.
4. Re-run `--database-consolidation-verify` on restored DB only as diagnostics.

## Release evidence

Attach the focused/full test result, `dotnet build` result, and the zero-error `--database-consolidation-verify` output to the deployment ticket before applying `ContractAggregateSchema`.

## Contract note

`ContractAggregateSchema` drops legacy tables. After that migration, production rollback requires the pre-contract backup; EF `Down` alone is insufficient if post-cutover writes occurred.
