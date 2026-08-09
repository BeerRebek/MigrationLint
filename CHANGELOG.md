# Changelog

All notable changes to MigrationLint are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/); versions follow SemVer.

## [0.1.0-preview.1] — unreleased

Tag `v0.1.0-preview.1` to cut this release; MinVer derives the package version from the tag and
the release workflow pushes to NuGet.

### Added
- IR + Roslyn syntax parser (no build, no DbContext load): argument reader, migration
  discovery, provider auto-detection (Postgres / SQL Server / MySQL / Sqlite).
- Rule engine with pure `(operation, context)` rules and a migration-level rule pass.
- Lock/downtime rules (the differentiators): **MIG007** (index without CONCURRENTLY/ONLINE),
  **MIG008** (unique-constraint scan), **MIG009** (FK without NOT VALID), **MIG010** (mixed DDL/DML).
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

_Not yet published to NuGet — reserve/push needs your API key; see docs/PHASE0-DECISION.md._
