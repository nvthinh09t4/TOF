# Feature Specification: Admin Dashboard (Operations Console)

**Feature Branch**: `022-admin-dashboard`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Build an administrative dashboard for operating the trending-news platform. The dashboard should provide operational visibility and management for: RSS/news sources; trending topics; articles; AI summaries; background processing jobs; failed processing operations. Administrators should be able to disable problematic sources and safely trigger supported reprocessing operations. Use existing APIs and application services rather than accessing database entities directly from the frontend. Keep the dashboard focused on system operation and debugging. Do not implement analytics or content moderation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Source operations: visibility and problem response (Priority: P1)

Administrators see every configured source with its operational state (active status, validation status, last success, last failure with category, crawl interval) and act on them: re-validate a feed, disable a problematic source immediately, and re-enable after a successful validation — all through the existing source administration API, never directly against the database.

**Why this priority**: The most common operational incident is a bad feed; surfacing and disabling it safely is the dashboard's first job.

**Independent Test**: With mocked source-administration responses, verify the list rendering (including problem indicators), validate/disable/enable action flows with confirmations, and post-action state refresh.

**Acceptance Scenarios**:

1. **Given** configured sources with mixed states, **When** the sources view loads, **Then** each row shows configuration plus operational state, with failing/invalid sources clearly flagged.
2. **Given** a problematic source, **When** the administrator disables it, **Then** the action is confirmed first and the change takes effect immediately in the refreshed view (attribution elsewhere unaffected).
3. **Given** a disabled source with a fresh successful validation, **When** the administrator re-enables it, **Then** the state updates accordingly; failing validation keeps it disabled with the categorized reason shown.

---

### User Story 2 - Processing jobs and failed operations visibility (Priority: P1)

Administrators browse background processing runs — filtered by type, outcome, and time range — and inspect individual runs down to their attempts, outcome categories, counts, diagnostics, and correlation identifier. A dedicated failures view highlights failed, interrupted, partial, and skipped runs with their categorized reasons so incidents are findable in seconds.

**Why this priority**: Operability demands answering "what ran, what failed, and why" without touching the server or database.

**Independent Test**: With mocked job-run fixtures (success, partial, failed, interrupted, skipped across types), verify list filtering, detail rendering (attempts/diagnostics/correlation), and the failures view's completeness.

**Acceptance Scenarios**:

1. **Given** job runs across types and outcomes, **When** the jobs view loads, **Then** runs list paginated and filterable by type/outcome/time with correct status presentation.
2. **Given** any run, **When** its detail opens, **Then** attempts, outcomes, counts, diagnostics reference, and correlation id are shown — nothing internal beyond the admin-safe contract.
3. **Given** failed or abnormal runs, **When** the failures view is opened, **Then** each shows its categorized reason and links to its run detail for investigation.

---

### User Story 3 - Safe reprocessing triggers (Priority: P1)

Administrators can trigger supported reprocessing operations — manual runs of registered job types — from the dashboard: each trigger is confirmed first, sent through the admin API, and honored by the background processing semantics (concurrent/overlapping triggers queue or are rejected per configuration, the manual trigger source is recorded, and the outcome is observable by watching the resulting run). No reprocessing operation bypasses the platform's idempotent, convergent passes.

**Why this priority**: Explicitly required (safe triggering); recovery from operational incidents depends on it.

**Independent Test**: With mocked trigger responses (accepted/queued/rejected), verify confirmation flow, feedback accuracy, manual trigger attribution, and that no unsafe or unsupported operation is offerable.

**Acceptance Scenarios**:

1. **Given** a supported job type, **When** the administrator triggers it, **Then** a confirmation precedes submission and the returned outcome (accepted/queued/rejected with reason) is surfaced accurately.
2. **Given** a trigger already running for a type, **When** another is attempted, **Then** the dashboard reflects the server's concurrency resolution (queued or rejected per configuration) rather than duplicating work.
3. **Given** a submitted trigger, **When** the resulting run completes, **Then** its record shows the manual trigger source and the dashboard can navigate to it.

---

### User Story 4 - Content visibility: topics, articles, AI summaries (Priority: P2)

Administrators inspect topics (operational view: score/version context, cluster membership), articles (source linkage, dedup/merge state), and AI summaries (provider/model, prompt version, generation time, validation outcome, freshness/reuse state) through admin API reads — enough internal context to debug pipeline behavior, without editing any content.

**Why this priority**: Debugging the pipeline (why a summary is missing, why an article is merged) requires this operational context.

**Independent Test**: With mocked fixtures per entity view (including merged articles, summary validation failures, stale summaries), verify rendering and read-only behavior.

**Acceptance Scenarios**:

1. **Given** topics with operational context, **When** the topics view loads, **Then** score/category context and cluster membership are visible and read-only.
2. **Given** articles including merged/retired duplicates, **When** inspected, **Then** their source linkage and dedup/merge state are visible (no editing actions exist).
3. **Given** summaries in varied states (validated, failed-validation context, stale/superseded), **When** inspected, **Then** provider/model, prompt version, generation time, validation outcome, and freshness state are shown — read-only.

---

### User Story 5 - Admin-only boundary, API-only access, and focused scope (Priority: P3)

The dashboard lives behind the administrative authorization boundary, is reachable in the app's admin area only (never in public navigation), and obtains every piece of data and action through versioned API contracts (public and admin) — zero direct database or service-internal access from the frontend. Scope stays operational: no analytics (charts, usage reports) and no content moderation (no editing or approving content).

**Why this priority**: Explicit user constraints (API-only access; operation and debugging focus; no analytics/moderation).

**Independent Test**: Authorization fixtures (unauthorized/forbidden handling), code audits (no direct data access, no credential logging), route audits (admin area separation), and a scope audit for absent analytics/moderation features.

**Acceptance Scenarios**:

1. **Given** no or invalid admin credentials, **When** the dashboard is accessed, **Then** an authorization state renders and no data loads; credentials never appear in logs or URLs.
2. **Given** the frontend codebase, **When** audited, **Then** zero direct database/service access and zero non-API data paths exist.
3. **Given** the feature scope, **When** audited, **Then** no analytics or moderation capabilities exist anywhere in the dashboard.

---

### Edge Cases

- Admin credential absent/expired → authorization state with guidance; existing views do not leak previously loaded sensitive data; no credential in URLs or logs.
- Trigger rejected by concurrency rules → rejection reason surfaced verbatim from the server; no silent retries from the client.
- Trigger accepted but the run later fails → the failure is visible through the normal jobs/failures views (dashboard never re-implements run semantics).
- Source disabled while its run is in flight → in-flight semantics per the platform's documented behavior; the dashboard shows the source's new state and the run's own record independently.
- Long-running views (job lists) with auto-refresh → refresh is gentle and configurable (manual refresh always available); no aggressive polling.
- Summary missing for a topic → explanatory state pointing at operational context (relevance/validation/job history), never fabrication.
- Empty datasets (fresh installation: no sources/jobs/summaries) → designed empty states per view with guidance, not errors.
- Partial API failures (one widget fails, others succeed) → each view degrades independently with per-view retry (no whole-dashboard blanking).
- Unknown additive fields in admin responses → tolerated without breakage (evolution rules).
- Very large run counts → pagination and filters bound everything; no unbounded fetches.
- Destructive-looking actions → only platform-supported operations exist (validate/enable/disable/trigger); retire flows remain in the source administration surface with its own safety rules; every action is confirmed and result-surfaced.
- Clock/timezone displays → all timestamps rendered from UTC payloads via the shared formatting utilities (no client date math beyond formatting).
- Non-admin authenticated access (future auth tiers) → authorization state per the boundary's rules; no partial data exposure.
- Copying a run's correlation id for server-log investigation → supported affordance; nothing else internal is displayed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The dashboard MUST be a React + TypeScript area within the existing frontend application, obtaining ALL data and actions through versioned API contracts (the source administration surface and the new admin reads/trigger described below) — zero direct database access, zero direct application-service invocation, and zero provider coupling from the frontend.
- **FR-002**: A sources operations view MUST list configured sources with operational state (active/validation status, last success, last failure with category, interval) and provide the supported actions: validate, disable, and (with fresh successful validation) enable — via the existing source administration API; failures and warnings MUST be visually prominent.
- **FR-003**: A jobs view MUST list background processing runs (paginated, filterable by type, outcome, and time range) and a run-detail view MUST show attempts, outcomes, counts, diagnostics reference, and correlation identifier; a failures view MUST aggregate failed, interrupted, partial, and skipped runs with categorized reasons.
- **FR-004**: Reprocessing MUST be limited to manually triggering registered job types through an admin API that honors the background processing semantics (concurrency/overlap resolution, manual trigger source recorded, queued/rejected outcomes reported); the dashboard MUST confirm before triggering, surface the server's outcome verbatim, and make the resulting run observable — it MUST NOT implement run semantics of its own or offer unsupported operations.
- **FR-005**: Content visibility MUST cover: topics (score/version context, cluster membership), articles (source linkage, dedup/merge state), and AI summaries (provider/model, prompt version, generation time, validation outcome, freshness/reuse state) through admin reads; all content views MUST be read-only (no editing, approving, or deleting content anywhere).
- **FR-006**: Actions MUST be safe and deterministic in the UI: confirmations before enable/disable/trigger; post-action state refresh; results (success/rejection reasons) surfaced verbatim from the API; no client-side retries that could duplicate triggers; only platform-supported operations offered.
- **FR-007**: The dashboard MUST sit behind the administrative authorization boundary (the platform's admin credential model): unauthorized access renders an authorization state with no data; credentials are never logged or placed in URLs; previously loaded data is cleared on authorization loss.
- **FR-008**: Admin API additions required by this feature (jobs list/detail + trigger endpoint; content operational reads for topics/articles/summaries) MUST follow the established admin-surface conventions: separate versioned contract, DTOs mapped from application services, consistent error shapes, pagination, no entity/provider/internal leakage beyond admin-safe fields (AI summary internals are admin-safe; reader APIs remain untouched).
- **FR-009**: The dashboard MUST distinguish itself from the public reader experience: admin area routing only, never linked from public navigation; public reader APIs and pages MUST NOT gain admin capabilities or exposure through this feature.
- **FR-010**: All views MUST implement the established UX conventions: skeleton-first loading, designed empty states, per-view error states with retry, manual refresh (optional gentle configurable auto-refresh without aggressive polling), WCAG AA accessibility, and responsive layout per the frontend's component library.
- **FR-011**: Reuse MUST hold: status/skeleton/empty/error components, the strings/formatting module, and the typed API client from the existing frontend library; new admin-specific components (data table, run-detail panel, action-confirm dialog) MUST be presentational, prop-driven, and documented for reuse; fetching lives only in page-level layers.
- **FR-012**: Operational actions and their outcomes MUST be logged with actor identity per the platform's admin logging pattern; client-side error logging MUST NOT leak credentials or internal data.
- **FR-013**: Performance MUST be bounded: paginated fetches everywhere, no unbounded queries, no eager loading of expensive detail data (load on demand); the dashboard MUST respect API caching semantics (admin responses may be non-cacheable or short-lived — per server policy).
- **FR-014**: Out of scope: analytics (charts, usage/growth reports, metrics dashboards), content moderation (editing, approving, or removing content), user management/authentication systems (the admin credential boundary is consumed as-is), per-entity scoped reprocessing beyond registered job types (no such services exist), and any changes to reader-facing features.
- **FR-015**: The dashboard MUST be testable offline and deterministically: API-mocked fixtures cover every view and state (loading/empty/error/partial), action flows (confirm/success/rejection/queued), authorization states, additive-field tolerance, and pagination; audits cover API-only access, admin-area separation, and scope exclusions.
- **FR-016**: Failure investigation MUST be first-class: any failed or abnormal run is reachable within two interactions from the dashboard home (failures view), and its detail exposes the categorized cause, counts, and correlation id sufficient for server-log correlation.

### Key Entities

This feature introduces a frontend operations console plus additive admin API reads/actions; no backend domain entities or persistence are added.

- **Admin dashboard area (frontend)**: routes, shell, and views (home/failures, sources, jobs + run detail, topics, articles, summaries) behind the administrative authorization boundary; API-only data access.
- **Admin API additions (versioned contract; delta to reconcile with the admin surface conventions)**: jobs list/detail reads and the manual trigger action (honoring background processing semantics); operational reads for topics, articles, and AI summaries with admin-safe internal fields; implemented on application services — never raw database access.
- **Reused frontend library**: status/skeleton/empty/error components, strings/formatting module, and the typed API client from the existing frontend features.
- **New reusable admin components**: data table (paginated, filterable), run-detail panel (attempts/outcomes/diagnostics/correlation), action-confirm dialog, problem-indicator badges — presentational and prop-driven.
- **Admin credential handling (client)**: per the deployment's admin boundary model; in-memory/session handling per plan; never logged, never in URLs.
- **Fixture & mock suite**: API-mocked responses for every view/state/action, authorization scenarios, and audit harnesses (API-only access, admin-area separation, scope exclusions).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: View coverage: with API-mocked fixtures, 100% of views (sources, jobs, failures, topics, articles, summaries, home) render their loading, empty, populated, and error states correctly, including per-view independent degradation.
- **SC-002**: Action safety: 100% of enable/disable/validate/trigger flows require confirmation, surface the server's outcome verbatim (including queued/rejected reasons), and never duplicate triggers client-side.
- **SC-003**: Reprocessing fidelity: manual triggers appear with trigger source recorded in 100% of fixture flows, and the dashboard offers only supported job types (no unsupported operations exist).
- **SC-004**: Failure investigation: 100% of failed/abnormal runs are reachable from the home/failures view within two interactions and expose category, counts, and correlation id.
- **SC-005**: Boundary guarantees: unauthorized access yields an authorization state with zero data in 100% of tests; audits find zero credential leaks (logs/URLs) and zero direct database/service access from the frontend.
- **SC-006**: UX conformance: responsive and accessible behavior meets the established WCAG AA bar across all views and states in 100% of audited scenarios.
- **SC-007**: Scope and reuse: audits find zero analytics or moderation capabilities and zero public-navigation exposure; shared components render correctly in both admin and existing frontend contexts (100% of reuse fixtures).

## Assumptions

- **Admin surface dependency**: the source administration contract (feature 021) provides source operations; new admin endpoints for jobs (list/detail/trigger) and content operational reads (topics/articles/summaries) follow its conventions — a flagged delta reconciled in the plan, implemented on application services and versioned like the rest of the platform's API surfaces.
- **Reprocessing scope**: "supported reprocessing operations" means manually triggering registered job types per the background processing semantics (features 013); per-entity scoped reprocessing is out of scope because no such services exist.
- **Admin-safe internals**: AI summary internals (provider/model, prompt version, validation outcome, freshness) and pipeline context (score versions, dedup/merge state) are admin-safe and exposed only on the admin surface; reader-facing contracts remain untouched.
- **Credential model**: the MVP consumes the platform's pre-shared admin credential boundary; session/storage handling is deployment-appropriate (no credentials in URLs or logs; cleared on authorization loss); a future authentication feature may replace the mechanism without dashboard redesign.
- **Refresh strategy**: manual refresh is primary; optional light auto-refresh is configurable and gentle (no aggressive polling); admin responses follow server caching policy (typically short/no-cache).
- **Frontend consistency**: built inside the existing React/TypeScript app with the established component library, conventions (skeleton-first, per-view retry, stale-while-error), accessibility bar, and strings/formatting module from the reader features.
- **No analytics/no moderation**: strictly absent — system operation and debugging only; no charts, reports, content editing, or approval workflows.
- **Public surface**: unchanged; the dashboard is a separate admin area with no public navigation exposure.

**Dependencies**: Feature 021 (source administration — sources view and actions; admin surface conventions and credential boundary); Feature 013 (background processing — run records semantics, manual trigger rules, failure categories; admin jobs endpoints delta); Features 003-012 (topics, articles, summaries and their operational metadata as read-only admin views); Feature 014 (public contract conventions — untouched; admin surface follows the same discipline separately); Features 017/018 (frontend stack, component library, UX conventions to reuse); Project Constitution v1.0.0 (Principles III — no analytics/moderation scope creep, IV — credentials via environment/secure store, never in logs); API-mocked fixtures and audit harnesses for testing.
