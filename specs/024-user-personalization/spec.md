# Feature Specification: User Personalization (Explicit Follows & Recents)

**Feature Branch**: `024-user-personalization`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement lightweight explicit user personalization using the existing User core entity. Users should be able to: follow topics; follow sources; hide topics; view recently visited topics. Use dedicated relationship entities such as UserTopic and UserSource rather than adding collections or personalization state directly to Topic or Source. Keep behavior deterministic and explicit. Do not implement machine-learning recommendations or behavioral ranking in this feature. Define APIs, authorization rules, persistence constraints, and frontend integration points."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Follow topics and sources explicitly (Priority: P1)

An authenticated user can follow and unfollow topics and news sources through explicit operations, and can list what they follow. Follow state lives in dedicated relationship entities (UserTopic and UserSource) — never as collections or state on Topic or Source. Operations are idempotent, lists are deterministically ordered, and follows are private to the user.

**Why this priority**: Following is the foundation of explicit personalization; it is the user's stated first capability.

**Independent Test**: API fixtures following/unfollowing topics and sources (including repeats, caps, empty lists); verify relationship rows, idempotency, deterministic ordering, and private scoping.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they follow a topic or source, **Then** a relationship row exists with a UTC timestamp and the operation is idempotent on repeat.
2. **Given** follows exist, **When** the user lists them, **Then** results are ordered deterministically (most recent first, tie-broken by identifier) and scoped strictly to that user.
3. **Given** a followed topic or source, **When** the user unfollows it, **Then** the relationship is removed and subsequent operations remain consistent.

---

### User Story 2 - Hide topics with deterministic mutual exclusivity (Priority: P1)

A user can hide topics they are not interested in and unhide them later. Follow and hide are mutually exclusive states on the same user–topic relationship: one row per (user, topic) carries a state (followed or hidden) with defined transition rules — following a hidden topic makes it followed; hiding a followed topic makes it hidden. Hidden topics are excluded from the user's personalized listings, while public pages remain unaffected.

**Why this priority**: Explicitly required; the mutual-exclusivity rule is what keeps behavior contradiction-free and easy to reason about.

**Independent Test**: Transition fixtures (follow→hide→unhide→follow), list-scoping checks, and public-surface audits; verify single-row state, deterministic outcomes, and no public impact.

**Acceptance Scenarios**:

1. **Given** a followed topic, **When** the user hides it, **Then** the relationship's state becomes hidden (no duplicate row) and it disappears from the user's follow list.
2. **Given** a hidden topic, **When** the user follows it again, **Then** the state becomes followed deterministically.
3. **Given** any hidden state, **When** public surfaces are examined, **Then** nothing changes for other readers (personalization never leaks outward).

---

### User Story 3 - Recently visited topics (explicit, bounded, deterministic) (Priority: P1)

Opening a topic records an explicit visit (an API call from the topic detail page — no hidden background tracking), and the user can view a bounded, deduplicated list of recently visited topics ordered by most recent visit. Retention is capped per user with deterministic pruning (the oldest visits are dropped when the cap is exceeded), and visit data is private to the user.

**Why this priority**: Explicitly required; the bounded-cap rule keeps the feature lightweight and privacy-conscious.

**Independent Test**: Visit fixtures (repeated visits, many topics exceeding the cap); verify deduplication with updated timestamps/counts, deterministic pruning at the cap, ordering, and privacy scoping.

**Acceptance Scenarios**:

1. **Given** a user opens a topic, **When** the visit is recorded, **Then** one entry per (user, topic) exists with the latest visit time (UTC) and an updated visit count.
2. **Given** visits exceed the configured cap, **When** a new visit is recorded, **Then** the least-recently-visited entries are pruned deterministically to respect the cap.
3. **Given** a user's recent list, **When** it is queried, **Then** it is ordered by last-visited time (descending, tie-broken by identifier) and contains only that user's data.

---

### User Story 4 - Authorization, isolation, and privacy (Priority: P2)

Personalization operates strictly per authenticated user: the personalization surface requires an authenticated identity (the platform's authentication boundary supplies it; unauthenticated requests are rejected without data), every query and mutation is scoped to the caller, cross-user access fails with non-disclosing semantics, and personalization data (follows, hides, visits) is never exposed on public or admin surfaces beyond what those surfaces already define.

**Why this priority**: Explicitly required authorization rules; privacy is non-negotiable for personal data.

**Independent Test**: Isolation fixtures (user A vs user B), unauthenticated requests, cross-user probes; verify rejections, non-disclosure, and audited absence of personalization data on public surfaces.

**Acceptance Scenarios**:

1. **Given** no authenticated identity, **When** personalization endpoints are called, **Then** requests are rejected with the standard authentication error and no data.
2. **Given** user A's credentials, **When** user B's personalization data is probed, **Then** access fails with non-disclosing semantics (no existence disclosure).
3. **Given** any personalization data, **When** public API responses are audited, **Then** zero personalization data appears (100% clean).

---

### User Story 5 - Deterministic, explicit behavior with defined integration points (Priority: P3)

The feature stays strictly explicit and deterministic: no machine-learning recommendations, no behavioral ranking, no implicit tracking beyond the visit API; identical operations produce identical states. Frontend integration points are defined and minimal: the topic detail page hosts follow/unfollow and hide/unhide controls reflecting server state (with the established loading/error/retry conventions) and records a visit when a topic is opened; other pages are unchanged in the MVP.

**Why this priority**: Explicit user constraints (no ML/ranking; define integration points) that also keep the feature's scope small.

**Independent Test**: Determinism fixtures (repeat operation sequences), scope audits (no ranking/ML logic), and integration-point fixtures on the topic detail page (controls reflect and update server state; visit fires on open).

**Acceptance Scenarios**:

1. **Given** repeated identical operation sequences, **When** they run in any order permitted by the API, **Then** the resulting state is identical for identical inputs (no hidden ordering effects).
2. **Given** the topic detail page, **When** a user toggles follow or hide, **Then** the control reflects the server-confirmed state and survives reloads; errors render per the established conventions.
3. **Given** a topic open event, **When** the page loads the topic, **Then** exactly one visit record operation is issued per open, with no tracking beyond it.

---

### Edge Cases

- Follow/hide on the same topic in rapid succession → single row transitions deterministically (last explicit operation wins); no contradictory states ever exist.
- Cap exceeded during follows (max follows per user) → operation rejected with actionable error, or per configured policy (rejection is the deterministic default); existing follows untouched.
- Visit cap pruning at exact boundary → deterministic order (oldest last-visited pruned first; ties broken by identifier).
- Two devices recording visits concurrently → dedupe per (user, topic) with last-write timestamp resolution; counts remain consistent.
- Topic or source later retired/disabled → relationship rows remain (user intent is independent of lifecycle); listings show items per public exposure rules (retired/disabled items follow the same visibility rules as other surfaces).
- User requests lists while item data is missing (topic removed) → list renders placeholders or omits per the defined rule (deterministic omission with counts preserved is the default).
- Duplicate API calls (follow when already followed, hide when already hidden) → idempotent success, no state change, no error.
- Unfollow of a hidden topic → removal of the relationship (unfollow semantics apply to the row regardless of state) as the documented rule.
- Authentication token absent/expired mid-session → subsequent operations fail with the authentication error; already-confirmed local state stops updating (no stale optimistic states persist).
- Personalization API additions over time (new explicit capabilities) → additive evolution per the platform's versioning rules; no changes to existing semantics.
- Visit recording disabled by configuration → API returns the documented disabled response; no data collected; lists remain available from prior data.
- Concurrent hide and follow from two sessions → last explicit operation wins deterministically; the single-row invariant holds.
- Cross-user probing with valid identifiers → non-disclosing not-found semantics, never 403-with-existence disclosure.
- Anonymous reader browsing topics (no account) → platform fully usable; visit recording simply doesn't occur (no accounts required to read).
- Personalization data export/deletion requests (future privacy features) → flagged as future work; current delete semantics are per-item unfollow/unhide only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST use the existing User core entity as the single user model and introduce dedicated relationship entities — **UserTopic** (follow/hide state per (user, topic)), **UserSource** (follow state per (user, source)), and **UserTopicVisit** (visit records per (user, topic)) — no personalization collections or state may be added to Topic, Source, or any other core entity, and no other entities are modified.
- **FR-002**: Personalization MUST require an authenticated user identity supplied by the platform's authentication boundary (interface-defined; the MVP mechanism follows the platform's established credential approach with per-user credentials from secure storage; a future authentication feature replaces the mechanism without changing personalization semantics); unauthenticated requests are rejected with the standard authentication error and no data.
- **FR-003**: Per-user isolation MUST be enforced at every layer: all reads and writes are scoped to the authenticated user; cross-user access fails with non-disclosing semantics (no existence disclosure); personalization data MUST NOT appear on public or admin surfaces beyond what those surfaces already define.
- **FR-004**: Follow operations MUST cover topics and sources: idempotent add/remove via explicit APIs; listings ordered deterministically (most recent first, tie-broken by identifier) with pagination; a configurable maximum-follows cap per user enforced with actionable errors; follows are private to the user.
- **FR-005**: Hide operations MUST apply to topics with deterministic mutual exclusivity against follow: exactly one UserTopic row per (user, topic) carrying state followed|hidden; transitions are defined (hide a followed topic → hidden; follow a hidden topic → followed; unfollow removes the row regardless of state); hidden topics are excluded from the user's personalized listings/consumption while public surfaces remain unchanged.
- **FR-006**: Recently visited topics MUST be explicit and bounded: visits are recorded only through an explicit API call (issued by the topic detail page on open — no background or implicit tracking); records deduplicate per (user, topic) keeping the latest visit time (UTC) and a visit count; retention is capped per user with deterministic pruning of least-recently-visited entries (ties by identifier); the list is ordered and paginated deterministically.
- **FR-007**: Persistence constraints MUST hold: unique constraints on (user, topic) for UserTopic and UserTopicVisit and on (user, source) for UserSource; UTC timestamps for followed/hidden/last-visited; caps enforced deterministically; relationship rows are explicit user data — deletion (unfollow/unhide) removes rows (no append-only history obligation for personalization state).
- **FR-008**: The personalization API surface MUST be a versioned, authenticated contract following platform conventions (mapped DTOs, stable error shapes, pagination, additive-only evolution): follows (list/add/remove for topics and sources), hidden topics (list/add/remove), and visits (record/list); list items expose only public-safe fields for topics/sources (consistent with the reader contract), never internals.
- **FR-009**: Frontend integration points MUST be defined and minimal: the topic detail page hosts follow/unfollow and hide/unhide controls reflecting server-confirmed state (loading/error/retry per the established conventions, controls disable during in-flight operations) and records exactly one visit per topic open; the trending page and other pages remain unchanged in the MVP (their established extension seams are the future integration path); component reuse from the existing frontend library is required.
- **FR-010**: Behavior MUST remain deterministic and explicit: no machine-learning recommendations, no behavioral ranking, no implicit tracking, no ordering effects beyond the documented rules; identical operations produce identical states; the only personalization signals are the user's explicit follows, hides, and visits.
- **FR-011**: Configuration MUST cover: maximum follows per user, recent-visits cap, visit-recording enablement (a disabled switch returns a documented response and collects nothing), and authentication integration settings; no secrets in code or logs; personalization identifiers are treated as sensitive in logging (identifiers only when necessary, never content).
- **FR-012**: Caching and performance rules MUST hold: user-specific personalization responses are never stored in shared caches (per the platform's caching policy); list endpoints are paginated and index-backed (user + timestamp), with bounded reads; visit recording is a cheap, idempotent write.
- **FR-013**: The feature MUST be testable offline and deterministically: fixtures cover follow/hide transitions and mutual exclusivity, caps and pruning boundaries, visit deduplication and ordering, isolation and non-disclosure, idempotency, integration-point behaviors (controls and visit-on-open), and additive API evolution.
- **FR-014**: Out of scope: machine-learning recommendations, behavioral ranking or feeds, notifications, social features, profile management, full authentication/user-management systems (the boundary is consumed as-is), and any changes to public reader or admin surfaces beyond the defined integration points.

### Key Entities

This feature introduces three dedicated relationship entities and an authenticated personalization surface; the User core entity is used as-is (minimal additive fields only if the plan requires, never credentials).

- **UserTopic (new relationship entity)**: one row per (user, topic) with state (followed|hidden), state-updated-at (UTC); unique (user, topic); transitions per the mutual-exclusivity rules; removed on unfollow.
- **UserSource (new relationship entity)**: one row per (user, source) with followed-at (UTC); unique (user, source); removed on unfollow.
- **UserTopicVisit (new relationship entity)**: one row per (user, topic) with last-visited-at (UTC) and visit count; unique (user, topic); pruned deterministically at the configured cap.
- **Personalization API surface (versioned, authenticated)**: follows/hidden/visits operations with mapped DTOs, stable errors, pagination, and additive evolution; list items expose public-safe topic/source fields only.
- **Authentication boundary (interface-defined)**: supplies the authenticated user identity; MVP credential mechanism from secure storage; replaceable by a future auth feature without semantic changes.
- **Frontend integration points**: topic detail controls (follow/hide, server-confirmed state) and visit-on-open recording, reusing the existing component library and UX conventions.
- **Fixtures & harnesses**: transition/idempotency/ordering/isolation fixtures, cap-pruning boundary cases, integration-point tests, and cross-user probe suites.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Follow/hide correctness: 100% of transition fixtures end in the defined single-row state with mutual exclusivity holding (no contradictory states ever exist); follow/unfollow and hide/unhide are idempotent in 100% of repeat operations.
- **SC-002**: Isolation and privacy: 100% of cross-user probes fail with non-disclosing semantics; unauthenticated requests are rejected without data in 100% of tests; audits find zero personalization data on public surfaces.
- **SC-003**: Recents behavior: deduplication (latest time + count), deterministic pruning at the exact cap boundary, and ordering are verified in 100% of visit fixtures; visit recording issues exactly one operation per topic open.
- **SC-004**: Determinism: repeated identical operation sequences produce identical states in 100% of fixtures; scope audits find zero ML, ranking, or implicit-tracking logic.
- **SC-005**: API conformance: golden contract tests pass for 100% of personalization endpoints (auth errors, validation errors, pagination, public-safe list fields); additive evolution verified with unknown-field tolerance.
- **SC-006**: Integration points: topic detail controls reflect server-confirmed state across reloads and render errors per conventions in 100% of integration fixtures; the trending page and other surfaces show zero unintended changes.
- **SC-007**: Data-model discipline: audits confirm zero personalization collections/state added to Topic/Source/other entities and zero writes to them by this feature (100% clean).

## Assumptions

- **Authentication dependency (flagged)**: no full authentication feature exists yet; the MVP uses the platform's established credential pattern extended per-user (credentials from secure storage, never on the User entity), behind an interface boundary that a future authentication feature replaces without changing personalization semantics.
- **Personalization data is user data, not domain history**: unfollow/unhide delete rows; visits are pruned at caps; the platform's append-only rules (observations, scores, summaries, timeline) do not apply here.
- **Hidden topics' effect scope**: hidden state excludes topics from the user's personalization surface (follow lists and future personalized consumption); public and admin surfaces are unaffected in the MVP.
- **Visit recording**: explicit API call from the topic detail page on open; no devices/IPs/behavioral signals stored; visit-recording can be disabled by configuration (documented response; nothing collected).
- **Caps**: maximum follows per user and recent-visits cap are configuration with conservative defaults in the plan; cap violations fail with actionable errors (default) — never silent eviction of follows.
- **List item semantics**: personalization lists expose public-safe topic/source fields (consistent with the reader contract); items whose underlying records are missing render per the deterministic omission rule with counts preserved.
- **Caching**: user-specific responses bypass shared caches per the caching policy; no personalization data enters cache keys intended for shared use.
- **Frontend scope**: topic detail integration (controls + visit recording) is part of this feature; other pages rely on their existing extension seams as future integration paths; components reuse the established library and UX conventions.
- **Privacy follow-ups**: data export/deletion and consent flows are future privacy features; flagged, not implemented.
- **Out of scope**: ML recommendations, behavioral ranking, personalized feeds, notifications, social features, profile management, full auth systems, and changes to other surfaces beyond the defined integration points.

**Dependencies**: Feature 001 (core model — User as the single user model; three new relationship-entity mappings designed in the plan; Topic/Source untouched); Feature 014 (contract conventions — versioned DTOs, error shapes, pagination, additive evolution for the authenticated personalization surface; public-safe field reuse); Feature 016 (caching policy — user-specific data never shared-cached); Feature 017/018 (frontend stack, component library, UX conventions; the topic detail page hosts the integration points); Feature 023 (logging redaction — personalization identifiers treated sensitively); Project Constitution v1.0.0 (Principle III — explicit MVP, no ML/ranking scope creep; Principle IV — configuration-driven caps and credentials from secure storage, never in code or logs); deterministic offline fixtures for testing.
