# MigrationLint

[![CI](https://github.com/BeerRebek/MigrationLint/actions/workflows/ci.yml/badge.svg)](https://github.com/BeerRebek/MigrationLint/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/BeerRebek/MigrationLint?sort=semver&label=release)](https://github.com/BeerRebek/MigrationLint/releases/latest)
[![GitHub Marketplace](https://img.shields.io/badge/Marketplace-MigrationLint-8957e5?logo=githubactions&logoColor=white)](https://github.com/marketplace/actions/migrationlint)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/BeerRebek/MigrationLint/blob/main/LICENSE)

**The EF Core migration linter that understands database locks.**

> 🎉 **v0.1.0 is here — first stable release.** Lock-first rules across PostgreSQL, SQL Server, and
> MySQL, a GitHub Action, an IDE analyzer with code fixes, and opt-in live-DB awareness.
> [Read the release notes →](https://github.com/BeerRebek/MigrationLint/releases/tag/v0.1.0)

An EF Core migration that succeeds instantly on an empty local database can take production
down when it runs against a table with millions of rows. `CREATE INDEX` blocks writes for the
whole build. Adding a foreign key validates every existing row under a lock. A dropped column
crashes the previous app version mid rolling-deploy. These migrations pass code review, pass
staging, and fail in production.

MigrationLint catches them at write time — no build, no database connection, no DbContext loading.

![MigrationLint flagging a MIG007 lock violation](docs/img/mig007-violation.png)

## On the GitHub Marketplace

Add migration safety to any workflow in one line — no install step:

```yaml
- uses: BeerRebek/MigrationLint@v1
  with:
    path: src/Orders.Api/Migrations
```

[**View on the GitHub Marketplace →**](https://github.com/marketplace/actions/migrationlint) · see
[Use in CI](#use-in-ci-github-action) for SARIF code scanning and PR-diff mode.

## Why it's different

MigrationLint leads with the problem no other .NET tool addresses — **lock and downtime safety**:

1. **Lock/downtime rules** — `CONCURRENTLY`, `ONLINE = ON`, `NOT VALID` foreign keys, unique-constraint scans.
2. **Provider awareness** — dialect-correct rules for PostgreSQL, SQL Server, and MySQL, not generic warnings.
3. **Adoptable on existing repos** — baseline, config, and per-migration suppression.
4. **CI-native** — SARIF, GitHub annotations, and PR-diff mode.

Data-loss rules (dropped columns/tables, renames, type narrowing, NOT NULL failures) are table
stakes and included — but they are not the lead.

## Install

MigrationLint is distributed from this repository (not NuGet). Install it as a local `dotnet tool`
straight from source:

```bash
git clone https://github.com/BeerRebek/MigrationLint.git
cd MigrationLint
dotnet pack src/MigrationLint.Cli/MigrationLint.Cli.csproj -c Release -o ./artifacts
dotnet tool install -g --add-source ./artifacts MigrationLint.Cli
```

`migrationlint` is then on your PATH. To update later, `git pull`, re-pack, and
`dotnet tool update -g --add-source ./artifacts MigrationLint.Cli`.

Prefer not to install a tool? Run it directly:

```bash
dotnet run --project src/MigrationLint.Cli -- check ./path/to/Migrations
```

Tagged releases also attach the built `.nupkg` files to the
[GitHub Releases](https://github.com/BeerRebek/MigrationLint/releases) page — download one and
`dotnet tool install -g --add-source <folder> MigrationLint.Cli`.

## Use

Run `migrationlint --help` for the full picture of what you can do:

![migrationlint --help overview](docs/img/help.png)

```bash
migrationlint check ./src/Orders.Api          # auto-detects provider and migrations path
migrationlint check --category locking        # try the differentiating rules first
migrationlint check --format sarif -o results.sarif
migrationlint check --changed-only --base main
migrationlint plan ./src/Orders.Api           # show each migration as a safe deploy sequence
migrationlint init ./src/Orders.Api           # scaffold a migrationlint.json
migrationlint list-rules
migrationlint explain MIG007
migrationlint baseline ./src/Orders.Api        # suggest a baseline for a mature repo
```

Exit codes: `0` clean · `1` violations at/above `--fail-on` · `2` config error · `3` no migrations found.

### Example

Given this EF-generated migration (PostgreSQL):

```csharp
public partial class AddOrderNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Notes", table: "Orders", type: "text", nullable: false);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_Notes", table: "Orders", column: "Notes");
    }
}
```

`migrationlint check ./Migrations` reports both problems before they reach production:

```
✖ MIG004  error   Migrations/20260809103000_AddOrderNotes.cs:13
  Orders.Notes                                                       [failure]

  Adding NOT NULL column 'Orders.Notes' without a default value. This
  statement fails immediately if the table contains any rows.

  Safe alternative:
    Option A — supply a default (single deployment):
      migrationBuilder.AddColumn<string>(
          name: "Notes", table: "Orders",
          nullable: false, defaultValue: "");
    ...

✖ MIG007  error   Migrations/20260809103000_AddOrderNotes.cs:19
  Orders(Notes)                                                      [locking]

  Index 'IX_Orders_Notes' on 'Orders' is created without CONCURRENTLY.
  PostgreSQL blocks writes to the table for the entire index build.

  Safe alternative:
      migrationBuilder.CreateIndex(
              name: "IX_Orders_Notes", table: "Orders", column: "Notes")
          .Annotation("Npgsql:CreatedConcurrently", true);
    ...

2 errors, 0 warnings across 1 migration.
```

The process exits `1`, so the CI step fails and the unsafe migration never merges.

### Deployment plan

`migrationlint plan` reframes the problem as *deployment sequencing* — it turns an unsafe migration
into the safe expand → migrate → contract steps it should have been:

```
20260809103000_AddOrderNotes — 2 deploys recommended

  Deploy 1 · expand (safe to deploy now)
    • Add column Orders.Notes as nullable (no default)      [MIG004]
    • Create index IX_Orders_Notes CONCURRENTLY on Orders   [MIG007]

  Deploy 2 · migrate (backfill / validate)
    • Backfill Orders.Notes in batches, then set NOT NULL   [MIG004]

  The current migration attempts all of this in one step.
```

See [docs/DEPLOYMENT-PLAN.md](docs/DEPLOYMENT-PLAN.md) for the phase model.

## Use in CI (GitHub Action)

Add the action to any workflow — it builds and runs the linter for you:

```yaml
- uses: BeerRebek/MigrationLint@v1
  with:
    path: src/Orders.Api/Migrations
    provider: postgres          # optional; auto-detected otherwise
    fail-on: error              # error | warning | none
```

Violations appear as inline annotations on the PR. To also surface them in the **Security → Code
scanning** tab, emit SARIF and upload it:

```yaml
jobs:
  migration-safety:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      security-events: write
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }     # needed for changed-only
      - uses: BeerRebek/MigrationLint@v1
        with:
          path: src/Orders.Api/Migrations
          changed-only: "true"        # only check migrations added in this PR
          base: main
          sarif-file: migrationlint.sarif
      - uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: migrationlint.sarif
```

| Input | Default | Description |
|---|---|---|
| `path` | `.` | Folder or project/solution root to scan |
| `provider` | auto | `postgres` \| `sqlserver` \| `mysql` \| `sqlite` |
| `config` | — | Path to `migrationlint.json` |
| `fail-on` | `error` | Severity that fails the step |
| `changed-only` / `base` | `false` / `main` | Check only PR-added migrations |
| `sarif-file` | — | Also write a SARIF report to this path |
| `args` | — | Extra raw args passed to `check` |

## Live-DB awareness (opt-in, cuts false positives)

Point the linter at a read-only connection and it uses real database stats to cut noise and add
scale:

- an **empty table can't fail a NOT NULL add** (MIG004/MIG006) → not flagged;
- a **small table doesn't lock long** (MIG007/MIG008/MIG009) → not flagged;
- a **column with zero NULLs** can be set NOT NULL safely (MIG006) → not flagged;
- violation messages gain the real **row count** — e.g. `… (Orders has ~4,238,901 rows)`.

```bash
migrationlint check ./Migrations --connection "$READONLY_CONN" --small-rows 10000
```

- **Read-only and estimate-based** — PostgreSQL uses `pg_class.reltuples`, SQL Server uses
  `sys.partitions` (both metadata, no table scan). Short timeout; it never writes.
- **Fails soft** — if the database is unreachable, it warns and continues with no stats, so the
  zero-config path is never blocked.
- Supported providers: PostgreSQL, SQL Server, MySQL. The threshold is also settable in config
  (`options.smallTableRowThreshold`).

## IDE analyzer (see violations as you type)

`MigrationLint.Analyzers` is a Roslyn analyzer + code-fix package. Reference it from your
migrations project and the same rules run inside Visual Studio / Rider — the diagnostic appears
the moment EF generates the migration, no build or CI round-trip:

![Illustration: the MIG007 diagnostic and its code fix in the editor](docs/img/analyzer-lightbulb.png)

*Illustration of the MIG007 diagnostic and the "Build the index CONCURRENTLY" code fix. A runnable
demo lives in [`samples/SampleApi`](samples/SampleApi/README.md) — open it in Rider/VS to see the
real lightbulb.*


```xml
<PackageReference Include="MigrationLint.Analyzers" Version="0.1.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

(Install from a downloaded release `.nupkg` via a local NuGet source, e.g. a `nuget.config`
`<add key="local" value="./packages" />`.)

Several rules ship with **code fixes** — the lightbulb rewrites the migration for you:
- **MIG007** → appends `.Annotation("Npgsql:CreatedConcurrently", true)` / `SqlServer:Online`
- **MIG004** → adds a typed default value, or makes the column nullable
- **MIG009 / MIG013** → splits into the two-step `NOT VALID` / `WITH NOCHECK` deferred validation
- **any rule** → "suppress with justification" (`[SuppressMigrationLint(...)]`)

Tune severities in `.editorconfig`:

```ini
dotnet_diagnostic.MIG007.severity = error
dotnet_diagnostic.MIG012.severity = none
```

## Configure

`migrationlint.json`, discovered by walking up from the scan path:

```json
{
  "provider": "postgres",
  "baseline": "20260601120000_AddCustomerIndex",
  "deploymentStrategy": "rolling",
  "failOn": "error",
  "rules": { "MIG012": "off" },
  "options": { "smallTables": ["Countries", "Currencies"] }
}
```

## Suppress a reviewed violation

```csharp
[SuppressMigrationLint("Orders is a small lookup table; the scan is trivial.", "MIG008")]
public partial class AddUniqueSku : Migration { }
```

A justification is required — omitting it is itself an error (MIG000).

## Rules

`migrationlint list-rules` shows the whole catalog with default severities:

![migrationlint list-rules](docs/img/list-rules.png)

| Id | Category | Rule |
|----|----------|------|
| MIG007 | locking | Index created without CONCURRENTLY / ONLINE |
| MIG008 | locking | Unique constraint / unique index added (table scan under lock) |
| MIG009 | locking | Foreign key added without deferred validation (PostgreSQL `NOT VALID` / SQL Server `WITH NOCHECK`) |
| MIG010 | locking | Schema changes mixed with data changes |
| MIG013 | locking | Check constraint added without deferred validation |
| MIG014 | locking | Primary key added to an existing table |
| MIG015 | locking | Integer type widened (forces a table rewrite) |
| MIG016 | locking | Column added with a volatile default (forces a table rewrite) |
| MIG019 | locking | Stored computed column added (forces a table rewrite) |
| MIG020 | locking | Index dropped without CONCURRENTLY (PostgreSQL) |
| MIG017 | failure | CONCURRENTLY index without `SuppressTransaction` (fails at runtime) |
| MIG018 | hygiene | Migration checked in without a ModelSnapshot update (drift) |
| MIG001 | dataloss | Column dropped |
| MIG002 | dataloss | Table dropped |
| MIG003 | dataloss | Column or table renamed |
| MIG005 | dataloss | Column type narrowed |
| MIG011 | dataloss | Destructive raw SQL |
| MIG004 | failure | NOT NULL column added without a default |
| MIG006 | failure | Nullable column made NOT NULL |
| MIG012 | hygiene | Too many operations in one migration |
| MIG000 | hygiene | Suppression without justification |

## License

MIT © BeerRebek
