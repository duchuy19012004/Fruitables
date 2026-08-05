# Remove Return Module Design

- Date: 2026-08-05
- Status: Approved for removal
- Scope: Remove the current return/refund module so it can be rebuilt from a clean baseline.

## Goal

Remove the current return module, its database model and migration history, UI, permissions, tests, documentation, and shared integration points. The existing return database may be reset, so preserving return data is not required.

## Removal Scope

- Delete return-only models, services, view models, controllers, views, helpers, styles, tests, migrations, SQL scripts, and design/plan documents.
- Remove return registrations and DbSets from dependency injection and `ApplicationDbContext`.
- Remove return-specific RBAC roles, permissions, mappings, migration seed logic, sidebar links, and outbox handling.
- Remove return/refund-only status branches and navigation properties from shared order, payment, history, analytics, and admin UI code.
- Remove all return-specific references from shared files without reverting unrelated worktree changes.
- Preserve a shared enum/property only when a non-return feature still uses it; otherwise remove the return-only member and its references.
- Keep this removal spec as the record of the approved purge; delete the older return implementation specs and plans.

## Safety Constraints

- Do not run `git reset`, `git checkout`, or broad cleanup commands.
- Do not modify unrelated pending work.
- Do not commit automatically.
- Because the database can be reset, deleting return migrations is intentional.

## Verification

- Search for remaining return-specific namespaces, entity names, routes, permissions, and view paths.
- Build the application.
- Run the test project and report any failures caused by unrelated pending changes separately.
