# Implementation Plan: Core Domain & Persistence Model

**Branch**: `001-core-domain-model` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-core-domain-model/spec.md`

**Note**: Produced by `/speckit-plan` for feature 001; incorporates the clarification session of 2026-09-28 (multi-source article links, topic-XOR-article summary scopes, observation window instances, append-only job attempts, auth-agnostic user identity).

## Summary

Deliver the stable core domain and persistence model for HomNay Live: ten persisted record types — Topic, TopicAlias, TopicTrendObservation, Article, ArticleSourceLink, Source, AiSummary, User, ProcessingJob, ProcessingJobAttempt — plus the supporting TopicArticleLink and summary-derivation links, deterministic normalization/canonicalization/fingerprint rules, and a SQL Server schema (EF Core 10) whose unique constraints, indexes, and no-cascade integrity make ingestion idempotent at the data layer and attribution permanent. The feature ships as two new projects (`HomNayLive.Domain`, `HomNayLive.Persistence`) with unit and SQL Server integration tests. Every record type is additive-friendly for later features (provider keys as data, window descriptors as data, states as enums with room to grow).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS; SDK 10.0.302 available on the dev machine)

**Primary Dependencies**: EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`), `Microsoft.Extensions.Options`/`TimeProvider` abstractions; xUnit for tests

**Storage**: SQL Server (LocalDB for development and tests; production connection string via environment/secure store — never committed)

**Testing**: xUnit — pure unit tests for normalization/canonicalization/fingerprint rules (offline); integration tests for constraints, indexes, delete restrictions, concurrency, and the summary-grounding rule against a real SQL Server LocalDB database (the in-memory provider cannot enforce SQL constraints and MUST NOT be used for these tests)

**Target Platform**: Windows/Linux server (.NET 10), single process; development on Windows

**Project Type**: Backend data layer (domain library + persistence library) inside the .NET modular monolith; no UI or API surface in this feature

**Performance Goals**: Trend-history and lookup queries meet SC-006 (≤1 s at 10× the launch-volume baseline) through index-backed access paths only (FR-014); no table scans on the hot paths

**Constraints**: All persisted timestamps UTC (`datetime2(0)`); no cascade deletes (attribution is indestructible); no distributed-system concerns (single database, single instance); JSON only for provider-specific/non-queried metadata with ISJSON checks; secrets via environment/secure store

**Scale/Scope**: MVP volumes assumed for design targets — ~10k topics, ~100k articles, ~1M trend observations, ~20 sources — with 10× headroom; ten record types + three link/child record types; two new projects + two test projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Pre-Phase 0 gate (constitution v1.0.0):**

- **I. Replaceable Integrations (NON-NEGOTIABLE)** — PASS: provider identity is stored as data (kind + key), core entities carry no provider-specific columns or payload shapes (FR-013); normalization/canonicalization modules are provider-neutral; this feature introduces no provider code.
- **II. Source Attribution and Grounded Summaries (NON-NEGOTIABLE)** — PASS: ArticleSourceLink preserves every discovered source (original URL + earliest-discovery primary attribution, FR-005 as clarified); summaries are scoped topic-XOR-article with at least one grounding link enforced transactionally (research R-010) and audited (SC-005); all foreign keys restrict deletes so attribution cannot be destroyed (FR-015, US4).
- **III. MVP Simplicity (YAGNI)** — PASS: only the record types required by the spec are introduced (eight core concepts + link/attempt records); no repository/mediator/speculative abstractions; single database.
- **IV. Configuration Over Code** — PASS: sources and provider identities are data; URL canonicalization options and connection strings come from configuration/environment; no secrets committed.
- **V. Consistent Time Windows** — PASS: all timestamps are UTC `datetime2(0)` set through a TimeProvider-driven interceptor; observation windows are stored as descriptors supplied by the shared time-window module; no local times, no ad-hoc date arithmetic in persistence.

**Post-Phase 1 design re-check:** the data model, research decisions, and validation artifacts introduce no new violations; Complexity Tracking has no entries to justify.

## Project Structure

### Documentation (this feature)

```text
specs/001-core-domain-model/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

Note: `contracts/` is intentionally not created — this feature exposes no external interfaces (the public API arrives with feature 014); its interfaces are the domain/persistence types documented in `data-model.md`.

### Source Code (repository root)

The repository currently contains only specs and Spec Kit tooling; this feature creates the first code:

```text
HomNayLive.sln
src/
├── HomNayLive.Domain/                 # Entities + pure deterministic rules (no EF dependency)
│   ├── Entities/                      # Topic, TopicAlias, TopicTrendObservation, Article, ArticleSourceLink, Source, AiSummary, TopicArticleLink, AiSummarySourceLink, User, ProcessingJob, ProcessingJobAttempt
│   ├── Enums/                         # WindowType, SourceState, ArticleLifecycleState, ProcessingJobState, ProcessingJobAttemptOutcome
│   └── Normalization/                 # NameNormalizer, UrlCanonicalizer (+options), ContentHasher
└── HomNayLive.Persistence/            # EF Core model + migrations
    ├── HomNayLiveDbContext.cs
    ├── Configurations/                # IEntityTypeConfiguration per record type
    ├── Interceptors/                  # UTC audit timestamps via TimeProvider
    └── Migrations/                    # InitialCoreModel, later additions

tests/
├── HomNayLive.Domain.Tests/           # Offline unit tests (normalization, canonicalization, hashing)
└── HomNayLive.Persistence.Tests/      # SQL Server LocalDB integration tests (constraints, indexes, deletes, concurrency, grounding rule)
```

**Structure Decision**: Two projects only (domain + persistence) — the smallest layout that keeps EF Core out of the domain and lets later features (002+ adapters, 013 scheduler, 014 API) attach new projects without reshaping this one. Tests split unit vs. SQL Server integration exactly along that boundary.

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

No constitution violations — no justifications required.
