# Feature Specification: Topic Timeline (Backend & API)

**Feature Branch**: `019-topic-timeline`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement a topic timeline as an extension of the Topic model. The timeline should represent how a topic evolves over time using existing TrendObservation data and newly discovered relevant Articles. Where necessary, introduce dedicated extension entities rather than adding timeline-specific state directly to Topic. The timeline should allow the system to show: when the topic was first detected; changes in trend observations; significant changes in trend score; newly discovered relevant articles; important topic updates. Historical observations must be immutable or append-only where practical. Provide an API contract that can be consumed by the topic detail page without coupling the frontend to persistence models."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Timeline events assembled correctly from existing data (Priority: P1)

A repeatable assembly process derives a topic's evolution history from data the platform already stores — trend observations, trend score records, relevant article links, and cluster membership changes — and records it as append-only timeline entries of defined types: first detected, observation change, significant score shift, new relevant article, and important topic update. Significance (which changes deserve an entry) is decided by a single-sourced, configurable rulebook so timelines stay meaningful rather than noisy.

**Why this priority**: This is the feature's core value: turning raw, high-frequency signals into a readable evolution story for a topic.

**Independent Test**: Fixture topics with scripted observation/score/article/cluster histories; run assembly and verify exactly the expected entries of each type at significance boundaries — including the single first-detected entry and the absence of entries for non-significant changes.

**Acceptance Scenarios**:

1. **Given** a topic's stored observations, **When** assembly runs, **Then** a first-detected entry exists exactly once, derived from the earliest observation, and observation-change entries appear only where the rulebook's significance criteria are met.
2. **Given** trend score records, **When** a significant shift occurs (category transition or delta beyond the configured threshold, version-aware), **Then** a score-shift entry records the from/to values and algorithm version.
3. **Given** newly discovered relevant article links (relevance-band gated), **When** assembly runs, **Then** article entries are recorded carrying public article references (title, source, original URL).
4. **Given** cluster membership changes for the topic, **When** they occur, **Then** topic-update entries describe them (display-safe summary only).

---

### User Story 2 - Append-only history with idempotent, convergent assembly and backfill (Priority: P1)

Timeline entries are immutable and append-only (never edited, never deleted), and the assembly process is idempotent and convergent: re-running it adds nothing; late-arriving or backfilled source data is appended as historical entries ordered by their original occurrence time; unique deduplication keys prevent duplicates even under concurrent runs; and no source record (observation, score, link, membership) is ever modified by this feature.

**Why this priority**: Explicitly required immutability/append-only guarantees; idempotency protects the history's integrity as background processing repeats continuously.

**Independent Test**: Run assembly repeatedly (expect zero delta), replay with concurrent runs and late backfills (expect deduplicated historical entries in correct time order), and audit immutability of entries and sources.

**Acceptance Scenarios**:

1. **Given** a completed assembly, **When** it reruns unchanged, **Then** exactly zero new entries are added.
2. **Given** backfilled historical observations/articles, **When** assembly processes them, **Then** entries appear at their original occurrence times (order by occurrence, not insertion) with no duplicates.
3. **Given** concurrent assembly runs, **When** they complete, **Then** dedupe keys guarantee single entries and no run fails from races.
4. **Given** any timeline entry or source record, **When** audits run, **Then** zero modifications or deletions are found.

---

### User Story 3 - Stable API contract for the topic detail page (Priority: P2)

The timeline is exposed through the public API as an additive, versioned contract — a paginated endpoint (or additive details extension) returning mapped timeline DTOs whose entries carry type, occurrence time (UTC), and display-safe summaries: score shifts include from/to categories; article entries carry the public article reference (title, source name, original URL) with attribution intact. The topic detail page consumes these DTOs without any coupling to persistence models, and internal changes (new entry sources, significance tuning) never alter contract shapes.

**Why this priority**: Explicitly required — the contract is the deliverable that unlocks the waiting extension seam on the topic detail page (feature 018).

**Independent Test**: Golden contract fixtures per entry type; simulate internal changes (significance thresholds, new source data) and verify zero DTO shape diffs; verify pagination ordering, validation errors, and caching validators per the public API rules.

**Acceptance Scenarios**:

1. **Given** a topic with timeline entries, **When** the endpoint is called, **Then** paginated DTOs return entries in deterministic order with type, occurrence time, and type-specific display fields — no internal identifiers or provider data.
2. **Given** article-bearing entries, **When** they render, **Then** the public article reference (title, source name, original URL) is present — attribution preserved end to end.
3. **Given** simulated internal changes, **When** the same requests replay, **Then** DTO shapes and required fields are unchanged (additive-only evolution per the API rules).
4. **Given** the caching policy, **When** timeline responses are cached and revalidated, **Then** validators/304 behavior follows the platform's caching rules.

---

### User Story 4 - Extension-ready without scope creep (Priority: P3)

The design stays extensible without overreaching: entry types are enumerable and additive (new types can be introduced without breaking contracts or consumers), the API remains consumable by the topic detail page's reserved extension seam, and everything this feature must NOT do stays out of scope — no timeline UI rendering, no notifications, no personalization, no related-topics work, no predictions/forecasts, and no AI-generated commentary.

**Why this priority**: Keeps the MVP disciplined while guaranteeing that a future frontend timeline feature and richer sources plug in cleanly.

**Independent Test**: Audit scope boundaries; introduce a hypothetical additional entry type via fixtures and verify additive handling (existing consumers unaffected); verify the topic detail page can consume the contract through its seam without persistence coupling.

**Acceptance Scenarios**:

1. **Given** a new entry type addition, **When** it appears in responses, **Then** existing contract fields remain unchanged and consumers tolerate the addition per the documented evolution rules.
2. **Given** the topic detail page's extension seam, **When** the timeline contract is consumed, **Then** no persistence-model coupling is required (DTO-only integration).
3. **Given** the feature scope, **When** audited, **Then** zero UI rendering, notifications, personalization, predictions, or AI commentary logic exists.

---

### Edge Cases

- Non-significant observation changes (below rulebook thresholds) → no entry; timelines stay meaningful, never a raw dump.
- Missing score records for a topic → no score-shift entries; other entry types unaffected.
- Score algorithm version change (coexisting versions) → version-aware entries (from/to categories carry the algorithm version involved); no shape change.
- Relevance classification shifts an article below the exposure band after it was timeline-recorded → the entry remains (append-only history records what was true at the time; it is not retroactively removed).
- Topic whose earliest observation predates timeline feature deployment → first-detected entries are backfilled from stored observations (history is derived, not lost).
- Backfill of very old data en masse → bounded batch processing; occurred-at ordering keeps the timeline correct regardless of processing order.
- Duplicate source events (recollection prevented upstream, but defensive) → dedupe keys collapse them to one entry.
- Concurrent assembly runs or overlapping schedules → unique dedupe keys resolve races deterministically; no failures.
- A topic that stops producing signals → its timeline simply stops growing; existing entries remain.
- Entry summaries with long article titles or long alias forms → display-safe bounded summaries with deterministic truncation rules (documented; no mid-word loss for URLs — URLs are never truncated in payloads).
- Deleted or disabled sources referenced by historical article entries → entries remain resolvable; source display follows public exposure rules (attribution retained).
- Pagination while assembly appends concurrently → deterministic ordering and documented stability semantics (per the API rules); no duplicates or skips within a stable read.
- Cluster membership churn (rapid join/leave within 008 rules) → each episode meeting significance rules yields one entry; churn below thresholds is suppressed.
- A hypothetical new entry type added later → responses remain additive; existing consumers tolerate unknown types per the evolution rules.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST be additive: a new **TopicTimelineEntry** extension entity stores timeline records; Topic, TopicTrendObservation, score records, link/relevance records, articles, and cluster memberships MUST NOT be modified — they are read-only inputs; no timeline state is added to Topic.
- **FR-002**: Entry types MUST include at least: first detected, observation change, significant score shift, new relevant article, and important topic update; types MUST be enumerable and additive (new types introducible without breaking contracts or consumers); each entry MUST carry its type, occurrence time (UTC), a deterministic deduplication key, display-safe summary fields for its type, and stable references to the underlying public-safe records.
- **FR-003**: Significance MUST be governed by a single-sourced, configurable rulebook: observation-change criteria (for example, appearance of new windows/cadence or rank/strength shifts beyond thresholds), score-shift criteria (category transitions or delta beyond thresholds, version-aware), article-event gating (relevance band), and topic-update criteria (cluster membership episodes meeting significance); identical inputs yield identical entries (deterministic).
- **FR-004**: Exactly one first-detected entry per topic MUST exist, derived from the earliest stored observation; missing history (topics predating this feature) MUST be backfilled from stored data.
- **FR-005**: Observation-change entries MUST reference the underlying observation and MUST NOT alter it; raw observations remain append-only exactly as established by the trend collection feature.
- **FR-006**: Score-shift entries MUST record from/to display values (for example, category transitions like rising to exploding) and the algorithm version involved, per the append-only score history semantics.
- **FR-007**: Article entries MUST be created for newly discovered relevant article links within the configured relevance band and MUST carry the public article reference — title, source name, original article URL — preserving attribution (constitution Principle II).
- **FR-008**: Topic-update entries MUST cover cluster membership changes (join, merge outcomes, split outcomes, dissolution) as display-safe summaries (for example, canonical cluster name transitions), without exposing internal mechanics.
- **FR-009**: Assembly MUST run as a repeatable, restartable background pass (recorded as a ProcessingJob run with per-type counts); it MUST be idempotent (re-runs add zero entries), convergent, and safe under concurrent or overlapping runs (unique deduplication keys resolve races deterministically without failures).
- **FR-010**: Late-arriving or backfilled source data MUST be appended as historical entries ordered by occurrence time (UTC), not by insertion order; timeline ordering (occurred-at with defined tie-breakers) MUST be deterministic and pagination-stable per the public API's rules.
- **FR-011**: The timeline MUST be exposed as an additive, versioned public API contract (a paginated endpoint and/or additive topic-details extension): mapped DTOs per entry type with type, occurrence time (UTC), and display-safe fields; no internal identifiers, provider data, job records, or persistence models may leak; frontend consumption MUST require no persistence coupling (the topic detail page's reserved extension seam can consume it directly).
- **FR-012**: Contract stability MUST hold under internal change: new entry sources, significance tuning, or algorithm-version changes MUST NOT alter contract shapes or required fields (additive-only evolution under the API feature's rules); unknown entry types MUST be tolerated by consumers per documented evolution rules.
- **FR-013**: Timeline responses MUST participate in the platform's caching policy as a new cacheable class (additive reconciliation with the caching feature): deterministic validators, conditional-request support (304), and freshness tokens per the established rules.
- **FR-014**: Performance MUST be bounded: timeline queries are index-backed (topic + occurrence time) with paginated results within the established latency targets; assembly processes bounded batches and never performs unbounded scans.
- **FR-015**: This feature MUST NOT implement: timeline UI rendering (a future frontend feature consumes the contract via the topic detail page's extension seam), notifications, personalization, related topics, predictions/forecasts, or AI-generated commentary; it MUST NOT modify the semantics of observations, scores, matching, relevance, clustering, or articles.
- **FR-016**: The feature MUST be testable offline and deterministically: fixtures cover every entry type, significance boundaries (in/out), first-detected derivation and backfill, idempotency/concurrency, backfill ordering, pagination determinism, golden contract tests per entry type, unknown-type tolerance, caching validators, and immutability audits.
- **FR-017**: Operations MUST be observable: assembly runs record counts per entry type and outcome categories via ProcessingJob records (concordant with the background processing feature); failures follow the platform's error taxonomy and never leave partial or duplicated entries.

### Key Entities

This feature is additive: one new append-only entity, a rulebook, an assembly pass, and an additive API contract; all existing entities remain untouched.

- **TopicTimelineEntry (new; append-only)**: one immutable timeline record: topic reference; entry type; occurrence time (UTC); deterministic deduplication key (unique); type-specific display-safe summary fields (for example, score from/to categories and algorithm version; article public reference — title, source, original URL; cluster transition summary); stable references to public-safe underlying records; created-at (UTC). Never edited, never deleted.
- **Significance Rulebook (configuration)**: the single-sourced thresholds and criteria deciding which source events produce entries (observation changes, score shifts, article gating, topic updates) plus summary/truncation rules — conservative defaults in the plan.
- **Timeline assembly pass**: a repeatable background job type (added to the orchestration registry — additive delta) deriving entries from new source records since the last marker; idempotent, convergent, bounded, recorded as ProcessingJob runs with per-type counts.
- **Timeline DTOs (additive public contract)**: paginated, versioned response shapes per entry type — type, occurrence time, display-safe fields, and attributed article references; consumed by the topic detail page through its extension seam with zero persistence coupling.
- **Read-only sources (consumed)**: TopicTrendObservation records (trend collection), TopicTrendScore records (trend scoring), topic–article links and relevance results (matching/relevance), Article and Source records (ingestion/deduplication), cluster membership episodes (clustering) — all read-only; none modified.
- **Fixtures**: scripted histories per entry type, significance boundary cases, backfill and concurrency scenarios, golden contract samples, and immutability audit harnesses.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Fixture histories yield exactly the expected entries per type at significance boundaries (100%), including exactly one first-detected entry per topic and zero entries for non-significant changes.
- **SC-002**: Immutability: zero modifications or deletions of timeline entries or source records across all scenarios (audit = 0 violations).
- **SC-003**: Idempotency and convergence: repeated assembly passes produce a zero-change delta in 100% of runs; concurrent runs produce no duplicate entries and no failures (unique dedupe keys verified).
- **SC-004**: Backfill correctness: late-arriving and historical data appear as entries ordered by occurrence time with zero duplicates and zero misordering across 100% of backfill fixtures; pagination sequences remain deterministic.
- **SC-005**: Contract stability: golden DTO tests pass for 100% of entry types; simulated internal changes (threshold tuning, version changes, new source data) produce zero shape or required-field diffs; unknown-type tolerance verified.
- **SC-006**: Attribution: 100% of article-bearing entries expose title, source name, and original article URL; audits find zero internal identifiers or provider data in responses.
- **SC-007**: Performance and caching: paginated timeline responses stay within the established latency targets at 10× baseline; cache validators and 304 behavior match the platform's caching rules in 100% of tested scenarios.

## Assumptions

- **Assembly via background processing**: the timeline is derived by a scheduled pass added to the existing orchestration registry (a small additive delta — job type entry) rather than instrumenting production writers; late-arriving data is handled by occurrence-time ordering, not write-path coupling.
- **Significance defaults**: conservative default thresholds (avoiding noisy timelines) are fixed in the plan phase and configurable afterwards; no AI or probabilistic significance scoring.
- **Ordering**: newest-first ordering with defined tie-breakers is the default for the API; the rulebook fixes exact boundary semantics.
- **Immutability**: entries are never edited or deleted in the MVP; corrections (if ever needed) would append reconciliation entries in a future spec — no retroactive edits, consistent with the platform's append-only philosophy.
- **Historical truth**: entries record what was true when the event occurred (for example, an article that later leaves the relevance band keeps its historical entry) — timelines are historical records, not live views.
- **Contract additions (additive)**: the timeline endpoint/fields extend the public API additively under its versioning rules (reconciliation with the API feature in the plan); the topic detail page's reserved extension seam (feature 018) consumes the DTOs with zero persistence coupling — timeline UI rendering is a separate future feature.
- **Caching**: timeline responses become a new cacheable class under the platform's caching policy (additive reconciliation); freshness aligns with the assembly cadence.
- **Retention**: timeline entries are retained indefinitely (append-only history) with bounded sizes per topic ensured by significance filtering.
- **Out of scope**: UI rendering, notifications, personalization, related topics, predictions/forecasts, AI commentary, and any changes to source-feature semantics.

**Dependencies**: Feature 001 (core model — read-only sources; new entity mapping to be designed in the plan); Feature 003 (observations — first-detected and observation-change sources); Feature 009 (append-only score records — shift sources); Features 004/005/006/007 (articles, links, relevance gating — article-event sources and public references); Feature 008 (cluster membership episodes — topic-update sources); Feature 013 (background processing — new assembly job type, ProcessingJob observability, taxonomy); Feature 014 (versioned public contract — additive timeline extension to reconcile); Feature 016 (caching policy — new class to reconcile); Feature 018 (topic detail page — the reserved extension seam that will consume the contract); Project Constitution v1.0.0 (Principle II — attribution on article entries; Principle III — no UI/notification/AI scope creep; Principle IV — configurable significance rulebook; Principle V — UTC occurrence times); deterministic offline fixtures for testing.
