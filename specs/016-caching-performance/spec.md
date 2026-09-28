# Feature Specification: Caching & Performance Strategy (Read Paths)

**Feature Branch**: `016-caching-performance`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Define caching and performance strategies for the trending-news platform without changing the stable domain model. Identify high-read operations such as trending topics, topic details, summaries, and search results that can benefit from caching. Define cache keys, expiration, invalidation, stale-data behavior, and concurrency protection. Optimize SQL Server access through appropriate indexes, projections, pagination, and query patterns. Minimize repeated AI provider calls and expensive topic processing. Use simple application-level caching for the MVP. Keep an abstraction that allows distributed caching to be introduced later without coupling domain logic to a specific cache implementation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cached high-read responses with defined keys and expiry (Priority: P1)

The platform's hottest read operations — windowed trending topics, topic details (including summaries), and search results — are served through an application-level caching layer with deterministic, documented cache keys and per-class expiration, so repeated reads avoid re-querying SQL Server while never mixing results across windows, filters, or pages.

**Why this priority**: The user's core ask; read amplification is the largest avoidable cost, and wrong keys would silently serve incorrect data.

**Independent Test**: Fixture reads across windows, filters, and pages with instrumentation; verify repeated reads hit the cache, results are identical to uncached reads, and no key collisions occur across variants.

**Acceptance Scenarios**:

1. **Given** a cached class (for example, windowed topics), **When** the same read repeats within its TTL, **Then** the second read is served from cache with results identical to a fresh computation.
2. **Given** variants (different windows, filters, pages, reference timezone), **When** they are cached, **Then** each variant maps to a distinct deterministic key and no variant returns another's data.
3. **Given** a cache-disabled configuration, **When** reads run, **Then** behavior is unchanged (cache is transparent).

---

### User Story 2 - Invalidation, staleness bounds, and concurrency protection (Priority: P1)

Cached data stays within explicitly bounded staleness: TTLs are the primary mechanism, optionally tightened by freshness tokens derived from completed background processing (so reads after a job see refreshed data without manual purging); stale-serving on computation errors is permitted only within a configured grace and a hard maximum; and concurrent misses are protected by single-flight so one computation serves all waiters — in-process only, no distributed locks.

**Why this priority**: The user explicitly requires invalidation, stale-data behavior, and concurrency protection; unbounded staleness or cache stampedes would undermine trust and stability.

**Independent Test**: Fixture scenarios for TTL expiry, job-completion freshness tokens, error fallbacks within/without grace, and bursty concurrent misses; verify bounds honored and single-flight behavior.

**Acceptance Scenarios**:

1. **Given** a completed processing run affecting cached data, **When** reads follow, **Then** refreshed data is served within the defined freshness bound (no manual purge needed).
2. **Given** a computation failure and a stale entry within grace, **When** the read occurs, **Then** stale data may be served, and the event is logged; beyond the hard maximum, stale data MUST NOT be served.
3. **Given** many concurrent misses for one key, **When** they arrive together, **Then** exactly one computation runs and all callers receive its result (single-flight).

---

### User Story 3 - Optimized SQL Server access for read paths (Priority: P2)

Public reads use optimized database access patterns: projection-only queries shaped for the consumed fields (no entity materialization), no N+1 query storms (batched/include-based loading), parameterized queries, read-only no-tracking execution, bounded (keyset or bounded-offset) pagination per the public API rules, bounded count strategies, and query-pattern-driven index coverage — with any missing indexes added through core-model migrations and no changes to entities.

**Why this priority**: The user explicitly requires index/projection/pagination/query-pattern optimization; caching cannot fix fundamentally slow queries.

**Independent Test**: Query instrumentation in fixtures: per-request query counts (no N+1), projection audits (only needed columns), parameterization scans, index-usage checks for hot queries at baseline volume, and latency targets.

**Acceptance Scenarios**:

1. **Given** any public read, **When** executed, **Then** query counts stay within the documented budget per endpoint (no N+1), and only required columns are projected.
2. **Given** hot query patterns, **When** executed at baseline volume, **Then** plans use the defined indexes (no table scans), and latency targets are met.
3. **Given** a query pattern lacking index coverage, **When** identified, **Then** the missing index is added via a core-model migration with zero entity changes.

---

### User Story 4 - Distributed-ready cache abstraction without domain coupling (Priority: P3)

Caching is consumed exclusively through the application-level cache contract: the MVP ships a simple in-process implementation, and a distributed cache can be introduced later (should a demonstrated multi-instance requirement arise) by implementing the same contract with configuration-driven selection — with zero changes to domain logic or API/frontend contracts.

**Why this priority**: Explicitly required forward-compatibility; introducing distributed infrastructure now would violate the demonstrated-need rule (constitution Principle III).

**Independent Test**: Register alternative in-memory and remote-style stub implementations; swap them via configuration; verify identical behavior through the contract and zero cache-implementation references in domain layer code.

**Acceptance Scenarios**:

1. **Given** two cache implementations registered, **When** one is selected via configuration, **Then** reads, freshness, invalidation, and staleness behavior remain identical through the contract.
2. **Given** the domain and API layers, **When** audited, **Then** they reference only the cache contract — never an implementation type.
3. **Given** an implementation swap (cold start), **When** reads continue, **Then** correctness holds (recomputation is acceptable; serving beyond staleness bounds is not).

---

### Edge Cases

- Cache key collisions across windows (pre/post boundary) → keys embed the resolved window boundaries from the shared module; rollover naturally selects a new key (never a mixed result).
- Reference timezone change (configuration) → keys include the reference timezone; entries from the previous timezone are never served after the change.
- Cache stampede at expiry (many simultaneous misses) → single-flight guarantees one computation; optional deterministic (hash-based) TTL jitter MAY spread expirations (defined rule, not randomness).
- Process restart → in-process cache is empty (cold start); no stale data survives; stampede protection still applies.
- Long-running background pass while reads occur → reads continue serving within staleness bounds; freshness token advances when the pass completes.
- Computation error with no cached entry → the error surfaces per the public API's stable error shape (no fabricated data).
- Computation error with a stale entry beyond the hard maximum → stale data MUST NOT be served; failure surfaces.
- Very large search result sets → bounded pagination prevents unbounded materialization; counts strategy documented (no unbounded count queries).
- Summary latest-valid resolution → cached projection can serve stale summaries only within the staleness bounds defined for that class; validation guarantees from features 010-012 are never bypassed (only stored, validated summaries are ever cached or served).
- Cache growth pressure (memory limits) → bounded entry counts/size per class with eviction policy defined; evictions cause recomputation, never incorrectness.
- Two nodes of the application (accidental) → out of scope per the single-instance assumption; in-process caches are independent but correctness is preserved because keys are deterministic and freshness tokens come from the shared store.
- Configuration change (TTL/class enablement) → applies at runtime without code changes; in-flight entries follow their existing bounds until expiry.
- Personalization-related keys → none exist: no user-specific data enters the cache in the MVP.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The stable domain model MUST NOT change: no new entities, fields, or business logic are introduced; caching and query optimization live in the application/infrastructure layers only, and the domain layer MUST NOT reference any cache type or implementation.
- **FR-002**: A cache abstraction (application-level contract) MUST provide get-or-create, set, and invalidate semantics (by enumerated class and key); the MVP implementation is a simple in-process cache; a distributed cache MUST be introducible later by implementing the same contract with configuration-driven selection — no domain or API-contract changes.
- **FR-003**: Cacheable classes MUST include at least: windowed trending topics, topic details (including summary projections), summary latest-valid projections, and search results; each class MUST be independently enableable and configurable; classes not configured remain uncached (transparent pass-through).
- **FR-004**: Cache keys MUST be deterministic, documented, and composed of: a key-scheme version, the data class, the resolved window boundaries where applicable (shared module), all filter and pagination inputs, the reference timezone, and the class's freshness token where enabled; keys MUST NEVER include user-specific data (no personalization exists in the MVP).
- **FR-005**: Expiration MUST be per-class configurable with documented maximum-staleness bounds aligned to background-processing cadence (feature 013); deterministic (hash-based) TTL jitter MAY be used to spread expirations under a defined rule.
- **FR-006**: Invalidation MUST be deterministic and layered: TTL is primary; optionally, freshness tokens derived from completed processing markers (for example, the latest completed run of the data's producing job type) are embedded in keys so reads after a completed pass naturally bypass stale entries — no manual purging required; explicit purge-by-class remains available to operators.
- **FR-007**: Stale-data behavior MUST be bounded: only entries within their class TTL/limits are served; serving stale entries on computation errors MAY be enabled per class but MUST respect a configured grace and a hard maximum (beyond which failures surface rather than stale data); staleness events are logged; HTTP-level caching (feature 014) remains unchanged and consistent with these bounds.
- **FR-008**: Concurrency protection MUST prevent cache stampedes: single-flight per key (one in-process computation serving all concurrent waiters, with bounded wait and cancellation) — no distributed locks in the MVP; every waiter receives the same result or a categorized failure.
- **FR-009**: Windowed-view correctness MUST hold: keys embed resolved boundaries per the shared time-window module, so time passing across a boundary always lands on a new key — a pre-boundary result can never be served as a post-boundary result.
- **FR-010**: SQL access for read paths MUST follow optimized patterns: projection-only queries for consumed fields (no entity materialization for public reads); no N+1 (batched or include-based loading, verified by query-count budgets); parameterized queries only (no dynamic SQL concatenation); read-only, no-tracking execution; bounded pagination consistent with the public API's rules; bounded or cached count strategies (no unbounded COUNT scans).
- **FR-011**: Index coverage MUST match query patterns: hot read queries MUST be mapped to defined indexes (from the core model and feature 015) at baseline volume without table scans; any missing index is added via core-model migrations with zero entity changes; index usage is verified in tests.
- **FR-012**: Read paths MUST NEVER trigger expensive computation: no AI provider calls, no scoring/relevance/clustering recomputation on reads; read paths resolve persisted latest-valid results only (features 006-010 semantics unchanged); summary reuse rules (feature 010) are reinforced and unaffected — caching MUST NOT cause additional AI calls.
- **FR-013**: Performance targets MUST be measurable: public reads meet the established latency targets (paged responses within 1 second at 10× baseline per features 014/015); cache-hit paths are measurably faster (documented hit-path target fixed in the plan); cache hit ratios and query counts are observable from logs/metrics for verification.
- **FR-014**: All cache policies (class enablement, TTLs, staleness grace/hard maximum, freshness-token usage, size bounds/eviction) and the implementation selection MUST be configuration-driven without code changes (constitution Principle IV); no secrets are involved.
- **FR-015**: The strategy MUST be testable offline and deterministically: fixtures cover key derivation (windows/filters/pages/timezones), TTL expiry, freshness-token invalidation on job completion, stale-on-error bounds, single-flight behavior, eviction, cache-disabled transparency, and query-pattern audits (query counts, projections, parameterization, index usage).
- **FR-016**: Out of scope: domain/model changes (zero), distributed cache deployment or Redis introduction in the MVP (abstraction only — introduced when a demonstrated requirement exists), CDN/edge caching (feature 014's HTTP caching remains the client-facing layer), personalization caches, and any new business entities or background pipelines.

### Key Entities

The stable domain model is untouched; this feature introduces cache/performance infrastructure artifacts in the application layer.

- **Cache abstraction contract (application-level)**: get-or-create, set, and invalidate-by-class/key semantics used by read paths; MVP implementation is in-process; future distributed implementations plug in via the same contract and configuration.
- **Cache policy registry (configuration)**: per-class definitions — enablement, TTL, staleness grace and hard maximum, freshness-token usage, size bounds/eviction, deterministic jitter rules.
- **Key scheme specification**: the documented composition of cache keys (scheme version, class, window boundaries, filters/pagination, reference timezone, freshness token) and their canonical serialization.
- **Freshness tokens (derived)**: read-only values sourced from processing markers (the latest completed run of the data's producing job type per feature 013) embedded in keys to achieve post-job freshness without purging.
- **Query-pattern and index mapping**: the documented mapping from each hot read query to its projection shape, pagination strategy, and covering index — with migration-level index additions when missing (no entity changes).
- **Observability hooks**: hit/miss, staleness-served, eviction, and query-count instrumentation for verification and operations.
- **Fixtures & benchmarks**: offline corpora and harnesses for key derivation, invalidation, staleness bounds, single-flight, query-pattern audits, and latency targets.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Cached-class correctness: 100% of fixture reads return results identical to uncached computation, and no variant (window, filter, page, timezone) ever receives another variant's data.
- **SC-002**: Freshness after jobs: following a completed processing run, reads serve refreshed data within the defined freshness bound in 100% of fixture scenarios; pre/post-boundary window reads never conflate.
- **SC-003**: Staleness bounds: stale-on-error never exceeds the configured grace or hard maximum in 100% of error scenarios; stale-served events are logged.
- **SC-004**: Stampede protection: bursty concurrent misses yield exactly one computation per key with all waiters receiving its result in 100% of simulations.
- **SC-005**: SQL patterns: query-count budgets per endpoint hold (zero N+1), projection and parameterization audits are clean, and hot queries use defined indexes without table scans in 100% of baseline-volume tests.
- **SC-006**: Latency: paged public reads stay within 1 second at 10× baseline; cache-hit paths meet the planned hit-path target (measurably faster than uncached, benchmarked).
- **SC-007**: Abstraction and boundaries: an alternative cache implementation (stub simulating distributed behavior) integrates via configuration with zero domain or API-contract changes in 100% of swap tests; audits confirm zero domain-layer cache references and zero read-path AI/recomputation calls.

## Assumptions

- **MVP cache**: a simple in-process application-level cache inside the single-instance modular monolith (feature 013); a distributed cache (for example, Redis) is introduced later only if a demonstrated requirement arises, through the same abstraction.
- **Freshness model**: processing markers from the background system (completed run timestamps per job type) supply invalidation signals without inter-process messaging; TTL remains the safety net if markers are unavailable.
- **HTTP caching relationship**: feature 014's HTTP validators/directives remain the client-facing layer; the application cache is server-side and complementary, and its staleness bounds are not looser than the effective public freshness the platform promises.
- **Read paths**: features 014/015 already serve stored data only; this feature reinforces that reads never trigger AI or recomputation and measures it.
- **Counts and pagination**: total-count strategies (bounded counts or cached counts) are fixed in the plan within the public API's pagination contract; no unbounded count scans.
- **Determinism**: keys, jitter (if enabled), and invalidation rules are deterministic and documented; randomness is not permitted in correctness-affecting behavior.
- **Eviction**: bounded entry counts/size per class with a defined eviction policy; eviction causes recomputation, never incorrectness.
- **Configuration**: TTLs and policies are per-environment configuration with conservative defaults in the plan; no secrets involved.
- **Out of scope**: domain changes (zero), distributed caching deployment, CDN/edge, personalization caches, and new background pipelines (feature 013 owns scheduling).

**Dependencies**: Feature 001 (entities and indexes — read-only; index additions via migrations only); Feature 013 (processing markers for freshness tokens and cadence for TTL alignment); Feature 014 (HTTP caching, latency targets, contract stability — unchanged); Feature 015 (search result caching and its engine seam); Feature 010 (summary reuse rules — reinforced, never bypassed: only stored, validated summaries are cached/served); Features 006/007/009 (persisted latest results read-only); Project Constitution v1.0.0 (Principle II — validated summaries and attribution never bypassed; Principle III — no distributed scope creep in the MVP; Principle IV — configuration-driven policies and implementation selection; Principle V — window boundaries via the shared module only); fixtures, benchmarks, and query-instrumentation harnesses for testing.
