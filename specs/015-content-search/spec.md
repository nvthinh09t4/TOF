# Feature Specification: Search & Discovery (Topics and Articles)

**Feature Branch**: `015-content-search`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement search across Topics and Articles using the existing core model. Support keyword search, topic navigation, relevant article results, pagination, and basic filtering by date, language, and source where appropriate. Use SQL Server capabilities for the MVP and define an application-level search abstraction so a dedicated search engine can be introduced later without changing API or frontend contracts. Search should use strongly typed indexed fields from the core model rather than querying arbitrary metadata JSON. Do not implement recommendation or personalized search ranking in this feature."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Keyword search across topics and articles (Priority: P1)

Readers can search by keyword and receive deterministic results in two groups: matching topics (navigable to topic details) and matching articles with their public attribution fields. Matching uses only strongly typed indexed fields of the core model — topic normalized names and aliases, article titles and descriptions — with deterministic relevance tiers (exact match, then prefix, then contained) and defined tie-breakers.

**Why this priority**: This is the feature's core value: finding known topics and articles quickly, powered by the platform's normalized identity fields rather than arbitrary content.

**Independent Test**: Fixture corpora with exact, prefix, contained, and normalization-variant queries (including Vietnamese diacritics); verify correct results per matching tier, deterministic ordering, and correct empty-result behavior.

**Acceptance Scenarios**:

1. **Given** stored topics and articles, **When** a keyword query runs, **Then** topics match on normalized names and aliases and articles match on titles and descriptions only — no other fields are searched.
2. **Given** matches across relevance tiers, **When** results are ordered, **Then** exact matches precede prefix matches, which precede contained matches, with defined tie-breakers (recency, identifier) applied deterministically.
3. **Given** a query with no matches, **When** it runs, **Then** both groups return empty lists with the standard envelope (no errors, no fabricated results).

---

### User Story 2 - Filtering and pagination with validation (Priority: P1)

Article results can be filtered by date range, source, and language where appropriate, combined deterministically, and paged with uniform bounded pagination consistent with the public API's rules. Invalid filters, malformed dates, or out-of-range pages produce the standard stable validation errors.

**Why this priority**: The user explicitly requires basic filtering and pagination; without validation discipline, filters would silently degrade into wrong results.

**Independent Test**: Fixture combinations of date/source/language filters across boundary values (inclusive/exclusive semantics), invalid inputs, and paging sequences; verify exact filtering, deterministic pages, and correct errors.

**Acceptance Scenarios**:

1. **Given** a date-range filter, **When** articles are searched, **Then** results include only articles whose applicable date falls within the range per the documented date semantics (publication time when present, documented fallback otherwise).
2. **Given** source or language filters, **When** applied, **Then** results include only articles of that source or stored language; articles without a stored language are excluded from language-filtered queries (documented).
3. **Given** malformed or unsupported filter parameters, **When** the query runs, **Then** the standard validation error shape is returned (no silent ignoring).

---

### User Story 3 - Replaceable search engine behind an application-level abstraction (Priority: P2)

Search is consumed through an application-level search contract (query, filters, ordering, pagination in; neutral results out). The MVP implementation uses SQL Server capabilities over the typed indexed fields; a dedicated search engine can replace it later by implementing the same contract — with configuration-driven selection and no changes to API or frontend contracts.

**Why this priority**: Explicitly required forward-compatibility; it prevents search-engine decisions from leaking into consumers.

**Independent Test**: An alternative stub engine implementation passes the identical contract test suite; swapping the configured implementation changes nothing in consumer-visible contracts.

**Acceptance Scenarios**:

1. **Given** two engine implementations (MVP and stub), **When** both run the same contract test suite, **Then** both satisfy identical result semantics (matching tiers, filters, ordering, pagination).
2. **Given** an engine swap via configuration, **When** consumers query, **Then** API and frontend contracts are unchanged (no shape or behavioral contract changes).
3. **Given** the search contract, **When** audited, **Then** it contains no engine-specific types; engine details stay inside implementations.

---

### User Story 4 - Typed-fields discipline and public-contract compatibility (Priority: P3)

Search reads only strongly typed indexed fields of the core model — never arbitrary metadata JSON — and its responses remain fully compatible with the public API's versioned contract: the same envelope, error shape, pagination rules, attribution fields, and additive-only evolution, so frontends and API consumers are unaffected by internal search changes.

**Why this priority**: The user explicitly forbids metadata-JSON querying and requires contract stability; it protects both performance and consumers.

**Independent Test**: Audit query paths for any metadata-JSON access (must be zero); validate search responses against the public API's golden contract fixtures (additive fields only); verify required indexes exist for baseline performance.

**Acceptance Scenarios**:

1. **Given** the search implementation, **When** query paths are audited, **Then** zero metadata-JSON querying exists (typed fields only).
2. **Given** search responses, **When** compared to the public API contract fixtures, **Then** they validate with additive fields only (attribution intact on every article result).
3. **Given** baseline data volumes, **When** searches run, **Then** paged responses stay within the documented latency target using the defined indexes.

---

### Edge Cases

- Query normalization variants (case, whitespace, diacritics for Vietnamese) → deterministic normalization shared with the platform rules; identical semantic queries match identically.
- Very short queries below the minimum length → validation error (documented minimum); very long queries beyond the maximum → validation error.
- Wildcard/special characters in queries → treated as literal text after normalization (no injection into engine syntax); behavior documented.
- Date-range boundaries → inclusive/exclusive semantics defined once; results at the exact boundary follow the rulebook.
- Articles without a stored language or publication date → excluded from language-filtered queries; date filtering uses the documented fallback semantics.
- Source filter referencing an unknown or disabled source → empty results for unknown identifiers (documented) or validation error per the rulebook; disabled sources' articles follow exposure policy (disabled sources excluded from primary search listings while remaining attributable elsewhere).
- Duplicate normalized matches (topic name and alias both matching) → a topic appears once per result set (deduplicated deterministically).
- Retired (merged) duplicate articles → never returned as separate results (deduplication guarantees one article).
- Empty result sets → standard empty envelope with deterministic ordering metadata intact.
- Concurrent writes during paging → documented stability semantics (consistent with the public API); deterministic within a stable read.
- Engine swap mid-life → consumers see identical contracts; any latency or ordering differences remain within the contract's documented semantics.
- Result-size extremes (huge match counts) → pagination bounds enforced; no unbounded result materialization.
- Search terms matching only metadata internals (values that exist solely in JSON metadata) → no matches (typed-fields rule enforced).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Search MUST be consumed exclusively through an application-level search contract (neutral query, filter, ordering, pagination inputs; neutral results out); the contract MUST contain no engine-specific types, and implementations MUST be selectable via configuration/dependency injection — the SQL Server implementation is the MVP; a dedicated search engine replaces it later by implementing the same contract.
- **FR-002**: Search scope MUST be: topics — normalized names and aliases; articles — titles and descriptions; these strongly typed indexed core-model fields are the ONLY searchable fields, and querying arbitrary metadata JSON is forbidden (no search path may depend on metadata internals).
- **FR-003**: Matching MUST be deterministic under a single rulebook: shared normalization (case, whitespace, Unicode/diacritics), defined matching semantics (exact, prefix, contained tiers per the plan-fixed rules), special characters treated as literals; no fuzzy, semantic, or AI matching.
- **FR-004**: Ordering MUST be deterministic: matching tiers first (exact, then prefix, then contained), then defined tie-breakers (recency, identifier); identical queries MUST return identical page sequences under a stable read.
- **FR-005**: Topic results MUST carry the public topic display fields and a navigable identifier (drilling into topic details via the public API); a topic MUST appear at most once per result set even when multiple forms match.
- **FR-006**: Article results MUST carry the public article fields — title, applicable dates, original article URL, source information, AI-labeled summary when one exists — with attribution intact on every result (constitution Principle II) and the same exposure rules as the public API (no retired duplicates; disabled-source policy per the exposure rulebook).
- **FR-007**: Filters MUST support, where appropriate: date range (defined date semantics with documented fallback when publication time is absent), source, and language (only where stored; articles without a stored language are excluded from language-filtered queries); filters MUST be combinable and independently validated.
- **FR-008**: Pagination MUST follow the public API's uniform rules: bounded page sizes, deterministic ordering, documented stability semantics under concurrent writes, and validation errors for out-of-range or malformed parameters.
- **FR-009**: Validation MUST apply to queries and filters (minimum/maximum query lengths, date formats, identifier formats, supported filter values); failures MUST use the public API's stable error shape with categorized codes and no internal leakage (constitution — consistent error contract).
- **FR-010**: The MVP implementation MUST use SQL Server capabilities over the typed indexed fields (for example, indexed prefix/substring matching or full-text capabilities as fixed in the plan); required indexes (including any new ones) MUST be defined in the plan and applied via core-model migrations — no arbitrary JSON scanning, no unbounded queries.
- **FR-011**: Contract compatibility MUST hold: search responses live within the public API's versioned contract (same envelope, error shape, pagination, and caching rules; additive-only evolution) so that introducing a dedicated search engine later requires zero API or frontend contract changes.
- **FR-012**: This feature MUST NOT implement recommendation, personalization, learned ranking, user-context signals, or behavioral tracking; ranking is deterministic retrieval ordering only.
- **FR-013**: Query normalization, matching tiers, filter semantics, and ordering rules MUST be single-sourced and configurable where appropriate (thresholds, page sizes); no secrets are involved; all timestamps (date filters) are UTC (constitution Principle V).
- **FR-014**: The feature MUST be testable offline and deterministically: fixtures cover matching tiers, normalization variants (incl. Vietnamese diacritics), filter combinations and boundaries, pagination sequences, validation errors, empty results, deduplication of topics, and the engine contract suite (MVP implementation and an alternative stub both pass).
- **FR-015**: Out of scope: dedicated search-engine deployment and its indexing pipeline (a future engine implements the same contract; index-maintenance design arrives with it), fuzzy/AI/semantic search, recommendations, personalized ranking, and search over metadata JSON or non-public fields.

### Key Entities

This feature introduces an application-level search contract, a rulebook, and a replaceable engine implementation; no new persisted business entities (index additions are migration-level, per the plan).

- **Search Service contract (application-level)**: the neutral query interface (keyword, filters — date range/source/language, pagination, ordering rules) and neutral results (topic items, article items matching the public contracts); consumed by the API layer; implementations are interchangeable (SQL Server MVP; future dedicated engine).
- **Search Rulebook**: the single-sourced definition of normalization, matching tiers and semantics, ordering/tie-breakers, filter semantics (date fallback, language absence, disabled-source policy), pagination bounds, and query-length validation.
- **SQL Server implementation (MVP)**: executes the contract over typed indexed fields using SQL Server capabilities; contains all engine-specific details; replaced without consumer impact.
- **Result DTOs (public-contract bound)**: topic item (display fields, navigable identifier, matched-form information where exposed) and article item (public article fields incl. attribution and AI-labeled summary when present) — shaped exactly as the public API's versioned contract with additive-only extensions.
- **Required indexes (migration delta)**: any indexes needed for baseline search performance over the typed fields, defined in the plan and applied via core-model migrations (no entity changes).
- **Fixtures & engine contract suite**: offline corpora for matching/filter/pagination scenarios plus the engine-agnostic contract test suite every implementation must pass.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of fixture queries return correct results per the matching tiers (exact > prefix > contained) with deterministic tie-breakers, including normalization variants (Vietnamese diacritics) and empty-result cases.
- **SC-002**: Filter correctness: date-range, source, and language filters (individually and combined) select exactly the documented sets at boundary values in 100% of fixtures; 100% of malformed filters return the stable validation error shape.
- **SC-003**: Pagination determinism: repeated identical queries return identical page sequences in 100% of runs under stable reads, with bounded page sizes and no overlaps or skips.
- **SC-004**: Engine abstraction: an alternative stub engine passes the same contract suite as the MVP implementation (100%), and a configured swap produces zero API/frontend contract changes (verified against golden contract fixtures).
- **SC-005**: Typed-fields discipline: audits show zero metadata-JSON query paths and zero searches over non-public fields (100% clean); topics appear at most once per result set.
- **SC-006**: Search performance: paged searches respond within 1 second at the baseline data volume using the defined indexes; no unbounded queries exist (schema/query audit 100% clean).
- **SC-007**: Contract compatibility: 100% of search responses validate against the public API's golden contract fixtures with additive fields only, and attribution fields are present on 100% of article results.

## Assumptions

- **Relationship to the public API feature**: this feature refines and expands the minimal search behavior planned in feature 014 — behavior supersedes it, while response shapes remain compatible (additive-only), so API and frontend contracts stay stable; the versioning/evolution rules from feature 014 govern.
- **Language filter scope**: applies only where a language value is stored on articles (an optional field from ingestion); articles lacking it are excluded from language-filtered queries (documented rule, not an error).
- **Date semantics**: date filters use publication time when present; the fallback rule (for example, fetched time) and boundary inclusivity are fixed once in the rulebook (plan phase) and applied identically everywhere.
- **MVP matching implementation**: SQL Server-indexed strategies (prefix/substring via indexed normalized columns and/or full-text capabilities) are plan-phase decisions; the spec fixes only the matching semantics and tiers (no engine-dependent behavior leaks into the contract).
- **Index additions**: search may require new indexes on existing typed columns; they are added via core-model migrations with no entity changes (migration-level delta).
- **Future dedicated engine**: swaps in by implementing the search contract; its index-maintenance pipeline is designed in its own future spec; no API/frontend changes are anticipated then.
- **Exposure consistency**: article exposure rules (relevance band policy per feature 014's configuration, retired-duplicate exclusion, disabled-source policy) apply identically in search results.
- **No personalization**: zero user-context or behavioral inputs exist in the contract; determinism makes results cacheable under the public API's caching rules (constitution Principle IV).
- **Architecture**: application-layer contract within the modular monolith; reads live stored data (no separate index in the MVP); no distributed components (constitution Principle III).

**Dependencies**: Feature 001 (core entities and indexes — one possible migration delta for search indexes); Feature 003 (topic normalized names and aliases); Features 004/005 (articles, sources, original URLs, attribution fields); Features 006/007 (link/relevance context for exposure policies); Features 010-012 (validated summaries and AI labeling surfaced in results); Feature 013 (data freshness semantics); Feature 014 (public API versioned contracts, envelope, error shape, pagination and caching rules — search MUST remain within them); Project Constitution v1.0.0 (Principle II — attribution on every article result; Principle III — no engine/distributed scope creep in the MVP; Principle IV — configuration-driven implementation selection; Principle V — UTC date semantics); deterministic offline fixtures and engine contract tests.
