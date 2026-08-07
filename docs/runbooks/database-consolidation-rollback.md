# Database consolidation rollback

## Pre-contract backup

1. Take a full SQL Server backup before `--database-consolidation-backfill --apply`.
2. Store backup path and timestamp in the release ticket.

## Commands

```powershell
dotnet run -- --database-consolidation-backfill
dotnet run -- --database-consolidation-backfill --apply
dotnet run -- --database-consolidation-verify
```

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
