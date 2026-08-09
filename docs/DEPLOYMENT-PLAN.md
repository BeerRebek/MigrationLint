# `migrationlint plan` — deployment sequencing

`check` tells you *whether* a migration is safe. `plan` tells you *how to deploy it safely* — it
reframes an unsafe migration as the sequence of deployments it should have been.

```bash
migrationlint plan ./src/Orders.Api
```

## The three phases

Every step is placed in one of three phases, applied in order:

| Phase | When | Examples |
|---|---|---|
| **expand** | Deploy now — backward-compatible | add a nullable column, `CREATE INDEX CONCURRENTLY`, add a FK `NOT VALID` |
| **migrate** | After expand — backfill / validate | backfill then set `NOT NULL`, `VALIDATE CONSTRAINT`, resolve duplicates then add a unique index |
| **contract** | After the previous deploy is **fully rolled out** | drop a column, drop the old side of a rename |

The number of deploys is the number of distinct phases a migration touches. A migration that only
does expand-phase work is "safe to deploy in one step."

## Example

```
20260809103000_AddOrderNotes — 2 deploys recommended

  Deploy 1 · expand (safe to deploy now)
    • Add column Orders.Notes as nullable (no default)      [MIG004]
    • Create index IX_Orders_Notes CONCURRENTLY on Orders   [MIG007]

  Deploy 2 · migrate (backfill / validate)
    • Backfill Orders.Notes in batches, then set NOT NULL   [MIG004]

  The current migration attempts all of this in one step.
```

Each step is tagged with the rule that motivates the phasing, so `plan` and `check` stay in step:
the same rules that flag an operation decide which deploy it belongs to.

## Options

`plan` accepts `--provider` and `--config` (it reads `migrationlint.json` like `check`). It does not
fail the build — it's a planning aid, so it always exits `0`.
