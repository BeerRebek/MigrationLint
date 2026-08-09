# Changelog

All notable changes to MigrationLint are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/); versions follow SemVer.

## [0.3.0] — 2026-08-10

### Added
- **`migrationlint plan`** — reframes each migration as a safe deployment sequence
  (expand → migrate → contract), driven by the same rules that flag the operations.
- **Live-DB enrichment** (`--connection`): MIG006 is cleared when the column has zero NULLs, and
  violation messages gain the real row count (e.g. `… (Orders has ~4,238,901 rows)`).
  NULL counts are queried for PostgreSQL, SQL Server, and MySQL.

## [0.2.0] — 2026-08-10

Eight new rules (13 → 21), a code fix, a markdown format, and `init`.

### Added
- Four new lock/downtime rules (ship as **warning** per the new-rule policy):
  - **MIG013** — check constraint added without deferred validation (PostgreSQL `NOT VALID` /
    SQL Server `WITH NOCHECK`).
  - **MIG014** — primary key added to an existing table (unique-index build / clustered rewrite under lock).
  - **MIG015** — integer type widened (`int` → `bigint`), forcing a full table rewrite.
  - **MIG016** — column added with a volatile default (`now()`, `gen_random_uuid()`, `NEWID()`, …),
    forcing a per-row rewrite.
- Parser now maps `AddPrimaryKey` / `AddCheckConstraint`, reads `defaultValueSql` text, and resolves
  `oldClrType: typeof(...)` arguments.
- Four more EF-aware rules:
  - **MIG017** — a `CreatedConcurrently` index without `SuppressTransaction` (fails at runtime).
  - **MIG018** — a migration checked in without a `ModelSnapshot` update (drift), detected by comparing
    the newest migration's `BuildTargetModel` with the snapshot's `BuildModel`.
  - **MIG019** — a stored computed column added to an existing table (per-row rewrite).
  - **MIG020** — `DropIndex` without `CONCURRENTLY` on PostgreSQL.
- Parser reads `computedColumnSql` / `stored` and detects the `SuppressTransaction` override.
- **Code fix for MIG009 / MIG013** — the analyzer lightbulb splits an `AddForeignKey`/
  `AddCheckConstraint` into the two-step `NOT VALID` + `VALIDATE` (PostgreSQL) or `WITH NOCHECK` +
  `WITH CHECK CHECK` (SQL Server) form.
- **`--format markdown`** — a PR-summary table (rules linked to docs) for posting as a PR comment.
- **`migrationlint init`** — scaffolds a starter `migrationlint.json` with the detected provider.

## [0.1.1] — 2026-08-09

### Added
- Runnable `samples/SampleApi` project wired to the analyzer package, so the diagnostics and
  lightbulb code fixes can be reproduced (and screenshotted) in Visual Studio / Rider.
- README screenshots: CLI `--help` overview, `list-rules`, and the IDE analyzer lightbulb.

### Changed
- Marketplace-ready GitHub Action metadata (concise description within the 125-char limit,
  `shield` branding). No behavioral change to the Action.

## [0.1.0] — 2026-08-09

First stable release. Distribution is GitHub-only — install as a `dotnet tool` from source or a
release `.nupkg` (see the README). Reached here through previews `0.1.0-preview.1` … `preview.5`.

### Added
- IR + Roslyn syntax parser (no build, no DbContext load): argument reader, migration
  discovery, provider auto-detection (Postgres / SQL Server / MySQL / Sqlite).
- Rule engine with pure `(operation, context)` rules and a migration-level rule pass.
- Lock/downtime rules (the differentiators): **MIG007** (index without CONCURRENTLY/ONLINE),
  **MIG008** (unique-constraint scan), **MIG009** (FK without deferred validation — PostgreSQL
  `NOT VALID` and SQL Server `WITH NOCHECK`), **MIG010** (mixed DDL/DML).
- Data-loss & failure rules: MIG001–MIG006, MIG011 (destructive raw SQL), MIG012 (too many ops),
  MIG000 (suppression without justification).
- Config file (`migrationlint.json`), `[SuppressMigrationLint]` attribute, and baseline filtering.
- CLI (`check`, `baseline`, `explain`, `list-rules`) with console, GitHub, SARIF, and JSON formats;
  `--category`, `--rules`/`--exclude-rules`, `--changed-only`, `--deployment-strategy`.
- Exit codes 0/1/2/3; packs and installs as a `dotnet tool`.
- MIG010 also detects EF's structured DML (`InsertData`/`UpdateData`/`DeleteData`), not just raw SQL.
- 22-file fixture corpus and 54 tests.
- `docs/rules/MIG000.md … MIG012.md`, JSON schema for `migrationlint.json`, CI + release workflows,
  MinVer git-tag versioning.
- Validated: parser ran clean against 514 real migrations across bitwarden/server, dotnet/eShop,
  and jellyfin (see docs/VALIDATION-CORPUS.md).
- **Live-DB awareness** (opt-in `--connection`, read-only) — estimate-based row counts suppress
  false positives: empty tables no longer trigger MIG004/MIG006, and small tables (by real row
  count, `--small-rows` / `options.smallTableRowThreshold`) no longer trigger MIG007/008/009.
  PostgreSQL, SQL Server, and MySQL; fails soft when the database is unreachable.
- Reusable **GitHub Action** (`uses: BeerRebek/MigrationLint@v1`) with inline PR annotations,
  optional SARIF for code scanning, `changed-only` PR-diff mode, and a self-scan workflow that
  dogfoods it.
- **Roslyn analyzer** (`MigrationLint.Analyzers`, netstandard2.0) — the same rules run inline in
  Visual Studio / Rider. Reuses the CLI's rule engine unchanged (the payoff of the pure-rule
  architecture). Code fixes: **MIG007** (insert concurrent/online index annotation), **MIG004**
  (add a typed default value, or make the column nullable), and a universal **"suppress with
  justification"** fix that adds `[SuppressMigrationLint(...)]` for any rule.

_Distributed via GitHub Releases (not NuGet) — see the README for install-from-source._
