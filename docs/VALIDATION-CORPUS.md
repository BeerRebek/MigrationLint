# Validation corpus results (PRD §13)

**Date:** 2026-08-09
**Goal:** run the parser against real-world migration folders to prove the architecture — no
exceptions, no silently-dropped operations (every unmapped method logged), record counts.

## Repos

| Repo | Migration files discovered | Migrations parsed | Result |
|---|---|---|---|
| [bitwarden/server](https://github.com/bitwarden/server) | 458 | 452 | ✅ no exceptions |
| [dotnet/eShop](https://github.com/dotnet/eShop) | 9 | 9 | ✅ no exceptions |
| [jellyfin/jellyfin](https://github.com/jellyfin/jellyfin) | 107 | 53 | ✅ no exceptions |

> "Parsed" counts reflect the auto-detected provider's snapshot directory; multi-provider repos
> (bitwarden) contain parallel Postgres/MySQL/SQL Server trees, and discovery scans the tree of
> the detected provider. Not one file threw.

**Outcome: architecture validated.** 514 real migrations processed with zero parser exceptions.

## Unmapped `migrationBuilder.*` methods (logged, reviewed)

Per §8.3 the parser logs any method it does not map. Observed across the corpus:

| Method | Decision |
|---|---|
| `AlterDatabase`, `EnsureSchema`, `CreateSequence`, `DropSequence`, `RenameSequence`, `SqlResource` | Schema/db-level; no rule applies. Leave unmapped. |
| `DropForeignKey`, `DropPrimaryKey`, `AddPrimaryKey`, `RenameIndex`, `AddCheckConstraint`, `DropCheckConstraint` | Structural; not covered by the current 12 rules. Candidate future rules (e.g. `AddPrimaryKey`/`AddCheckConstraint` lock like MIG009). |
| **`InsertData`, `UpdateData`, `DeleteData`** | **Fixed as a result of this run.** These are EF's structured DML. They are now mapped to data-operation kinds and feed MIG010, so a migration mixing DDL with `InsertData`/`UpdateData`/`DeleteData` is flagged (previously only raw `Sql()` DML was detected). |

None of the remaining unmapped methods should have been handled by the existing rules, so no rule
has a silent gap.

## Reproduce

```bash
BIN=src/MigrationLint.Cli/bin/Release/net8.0/MigrationLint.Cli
dotnet build src/MigrationLint.Cli -c Release
$BIN check /path/to/repo --format json --output out.json   # stderr lists unmapped methods
```
