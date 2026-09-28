# Feature Specification: Background Processing & Job Orchestration (In-Process)

**Feature Branch**: `013-background-processing`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Design the background processing system for the trending-news platform using the existing ProcessingJob core entity. The system must orchestrate scheduled operations such as trend collection, RSS crawling, article deduplication, topic matching, relevance evaluation, clustering, trend scoring, and AI summarization. Define job types, scheduling, execution order, dependencies, retries, idempotency, cancellation, concurrency limits, failure isolation, and recovery behavior. Jobs must not directly contain provider-specific business logic. They should invoke application services and provider abstractions. Use a solution appropriate for a .NET modular monolith. Do not introduce distributed queues or microservices unless there is a demonstrated requirement. The system must support safe repeated execution and provide enough state through ProcessingJob and logging for operational troubleshooting."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scheduled execution of every job type (Priority: P1)

Each platform operation — trend collection, news ingestion, article deduplication, topic matching, relevance evaluation, clustering, trend scoring, and AI summarization — is registered as a job type with a configurable schedule. The orchestration layer triggers the mapped application service, records a ProcessingJob run with its type, trigger source, lifecycle state, attempts, and timings, and contains no provider-specific or domain business logic itself (services and provider abstractions do the work).

**Why this priority**: This is the feature's core value: the platform's operations must run reliably without manual intervention, on data-over-code schedules.

**Independent Test**: With a fake clock and stub services, verify each registered type fires on its configured schedule, records a complete run, and that the orchestration layer invokes only the service contract (no provider calls, no domain logic).

**Acceptance Scenarios**:

1. **Given** enabled job types with configured schedules, **When** the scheduler runs, **Then** each type executes on schedule, invoking its application service and recording a complete ProcessingJob run.
2. **Given** any run, **When** records are audited, **Then** type, trigger source (schedule/manual/recovery), status lifecycle, attempts, start/finish (UTC), counts summary, and correlation id are present.
3. **Given** the orchestration layer, **When** it is audited, **Then** it contains no provider-specific calls and no domain business logic — only scheduling, lifecycle, and state.

---

### User Story 2 - Execution order, dependencies, and manual triggers (Priority: P1)

Pipeline dependencies are explicit: a job type may declare gated dependencies (it runs only after its upstream types succeeded in the current cycle) and an optional ordered cycle mode runs the stages in the platform's natural sequence (collection → ingestion → deduplication → matching → relevance → clustering → scoring → summarization). A failed upstream stage skips its dependents with a recorded reason while independent jobs continue; operators can trigger any job type manually, subject to the same overlap and concurrency rules.

**Why this priority**: Correct ordering prevents wasted work (e.g., summarizing before matching) and the user explicitly requires execution order, dependencies, and deterministic behavior.

**Independent Test**: Cycle fixtures with one stage failing; verify dependents are skipped with reasons, independent stages complete, manual triggers respect concurrency rules, and ordering is deterministic.

**Acceptance Scenarios**:

1. **Given** declared dependencies, **When** an upstream run fails, **Then** dependent runs are skipped with the failure recorded as the reason and unrelated job types continue normally.
2. **Given** cycle mode enabled, **When** a cycle executes, **Then** stages run in the defined order with per-stage isolation.
3. **Given** a manual trigger for a running type, **When** it is requested, **Then** overlap rules apply (queued or rejected per configuration) and the trigger source is recorded.

---

### User Story 3 - Resilience: retries, isolation, cancellation, limits (Priority: P2)

Runs survive the realities of external systems and process lifecycles: transient failures retry per type with bounded backoff while permanent failures never retry; a failing or stuck job type never blocks other types; graceful shutdown cancels running jobs within a configured grace period and records cancellations; maximum run durations stop runaway executions; global and per-type concurrency limits prevent overlaps; and cancelled or failed runs leave no corrupt state because every pass is idempotently re-runnable.

**Why this priority**: The user explicitly requires retries, failure isolation, cancellation, and concurrency limits; these guarantees are what make unattended operations safe.

**Independent Test**: Fixture scenarios with transient and permanent failures, a hanging run exceeding max duration, shutdown during execution, and concurrent trigger pressure; verify configured behavior, recorded outcomes, and isolation.

**Acceptance Scenarios**:

1. **Given** a transient failure, **When** retries are configured, **Then** the run retries with bounded backoff up to the configured attempts and ends with a categorized terminal outcome; attempts remain fully visible in history.
2. **Given** a permanent failure, **When** it occurs, **Then** no retry is attempted and the outcome is categorized distinctly.
3. **Given** graceful shutdown, **When** it is requested, **Then** running jobs cancel within the grace period, cancellations are recorded, and persisted state remains consistent for idempotent re-execution.
4. **Given** concurrency pressure (scheduled + manual triggers overlapping), **When** runs are dispatched, **Then** per-type single-flight and global caps hold, with deterministic queueing order.

---

### User Story 4 - Recovery after restarts and missed schedules (Priority: P2)

After an unclean stop, the orchestrator reconciles at startup: runs left in a running state by a previous process are marked interrupted (categorized), and a configurable recovery policy either re-runs them immediately or waits for the next schedule — always via idempotent re-execution, never partial patching. Missed schedule windows follow a configurable at-most-once catch-up policy, so downtime cannot cause storms of duplicated work.

**Why this priority**: The user explicitly requires recovery behavior; unattended platforms must self-heal after crashes and restarts.

**Independent Test**: Simulate a stale running record, restart the orchestrator with each recovery policy, and verify reconciliation, re-execution behavior, attempt accounting, and catch-up limits.

**Acceptance Scenarios**:

1. **Given** a run left running by a previous process, **When** startup reconciliation executes, **Then** it is marked interrupted with a categorized outcome and the configured recovery policy applies.
2. **Given** downtime that missed schedule windows, **When** the orchestrator resumes, **Then** at most one catch-up execution per type occurs (per policy), and normal scheduling resumes.
3. **Given** recovery re-execution, **When** the pass completes, **Then** platform data is consistent exactly as a normal run would leave it (idempotency preserved).

---

### User Story 5 - Operational state and troubleshooting (Priority: P3)

The ProcessingJob records and structured logs provide everything needed for operations: recent runs per type, statuses, attempts, durations, counts, categorized failures, and correlation identifiers linking logs to runs — without distributed tooling. Retention is configurable (default at least 30 days per the core model), and orchestrator infrastructure remains a plain in-process capability of the modular monolith.

**Why this priority**: The user explicitly requires operational troubleshooting state; it also closes the loop on the constitution's failure-reporting constraints.

**Independent Test**: Execute mixed success/failure fixtures and verify that operational questions (last run, last failure, duration trend, why skipped) are answerable from records plus correlated logs alone.

**Acceptance Scenarios**:

1. **Given** any past period, **When** operators query runs by type and status, **Then** results come entirely from ProcessingJob records and correlated structured logs.
2. **Given** a failed or skipped run, **When** it is investigated, **Then** the category, diagnostics reference, and correlating log entries explain the outcome without any distributed tracing infrastructure.
3. **Given** retention configuration, **When** records age out, **Then** the configured policy applies (default ≥ 30 days).

---

### Edge Cases

- Overlapping triggers (schedule fires while a manual run is active) → per-type single-flight wins; the second trigger queues or is rejected per configuration; both attempts visible.
- Two instances of the application running accidentally → out of scope for the MVP (single-instance assumption); the data-layer uniqueness rules of the feature passes still prevent duplicate business records, and duplicate job runs remain visible for detection.
- Clock changes (DST/timezone) → schedules defined against the configured reference timezone resolve deterministically; all stored times remain UTC; no ad-hoc date math outside the shared time-window module.
- Missed multiple schedule windows during long downtime → at-most-once catch-up per type (configurable), never a backlog storm.
- A job type disabled mid-cycle → already-running stages complete or cancel per policy; subsequent stages see it as skipped.
- Stale run from a previous process → startup reconciliation marks it interrupted exactly once (idempotent reconciliation across repeated restarts).
- Max-duration breach while the underlying service is mid-pass → cancellation propagates, feature pass persists consistent state per its own idempotency guarantees, run records categorized timeout plus cancellation.
- Retry budget exhausted → terminal categorized failure recorded; ops-visible; no infinite retries.
- Partial-success runs (features define partial outcomes) → recorded as their own outcome category; per-type policy decides whether partials consume retry attempts or are accepted.
- A dependency configured to a disabled or unknown job type → configuration validation fails fast at startup with an actionable error.
- Queue starvation risk → deterministic FIFO queueing by enqueue time with defined ties; caps ensure fairness across types.
- Log correlation across restarts → correlation id persisted on the run record, so post-restart log investigation still links to the same run.
- Very short schedules (e.g., every minute) for heavy types → configuration validation warns/fails per policy when the schedule cannot complete within its own interval (overlap prevention catches the rest deterministically).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The orchestrator MUST schedule and execute registered job types; each type MUST map to an application service (the pass defined by its feature spec) invoked through a single contract; scheduled jobs MUST NOT contain provider-specific or domain business logic — provider access happens only inside services via the provider abstractions.
- **FR-002**: Scheduling MUST be in-process within the modular monolith (hosted background capability; exact mechanism fixed in the plan); it MUST NOT introduce distributed queues, brokers, microservices, or external schedulers unless a demonstrated requirement exists; the MVP assumes a single process instance.
- **FR-003**: Job types MUST include at least: trend collection, news ingestion, article deduplication, topic matching, relevance evaluation, clustering, trend scoring, and AI summarization; the registry MUST be extensible by registering new types with configuration and a service binding (no orchestrator redesign).
- **FR-004**: Dependencies and ordering MUST be explicit: job types MAY declare gated dependencies (run only after upstream types succeeded within the cycle) and an optional ordered-cycle mode executes stages in the defined pipeline sequence; a failed upstream MUST cause dependents to be skipped with the recorded reason while unrelated types continue; dependency configuration MUST validate at startup (fail fast on unknown/disabled references).
- **FR-005**: Triggers MUST be recorded per run (schedule, manual, recovery); manual triggering MUST be supported for any enabled type and MUST obey the same overlap and concurrency rules as scheduled triggers.
- **FR-006**: Retries MUST be per-type configurable: bounded attempts with backoff for transient categorized failures only; permanent failures MUST NOT retry; partial outcomes follow the type's configured policy; attempt history accumulates on the run record and MUST NOT be overwritten (job-level retries are distinct from adapter-internal call retries).
- **FR-007**: Safe repeated execution MUST hold: every pass remains idempotent and convergent (guaranteed by the feature specs); the orchestrator MUST prevent harmful overlaps via per-type single-flight; missed schedule windows follow an at-most-once catch-up policy; duplicate scheduling MUST be impossible under normal operation.
- **FR-008**: Cancellation MUST be graceful and recorded: shutdown cancels running jobs within a configured grace period; job-level maximum durations cancel runaway executions; cancelled runs leave persisted state consistent for idempotent re-execution; cancellation and timeout outcomes are categorized distinctly.
- **FR-009**: Concurrency limits MUST be enforced in-process: a global maximum of concurrent jobs and per-type limits (single-flight by default) with deterministic FIFO queueing (defined tie-breaks) for pending runs.
- **FR-010**: Failure isolation MUST hold: a failing, stuck, or cancelled job type MUST NOT block other types (beyond declared dependency skips); categorized outcomes and correlation ids are logged; the orchestrator itself remains healthy after any run failure.
- **FR-011**: Recovery MUST be automatic: startup reconciliation marks runs left running by a previous process as interrupted (categorized, exactly once across repeated restarts); a configurable recovery policy re-runs interrupted types immediately or defers to the next schedule; attempts budgets apply across recovery; recovery re-execution uses the same idempotent passes.
- **FR-012**: Operational state MUST be sufficient without distributed tooling: ProcessingJob records capture type, trigger source, lifecycle states, attempts/outcomes, start/finish (UTC), counts summary, diagnostics reference, and a correlation id; structured logs correlate to runs; retention is configurable (default at least 30 days).
- **FR-013**: All schedules, enablement flags, retry policies, timeouts/max durations, concurrency caps, catch-up policy, and dependency gates MUST be configurable without code changes (constitution Principle IV); no secrets are involved in the orchestrator (provider credentials remain with the providers).
- **FR-014**: All stored times MUST be UTC; time-of-day schedules are expressed against the configured reference timezone and resolved through the shared time-window module's conventions; no ad-hoc date arithmetic elsewhere (constitution Principle V).
- **FR-015**: The orchestrator MUST NOT duplicate feature logic: convergence, invalidation, reuse, and validation remain owned by the feature specs (003 through 012); the orchestrator only triggers, records, limits, and recovers.
- **FR-016**: The orchestration layer MUST be testable offline and deterministically with a fake clock and stub services: fixtures cover scheduling, overlap prevention, dependency skipping, cycle ordering, retries/backoff, cancellation/shutdown, maximum durations, recovery policies, catch-up, concurrency caps, and failure isolation.
- **FR-017**: This feature MUST NOT introduce presentation concerns, notification systems, workflow designers, or distributed infrastructure; multi-instance coordination and HA scheduling are explicitly out of scope for the MVP (future, only if a demonstrated requirement arises).

### Key Entities

This feature consumes the core ProcessingJob entity and adds an in-process orchestration capability; no new persisted business entities are required.

- **Job Type (registry entry)**: a registered operation (the eight platform passes plus future types): identity, bound application-service contract, schedule definition, enablement, retry policy, max duration, concurrency limits, dependency gates — all configuration-backed.
- **ProcessingJob (core; used)**: the run record per execution: type, trigger source, lifecycle state (queued/running/completed/failed/cancelled/interrupted/skipped), attempts with outcomes, start/finish (UTC), counts summary, diagnostics reference, correlation id; retention configurable (default ≥ 30 days); history never overwritten.
- **Scheduler (in-process capability)**: resolves schedules against the reference timezone, queues runs deterministically (FIFO with defined tie-breaks), enforces global/per-type limits, and applies catch-up policy.
- **Application Service contract (invoked)**: the single generic contract through which every job type is executed (pass context in; categorized result + counts out); implemented by each feature's pass service.
- **Recovery reconciler**: startup logic marking stale runs interrupted and applying the configured recovery policy exactly once, idempotently across restarts.
- **Structured logging & correlation**: run-correlated log entries sufficient for troubleshooting without distributed tracing.
- **Fixtures**: fake clock, stub services with scripted outcomes (success/partial/transient/permanent/hang), and scenario scripts for scheduling, dependencies, recovery, and limits.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With fixture schedules, 100% of enabled job types execute on time and produce complete run records (type, trigger, status, attempts, UTC timings, counts, correlation id).
- **SC-002**: Overlap prevention holds in 100% of scheduling and manual-trigger scenarios: no two concurrent runs of the same type, global caps respected, deterministic queueing order.
- **SC-003**: Dependency correctness: 100% of dependent stages are skipped with recorded reasons when upstream stages fail, while independent types continue unaffected.
- **SC-004**: Retry behavior exactness: transient failures follow configured bounded attempts with backoff and end categorized; permanent failures never retry; attempt history is complete in 100% of scenarios.
- **SC-005**: Cancellation and recovery: shutdown cancels within the configured grace period; stale runs are reconciled exactly once per restart; recovery policies apply as configured; re-execution leaves data consistent in 100% of scenarios.
- **SC-006**: Isolation: a permanently failing or hanging job type prevents zero other types from running in 100% of fixture scenarios, and the orchestrator remains healthy.
- **SC-007**: Boundary and operability audits: zero provider-specific or domain business logic exists in the orchestration layer; zero distributed infrastructure dependencies; operational questions (last run, last failure, durations, skip reasons) are answerable from records and correlated logs in 100% of test cases.

## Assumptions

- **Single process instance**: the MVP runs one application instance; multi-instance coordination (leader election, distributed leases) is explicitly out of scope and only added if a demonstrated requirement arises; feature-pass data uniqueness still protects business data if duplicate instances are accidentally run.
- **In-process scheduling**: a hosted in-application scheduler (for example, a background hosted service loop; exact mechanism in the plan) drives schedules; interval-based and time-of-day schedules are supported; the reference timezone is a single configured setting.
- **Restartable passes**: features 003-010 guarantee idempotent, convergent passes, so the MVP orchestrator needs no checkpointing granularity; recovery means safe re-execution, not partial resumption.
- **Retry layering**: adapter-internal call retries (feature 002) and job-level run retries are distinct layers; both are bounded and recorded; job retries never multiply unboundedly with adapter retries (bounded composition documented in the plan).
- **Cycle mode**: optional ordered pipeline execution complements independent scheduled types; defaults are configured in the plan phase; both modes use the same per-type machinery.
- **Catch-up**: at-most-once per missed window, configurable; no backlog storms.
- **Manual triggers**: exposed as an operational capability (configuration/ops path in the MVP; an admin UI is out of scope); recorded with trigger source.
- **Retention**: ProcessingJob retention defaults to at least 30 days (feature 001) and is configurable.
- **Out of scope**: distributed queues/brokers, microservices, workflow engines/designers, alerting integrations, HA/multi-instance scheduling, presentation concerns.

**Dependencies**: Feature 001 (ProcessingJob entity — states, attempts, retention; this feature provides the orchestration semantics around it); Feature 002 (categorized failure taxonomy and adapter-internal retry semantics — job-level retries build on the same categories); Features 003-010 (the eight job types and their pass services this feature schedules and orchestrates); Features 011-012 (consumed transitively through the summarization service; not separately scheduled); Project Constitution v1.0.0 (failure isolation constraints; Principle III — no distributed scope creep; Principle IV — schedules/policies configuration; Principle V — UTC and reference-timezone discipline); fake clock and stub-service fixtures for testing.
