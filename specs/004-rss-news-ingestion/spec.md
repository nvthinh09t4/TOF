# Feature Specification: RSS/Atom News Ingestion (via News Provider Abstraction)

**Feature Branch**: `004-rss-news-ingestion`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement the RSS/Atom news ingestion feature using the existing news provider abstraction and core Article and Source entities. The system must support configurable RSS/Atom sources, fetch new items periodically, parse standard RSS and Atom formats, normalize article metadata, and persist articles using canonical URLs and content hashes. Do not create provider-specific article entities. RSS-specific parsing logic must remain inside the provider/infrastructure layer. Support malformed feeds, unavailable feeds, timeouts, redirects, invalid URLs, duplicate items, missing publication dates, missing descriptions, and partial feed failures. The ingestion process must be idempotent and safe to retry. Preserve source attribution and original article URLs. Do not implement article relevance, topic matching, clustering, AI summarization, or source-quality scoring in this feature."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fetch and persist articles from configured feeds (Priority: P1)

Scheduled ingestion runs fetch items from every configured RSS/Atom source through the news provider abstraction, parse standard RSS 2.0 and Atom formats, normalize article metadata, and persist Articles linked to their Source — anchored by canonical URL and content hash, with source attribution and original article URLs preserved.

**Why this priority**: This is the feature's core value — the article corpus that every reader-facing view and every later feature (topic matching, summaries, scoring) consumes. Without it there is no news content at all.

**Independent Test**: Run ingestion against recorded RSS and Atom fixtures; verify articles are persisted per source with canonical URLs, content hashes, titles, and preserved attribution/original URLs, and that the run is recorded with its counts and outcomes.

**Acceptance Scenarios**:

1. **Given** an enabled source with a reachable feed, **When** an ingestion run processes it, **Then** every valid item becomes an Article linked to that source with canonical URL, content hash, and attribution preserved.
2. **Given** feed items with metadata (title, description/content, publication date, links), **When** they are normalized, **Then** the standard article fields are populated and the original item URL is retained even when it differs from the canonical URL.
3. **Given** a feed whose items are updated over time, **When** a later run fetches it, **Then** only genuinely new items add records; previously ingested articles remain unchanged and attributable.

---

### User Story 2 - Idempotent, retry-safe ingestion (Priority: P1)

Re-ingesting the same feed content never creates unintended duplicates, and interrupted or failed runs can be safely re-run: uniqueness and duplicate-detection rules at the data layer guarantee a stable stored state regardless of how many times or how concurrently ingestion runs.

**Why this priority**: Feeds are polled continuously and overlap across runs; without data-layer idempotency every re-run would duplicate the corpus and corrupt downstream analytics and matching.

**Independent Test**: Ingest the identical fixture twice, re-run after a simulated interruption mid-program, and run two overlapping ingests of the same feed; verify zero duplicate articles and stable counts in all cases.

**Acceptance Scenarios**:

1. **Given** a completed ingestion for a feed, **When** the identical feed content is ingested again, **Then** no new article records are created and the run completes with a zero-additions count.
2. **Given** an ingestion interrupted partway, **When** the run is retried, **Then** items already persisted are not duplicated and remaining items complete the import.
3. **Given** two overlapping runs fetching the same feed, **When** both complete, **Then** each item is stored exactly once (uniqueness rules resolve the race).
4. **Given** the same content syndicated by two different sources, **Then** each source's copy is retained with its own attribution and the copies are detectable as duplicates by content hash.

---

### User Story 3 - Robust against real-world messy feeds (Priority: P2)

Ingestion survives malformed feeds, unavailable feeds, timeouts, redirects, invalid item URLs, duplicate items, missing publication dates, missing descriptions, and partial feed failures: per-source isolation ensures one bad feed never stops the rest, valid items are preserved, and every failure is categorized and recorded in the run's processing-job diagnostics.

**Why this priority**: Real feeds are unreliable by nature; the platform must degrade gracefully (constitution failure-isolation constraints) and keep the corpus growing even while some sources are broken.

**Independent Test**: Run ingestion against a fixture matrix (malformed XML, 404/5xx, timeout, redirect chains, invalid URLs, duplicate items, missing dates/descriptions, partially malformed feeds); verify per-source categorized outcomes, isolation, partial-result preservation, and bounded durations.

**Acceptance Scenarios**:

1. **Given** a malformed feed, **When** it is processed, **Then** the source is marked failed with a categorized outcome and diagnostics, its previously ingested articles are untouched, and other sources in the same run complete normally.
2. **Given** a feed where some entries are invalid, **When** it is processed, **Then** valid entries persist (partial success), invalid entries are reported individually, and the run's outcome reflects partial success.
3. **Given** an item with an invalid or unusable URL, **When** it is processed, **Then** the item is rejected with a diagnostic and the rest of the feed continues.
4. **Given** items missing publication dates or descriptions, **When** they are ingested, **Then** they are stored with absent optional fields (never fabricated) and a recorded fetch time.

---

### User Story 4 - Configuration-driven source management (Priority: P3)

The set of news sources — their feed URLs, kinds, display names, and enablement — is managed through configuration and data, so operators can add, remove, or disable a source without code changes, and future news provider kinds (non-RSS) can join through the same abstraction.

**Why this priority**: Constitution Principle IV (configuration over code): the feed list must be changeable without a release; the MVP starts with RSS/Atom only.

**Independent Test**: Add a new feed via configuration and run ingestion; disable an existing source and verify it is skipped; verify no code changes are required in both cases.

**Acceptance Scenarios**:

1. **Given** a new feed entry in configuration, **When** ingestion next runs, **Then** the source is created or enabled and its items are fetched, with no code changes.
2. **Given** a disabled source, **When** ingestion runs, **Then** it is skipped entirely and its existing articles remain attributed.
3. **Given** any credentials a feed may require, **When** they are configured, **Then** they come from environment variables or a secure store and never appear in code or logs.

---

### Edge Cases

- Malformed or unsupported feed XML → feed-level failure, categorized, source isolated; raw sample retained only as diagnostic metadata.
- Feed unavailable (DNS failure, 404, 5xx) → categorized per the abstraction's taxonomy after bounded retries; previously ingested articles untouched.
- Feed is slow or hangs → timeout bounds the fetch; retry budget does not amplify the hang.
- Rate limiting (429) → transient handling with bounded backoff; source isolated from the rest.
- Redirects: bounded redirect following; permanent moves are recorded in run diagnostics for operator action (configuration update), never auto-migrated silently.
- Invalid item URLs (relative, malformed, non-HTTP) → resolved against the feed's base where the format allows; otherwise rejected with per-item diagnostics.
- Duplicate items within one feed (same URL, or same content under different URLs) → one article per canonical identity; duplicates skipped and counted.
- Same content syndicated across sources → retained per source with attribution; duplicates detectable via content hash (no merging in this feature).
- Partially malformed feeds → valid items ingested; invalid items reported individually.
- Missing publication date → stored absent; fetched/observed time always recorded (UTC).
- Missing description/content → stored absent under defined fallback rules; items with no usable title and no description are rejected with diagnostics; nothing is ever fabricated.
- Empty feed → successful run with zero new articles (not an error).
- Non-UTF8 encodings (e.g., Vietnamese feeds) → decoded per declared encoding without lossy transformation.
- Very large feeds → configurable per-source item limits keep runs bounded.
- Concurrent or overlapping ingestion runs → uniqueness rules make writes idempotent; no duplicates, no data loss.
- Cancellation mid-run → stops promptly; persisted articles remain consistent; run recorded as cancelled.
- Feed URL changed by the publisher → operator updates configuration; existing article attribution remains valid.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ingestion MUST run as a background processing job (recorded as a ProcessingJob per run) and interact with feeds exclusively through the news/feed provider contract; RSS-specific parsing MUST remain inside the provider/infrastructure layer, and no provider-specific article entities may exist in the core. The run record MUST capture at least: sources processed with per-source outcomes, counts (items seen, accepted, deduplicated, rejected), UTC timings, and the final outcome category (success, partial success, failed, cancelled).
- **FR-002**: News sources MUST be configurable (feed URL, source kind, display name, enable/disable, per-source settings) and changeable through configuration and data only; every Article MUST be linked to exactly one Source, and source attribution MUST be preserved for every item.
- **FR-003**: Ingestion MUST fetch from every enabled source periodically at a configurable cadence, with configurable per-source item limits to keep runs bounded.
- **FR-004**: The adapter MUST parse standard RSS 2.0 and Atom 1.0 formats; format-specific details MUST NOT leak beyond the adapter, and non-standard extension data may be retained only as controlled metadata.
- **FR-005**: Every valid item MUST be normalized into an Article with: canonical URL (deterministic canonicalization; relative links resolved), title, content/excerpt when present, publication time in UTC when present, content hash, source reference, and retention of the original item URL for attribution when it differs from the canonical URL; optional fields (author, language, etc.) are stored when present and never fabricated.
- **FR-006**: The content hash MUST be computed deterministically from defined normalized inputs so identical content always yields identical hashes, enabling duplicate-content detection across runs and sources.
- **FR-007**: Ingestion MUST be idempotent at the data layer: identical re-ingestion creates no duplicate articles or updates; interrupted runs MUST be safe to retry; concurrent or overlapping runs MUST resolve through uniqueness constraints without data loss.
- **FR-008**: Duplicate handling MUST follow: canonical-URL uniqueness prevents URL-variant duplicates; content-hash matches within the same source are treated as duplicates (skipped with diagnostics); identical content across different sources is retained per source with attribution and remains detectable via content hash.
- **FR-009**: Missing data MUST be handled deterministically: absent publication dates and descriptions are stored as absent (never fabricated); the fetch/observed time is always recorded in UTC; items lacking any usable title or description, or lacking a usable URL, are rejected with per-item diagnostics.
- **FR-010**: Feed failures MUST follow the abstraction's error taxonomy with bounded retries inside the adapter: malformed feeds, unavailable feeds, timeouts, rate limits, and redirect failures each map to their defined categories; permanent moves after redirects are recorded for operator action.
- **FR-011**: Per-source isolation MUST hold: a failing, slow, or cancelled source MUST NOT block or fail the run for other sources; per-source outcomes and failure details MUST be recorded in the run's ProcessingJob diagnostics; previously ingested articles MUST remain untouched by later failures.
- **FR-012**: Partial results MUST be preserved: partially valid feeds ingest their valid items (partial-success outcome per source) while invalid items are reported; a run is never marked fully successful when part of its data was rejected.
- **FR-013**: Cancellation MUST stop ingestion promptly; articles persisted before cancellation MUST remain consistent; the run is recorded as cancelled.
- **FR-014**: All timestamps MUST be stored in UTC; ingestion MUST NOT perform any time-window arithmetic (windows belong exclusively to the shared time-window module; constitution Principle V).
- **FR-015**: This feature MUST NOT implement article relevance, topic matching, clustering, AI summarization, or source-quality scoring; it MUST provide the raw, queryable article data those features require (attribution, canonical URL, hash, publication and fetch times, content).
- **FR-016**: No feed lists or credentials may be hardcoded: sources come from configuration/data; credentials (when a feed requires them) come from environment variables or a secure store and MUST never appear in code or logs (constitution Principle IV).
- **FR-017**: Ingestion MUST be testable offline: adapter parsing runs against recorded RSS and Atom fixtures (including malformed and edge-case feeds); the ingestion flow runs against deterministic stub news providers; contract tests per the provider abstraction apply to the RSS/Atom adapter.

### Key Entities

This feature introduces no new persisted entities; it maps feed data into the core model from the core domain & persistence model and produces run records via ProcessingJob.

- **News Ingestion Run**: A ProcessingJob of the news-ingestion type; attributes: sources processed with per-source outcomes, counts (seen/accepted/deduplicated/rejected), diagnostics reference, UTC timings, outcome category (success, partial success, failed, cancelled).
- **Normalized News Item**: The neutral adapter output consumed by ingestion; fields: canonical URL candidate, original URL, title, content/excerpt, publication time (UTC, optional), optional author/language, source identity, content-hash inputs, optional controlled metadata.
- **Article (core concept used)**: Persisted per the core model: canonical URL (uniqueness anchor), content hash, title, optional publication timestamp (UTC), fetched/observed time, lifecycle state, source reference, original URL retained for attribution, optional ingestion metadata. No provider-specific article entity exists.
- **Source (core concept used)**: The feed channel record per the core model: unique source identifier (feed URL), kind, display name, enabled state, audit timestamps; retained for attribution even when disabled.
- **RSS/Atom Adapter**: The first implementation of the news/feed provider contract; parses RSS 2.0 and Atom, isolates format-specific logic, translates errors into the abstraction's taxonomy, and normalizes items. No RSS-specific entities exist anywhere else.
- **Source Configuration**: The configuration/data entries that determine which sources exist and are enabled (feed URL, kind, display name, per-source settings) — changeable without code changes.
- **Recorded Fixtures & Stub News Providers**: Offline deterministic inputs (recorded real-world feeds, malformed variants) used for adapter parsing tests and ingestion-flow tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An ingestion run against recorded RSS and Atom fixtures persists 100% of valid items as Articles with canonical URL, content hash, title, and source attribution; the original item URL is retained in 100% of cases where it differs from the canonical URL; zero RSS-specific entities exist anywhere in the core.
- **SC-002**: Running the identical ingestion twice creates zero duplicate articles (duplicate count = 0 on unique business keys); an interrupted-then-retried run reaches the same final counts as an uninterrupted run (100% convergence).
- **SC-003**: The messy-feed fixture matrix (malformed XML, unavailable, timeout, redirect chains, invalid URLs, duplicates, missing dates/descriptions, partial failures) produces correct categorized per-source outcomes with diagnostics in 100% of cases; no source failure ever interrupts other sources (isolation verified in 100% of scenarios).
- **SC-004**: A fixture set of URL-variant and duplicate-content cases resolves to single canonical identities with 100% consistency; within-source content duplicates produce zero duplicate articles; cross-source syndicated copies remain separately attributed and detectable via hash in 100% of cases.
- **SC-005**: In missing-data fixtures, 100% of ingested articles carry a UTC fetch time, zero fields are fabricated, and items lacking a usable URL or any usable text are rejected with diagnostics in 100% of cases.
- **SC-006**: Adding or disabling a feed requires only configuration changes (zero code edits); secret scans of code and logs return zero credentials.
- **SC-007**: Cancellation stops ingestion within the configured grace period with 100% consistency checks passing; no run exceeds its timeout plus bounded retry budget.

## Assumptions

- **Builds on existing features**: the core domain & persistence model (feature 001) provides Article, Source, ProcessingJob, canonical-URL uniqueness, content-hash support, lifecycle states, and the metadata policy; the provider abstraction (feature 002) provides the news/feed contract, error taxonomy, timeout/retry/isolation behavior, registration, and stub/contract-test patterns. This feature adds the first concrete news adapter (RSS/Atom) and the ingestion flow built on them.
- **Polling model**: feeds are polled at a configurable cadence; conditional fetching (ETag/Last-Modified) and push mechanisms (WebSub etc.) are out of scope for the MVP — idempotent dedupe makes repeated full fetches safe. This may be added later behind the same contract.
- **Format coverage**: standard RSS 2.0 and Atom 1.0 elements are supported; non-standard extension namespaces are not required and, if retained, only as controlled metadata.
- **Canonicalization and hashing details**: the canonical-URL ruleset (scheme/host normalization, tracking-parameter stripping, relative resolution) and the content-hash inputs/function are defined in the plan phase; both MUST be deterministic and stable across runs.
- **Cross-source syndication**: copies from different sources are kept separately with their own attribution (per the core model's rules); merging/deduplication policy beyond detection is out of scope.
- **Source management**: sources are created/updated through configuration or seed data; an administrative UI is out of scope for this feature.
- **Redirects**: followed with a bounded limit; permanent moves are surfaced in run diagnostics for an operator to update configuration — never applied silently.
- **Encoding**: the declared feed encoding is honored; deterministic fallback behavior is defined in the plan; text is preserved without lossy transformation (Vietnamese diacritics included).
- **Retention**: ingestion never deletes articles; sources' lifecycle states handle retirement while preserving attribution.
- **Out of scope (explicit user constraint)**: article relevance, topic matching, clustering, AI summarization, source-quality scoring, presentation/UI.
- **Volume**: with configurable per-source item limits and MVP feed volume, repeated full fetch with dedupe is acceptable; no premature optimization.

**Dependencies**: Feature 001 (core domain & persistence model — Article, Source, ProcessingJob, uniqueness and metadata policy); Feature 002 (provider abstraction — news/feed contract, error taxonomy, timeouts/retries/isolation, DI registration, stubs and contract tests); Project Constitution v1.0.0 (Principles I, II — attribution and original links, III, IV — config-driven sources and secrets, V — UTC storage); recorded RSS/Atom fixtures for offline testing.
