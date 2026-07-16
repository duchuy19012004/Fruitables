# Diagram skills for Fruitables

This project includes 11 repository-scoped Codex skills under `.agents/skills`.
Codex discovers them from every task opened in this repository.

## Invoke a skill

Type `$` in a new Codex task and select one of these skills:

- `$sequence`, `$activity`, `$state`, `$erd`
- `$activity-swimlane`, `$usecase-diagram`, `$bpmn`
- `$d2-activity`, `$d2-erd`, `$d2-architect`, `$dbdiagram`

Only the 11 skills above are included in this package. References in the source
guides to broader BA skills such as `$srs` or `$brainstorm` are optional next
steps; the diagram skills themselves can start from a natural-language prompt.

Example:

```text
$activity

Vẽ quy trình checkout của Fruitables. Feature slug: checkout.
Trước tiên chỉ hiển thị L1 plan; chỉ ghi file sau khi tôi trả lời Y.
```

## Install project-local tools

From PowerShell at the repository root:

```powershell
& .\.agents\scripts\setup-tools.ps1
```

The setup installs Mermaid CLI and DBML CLI under `.agents/node_modules`, the
BPMN dependencies under its skill directory, and the Windows D2 binary under
`.agents/tools`. Generated dependencies and binaries are intentionally not
committed.

## Verify

```powershell
node .agents/scripts/verify-install.mjs
```

PlantUML diagrams use the public `plantuml.com` server. Do not use the
PlantUML skills for sensitive business content unless the renderer is changed
to an approved internal or local server.
