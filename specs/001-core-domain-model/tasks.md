---

description: "Task list for feature 001 implementation"
---

# Tasks: Core Domain & Persistence Model

**Input**: Design documents from `/specs/001-core-domain-model/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md (R-001…R-014), data-model.md, quickstart.md

**Tests**: INCLUDED — the plan (research R-013) and the constitution's quality gates explicitly require offline unit tests for the deterministic rules and SQL Server LocalDB integration tests for data-layer guarantees. Test tasks appear inside each user story phase.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., [US1], [US2])
- Include exact file paths in descriptions

## Path Conventions

Single-project layout per plan.md: `src/HomNayLive.Domain/`, `src/HomNayLive.Persistence/`, `tests/HomNayLive.Domain.Tests/`, `tests/HomNayLive.Persistence.Tests/`; solution `HomNayLive.sln` at the repository root.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution and project initialization per plan.md structure

- [X] T001 Create solution and projects with references: `HomNayLive.sln`; classlibs `src/HomNayLive.Domain/HomNayLive.Domain.csproj` and `src/HomNayLive.Persistence/HomNayLive.Persistence.csproj` (references Domain); xunit projects `tests/HomNayLive.Domain.Tests/HomNayLive.Domain.Tests.csproj` (references Domain) and `tests/HomNayLive.Persistence.Tests/HomNayLive.Persistence.Tests.csproj` (references Persistence); add all to the solution
- [X] T002 [P] Add packages per research R-002: `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.EntityFrameworkCore.Design` (10.x) to `src/HomNayLive.Persistence/HomNayLive.Persistence.csproj`; `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` to both test csproj files; Domain project stays dependency-free
- [X] T003 [P] Add root `.gitignore` covering .NET build artifacts (`bin/`, `obj/`, `*.user`, `.vs/`, `TestResults/`) so build outputs are never committed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, deterministic rules, DbContext shell, and test harness that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create enums per data-model.md in `src/HomNayLive.Domain/Enums/`: `WindowType.cs`, `SourceState.cs`, `ArticleLifecycleState.cs`, `ProcessingJobState.cs`, `ProcessingJobAttemptOutcome.cs`
- [X] T005 [P] Create entities `Topic` and `TopicAlias` in `src/HomNayLive.Domain/Entities/Topic.cs`, `src/HomNayLive.Domain/Entities/TopicAlias.cs`
- [X] T006 [P] Create entities `Article` and `ArticleSourceLink` in `src/HomNayLive.Domain/Entities/Article.cs`, `src/HomNayLive.Domain/Entities/ArticleSourceLink.cs`
- [X] T007 [P] Create entity `Source` in `src/HomNayLive.Domain/Entities/Source.cs`
- [X] T008 [P] Create entity `TopicTrendObservation` in `src/HomNayLive.Domain/Entities/TopicTrendObservation.cs`
- [X] T009 [P] Create entities `AiSummary`, `AiSummarySourceLink`, `TopicArticleLink` in `src/HomNayLive.Domain/Entities/AiSummary.cs`, `src/HomNayLive.Domain/Entities/AiSummarySourceLink.cs`, `src/HomNayLive.Domain/Entities/TopicArticleLink.cs`
- [X] T010 [P] Create entities `User`, `ProcessingJob`, `ProcessingJobAttempt` in `src/HomNayLive.Domain/Entities/User.cs`, `src/HomNayLive.Domain/Entities/ProcessingJob.cs`, `src/HomNayLive.Domain/Entities/ProcessingJobAttempt.cs`
- [X] T011 [P] Implement `NameNormalizer` (trim → collapse whitespace → NFC → invariant lowercase; diacritics preserved) in `src/HomNayLive.Domain/Normalization/NameNormalizer.cs` per research R-004
- [X] T012 [P] Implement `UrlCanonicalizer` + `UrlCanonicalizationOptions` (lowercase scheme/host, punycode, default-port removal, fragment drop, configurable tracking-parameter stripping, query sorting, slash collapsing, trailing-slash rule) in `src/HomNayLive.Domain/Normalization/UrlCanonicalizer.cs`, `src/HomNayLive.Domain/Normalization/UrlCanonicalizationOptions.cs` per research R-005
- [X] T013 [P] Implement `ContentHasher` (SHA-256 lowercase hex over NFC/whitespace-normalized, case-preserved text) in `src/HomNayLive.Domain/Normalization/ContentHasher.cs` per research R-006
- [X] T014 Create `HomNayLiveDbContext` with DbSets for all twelve record types in `src/HomNayLive.Persistence/HomNayLiveDbContext.cs` (depends on T005–T010)
- [X] T015 Implement `UtcAuditInterceptor` (TimeProvider-driven `CreatedAtUtc`/`UpdatedAtUtc`) in `src/HomNayLive.Persistence/Interceptors/UtcAuditInterceptor.cs` per research R-012
- [X] T016 Create `HomNayLiveDesignTimeFactory` reading `HOMNAY_DB_CONNECTION` with LocalDB fallback in `src/HomNayLive.Persistence/HomNayLiveDesignTimeFactory.cs` per research R-014 (no secrets committed)
- [X] T017 [P] Create SQL Server LocalDB test harness (fresh per-run database fixture + context factory) in `tests/HomNayLive.Persistence.Tests/Infrastructure/SqlServerLocalDbFixture.cs` per research R-013

**Checkpoint**: Foundation ready — user story implementation can begin

---

## Phase 3: User Story 1 - Persist the MVP knowledge chain (Priority: P1) 🎯 MVP

**Goal**: The full schema exists and one complete trend cycle (observation → topic/alias → article/source links → summary/grounding → user → job/attempts) persists and reconstructs from stored data alone.

**Independent Test**: Seed one cycle, dispose the context, reopen, and verify every element and attribution resolves (quickstart V1, SC-001).

- [X] T018 [US1] Create entity configurations with constraints/indexes/rowversion for `Topic`, `TopicAlias`, `TopicTrendObservation` in `src/HomNayLive.Persistence/Configurations/TopicConfiguration.cs`, `TopicAliasConfiguration.cs`, `TopicTrendObservationConfiguration.cs` (BIN2 collations, window-instance unique index, provider/time indexes per data-model.md)
- [X] T019 [P] [US1] Create configurations for `Article`, `ArticleSourceLink`, `Source` in `src/HomNayLive.Persistence/Configurations/ArticleConfiguration.cs`, `ArticleSourceLinkConfiguration.cs`, `SourceConfiguration.cs` (unique canonical URL, filtered one-primary-link index, original URL, unique source key)
- [X] T020 [P] [US1] Create configurations for `AiSummary` (scope XOR check constraint), `AiSummarySourceLink`, `TopicArticleLink` in `src/HomNayLive.Persistence/Configurations/AiSummaryConfiguration.cs`, `AiSummarySourceLinkConfiguration.cs`, `TopicArticleLinkConfiguration.cs`
- [X] T021 [P] [US1] Create configurations for `User`, `ProcessingJob`, `ProcessingJobAttempt` in `src/HomNayLive.Persistence/Configurations/UserConfiguration.cs`, `ProcessingJobConfiguration.cs`, `ProcessingJobAttemptConfiguration.cs` (unique identity reference, jobs by type/state/time index, attempt-number unique per job)
- [ ] T022 [US1] Wire all configurations + the audit interceptor into `HomNayLiveDbContext` and generate the `InitialCoreModel` migration in `src/HomNayLive.Persistence/Migrations/` (depends on T018–T021)
- [ ] T023 [US1] Create the reusable knowledge-chain seed fixture (one complete cycle incl. a grounding link) in `tests/HomNayLive.Persistence.Tests/Fixtures/KnowledgeChainFixture.cs`
- [ ] T024 [P] [US1] Integration test: full chain persists and reconstructs after dispose/reopen with attribution intact (SC-001, V1) in `tests/HomNayLive.Persistence.Tests/KnowledgeChainTests.cs`
- [ ] T025 [P] [US1] Integration test: scope XOR constraint rejects summaries with both or neither scope set (FR-008) in `tests/HomNayLive.Persistence.Tests/SummaryScopeTests.cs`
- [ ] T026 [P] [US1] Integration test: a job run persists lifecycle state, timing (UTC), and diagnostics reference after completion/failure (US1 acceptance 3) in `tests/HomNayLive.Persistence.Tests/JobRecordTests.cs`

**Checkpoint**: User Story 1 fully functional and testable independently

---

## Phase 4: User Story 2 - Trustworthy, idempotent data (Priority: P1)

**Goal**: Re-ingestion never duplicates topics, articles, observations, links, or summaries; variant spellings/URLs resolve to single records; window re-samples refresh; attempts stay distinguishable.

**Independent Test**: Re-run an identical batch (with variants and syndication) and verify zero new records of every kind (SC-002/SC-003, quickstart V2/V3).

### Tests for User Story 2

> **NOTE: Write the unit tests first where applicable and ensure they FAIL before implementing behavior they cover**

- [ ] T027 [P] [US2] Unit tests: `NameNormalizer` fixture set (casing, whitespace runs, composed/decomposed diacritics; no diacritic stripping) in `tests/HomNayLive.Domain.Tests/NameNormalizerTests.cs` (SC-003)
- [ ] T028 [P] [US2] Unit tests: `UrlCanonicalizer` fixture set (tracking params, casing, default ports, trailing slash, query sorting) in `tests/HomNayLive.Domain.Tests/UrlCanonicalizerTests.cs` (SC-003)
- [ ] T029 [P] [US2] Unit tests: `ContentHasher` determinism and normalization behavior in `tests/HomNayLive.Domain.Tests/ContentHasherTests.cs`
- [ ] T030 [P] [US2] Integration test: identical re-ingestion creates zero duplicates across all tables (SC-002) in `tests/HomNayLive.Persistence.Tests/IdempotencyTests.cs`
- [ ] T031 [P] [US2] Integration test: variant names/URLs resolve to single records; a syndicated second source adds only an `ArticleSourceLink` and the earliest discovery remains primary (SC-003, FR-005 clarifications) in `tests/HomNayLive.Persistence.Tests/SyndicationTests.cs`
- [ ] T032 [P] [US2] Integration test: re-sampling the same window refreshes the single record (latest values, `ObservedAtUtc` advanced) and the next window instance appends (V3) in `tests/HomNayLive.Persistence.Tests/ObservationWindowTests.cs`
- [ ] T033 [P] [US2] Integration test: a retried job keeps both attempts as separate distinguishable records (US2 acceptance 3) in `tests/HomNayLive.Persistence.Tests/JobAttemptHistoryTests.cs`

### Implementation for User Story 2

- [ ] T034 [US2] Implement the observation upsert path (refresh semantics: provided fields overwrite, nulls leave existing values; rowversion retry loop) in `src/HomNayLive.Persistence/WritePaths/ObservationWriter.cs` per research R-007/R-011
- [ ] T035 [US2] Integration test: two concurrent re-samples of the same window converge to one row without lost updates (rowversion retry) in `tests/HomNayLive.Persistence.Tests/ObservationConcurrencyTests.cs`

**Checkpoint**: User Stories 1 AND 2 both work independently

---

## Phase 5: User Story 3 - Provider isolation and extension seams (Priority: P2)

**Goal**: Providers are data (`ProviderKind`/`ProviderKey`/`ModelKey` + metadata JSON); onboarding a new provider requires zero schema changes; no provider-specific coupling exists.

**Independent Test**: Add a second provider via data only and verify observations coexist with no schema change (SC-004, quickstart V4).

- [ ] T036 [P] [US3] Integration test: onboard a second trend provider (new kind/key) via data only — no schema or migration change (SC-004, V4) in `tests/HomNayLive.Persistence.Tests/ProviderIsolationTests.cs`
- [ ] T037 [P] [US3] Integration test: JSON metadata acceptance (`ISJSON`) — valid metadata persists on observations/articles/summaries; malformed metadata is rejected by the check constraint in `tests/HomNayLive.Persistence.Tests/MetadataJsonTests.cs`
- [ ] T038 [P] [US3] Guard test: reflection audit asserting entity types expose no provider-specific properties beyond the documented `ProviderKind`/`ProviderKey`/`ModelKey` (FR-013) in `tests/HomNayLive.Persistence.Tests/ProviderCouplingGuardTests.cs`

**Checkpoint**: All provider-isolation guarantees verified

---

## Phase 6: User Story 4 - Lifecycle-independent records (Priority: P3)

**Goal**: Records survive processing/content lifecycles — no cascades or hard deletes, attribution survives disabling, summaries cannot exist without grounding links, histories remain queryable.

**Independent Test**: Disable a source, attempt destructive deletes, try to persist a link-less summary; verify everything is prevented/preserved (quickstart V5/V6).

### Implementation for User Story 4

- [ ] T039 [US4] Implement the transactional summary write path (summary + first grounding link committed atomically; links undeletable) in `src/HomNayLive.Persistence/WritePaths/SummaryWriter.cs` per research R-010

### Tests for User Story 4

- [ ] T040 [P] [US4] Integration test: a summary without any grounding link cannot be committed through the write path; failures leave no partial rows (SC-005) in `tests/HomNayLive.Persistence.Tests/SummaryGroundingTests.cs`
- [ ] T041 [P] [US4] Integration test: hard deletes of sources, articles, and summary links are rejected by restricted FKs; a disabled source's attribution still resolves (FR-015, US4 acceptance 1, V5) in `tests/HomNayLive.Persistence.Tests/DeleteRestrictionTests.cs`
- [ ] T042 [P] [US4] Integration test: observation history and attempt history remain fully queryable after lifecycle changes (US4 acceptance 2–3, SC-007) in `tests/HomNayLive.Persistence.Tests/HistoryRetentionTests.cs`
- [ ] T043 [P] [US4] Automated SC-005 audit: seeded data passes the dangling-summary audit query (zero rows) in `tests/HomNayLive.Persistence.Tests/SummaryAuditTests.cs`

**Checkpoint**: All user stories independently functional

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation and hardening across all stories

- [ ] T044 Run the full test suite; stabilize the LocalDB harness (deterministic create/drop of per-run databases; no cross-test leakage) in `tests/HomNayLive.Persistence.Tests/Infrastructure/`
- [ ] T045 [P] Create and run the performance smoke script toward SC-006: seed ~10× baseline volumes and verify index seeks + 1-second budgets (quickstart V8) in `scripts/perf-smoke.ps1`
- [ ] T046 Validate every quickstart.md command end-to-end (restore/build/migrate/test) and fix documentation drift in `specs/001-core-domain-model/quickstart.md`
- [ ] T047 Final consistency pass: cross-check the data-model.md requirement→enforcement map against the shipped configurations; confirm no secrets/connection strings committed and Domain stays dependency-free; commit

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 must land first (it delivers the schema every other story exercises)
  - US2, US3, US4 can then proceed in parallel (they add write paths/tests over the US1 schema)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Foundational — no other story dependencies
- **US2 (P1)**: After US1 (needs tables/constraints) — independent of US3/US4
- **US3 (P2)**: After US1 — independent of US2/US4
- **US4 (P3)**: After US1 — independent of US2/US3 (its `SummaryWriter` is the only write-path overlap in folder, not file)

### Within Each User Story

- Unit tests for deterministic rules are written and fail before behavior changes they target
- Entities/configurations before migrations; write paths before their integration tests
- Story complete and validated before moving to the next priority

### Parallel Opportunities

- T002–T003 in Setup; T004–T013 and T017 in Foundational (different files)
- US1: T019–T021 configurations in parallel; T024–T026 tests in parallel
- US2: T027–T029 unit tests in parallel; T030–T033 tests in parallel
- US3: T036–T038 all parallel
- US4: T040–T043 tests in parallel
- Polish: T045 parallel with T044/T046

---

## Parallel Example: User Story 1

```bash
# Launch configurations together (different files):
Task: "Create configurations for Article, ArticleSourceLink, Source in src/HomNayLive.Persistence/Configurations/"
Task: "Create configurations for AiSummary, AiSummarySourceLink, TopicArticleLink in src/HomNayLive.Persistence/Configurations/"
Task: "Create configurations for User, ProcessingJob, ProcessingJobAttempt in src/HomNayLive.Persistence/Configurations/"

# Launch US1 tests together after T022/T023:
Task: "Integration test: chain reconstruction in tests/HomNayLive.Persistence.Tests/KnowledgeChainTests.cs"
Task: "Integration test: scope XOR constraint in tests/HomNayLive.Persistence.Tests/SummaryScopeTests.cs"
Task: "Integration test: job record final state in tests/HomNayLive.Persistence.Tests/JobRecordTests.cs"
```

## Parallel Example: User Story 2

```bash
# Launch unit tests together (different files, offline):
Task: "Unit tests: NameNormalizer fixtures in tests/HomNayLive.Domain.Tests/NameNormalizerTests.cs"
Task: "Unit tests: UrlCanonicalizer fixtures in tests/HomNayLive.Domain.Tests/UrlCanonicalizerTests.cs"
Task: "Unit tests: ContentHasher determinism in tests/HomNayLive.Domain.Tests/ContentHasherTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1
4. **STOP and VALIDATE**: quickstart V1 passes
5. US2 completes the P1 trust guarantees (SC-002/SC-003) and is strongly recommended before any downstream feature builds on this schema

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → validate → the knowledge chain is persistable (MVP)
3. US2 → validate → idempotency guarantees hold
4. US3 + US4 → validate → extensibility and lifecycle guarantees hold
5. Polish → performance smoke + quickstart validation

### Parallel Team Strategy

After Foundational and US1: Developer A takes US2 (idempotency/write paths), Developer B takes US3 (provider isolation tests), Developer C takes US4 (lifecycle/grounding) — different files throughout.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] labels map tasks to user stories for traceability
- Every task includes its exact file path
- EF InMemory is banned for constraint tests (research R-013)
- Commit after each task or logical group (single repo, `main` branch)
