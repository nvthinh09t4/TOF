# Quickstart Validation Guide: Core Domain & Persistence Model (Feature 001)

Runnable validation scenarios that prove feature 001 works end-to-end. This is a run/validation guide, not an implementation manual — details live in `plan.md`, `research.md`, and `data-model.md`.

## Prerequisites

- .NET 10 SDK (`dotnet --list-sdks` shows a 10.x band).
- SQL Server LocalDB: `sqllocaldb info MSSQLLocalDB` reports an instance (start it with `sqllocaldb start MSSQLLocalDB` if needed).
- Optional: `HOMNAY_DB_CONNECTION` environment variable to override the connection string (defaults to LocalDB; no secrets committed).

## Setup

```powershell
dotnet restore
dotnet build
dotnet ef database update --project src/HomNayLive.Persistence --startup-project src/HomNayLive.Persistence
```

Expected: build succeeds; `InitialCoreModel` migration applies cleanly, creating all twelve tables from `data-model.md`.

## Run the tests

```powershell
dotnet test
# Focused runs:
dotnet test tests/HomNayLive.Domain.Tests                                   # normalization/canonicalization/hashing (offline)
dotnet test tests/HomNayLive.Persistence.Tests                              # constraints/indexes/deletes/concurrency (LocalDB)
```

Expected: all pass; persistence tests create a fresh per-run LocalDB database.

## Validation scenarios

### V1 — MVP knowledge chain reconstructs after restart (US1, SC-001)

1. Seed one full cycle via the persistence fixtures: trend observation → topic (+alias) → article (+source, canonical URL, hash) → topic-article link → summary + grounding link → user → job + attempts.
2. Dispose the context, open a new one, re-query the chain.
Expected: every element resolves; the summary shows its grounding article; the article shows its source attribution and original URL; nothing is lost.

### V2 — Idempotent re-ingestion (US2, SC-002, SC-003)

1. Re-run the identical seed batch, including: the same topic under casing/whitespace/diacritic variants, and the same article via URL variants (tracking params, casing, syndicated via a second source).
2. Count rows per table before/after.
Expected: zero new Topics/Articles/Observations; duplicate attempts raise the expected unique-constraint behavior at the store; the syndicated copy adds only an `ArticleSourceLink` (with the second source), and the earliest discovery remains primary.

### V3 — Window instance refresh vs. history (FR-004 clarified)

1. Insert an observation for (topic, provider, today-window).
2. Re-sample the same window with updated rank/score.
3. Insert an observation for the next window instance.
Expected: step 2 updates the single row (latest values; `ObservedAtUtc` advanced); step 3 appends a second row; total rows = 2.

### V4 — Provider isolation (US3, SC-004)

1. Add a second provider key (`providerKind`/`providerKey`) for a new "trend" source via data only.
Expected: no schema change, no migration; observation rows coexist.

### V5 — Lifecycle, attribution, and delete protection (US4, FR-015)

1. Disable a source; query historical articles and summaries of that source.
2. Attempt to hard-delete the source, the article, and a summary grounding link.
Expected: attribution still resolves via retained records; all delete attempts fail with FK restriction errors (no cascades anywhere).

### V6 — Summary grounding audit (SC-005)

```sql
SELECT s.Id
FROM AiSummaries s
WHERE NOT EXISTS (SELECT 1 FROM AiSummarySourceLinks l WHERE l.AiSummaryId = s.Id);
-- Expected: zero rows (and integration tests prove a summary cannot be committed without a link)
```

### V7 — Job attempt history (US2 acceptance 3, SC-007)

1. Run a job through failure and retry (two attempts).
Expected: both attempts are separate rows with outcomes/timings; the run row shows final state; nothing overwritten.

### V8 — Performance smoke toward SC-006

1. Populate ~10× the design baseline (≈100k topics, ≈1M observations, ≈1M articles) with a seeding script.
2. Run the representative queries (topic history by window range; observations by provider/time; articles by topic) with `SET STATISTICS IO ON`.
Expected: index seeks on the `data-model.md` index set; each query within the 1-second budget.

## Success criteria mapping

| Scenario | Covers |
|---|---|
| V1 | SC-001, US1 |
| V2 | SC-002, SC-003, US2, FR-016 |
| V3 | FR-004 (clarified semantics) |
| V4 | SC-004, FR-013 |
| V5 | US4, FR-015 |
| V6 | SC-005, FR-008 |
| V7 | SC-007, FR-010 |
| V8 | SC-006, FR-014 |
