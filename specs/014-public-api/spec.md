# Feature Specification: Public Application API (Read-Only, Versioned)

**Feature Branch**: `014-public-api`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Design and implement the public application API for the trending-news platform. Use the existing core domain and feature extensions rather than creating duplicate API-specific persistence models. Provide endpoints for trending topics grouped by last 5 hours, today, and yesterday; topic details; relevant summarized articles; original source information; search where applicable. Define DTOs, pagination, filtering, validation, error responses, API versioning, authorization boundaries, and caching boundaries. The API must expose stable contracts even when internal scoring, clustering, AI, or provider implementations change. Do not expose internal provider models or database entities directly through the API."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trending topics grouped by the three windows (Priority: P1)

Clients can fetch trending topics grouped into the platform's three presentation windows — last 5 hours, today, and yesterday — in one stable response: each group carries its label, UTC boundaries, and a deterministically ordered, paginated list of topic summaries with minimal display fields. Windows are computed exclusively by the shared time-window module against the configured reference timezone.

**Why this priority**: This is the platform's primary reader-facing contract; the window grouping is the constitution's single-source-of-truth rule applied at the API edge.

**Independent Test**: With stored fixture data crossing window boundaries, fetch the grouped endpoint and verify correct grouping, deterministic ordering, pagination, and boundary metadata — including the reference-timezone conversion contract.

**Acceptance Scenarios**:

1. **Given** stored observations and topics, **When** the grouped endpoint is called, **Then** the response contains the three window groups with labels, UTC boundaries, and reference timezone, and each topic appears in exactly the window(s) its observations place it in.
2. **Given** more topics than a page size in a window, **When** pages are requested, **Then** ordering is deterministic and documented (stable key with defined tie-breakers), and pages do not overlap or skip within a stable read.
3. **Given** an empty window, **When** it is requested, **Then** the group is present with an empty list (no errors, no fabricated content).

---

### User Story 2 - Topic details, relevant summarized articles, and original sources (Priority: P1)

A topic detail response presents the topic's stable display information, its cluster context when clustered, a trend-signal summary, its topic-level AI summary when one exists, and a paginated list of relevant articles — each article carrying its title, publication time, the original article URL, source information, and its article-level summary when one exists. Only stored, validated summaries are served, and every article keeps its attribution and original link.

**Why this priority**: This is the core reading experience the whole pipeline exists to produce; attribution guarantees (constitution Principle II) are enforced at this boundary.

**Independent Test**: With fixture topics, clusters, links, relevance results, and summaries, fetch details and the article list; verify field completeness, relevance gating, summary presence/absence rules, attribution, and original URLs — with zero internal fields leaked.

**Acceptance Scenarios**:

1. **Given** a topic with relevant articles and summaries, **When** details are fetched, **Then** the response includes display fields, cluster context, the topic summary when present, and a paginated article list ordered deterministically.
2. **Given** an article whose summary does not exist, **When** it is listed, **Then** the summary field is absent (never empty or fabricated); **Given** a stored validated summary, **Then** it is served with its AI-generated labeling and generation timestamp.
3. **Given** any listed article, **When** the response is audited, **Then** the original article URL and source name are present (attribution preserved), including for disabled sources.
4. **Given** articles below the relevance exposure band, **When** a topic's articles are listed, **Then** they are not exposed (exposure band configurable).

---

### User Story 3 - Stable, versioned contracts with clean errors (Priority: P1)

The API exposes versioned, documented contracts (DTO schemas) fully decoupled from database entities and provider models — explicit mapping happens in one layer, and internal evolution (scoring algorithm versions, cluster merges/splits, AI provider or prompt changes, provider swaps) never changes contract shapes or required fields. Within a version, evolution is additive only; breaking changes require a new version with a documented deprecation policy. All inputs are validated and failures return a stable error shape.

**Why this priority**: The user's central demand — stable contracts despite internal change — and the necessary condition for external clients to integrate safely.

**Independent Test**: Golden contract tests for every endpoint; simulate internal changes (score version swap, cluster merge, provider swap, summary regeneration) and verify zero DTO diffs; exercise invalid inputs and verify the stable error shape and statuses.

**Acceptance Scenarios**:

1. **Given** the contract fixtures, **When** contract tests run, **Then** response shapes match golden DTO schemas exactly (no entity/provider/internal fields, no undocumented fields).
2. **Given** simulated internal changes, **When** the same requests are replayed, **Then** no contract shape or required-field changes occur.
3. **Given** invalid query parameters or unknown ids, **When** requests are made, **Then** stable error responses are returned with categorized codes and no internal details or secrets; unknown enum values from future server versions are tolerated by clients per the documented evolution rules.

---

### User Story 4 - Search across topics and articles (Priority: P2)

Clients can search topics (by normalized name and aliases) and articles (by title) with deterministic matching, bounded result pages, and validation of search inputs — powered only by existing stored, normalized fields, with no external or AI-based ranking.

**Why this priority**: Required "where applicable" for reader navigation; determinism keeps it consistent with the platform's rules.

**Independent Test**: Search fixtures with diacritic/case variants, aliases, and empty results; verify deterministic matches, pagination, and validation errors.

**Acceptance Scenarios**:

1. **Given** a query matching a topic's normalized name or an alias, **When** topics are searched, **Then** the topic appears with the same deterministic ordering rules as listing endpoints.
2. **Given** a query matching article titles, **When** articles are searched, **Then** results paginate deterministically and carry the same attribution fields as the topic article list.
3. **Given** an empty or too-short query per validation rules, **When** search is called, **Then** a stable validation error is returned.

---

### User Story 5 - Caching boundaries and a read-only public surface (Priority: P3)

The public surface is anonymous and strictly read-only: no writes, no job triggering, no administrative access. Public GET responses carry deterministic caching validators (ETag and/or last-modified) and cache directives, conditional requests return 304 without bodies, and caching boundaries are documented per endpoint class — nothing user-specific exists to cache in the MVP.

**Why this priority**: Explicitly required boundaries; they keep public traffic economical and the attack surface minimal.

**Independent Test**: Conditional-request fixtures verifying ETag/304 behavior and headers per endpoint class; audit the surface for absence of write/admin routes.

**Acceptance Scenarios**:

1. **Given** an unchanged resource, **When** a conditional request with the previous validator is made, **Then** 304 is returned with no body and cache directives consistent with policy.
2. **Given** changed underlying data, **When** the same validator is presented, **Then** a fresh representation is returned and the validator changes deterministically.
3. **Given** the whole surface, **When** routes are audited, **Then** zero write, admin, or job-trigger routes exist (read-only boundary holds).

---

### Edge Cases

- Window boundary crossings (topic observed just before/after boundaries in the reference timezone) → grouping follows the shared window module exclusively; DST-like reference-zone changes resolve deterministically at the documented rules.
- A topic trending in multiple windows → appears in each appropriate group independently (no deduplication across groups required; documented).
- Topic with zero relevant articles → details still served; article list empty; topic summary absent (constitution Principle II: no news → no summary).
- Article with missing publication date → listed with the field absent; fetched time available for ordering tie-breaks (documented).
- Disabled or retired sources/articles → excluded from primary listings per exposure rules, but attribution stays resolvable for historical items already referenced.
- Retired (merged) duplicate articles → never exposed as separate items (deduplication guarantees one article); merged provenance is internal.
- Cluster merged or split between requests → topic details reflect current cluster context deterministically; no contract shape change (internal evolution).
- Scoring algorithm version change or unavailability → ordering remains deterministic (defined fallback ordering) and any exposed descriptive fields stay stable or absent — never shape-breaking.
- Summary regenerated concurrently with reads → readers may see either validated generation; both satisfy contract rules; no partial summaries ever (validation gate from feature 012).
- Very large result sets → pagination bounds enforced with validation errors beyond limits.
- Unknown enum values introduced by a future server version (additive evolution) → clients tolerating unknown values is the documented rule; server never removes values within a version.
- Clients requesting unsupported filters or parameters → explicit validation errors (no silent ignoring).
- Search query normalization edge cases (diacritics, case, whitespace) → deterministic normalization per the platform rules; no fuzzy matching.
- Cache poisoning attempt vectors → responses vary only on documented public inputs; no user-specific state exists in the MVP.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The API MUST serve read-only public data from the existing core domain and feature extensions; it MUST NOT create API-specific persistence models, and it MUST NOT expose database entities, provider models, or internal diagnostics (jobs, prompts, metadata internals) directly — an explicit mapping layer produces documented DTOs.
- **FR-002**: A trending-topics endpoint MUST return the three window groups (last 5 hours, today, yesterday) with labels, UTC boundaries, and the reference timezone; grouping MUST be computed exclusively via the shared time-window module (constitution Principle V) with no ad-hoc date arithmetic in the API.
- **FR-003**: Topic summaries within groups MUST have deterministic ordering (documented key with tie-breakers; internal scoring MAY inform ordering but no shape-breaking exposure) and pagination with bounded page sizes.
- **FR-004**: A topic-details endpoint MUST return stable display fields, cluster context when present (canonical cluster name; membership listed within bounds), a trend-signal summary, the topic-level summary when one exists, and the topic's relevant articles.
- **FR-005**: The relevant-articles list MUST expose only articles within the configured relevance exposure band, each with: title, publication time (when present), the original article URL, source information (name and kind), and the article-level summary when one exists; every displayed item MUST satisfy attribution rules (constitution Principle II).
- **FR-006**: Original source information MUST be served from the stored source records, including for disabled sources whose historical articles remain attributable; source exposure is limited to public-safe fields.
- **FR-007**: Summaries MUST only ever be exposed when a stored, validated summary exists; absent summaries MUST render as absent fields (never empty strings or fabricated content); exposed summaries MUST be labeled as AI-generated with their generation timestamp (internal prompt/provider diagnostics stay internal).
- **FR-008**: Search endpoints MUST cover topics (normalized name and alias matching) and articles (title matching) using existing normalized fields only — deterministic, paginated, with validation of query inputs; no fuzzy or AI-based ranking.
- **FR-009**: Pagination MUST be uniform across list endpoints (bounded page sizes, deterministic ordering, documented stability semantics under concurrent writes) and MUST reject out-of-range or malformed parameters with validation errors.
- **FR-010**: Filtering MUST be explicit per endpoint (at minimum: window-group selection for topics; source filter and relevance band exposure policy for articles); unsupported filters MUST produce validation errors rather than silent ignoring.
- **FR-011**: All inputs MUST be validated (enums, ranges, identifier formats, query lengths); failures MUST return a stable, documented error shape with categorized codes and appropriate statuses (validation, not-found, server) and MUST NOT leak internal details or secrets.
- **FR-012**: The API MUST be versioned as a public contract; within a version, evolution MUST be additive only (new optional fields, extended enum values with the documented tolerance rule); removals, retypes, or required-field changes MUST require a new version with a deprecation policy — internal changes (scoring versions, clustering operations, AI providers, prompts, provider swaps) MUST NEVER alter contract shapes.
- **FR-013**: Public GET responses MUST carry deterministic caching validators (ETag and/or last-modified derived from response-affecting data) and cache directives; conditional requests MUST return 304 without bodies when unchanged; caching boundaries MUST be documented per endpoint class, and nothing user-specific exists to cache in the MVP (constitution Principle IV: cache behavior configurable).
- **FR-014**: The authorization boundary MUST be explicit: the public surface is anonymous and read-only; write operations, job triggering, and administrative endpoints are out of scope and MUST NOT exist in this feature; future user-scoped capabilities arrive as separate specs.
- **FR-015**: The API MUST NOT perform its own time-window arithmetic, normalization rules, relevance computation, or summarization; it serves stored, computed results (windows via the shared module, relevance and summaries as stored) and documents the freshness semantics implied by background processing cadence (feature 013).
- **FR-016**: The API layer MUST be testable offline and deterministically: contract tests with golden fixtures cover every endpoint (success shapes, empty states, pagination, filtering, validation errors, versioning rules, caching/304 behavior), and internal-change simulations verify shape stability.
- **FR-017**: Out of scope: writes/mutations, admin or operational endpoints, job triggering, real-time push, notifications, user accounts/personalization, GraphQL, AI-powered search, and any exposure of internal tables, prompts, job records, or provider-specific payloads.

### Key Entities

This feature introduces a versioned public contract (documented DTOs and one mapping layer) over the existing stores; no new persisted business entities.

- **Public API contract (versioned)**: the endpoint set, DTO schemas, error schemas, evolution rules, and deprecation policy — the stable surface clients depend on; a machine-readable contract artifact (for example, an OpenAPI document, per the plan) accompanies it.
- **DTOs (transport-only, mapped)**: window group envelope (label, UTC boundaries, reference timezone, pagination metadata), topic summary item (display fields, optional descriptive trend label), topic details (display fields, cluster context, trend-signal summary, topic summary view), article item (title, publication time, original URL, source reference, AI-labeled summary view), source info (public-safe fields), search results, uniform pagination envelope, and the stable error shape — all explicitly mapped from domain data with no leakage.
- **Mapping layer**: the single translation boundary from stored domain/feature data to DTOs; the only place contract changes are implemented, keeping entities and providers internal.
- **Caching policy**: the documented per-endpoint-class cache directives and deterministic validator (ETag/last-modified) derivation.
- **Window binding**: the read-only use of the shared time-window module for the three groups' labels and boundaries.
- **Read-only data sources**: topics/aliases/observations (003), articles/sources (004/005), topic–article links (006), relevance results (007), clusters (008), scores (009), summaries (010-012) — consumed as stored, none written.
- **Contract fixtures**: golden request/response samples per endpoint used by offline contract tests and internal-change simulations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Windowed grouping matches the shared module's boundaries in 100% of boundary-crossing fixture cases (last 5 hours, today, yesterday), including reference-timezone edge cases.
- **SC-002**: Attribution completeness: 100% of exposed articles carry a source name and original article URL; zero summaries are exposed without a stored validated record; zero fabricated or empty summary fields (audit = 0 violations).
- **SC-003**: Contract stability: golden contract tests pass for 100% of endpoints, and simulated internal changes (scoring version swap, cluster merge/split, AI provider/prompt swap, summary regeneration) produce zero DTO shape or required-field diffs.
- **SC-004**: Determinism and validation: repeated identical requests return identically ordered pages in 100% of tests; invalid inputs return the stable error shape with correct statuses in 100% of cases.
- **SC-005**: Caching correctness: 100% of conditional-request scenarios return 304 when unchanged and fresh representations when changed, with validators changing deterministically; cache headers present per endpoint class in 100% of responses.
- **SC-006**: Public reads answer within 1 second (paged responses) at 10× the launch-volume baseline; no unbounded queries (index-backed lookups per the core model).
- **SC-007**: Leakage and boundary audits: zero internal entity/provider/job/prompt fields appear in any public DTO (100% of golden responses); a route audit confirms a strictly read-only public surface (zero write/admin routes).

## Assumptions

- **Read-only anonymous surface**: the MVP public API requires no authentication (public content only) and has no write operations; rate limiting is an edge/deployment concern documented as a boundary (implementation platform-specific, out of scope); user-scoped APIs (personalization, notifications) are future specs.
- **Versioning scheme**: a single public version at launch (for example, a URL-path version such as `/v1`; exact scheme fixed in the plan) with additive-only evolution and a documented deprecation policy; clients tolerate unknown enum values.
- **Relevance exposure**: only articles in the configured relevance band (relevant and weakly relevant by default; configuration decides) are exposed; irrelevant pairs remain internal.
- **Cluster exposure**: cluster context is a stable descriptor (identifier plus canonical name); membership listing is bounded and optional in the MVP contract; internal merge/split mechanics stay internal.
- **Scoring exposure**: ordering may use internal scores; any exposed scoring-related field is optional, descriptive, and version-labeled — never required and never shape-breaking; absence is valid.
- **Summaries exposure**: only stored, validated summaries (features 010-012) are served, labeled AI-generated with generation time; provider/model/prompt internals remain internal; the topic summary and article summaries are distinct contract fields.
- **Caching**: HTTP-level caching only (validators and directives); no server-side cache is required in the MVP; TTL guidance aligns with background-processing cadence and is documented per endpoint class.
- **Time handling**: responses use UTC timestamps plus window boundaries and the reference timezone identifier; display conversion is the client's concern (constitution Principle V).
- **Machine-readable contract**: a generated contract artifact (for example, OpenAPI) accompanies the implementation per the plan; contract fixtures remain the test authority.
- **Out of scope**: writes, admin/operational APIs, job triggering, real-time push, user features, GraphQL, AI search, and any exposure of internal records (jobs, prompts, diagnostics, provider payloads).

**Dependencies**: Feature 001 (core entities and indexes — read-only); Features 003-009 (topics, windows data, articles/sources, links, relevance, clusters, scores); Features 010-012 (validated summaries and their labeling); Feature 013 (background processing — freshness semantics documented; the API never triggers jobs); Project Constitution v1.0.0 (Principle II — attribution on every exposed item; Principle IV — cache/config boundaries; Principle V — windows only via the shared module, UTC storage); golden contract fixtures for testing.
