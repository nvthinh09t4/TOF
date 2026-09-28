# Feature Specification: Observability (Logs, Metrics, Health)

**Feature Branch**: `023-observability`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement observability for the trending-news platform. Provide structured logging, correlation IDs, health checks, processing metrics, background job status, RSS crawl failures, trend provider failures, AI provider failures, API errors, and important performance measurements. Logs should contain enough contextual information to diagnose failures across ingestion and processing pipelines. Do not expose secrets, API keys, full article content, or unnecessary sensitive data in logs. Integrate observability with the modular monolith architecture without coupling domain entities to a specific logging or monitoring vendor."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Structured logs with correlation across every pipeline (Priority: P1)

Every operation — an API request, a background run, a provider call, a source crawl — emits structured, key-value events carrying a single correlation identifier that follows the operation through API, application services, provider abstraction, and background passes. A failure can be reconstructed from logs alone: what ran, for which entity, what failed, with which category, how long it took, and what was attempted before.

**Why this priority**: Diagnosability is the feature's purpose; correlation is what turns scattered log lines into an incident story.

**Independent Test**: Fixture scenarios across pipelines (API error, crawl failure, AI failure, job failure); verify every emitted event in the operation shares one correlation id, required fields are present, and each failure path is reconstructable from its log sequence.

**Acceptance Scenarios**:

1. **Given** an API request that triggers downstream processing, **When** events are emitted, **Then** request, service, and provider events carry the same correlation id and the request's outcome is traceable end to end.
2. **Given** a background run, **When** it logs, **Then** events carry the run's correlation id (consistent with the run record) and per-target outcomes with categories.
3. **Given** any pipeline event, **When** inspected, **Then** it contains the required structured fields (timestamp in UTC, level, event name, correlation id, operation context, category where applicable, duration/counts where applicable) — never free-text-only entries on pipeline paths.

---

### User Story 2 - Health checks with defined semantics (Priority: P1)

Operational health is answerable in two standard questions: is the process alive (liveness) and is it ready to serve (readiness)? Readiness reflects what the process genuinely needs — database reachability, required configuration presence, and a running background scheduler — while deliberately NOT depending on external providers (their outages degrade features, not readiness). Health outputs use a stable shape and never contain secrets.

**Why this priority**: Deployments and operations depend on correct, honest health semantics; wrong readiness causes cascading failures.

**Independent Test**: Fixture states (database down, missing critical configuration, scheduler stopped, providers down); verify liveness/readiness outcomes, the documented provider independents, stable output shape, and secret-free responses.

**Acceptance Scenarios**:

1. **Given** a healthy process, **When** readiness and liveness are queried, **Then** both report healthy in the standard shape.
2. **Given** the database is unreachable or critical configuration is missing, **When** readiness is queried, **Then** it reports not-ready with the affected subsystem identified and no secrets in the output.
3. **Given** external providers are failing, **When** health is queried, **Then** readiness remains healthy (provider degradation is visible via metrics/logs, not readiness flapping).

---

### User Story 3 - Processing metrics and performance measurements (Priority: P1)

A documented metric catalog covers the platform's operational pulse: background runs per type/outcome with durations and queue depth; provider calls (trend, news, AI) with attempts, durations, and categorized outcomes; pipeline counters (articles accepted/deduplicated/pruned, links matched, relevance shifts, cluster changes, scores computed, summaries generated/reused/validation-failed, timeline entries, related pairs); crawl outcomes per source; API requests by route/status/duration/error category; cache hit/miss/stale and query counts; and the key latency measurements the platform already promises. Names, units, and labels are vendor-neutral and documented.

**Why this priority**: The user explicitly requires processing metrics and performance measurements; metrics are how trends and regressions become visible before users notice.

**Independent Test**: Fixture operations across pipelines; verify every cataloged metric is emitted with documented names/units/bounded labels, and that label cardinality stays bounded (no entity ids as labels).

**Acceptance Scenarios**:

1. **Given** a background run, **When** it completes, **Then** run outcome/duration metrics per type are emitted and mirror the run record's counts.
2. **Given** provider interactions, **When** they finish, **Then** per-kind attempt/duration/outcome metrics are emitted with categories but no payload data.
3. **Given** API traffic and cache activity, **When** operations occur, **Then** request and cache metrics appear with documented names and bounded label sets (audited).

---

### User Story 4 - Failure visibility across all integration points (Priority: P2)

Every failure class has a complete observable signature: RSS crawl failures (per source, categorized, with redacted context), trend provider failures, AI provider failures, and API errors (status, route, category, correlation — response bodies unchanged so no internals leak to clients while logs carry full diagnostic context).

**Why this priority**: The user lists these failure classes explicitly; consistent signatures make incidents findable and comparable.

**Independent Test**: Failure fixtures per class; verify each produces categorized events and metrics with identifiers and correlation, and that client-facing responses leak nothing while logs contain the diagnostic context.

**Acceptance Scenarios**:

1. **Given** a failing feed crawl, **When** it ends, **Then** the failure is logged and counted with the source identity, failure category, and correlation — none of which appear in reader-facing output.
2. **Given** an API request that fails, **When** it is handled, **Then** the response uses the platform's stable error shape while the log/metric carries route, status, category, and correlation.
3. **Given** trend or AI provider failures, **When** they occur, **Then** events carry provider kind/identity, category, attempts, and duration — never payloads or credentials.

---

### User Story 5 - Vendor-neutral integration without domain coupling (Priority: P3)

Observability integrates through application-owned abstractions: logging, metrics, and health are consumed via platform interfaces; the domain layer references none of them; the logging/metrics backend and export mechanism are selected by configuration; and replacing the vendor (or adding an exporter) requires no changes to domain or feature logic.

**Why this priority**: Explicitly required (no vendor coupling); it also keeps the domain clean per the platform's architectural discipline.

**Independent Test**: Swap observability exporters/backends via configuration with stub implementations; audit the domain for zero logging/monitoring references; verify behavior is identical through the abstractions.

**Acceptance Scenarios**:

1. **Given** two export configurations (for example, console and a stub collector), **When** one is selected, **Then** events/metrics flow identically through the abstractions with zero code changes.
2. **Given** the domain assemblies, **When** audited, **Then** zero references to logging/monitoring types exist.
3. **Given** a future vendor addition, **When** configured, **Then** it integrates through the abstraction without touching feature code.

---

### Edge Cases

- Secrets or keys in any log line or metric label → forbidden by the redaction policy; automated scans fail the build on detection; provider configs log kind/name only, never credentials.
- Article content in logs → never present; events carry identifiers, lengths, and counts rather than text; excerpts/prompts/summaries are excluded by the safe-field allowlist.
- Log storms from hot paths → per-item events only at debug level with documented sampling guidance; failure details bounded per operation.
- Correlation id absent (for example, a scheduled tick without a run) → a new correlation id is minted at the operation boundary; no event ever lacks correlation.
- Provider outage during a run → health stays ready (by design); metrics and logs carry the degradation; the run's own outcome records the categorized failure.
- API error with sensitive request parameters → logs capture route and category, not parameter values; response leaks nothing (platform rule).
- Health check flapping → readiness depends only on local dependencies (database, configuration, scheduler); external outages never flap readiness.
- High-cardinality label temptation (entity ids as metric labels) → forbidden; audited; identifiers belong in logs, aggregates in metrics.
- Clock/timezone in logs → timestamps always UTC; display conversion happens in consumers.
- Metrics backend unavailable → the platform continues operating (export failures are logged and dropped/buffered per the plan); no feature depends on metric delivery for correctness.
- Debug logging enabled in production for investigation → redaction rules still apply identically at all levels; no level bypasses the allowlist.
- Admin operation logs (source changes, triggers) → carry actor identity per the established convention, without credentials.
- Duplicate correlation across concurrent operations → ids are unique per operation; the log consumer can always disambiguate.
- Additive platform changes (new job types, new metrics) → metric catalog and event schema evolve additively under documented naming conventions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Observability MUST integrate through application-owned abstractions (logging, metrics, health): the domain layer MUST reference no logging/monitoring types; backends and exporters MUST be selectable by configuration; replacing or adding a vendor MUST NOT require domain or feature-code changes.
- **FR-002**: Pipeline paths MUST emit structured key-value events (no free-text-only entries) carrying at minimum: UTC timestamp, level, event name, correlation id, operation context (for example job type/run, endpoint route, source/provider kind, entity identifiers), failure category where applicable (per the platform's error taxonomy), and duration/counts where applicable.
- **FR-003**: Correlation IDs MUST follow each operation across API layer, application services, provider calls, and background passes; run-related events MUST use the run's correlation identifier consistent with its ProcessingJob record; an API request's events MUST share one identifier end to end; an optional additive API response header exposing the correlation id is allowed (a documented delta for the public API feature).
- **FR-004**: A redaction policy with a safe-field allowlist MUST apply at every level: secrets, API keys, tokens, and credentials MUST never appear in logs or metric labels; full article content, excerpts, prompt bodies, and summary texts MUST be excluded; provider payloads are represented only by categories/counts/durations; violations MUST be detectable by automated scans.
- **FR-005**: Log level and volume policy MUST be defined: information for lifecycle transitions, warning for degradations/partial successes, error for failures; per-item hot-path events only at debug with documented sampling guidance; per-operation failure detail is bounded (no storms).
- **FR-006**: Health checks MUST provide liveness and readiness with defined semantics: readiness reflects database reachability, required configuration presence, and a running background scheduler; it MUST NOT depend on external providers; outputs use a stable, secret-free shape identifying affected subsystems.
- **FR-007**: Job metrics MUST cover, per job type: runs and outcomes, durations, and queue depth (from the orchestration feature's semantics), mirroring run-record counts.
- **FR-008**: Provider metrics MUST cover trend, news, and AI interactions: attempts, durations, outcomes, and failure categories per provider kind — never payloads or credentials.
- **FR-009**: Pipeline counters MUST cover the established domains: articles accepted/deduplicated/pruned, topic–article links, relevance shifts, cluster changes, trend scores, summaries generated/reused/validation-failed, timeline entries, related pairs — with vendor-neutral documented names, units, and bounded label sets.
- **FR-010**: Crawl failures MUST be observable per source: outcomes and categories in logs and metrics (consistent with the source operational state's failure categories).
- **FR-011**: API error observability MUST cover route, status, duration, and error category with correlation in logs/metrics; reader-facing response bodies MUST remain per the public API's stable error shape (no internal leakage in responses).
- **FR-012**: Performance measurements MUST include: API latencies aligned with the established targets, job durations, provider call durations, cache hit/miss/stale behavior, and query counts/latencies — as cataloged metrics with documented names; metric label sets MUST be bounded (no entity identifiers as labels — identifiers belong in logs).
- **FR-013**: Administrative actions (source changes, manual triggers) MUST be logged with actor identity per the established convention, without credentials.
- **FR-014**: Health and recent-failure information MUST be consumable by operations tooling (health endpoints with the defined shape; the admin console may surface them via a small additive integration — flagged for reconciliation).
- **FR-015**: The observability layer MUST be testable offline and deterministically: fixtures verify correlation propagation, required fields, redaction (automated secret/content scans), metric emission and bounded cardinality, health semantics (database down, config missing, scheduler stopped, providers down), and vendor-swap behavior through stubs.
- **FR-016**: Out of scope: building alerting/dashboarding products (export hooks and documented metrics only), distributed tracing infrastructure (single-process correlation only), log storage/retention tooling (deployment concern), vendor APM SDKs inside domain code, and any change to domain semantics or reader-facing contracts beyond the optional additive correlation header.

### Key Entities

This feature introduces application-owned observability infrastructure; no domain entities or persistence are added.

- **Observability facade contracts (application-owned)**: logger, meter/metrics, and health-registry interfaces consumed by application and infrastructure layers; the single integration surface for backends and exporters.
- **Correlation context**: the per-operation identifier minted at boundaries (API request, scheduled tick, manual trigger, recovery) and propagated through services, providers, and runs; consistent with ProcessingJob correlation fields.
- **Log event schema**: the required structured fields for pipeline events and the level/volume policy governing emission.
- **Redaction policy & safe-field allowlist**: the enforced rules and field list; the basis for automated scans across levels and boundaries.
- **Health check registry**: liveness and readiness checks with defined semantics (database, required configuration, scheduler) and the stable, secret-free output shape.
- **Metric catalog**: documented metric names, units, types, and bounded label sets spanning jobs, providers, pipeline counters, crawls, API, cache, and performance measurements.
- **Export configuration**: configuration-driven selection of exporters/backends (for example console and a stub collector in tests; a future vendor integration plugs in here).
- **Fixtures & audit harnesses**: pipeline scenarios, failure-class fixtures, redaction scanners, correlation-propagation assertions, health-state simulations, and vendor-swap stubs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Correlation completeness: 100% of events emitted within a fixture operation (API → service → provider → run) share one correlation id, and each fixture failure path is fully reconstructable from its log sequence alone.
- **SC-002**: Redaction guarantees: automated scans across all fixtures and levels find zero secrets, credentials, article content, prompts, or summaries in logs or metric labels (0 findings); forbidden-field enforcement is part of the test suite.
- **SC-003**: Health correctness: liveness/readiness behave per the defined semantics in 100% of simulated states (database down → not ready; configuration missing → not ready with subsystem identified; scheduler stopped → not ready; providers down → ready); outputs are secret-free and shape-stable.
- **SC-004**: Metric completeness and hygiene: 100% of the cataloged metrics are emitted in fixtures with documented names/units; label-cardinality audits pass (zero entity identifiers used as labels).
- **SC-005**: Failure-class signatures: RSS crawl, trend provider, AI provider, and API error fixtures each produce complete categorized events and metrics with identifiers and correlation (100%), while client-facing responses leak nothing.
- **SC-006**: Vendor neutrality: exporters/backends swap via configuration with zero domain or feature-code changes in 100% of swap tests; audits find zero logging/monitoring references in domain assemblies.
- **SC-007**: Performance measurement coverage: API latency, job duration, provider duration, cache, and query-count measurements are present and consistent with the established platform targets in 100% of fixture runs.

## Assumptions

- **Standard platform abstractions**: the runtime's standard logging/metrics/health abstractions (with OpenTelemetry-style naming and semantics documented in the plan) are the integration surface; vendor SDKs appear only behind export configuration, never in domain or feature logic.
- **Single-process scope**: correlation is per-operation within the modular monolith; no distributed tracing infrastructure is introduced (a documented limitation, extensible later if a demonstrated need arises).
- **Health endpoints**: served unauthenticated per convention with strictly secret-free output; they integrate with deployment probes and may be surfaced in the admin console via a small additive integration.
- **API correlation exposure**: an optional additive response header (correlation id) may accompany API errors — a documented additive delta reconciled with the public API feature; response bodies keep the stable error shape.
- **Log storage and retention**: sink management, rotation, and retention are deployment concerns; this feature defines content, schema, and policy, not storage products.
- **Alerting and dashboards**: not built here; the metric catalog and health endpoints are the stable foundation any alerting/dashboarding integration consumes (the admin console's failures view remains the operational UI).
- **Redaction discipline**: allowlist-based for pipeline fields; automated scans enforce it at all levels; review is part of the change process.
- **Metric delivery is non-critical**: export failures never affect feature correctness (logged and dropped/buffered per the plan).
- **No user PII**: the MVP has no user accounts; if future features introduce users, PII rules extend the allowlist.

**Dependencies**: Feature 002 (error taxonomy — categories used in events and metrics); Feature 013 (background processing — run records, correlation fields, job lifecycle events, queue semantics mirrored by metrics); Feature 016 (cache and query instrumentation consumed by the metric catalog); Feature 014 (API error shape discipline — responses unchanged; optional additive correlation header delta); Features 021/022 (admin action logging with actor identity; health/recent-failure consumption by the console); Project Constitution v1.0.0 (Technical Constraints — failures reported in logs; Principle IV — secrets never in logs or committed configuration; Principle III — no vendor/dashboard scope creep); deterministic fixtures, redaction scanners, and vendor-swap stubs for testing.
