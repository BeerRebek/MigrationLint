# SampleApi — MigrationLint analyzer demo

A minimal EF Core project wired to the **MigrationLint Roslyn analyzer**, so you can see the
diagnostics and lightbulb code fixes live in Visual Studio or Rider (and capture a real screenshot).

`Migrations/20260809103000_AddOrderNotes.cs` deliberately contains two violations:
- **MIG007** — `CreateIndex` without `CONCURRENTLY` on PostgreSQL.
- **MIG004** — a NOT NULL column added without a default.

## Run it

The analyzer is consumed as its NuGet package (which bundles `MigrationLint.Core`). Pack it once
into the local `packages/` source, pinned to the stable version:

```bash
# from the repository root
dotnet pack src/MigrationLint.Analyzers/MigrationLint.Analyzers.csproj \
    -c Release -o samples/SampleApi/packages -p:MinVerVersionOverride=0.1.0
```

Then restore/build the sample:

```bash
dotnet build samples/SampleApi/SampleApi.csproj
```

You'll see `MIG007`/`MIG004` reported by the analyzer during the build.

## See the lightbulb

Open `samples/SampleApi` in **Rider** or **Visual Studio**, open
`Migrations/20260809103000_AddOrderNotes.cs`, and put the cursor on the squiggled
`migrationBuilder.CreateIndex(...)` call. The lightbulb offers:

- **Build the index CONCURRENTLY (PostgreSQL)** — inserts `.Annotation("Npgsql:CreatedConcurrently", true)`
- **Suppress MIG007 for this migration**

That popup is the real version of `docs/img/analyzer-lightbulb.png` — screenshot it and drop it in
over that file.

> Tip: the `.editorconfig` here sets the rules to `error` (red squiggles, best for a screenshot).
> The IDE shows them live even though `dotnet build` will then report errors. Switch them to
> `warning` if you'd rather the sample build cleanly.
