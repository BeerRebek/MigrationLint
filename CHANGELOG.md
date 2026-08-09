# Changelog

All notable changes to MigrationLint are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/); versions follow SemVer.

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
