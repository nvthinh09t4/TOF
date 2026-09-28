# Feature Specification: Source Administration (RSS/News Sources)

**Feature Branch**: `021-source-administration`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement administrative management of RSS/news Sources using the existing Source core entity. Administrators must be able to create, update, enable, disable, validate, and remove configured sources. Store operational information such as source name, feed URL, language, category, active status, crawl interval, last successful crawl, and last failure using core fields or dedicated operational extensions where appropriate. Validate feeds before activation. Prevent duplicate source configurations. Do not introduce a second source model or couple Source directly to a specific RSS provider."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create, update, and list configured sources (Priority: P1)

An administrator can list configured sources and create or update one with its configuration — display name, feed URL, source kind, language, category, and crawl interval — using the existing Source entity plus a dedicated operational extension for volatile state. Duplicate configurations are rejected up front with actionable errors, so the same feed cannot be registered twice through any URL variant.

**Why this priority**: Managing the source list is the capability's foundation; the platform's content pipeline begins at these records.

**Independent Test**: Admin fixtures (create, update, list, duplicate attempts across URL variants); verify records, canonical-equality duplicate rejection, list views with operational state, and validation errors.

**Acceptance Scenarios**:

1. **Given** valid source details, **When** an administrator creates a source, **Then** a Source record plus its operational extension exist with language, category, interval, and audit timestamps (UTC).
2. **Given** an existing source, **When** a create or update would produce an equivalent canonical feed URL (scheme/case/whitespace variants), **Then** it is rejected with an actionable duplicate error.
3. **Given** existing sources, **When** an administrator lists them, **Then** each row shows configuration plus current operational state (active status, validation status, last success/failure, interval).

---

### User Story 2 - Validate feeds before activation; enable/disable lifecycle (Priority: P1)

A source can be validated explicitly: the platform checks reachability and parseability through the news/feed provider abstraction for the source's kind (never through RSS-specific code in the admin domain) and records the outcome with its failure category and check time. Activation is gated: a source can be enabled only after a successful validation within a configured freshness window; disabling takes effect immediately for subsequent crawling while all historical attribution remains intact.

**Why this priority**: The user explicitly requires validation before activation; gating prevents broken feeds from entering the crawling pipeline.

**Independent Test**: Stub-provider fixtures for success, timeout, parse failure, and unavailable feeds; verify validation outcomes/categories, enable-gating rules, and immediate disable behavior.

**Acceptance Scenarios**:

1. **Given** a source never validated or with stale validation, **When** an administrator enables it, **Then** the operation is rejected with an actionable error requiring validation first.
2. **Given** a source with a successful validation within the freshness window, **When** it is enabled, **Then** the transition succeeds and is recorded with its timestamp.
3. **Given** an enabled source, **When** an administrator disables it, **Then** subsequent crawling excludes it immediately while its existing articles and attribution remain available.
4. **Given** a validation attempt, **When** it fails, **Then** the outcome is recorded with the platform's failure category (timeout, unavailable, parse error, and so on) and the source cannot be activated on that attempt.

---

### User Story 3 - Operational state feeding the crawl pipeline (Priority: P2)

Operational information is stored in a dedicated latest-state extension (not as volatile fields merged into the stable entity's identity): last successful crawl, last failure with its categorized reason, current validation status, the resolved feed URL when redirects occur, and the per-source crawl interval override. The ingestion scheduler consumes enabled sources with their intervals; administrators see the operational picture at a glance.

**Why this priority**: Required operational data; it directly drives crawl cadence and lets administrators diagnose failing sources.

**Independent Test**: Fixture crawls updating success/failure states through the ingestion pipeline; verify state updates, interval consumption, redirect-resolved URLs, and admin visibility.

**Acceptance Scenarios**:

1. **Given** completed crawls, **When** operational state is viewed, **Then** last-success and last-failure (with category) timestamps (UTC) reflect the latest outcomes.
2. **Given** a source with an interval override, **When** crawling is scheduled, **Then** the source's interval (not the global default) applies.
3. **Given** a feed that redirects, **When** it is validated or crawled, **Then** the resolved URL is recorded for operator awareness (configuration is updated deliberately, never silently).

---

### User Story 4 - Remove sources safely (Priority: P2)

Removing a source never destroys attribution: removal defaults to retirement (hidden from crawling and default admin views, retained for attribution); hard deletion is permitted only for sources with zero footprint (no articles, discovery links, or references), and is deterministically blocked otherwise.

**Why this priority**: The platform's attribution guarantees (constitution Principle II) must survive administrative mistakes.

**Independent Test**: Fixtures with footprint (articles present) and without; verify retirement behavior, blocked deletions, allowed zero-footprint deletions, and attribution audits.

**Acceptance Scenarios**:

1. **Given** a source with existing articles, **When** removal is requested, **Then** the source is retired (not deleted) and all article attribution remains resolvable.
2. **Given** a source with zero footprint, **When** removal is requested, **Then** deletion is allowed and the record is removed.
3. **Given** a retired source, **When** listings or crawls run, **Then** it is excluded from both while remaining resolvable historically.

---

### Edge Cases

- Duplicate via URL variants (scheme case, host case, trailing slash, whitespace) → canonical comparison rejects; error names the conflicting source.
- Duplicate via feed redirects (different URLs resolving to the same feed) → the resolver records the resolved URL; the duplicate check uses canonical forms; operators are warned about redirect overlap (deterministic, no silent merges).
- Feed URL changed on an existing source → duplicate re-check runs; the source requires re-validation before it can remain/return enabled (rulebook rule).
- Validation flakiness (temporarily unreachable) → failure recorded with category; the source remains/becomes disabled per gating rules; a later successful validation re-enables the path to activation.
- Enabling a source while a crawl run is in progress → the change affects subsequent runs/items per ingestion semantics; no corruption, in-flight behavior documented.
- Disabling or retiring a source mid-crawl → current run follows ingestion semantics; subsequent runs exclude it; articles already stored keep attribution.
- Interval set below/above platform bounds → validation error with limits; interval changes apply from the next scheduling decision.
- Concurrent admin operations on one source (for example, enable racing an update) → deterministic resolution via constraints and last-write semantics documented; no corrupted state.
- Validation succeeds but parse yields zero items → treated as a valid-but-empty feed (successful validation with a warning flag), per provider semantics (empty feeds are not errors).
- Non-RSS source kinds (future) → the same admin flow applies; validation delegates by kind; zero admin-code changes.
- Zero-footprint deletion of a source that was disabled for years → allowed if genuinely no references exist; audit confirms; otherwise blocked.
- Admin credential rotated/absent → admin surface rejects with a clear authorization error; public API unaffected; no secrets in logs.
- Mass updates (many sources toggled) → validation errors reported per item; partial-batch outcomes deterministic and audited.
- Source language/category values outside configured vocabularies → validation error (vocabulary configurable), never silently coerced.
- Same feed re-added after retirement → duplicate rules apply against retired records (retired sources remain in canonical duplicate space; reactivation path is via update/enable, not re-creation) — deterministic and documented.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The capability MUST use the existing Source core entity as the single source model (no second model); volatile operational state MUST live in a dedicated latest-state extension entity; the admin domain MUST NOT couple to any specific feed provider — validation and checks delegate to the news/feed provider abstraction by source kind.
- **FR-002**: Administrators MUST be able to list, create, and update sources with configuration: display name, feed URL, source kind, language, category, and per-source crawl interval (with platform defaults and validated bounds); audit timestamps are stored in UTC.
- **FR-003**: Duplicate prevention MUST be deterministic: source identifiers are compared in canonical form (scheme/host normalization, whitespace trimming — consistent with the platform's URL canonicalization discipline); equivalent URLs are rejected on create and update with actionable errors naming the conflict; unique constraints enforce this at the data layer under concurrency.
- **FR-004**: Changing a source's feed URL MUST re-run duplicate checks and require re-validation before the source can be (or remain) enabled.
- **FR-005**: An explicit validation operation MUST invoke the news/feed provider abstraction for the source kind and check reachability and parseability; outcomes MUST record success/failure with the platform's failure category, check time (UTC), and a resolved feed URL when redirects occur; empty-but-valid feeds are successes with a warning flag.
- **FR-006**: Activation MUST be gated: enabling requires a successful validation within a configurable freshness window; violations are rejected with actionable errors; disabling takes effect immediately for subsequent crawling; neither operation alters historical attribution.
- **FR-007**: Source lifecycle states MUST include active (enabled), disabled, and retired; transitions are recorded with UTC timestamps; retired sources are excluded from crawling and default admin listings while remaining resolvable for attribution.
- **FR-008**: The operational extension MUST hold latest-state values only (last successful crawl, last failure time with categorized reason, validation status/outcome/time, resolved URL, interval override, updated-at); updates replace prior state; it is not a history log (history remains in logs), and it is never merged into the source's identity fields.
- **FR-009**: Per-source crawl intervals MUST override the global default and MUST be consumed by ingestion scheduling (a documented delta reconciled with the crawling/orchestration features); interval changes apply from the next scheduling decision.
- **FR-010**: Removal MUST default to retirement; hard deletion MUST be permitted only when the source has zero footprint (no articles, discovery links, or references) and MUST be deterministically blocked otherwise — retirement preserves attribution forever.
- **FR-011**: The admin surface MUST be a versioned administrative contract (list/get/create/update/enable/disable/validate/remove) with DTOs mapped from core data — no entity or provider leakage, consistent error shapes, actionable validation errors, and pagination for listings; admin operations are logged with actor identity, operation, target, timestamp (UTC), and outcome (structured logs suffice; no audit entity required in the MVP).
- **FR-012**: The admin surface MUST require the administrative authorization boundary (MVP: a configured admin credential from environment/secure store; the mechanism is replaceable by a future authentication feature without changing management semantics) and MUST NOT appear on the public read-only API in any form.
- **FR-013**: Administrative operations MUST be safe against concurrent background crawling: unique constraints prevent race-created duplicates; enable/disable/retire transitions follow documented in-flight semantics (they affect subsequent runs/items per the ingestion feature's rules); no operation may corrupt stored articles or attribution.
- **FR-014**: Provider neutrality MUST hold end to end: future non-RSS kinds reuse the identical admin flow with zero admin-code changes (validation delegates by kind); provider-specific syntax stays in adapters.
- **FR-015**: Configuration seeding (the established data/configuration-only source management) MUST continue to work and MUST NOT clobber runtime-managed sources; exact merge precedence is documented in the plan; no code changes are required for any CRUD operation.
- **FR-016**: The capability MUST be testable offline and deterministically with a stub feed provider: fixtures cover duplicates and canonical variants, validation gating and categories, lifecycle transitions, retirement versus deletion, operational updates, interval consumption, concurrency, and admin-boundary checks.
- **FR-017**: Out of scope: reader-facing features, ingestion internals, provider adapters, an admin web UI (a future frontend may consume this contract), user management/authentication systems (the boundary is pluggable), and per-source credential management beyond existing configuration patterns.

### Key Entities

This feature introduces one latest-state extension entity and an administrative contract over the existing core model; the Source entity remains the single source model.

- **Source (core; used)**: the single source model — identity (unique canonical feed identifier), kind, display name, enabled state, audit timestamps; any additional semi-stable configuration fields needed (language, category) are reconciled as minimal additive core deltas in the plan; volatile state never lands here.
- **SourceOperationalState (new; latest-state extension)**: one row per source: last successful crawl (UTC), last failure (time + platform failure category), validation status/outcome/checked-at (UTC), resolved feed URL (redirect awareness), crawl interval override, updated-at; updates replace prior state; no history obligation (logs remain the history).
- **Validation operation**: a kind-agnostic feed check executed through the news/feed provider abstraction; produces categorized outcomes recorded in operational state.
- **Admin surface contract (versioned, separate from the public API)**: DTOs and operations for list/get/create/update/enable/disable/validate/remove, with pagination, validation errors, and no internal leakage; secured by the administrative authorization boundary.
- **Administrative authorization boundary (configuration)**: the MVP admin credential sourced from environment/secure store; replaceable mechanism; no secrets in code or logs.
- **Fixtures & stub provider**: offline corpora for duplicates, validation outcomes, lifecycle transitions, footprint rules, concurrency, and boundary tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Duplicate prevention: 100% of canonical-equivalent URL variants are rejected on create and update with actionable errors; concurrent create attempts yield exactly one source.
- **SC-002**: Activation gating: enabling without a fresh successful validation is rejected in 100% of cases; validation outcomes are recorded with correct categories and UTC timestamps.
- **SC-003**: Lifecycle effectiveness: enable/disable/retire transitions take effect for subsequent crawling in 100% of fixture scenarios, are recorded with timestamps, and preserve article attribution (audit = 0 violations).
- **SC-004**: Removal safety: deletions are blocked in 100% of footprint-present cases and permitted only for zero-footprint sources; retirement keeps attribution fully resolvable.
- **SC-005**: Operational state accuracy: last-success, last-failure (with category), validation status, and resolved URLs reflect the latest outcomes in 100% of fixture scenarios; per-source intervals are honored by scheduling decisions.
- **SC-006**: Boundary and audit checks: the public API exposes zero admin operations (route audit 100%); the admin surface rejects uncredentialed access in 100% of tests; every admin operation is logged with actor, operation, target, timestamp, and outcome.
- **SC-007**: Contract and neutrality: golden admin-DTO tests pass for 100% of operations; audits find zero entity/provider leakage; a hypothetical new source kind completes the full admin flow through the abstraction with zero admin-code changes (stub-kind verification).

## Assumptions

- **Admin surface scope**: this feature delivers the administrative capability and its versioned API contract; an admin web UI is a future frontend feature consuming it; no public reader-facing changes occur.
- **Authorization model (MVP)**: a pre-shared admin credential from environment/secure store protects the admin surface; a future authentication/authorization feature may replace the mechanism without changing management semantics.
- **Operational state placement**: volatile fields (last success/failure, validation status, resolved URL, interval override) live in the latest-state extension entity; semi-stable configuration (name, URL, kind, language, category) lives on or minimally extends the core Source per the plan; no volatile fields pollute the stable entity.
- **Validation semantics**: validation is a kind-agnostic reachability + parseability check executed through the feed provider abstraction (feature 002's contract; RSS specifics remain in adapters); empty-but-valid feeds are successes with a warning.
- **Duplicate discipline**: canonical comparison follows the platform's URL canonicalization discipline; retired sources remain in the duplicate space (reactivation, not re-creation).
- **Removal semantics**: retirement is the default removal; hard deletion only for zero-footprint sources; no cascades ever.
- **Configuration coexistence**: configuration/seed-based source management (as established by the ingestion feature) remains supported with documented precedence (runtime-managed records are never clobbered); exact merge rules are fixed in the plan.
- **Scheduling delta**: per-source crawl intervals are consumed by ingestion scheduling — a documented additive delta reconciled with the ingestion and orchestration features in the plan.
- **Logging over entities**: administrative audit trails rely on structured logs (actor, operation, target, timestamp, outcome) rather than a new audit entity in the MVP.
- **Out of scope**: admin UI, user/auth systems, provider adapters, ingestion internals, per-source secrets management beyond existing patterns.

**Dependencies**: Feature 001 (core model — Source as the single model; minimal additive fields reconciled in the plan; new operational-state entity mapping); Feature 002 (news/feed provider abstraction — validation delegates by kind; failure categories); Feature 004 (ingestion — enabled sources and per-source intervals consumed by crawling; retired/disabled exclusions; configuration coexistence); Feature 013 (orchestration — scheduling consumes source intervals; admin operations safe against runs); Feature 014 (contract conventions — versioned DTOs, error shapes, pagination, additive-evolution discipline for the separate admin surface); Project Constitution v1.0.0 (Principle I — provider-neutral delegation; Principle II — attribution preserved through all lifecycle operations; Principle IV — configuration and credentials via environment/secure store, no secrets in code or logs; Principle V — UTC timestamps); stub feed providers and deterministic fixtures for testing.
