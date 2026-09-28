# Feature Specification: Trending Page (React Web App)

**Feature Branch**: `017-trending-page`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Build the main React TypeScript trending page using the stable API contract. Display trending topics grouped into: Last 5 Hours, Today, Yesterday. Each topic should display useful information such as topic name, trend category/score where available, freshness, and relevant article count. The frontend must not depend on internal database entities or provider-specific fields. Support loading, empty, error, retry, and refresh states. Use responsive design suitable for desktop and mobile. Keep topic cards/components reusable for future pages such as search, related topics, and personalized feeds."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View trending topics grouped by the three windows (Priority: P1)

The trending page loads the three window groups — Last 5 Hours, Today, Yesterday — from the versioned public API and renders each group as a section of topic cards. Each card shows the topic name and, where available from the contract, the descriptive trend category label, freshness, and the relevant article count; topics link into the topic details experience provided by the platform.

**Why this priority**: This is the platform's primary reader-facing screen; everything else in this feature exists to support it.

**Independent Test**: With API fixtures for all groups (including optional fields present and absent), render the page and verify group order/labels, card field display rules, linking behavior, and correct count/freshness formatting.

**Acceptance Scenarios**:

1. **Given** the API responds with the three groups, **When** the page renders, **Then** groups appear in the fixed order last-5-hours, today, yesterday with their API-provided labels and each contains its topic cards.
2. **Given** a topic item with partial optional fields (for example, no trend label or no article count), **When** its card renders, **Then** the absent fields are omitted gracefully (never fabricated or shown as placeholder values).
3. **Given** a group with more topics than the initial page, **When** the user requests more, **Then** additional cards append deterministically per the API's ordering, without duplicates or skips.

---

### User Story 2 - Loading, empty, error, retry, and refresh states (Priority: P1)

Every transient condition has a designed state: skeleton loading (no layout shift), per-group and whole-page empty states, request errors with an explicit retry, and refresh that revalidates against the API while keeping the current content visible — if the refresh fails, the last good content stays with a non-blocking notice.

**Why this priority**: The user explicitly requires these states; unreliable networks and asynchronous data refresh make them first-class requirements.

**Independent Test**: State fixtures (delayed response, empty groups, error responses, refresh failure after a good load) verify each state's rendering, retry behavior, and content preservation rules.

**Acceptance Scenarios**:

1. **Given** the first load is in flight, **When** the page renders, **Then** skeleton placeholders preserve the layout of the three groups.
2. **Given** a request error on first load, **When** the error state renders, **Then** an explicit retry control refetches and resolves the state on success.
3. **Given** a successful load, **When** the user refreshes and the request fails, **Then** the existing content remains visible with a non-blocking error notice (no blanking).
4. **Given** all groups are empty, **When** the page renders, **Then** a clear whole-page empty state appears; a single empty group shows its own inline empty state.

---

### User Story 3 - Responsive, accessible presentation (Priority: P2)

Layouts adapt to device size — a multi-column desktop presentation and a stacked, single-column mobile presentation — with touch-friendly targets, no horizontal scrolling at supported widths, semantic structure (headings, lists), keyboard navigability for cards and controls, visible focus, and accessible status announcements for loading/error/refresh states.

**Why this priority**: The user explicitly requires desktop and mobile suitability; accessibility and responsiveness are baseline quality for a reader-facing page.

**Independent Test**: Viewport fixtures across mobile/tablet/desktop widths plus keyboard-only and screen-reader-oriented checks (semantic/ARIA) verify layout, interaction, and announcements.

**Acceptance Scenarios**:

1. **Given** mobile widths, **When** the page renders, **Then** groups stack vertically in one column with touch-friendly controls and no horizontal scroll.
2. **Given** desktop widths, **When** the page renders, **Then** groups use the multi-column layout with readable line lengths.
3. **Given** keyboard-only use, **When** navigating groups, cards, retry, refresh, and load-more, **Then** all are reachable and operable with visible focus and correct semantics.

---

### User Story 4 - Reusable, contract-bound components for future pages (Priority: P3)

Topic cards, group sections, pagination/load-more, and status components are presentational, prop-driven, and typed strictly against the public API contract — reusable without modification by future pages such as search results, related topics, and personalized feeds. The page layer owns data fetching and state; components stay pure and decoupled from fetching and from internal entities.

**Why this priority**: The user explicitly requires reuse; constructing the component system correctly now avoids retrofits when later pages arrive.

**Independent Test**: Render the same card and group components in a second context (a search-style result list fixture) with no component changes; audit component sources for absence of fetching logic and entity/provider references.

**Acceptance Scenarios**:

1. **Given** the topic card and group components, **When** used in a different page context with compatible contract data, **Then** they render correctly without modification.
2. **Given** component sources, **When** audited, **Then** zero data-fetching calls and zero references to internal entities or provider fields exist (fetching lives in page-level hooks).
3. **Given** absent optional contract fields in any reuse context, **When** components render, **Then** degradation rules apply identically (consistent behavior across pages).

---

### Edge Cases

- Optional contract fields absent (no trend label, no article count, no freshness timestamp) → fields omitted gracefully per field; never placeholder or fabricated values; cards remain fully operable.
- API contract adds new optional fields (additive evolution) → the page tolerates and ignores unknown fields; no runtime errors from extra data.
- Window labels differ from the canonical English labels (localized API labels) → the page uses API-provided labels verbatim (no hardcoded user-visible group names).
- A window boundary passes while the page is open (for example, 5-hour rollover) → data refreshes on the user's next refresh; the page notes freshness so users understand the snapshot.
- Extremely long topic names or large counts → text truncation/wrapping rules keep layout intact; counts formatted with locale-aware grouping.
- Very large groups with many pages → load-more appends progressively; ordering matches the API's deterministic order (no client-side re-sorting).
- Slow responses → skeletons persist; no spinner dead-ends; retry available on failure after the request settles.
- Offline/network loss mid-refresh → last good content preserved with the non-blocking notice (stale-while-error behavior from the states story).
- API returns a new version or unknown enum value for the trend label → unknown values render safely (for example, displayed as-is or mapped to a neutral style) without breaking rendering.
- Duplicate topic entries across windows (same topic trending in multiple windows) → each group renders independently as provided by the API; no client-side deduplication is applied.
- Deep links into topics → cards link into the topic details contract; navigation errors (removed topic) surface the standard not-found handling of the target page (out of scope here, but links must be well-formed).
- Very small screens (< minimum supported width) → layout remains usable via wrapping and stacking (documented minimum supported width).
- Reduced-motion preferences → animations (skeletons/transitions) respect the user's motion settings.
- High-contrast/forced-colors modes → structure and controls remain visible and operable (no information conveyed by color alone).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The page MUST be built with React and TypeScript and consume ONLY the versioned public API contract for windowed trending topics; it MUST NOT reference internal database entities, provider-specific fields, or non-public fields — types MUST be bound to the documented contract (generation from the contract artifact per the plan is preferred).
- **FR-002**: The page MUST render the three window groups in the fixed order (last 5 hours, today, yesterday) using the API-provided labels, boundaries, and reference timezone; group names MUST NOT be hardcoded in user-visible output.
- **FR-003**: Topic cards MUST display the topic name plus, when available from the contract, the descriptive trend label, freshness (formatted from the contract's freshness value), and the relevant article count; absent optional fields MUST be omitted gracefully, and no value may ever be fabricated client-side.
- **FR-004**: The page requires specific additional optional contract fields (descriptive trend label, freshness timestamp, relevant article count); these MUST be introduced as additive optional fields under the API's evolution rules (reconciled with the API feature in the plan), and the UI MUST degrade gracefully when any of them is absent (feature can ship progressively as fields land).
- **FR-005**: Per-group pagination MUST follow the API's rules: a bounded initial page, explicit load-more behavior, deterministic order as served (no client re-sorting), append-only rendering with loading indicators, and no duplicate or skipped items across pages.
- **FR-006**: A designed loading state MUST use skeletons that preserve the three-group layout without layout shift; concurrent group loads MUST be handled as one page-level load state.
- **FR-007**: Empty states MUST be distinguished: per-group inline empty messaging and a whole-page empty state when all groups are empty.
- **FR-008**: Error handling MUST provide an explicit retry on failed loads and MUST surface the API's standard error semantics (stable error shape) with user-friendly messaging; retries re-issue the request respecting HTTP caching semantics.
- **FR-009**: Refresh MUST revalidate against the API (conditional requests leveraging the API's HTTP caching) while keeping current content visible; on refresh failure the last good content MUST remain with a non-blocking notice; on success changed content replaces it and unchanged content (304) causes no visual disruption.
- **FR-010**: Responsive design MUST define supported breakpoints: single-column stacked groups on small screens, multi-column presentation on large screens; no horizontal scrolling at supported widths; touch-friendly control sizes on touch devices.
- **FR-011**: Accessibility MUST meet WCAG AA-level expectations: semantic structure (headings, lists, landmark regions), keyboard operability with visible focus for all interactive elements (cards, retry, refresh, load-more), screen-reader announcements for loading/error/refresh status changes, sufficient contrast, respect for reduced-motion preferences, and no information conveyed by color alone.
- **FR-012**: Components (topic card, group section, load-more/pagination, status components) MUST be presentational and prop-driven, typed strictly against the public contract shapes, free of data-fetching logic, and reusable across future pages (search results, related topics, personalized feeds) without modification.
- **FR-013**: Data fetching and state management MUST live in page-level layers (a typed API client plus hooks/state); client-side caching MAY be minimal for the MVP (in-memory per-page state) while respecting the platform's HTTP caching; no client-side business logic that duplicates API semantics (no client-side window calculation, filtering, or re-sorting).
- **FR-014**: Performance and behavior MUST be smooth: first meaningful content as early as practical with skeleton-first rendering, bounded DOM growth per load-more page, no layout shift on content arrival, and graceful behavior on slow networks (no spinner-only dead ends).
- **FR-015**: User-visible strings outside the API-provided labels MUST be centralized in a strings module (not scattered literals) to support future localization; formatting (times, counts) MUST use locale-aware formatting utilities; Vietnamese text (diacritics) MUST render correctly.
- **FR-016**: State and failure observability MUST exist for the frontend: non-fatal errors logged for diagnostics without leaking internals to users; no secrets or internal identifiers exposed in client code.
- **FR-017**: Out of scope: topic details page, search page, personalization, authentication/user accounts, write operations, real-time push/WebSockets, server-side rendering decisions, and any new backend endpoints beyond the additive optional fields in FR-004.

### Key Entities

This feature introduces frontend artifacts bound to the public API contract; no backend entities or persistence exist in scope.

- **Trending page (container)**: owns fetching, state orchestration (loading/empty/error/retry/refresh), and composition of sections; the only layer with data-fetching responsibilities.
- **Typed API client layer**: contract-bound client for the windowed-topics endpoint (and its pagination parameters); respects HTTP caching semantics (validators, 304); the single gateway to backend data for this page.
- **Topic card component (reusable)**: presentational, prop-driven rendering of one topic item — name, optional trend label, optional freshness, optional article count, link target — with graceful omission rules.
- **Topic group section component (reusable)**: renders a window group's label, boundary/freshness context, and its cards, plus the group's loading/empty/error sub-states.
- **Status components (reusable)**: skeleton, empty, error-with-retry, refresh notice, and load-more controls — shared across the page and future pages.
- **Strings & formatting module**: centralized user-visible strings and locale-aware formatting (relative freshness, number grouping, window labels passthrough).
- **Fixture & test suite**: API response fixtures per state and field combination, viewport fixtures, keyboard/a11y checks, and a reuse-context fixture (search-style list) proving component reusability.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With API fixtures covering all field combinations, the page renders the three groups in the fixed order with correct card rendering rules; absent optional fields are omitted in 100% of cases (zero fabricated values).
- **SC-002**: State coverage: skeleton loading (layout-stable), per-group and whole-page empty, error with working retry, refresh keep-stale-on-error, and 304 no-disruption are verified in 100% of state fixture scenarios.
- **SC-003**: Pagination: load-more appends in API order with zero duplicates and zero skips across multi-page fixtures (100%), with loading indicators during appends.
- **SC-004**: Responsive: at all defined breakpoints, no horizontal scrolling occurs and layouts match the specified stacking/column rules in 100% of viewport tests; touch targets meet the minimum size on touch viewports.
- **SC-005**: Accessibility: keyboard-only operation of all interactive elements passes a 100% check, automated semantic/ARIA audits pass for all states, contrast meets AA targets, and reduced-motion preferences are respected in 100% of tested scenarios.
- **SC-006**: Contract discipline: an audit of frontend sources finds zero internal entity/provider references and zero client-side window arithmetic or re-sorting (100% clean); unknown additive fields never cause runtime failures in fixture replays.
- **SC-007**: Reusability: card and group components render correctly in a second (search-style) context with no component modifications in 100% of reuse fixtures; component sources contain zero fetching logic.

## Assumptions

- **Contract additions (additive)**: the page needs three optional fields beyond the current draft contract — descriptive trend label (with optional value), freshness timestamp, and relevant article count; these are additive optional fields under feature 014's evolution rules and are reconciled with the API feature during the plan (the UI ships progressively, degrading gracefully when fields are absent).
- **Rendering approach and tooling**: a client-side React + TypeScript application; the build tooling, styling approach, and component-test runner are decided in the plan (no SSR requirement in the MVP; server-side caching from feature 016 benefits the API responses regardless).
- **HTTP caching interplay**: the browser's standard HTTP caching (ETag/304 per feature 014) is respected; manual refresh performs conditional revalidation; no client-side persistence (localStorage/service workers) in the MVP.
- **Freshness display**: relative-time formatting from the contract's timestamp; no client time-window math beyond formatting (constitution Principle V — windows come from the API).
- **Locale**: UI copy is centralized (future localization); Vietnamese diacritics render correctly with system fonts; number/time formatting uses locale-aware utilities.
- **Reuse targets**: future search results, related-topic lists, and personalized feeds reuse the card/group/status components with compatible contract data; those pages are separate features.
- **Scope boundaries**: no topic details, search, personalization, auth, writes, or real-time features; no new backend endpoints beyond the additive optional fields; deep links target pages provided by other features.
- **Quality standards**: WCAG AA expectations and the defined breakpoints are the accessibility/responsiveness bar for the MVP; device/browser support matrix is fixed in the plan.

**Dependencies**: Feature 014 (versioned public contract, windowed endpoint, window labels/boundaries, HTTP caching semantics — plus the additive optional fields delta identified here); Feature 016 (server-side caching and validators improve perceived performance; nothing frontend-specific required); Feature 013 (data freshness is asynchronous — the page reflects it via refresh semantics); Project Constitution v1.0.0 (Principle II — linking into attributed topic details; Principle V — no client-side window arithmetic, labels/boundaries from the API); API response fixtures, viewport fixtures, and a reuse-context fixture for testing.
