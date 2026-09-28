# Feature Specification: Notifications (Follow-Based Topic Activity Alerts)

**Feature Branch**: `025-notifications`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Design a notification system as an extension of user preferences and topic activity. Users may receive notifications when a followed topic becomes significantly active or receives important new coverage. Define dedicated notification entities and relationships rather than adding notification state directly to Topic or User. Define notification preferences, trigger conditions, deduplication, cooldown periods, delivery status, retries, and provider abstractions. The first implementation should focus on the domain and application contracts and remain independent of a specific email or push provider. Do not implement recommendation logic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Follow-based notifications for topic activity and new coverage (Priority: P1)

When a topic a user explicitly follows becomes significantly active or receives important new coverage, the platform creates a notification for that user. Targeting is strictly follow-based — the user's explicit follows are the only targeting source, never recommendations — and trigger conditions come from a single-sourced, configurable, deterministic rulebook that consumes existing topic-activity signals read-only. Notifications are dedicated entities; no notification state is added to Topic, User, or any other existing entity.

**Why this priority**: The core capability requested; everything else (preferences, delivery) builds on notification creation.

**Independent Test**: Fixture runs against prepared topic-activity signals; verify notifications are created only for followers, with correct type/reason payloads, zero writes to signal sources, and deterministic outcomes for identical inputs.

**Acceptance Scenarios**:

1. **Given** a followed topic with an elevated-activity signal, **When** evaluation runs, **Then** a topic-activity notification is created for each follower with the reason and rule version recorded, and nothing is written to Topic or User.
2. **Given** a followed topic with important new coverage, **When** evaluation runs, **Then** a new-coverage notification is created per follower with the coverage reference captured deterministically.
3. **Given** a topic that no one follows (or that a user has hidden), **When** evaluation runs, **Then** zero notifications are created for those users.

---

### User Story 2 - Explicit notification preferences (Priority: P1)

Each user controls whether they receive each notification type through explicit preferences stored in dedicated preference entities: enabling or disabling notification types, with deterministic defaults when no preference exists, immediate effect on future creations, and idempotent updates. Preferences are per-user data with strict ownership.

**Why this priority**: Explicitly required; user-governed delivery is what makes the system acceptable and deterministic.

**Independent Test**: Preference fixtures (defaults, toggles, repeated updates, missing rows); verify effective rules per type and immediate deterministic effect on subsequent evaluations.

**Acceptance Scenarios**:

1. **Given** no stored preference, **When** evaluation targets the user, **Then** the documented default applies deterministically.
2. **Given** a disabled notification type, **When** its trigger fires for a followed topic, **Then** no notification of that type is created (and other types are unaffected).
3. **Given** repeated identical preference updates, **When** applied, **Then** state converges with no duplicates and no errors.

---

### User Story 3 - Deduplication, cooldowns, and bounded volume (Priority: P1)

The same underlying reason never produces two notifications (deterministic dedupe identity enforced at the data layer), repeated triggers within a configurable cooldown window are suppressed deterministically and counted, and when multiple trigger types fire at once a documented precedence applies so at most one notification per topic per user per evaluation cycle is created.

**Why this priority**: Explicitly required (deduplication and cooldown periods); it is what prevents notification floods while staying deterministic.

**Independent Test**: Fixtures for same-reason replays, cooldown boundaries (just inside / just outside), concurrent triggers with precedence, and suppression counters.

**Acceptance Scenarios**:

1. **Given** a reason that has already produced a notification, **When** evaluation re-runs, **Then** no duplicate notification is created.
2. **Given** a notification inside the cooldown window for that topic, **When** a new trigger fires, **Then** creation is suppressed and counted; once the window passes, a new trigger creates a notification.
3. **Given** both trigger types firing in one cycle, **Then** precedence is applied deterministically and the suppressed type is counted.

---

### User Story 4 - Delivery lifecycle, retries, and provider independence (Priority: P2)

Every notification carries a delivery lifecycle (pending → dispatched | failed) with append-only delivery-attempt records (attempt number, UTC time, outcome, error category, provider identity). Dispatch retries are bounded, apply to transient failures only, use configurable backoff, and end in a terminal state; permanent errors fail immediately without retries. Delivery goes through a provider-neutral abstraction following the platform's provider contract pattern (uniform error taxonomy, timeouts, cancellation, credentials from secure storage); the first implementation ships a deterministic stub provider and no vendor-specific adapter.

**Why this priority**: Explicitly required (delivery status, retries, provider abstractions); it keeps the platform vendor-neutral per the constitution.

**Independent Test**: Stub-provider fixtures (success, transient failures until exhaustion, permanent failure, timeouts); verify status transitions, attempt records, exact retry bounds, and zero vendor coupling in domain/application layers.

**Acceptance Scenarios**:

1. **Given** a pending notification and a healthy provider, **When** dispatch runs, **Then** it becomes dispatched with one recorded attempt and is never sent again.
2. **Given** repeated transient failures, **When** retries exhaust, **Then** the notification ends failed with exactly the configured number of attempts.
3. **Given** a permanent failure, **Then** the notification ends failed immediately with no retry, and the provider identity and error category are recorded.

---

### User Story 5 - Ownership isolation, determinism, and exclusion of recommendation logic (Priority: P3)

Notification data is strictly per-user: users can only read and manage their own notifications; cross-user access fails with non-disclosing semantics; nothing appears on public surfaces; user-specific notification data never enters shared caches. Behavior is fully deterministic and explicit — identical inputs produce identical notifications — and there is no recommendation logic, engagement optimization, behavioral ranking, open/click tracking, or hidden targeting: the only targeting signal is explicit follows. The user-facing surface is a versioned authenticated contract following platform conventions; UI presentation is deferred to a future frontend integration point.

**Why this priority**: User-mandated exclusions plus privacy and determinism; it fixes the contract boundaries of the feature.

**Independent Test**: Isolation probes, determinism fixtures, scope audits (no recommendation/engagement logic), and contract golden tests.

**Acceptance Scenarios**:

1. **Given** user A, **When** user B's notifications are probed, **Then** access fails with non-disclosing semantics and zero data.
2. **Given** identical signal, user, and configuration inputs, **When** evaluation and dispatch are re-run, **Then** outcomes are identical (convergent, no duplicates).
3. **Given** a behavior and code audit, **Then** zero recommendation, engagement-optimization, or implicit-tracking logic exists.

---

### Edge Cases

- Both trigger types fire in the same cycle → documented precedence applies; at most one notification per topic per user per cycle; the suppressed type is counted.
- Trigger at the exact cooldown boundary → the boundary comparison rule (inclusive or exclusive) is documented in the plan and covered by fixtures; behavior is identical across runs.
- User unfollows (or hides) a topic between creation and dispatch → already-created notifications are not revoked (creation-time intent governs); no further notifications are created while unfollowed or hidden.
- Preferences disabled after creation but before dispatch → the pending notification still dispatches (creation-time snapshot); preference changes affect only future creations.
- Delivery provider completely down for an extended period → pending notifications accumulate with scheduled retries; per-cycle dispatch batches are bounded; nothing is lost; the backlog drains deterministically after recovery.
- Crash between provider success and status write → a duplicate delivery is possible in that narrow window; mitigation: notifications carry a stable dedupe-safe identity passed to providers that support it; the residual risk is documented as accepted.
- Topic later merged or retired (cluster lifecycle) → notification snapshot fields preserve the original display context; links resolve per public rules; no retroactive edits.
- Cooldown shortened by configuration → applies prospectively only; no retroactive sends for previously suppressed reasons.
- Trigger rulebook version changes → new creations use the active version; existing notifications keep their recorded version.
- A topic with a very large follower count triggers → per-cycle creation is batched with deterministic ordering; the remainder is deferred to subsequent cycles with no loss and no duplicates.
- Topic names or reason text containing special characters → snapshot fields are stored safely and rendered escaped (display-safe) on any future surface.
- Duplicate or overlapping evaluation runs (restarts, concurrent triggers) → dedupe identity prevents duplicates; runs converge.
- All notification types disabled for a user → zero creations; no errors; suppressions counted as preference-suppressed in run diagnostics.
- Clock handling across runs → all timestamps are UTC; ordering uses recorded timestamps with deterministic tie-breakers.
- Notification listing requested while dispatch is pending → current status is returned per contract; ordering remains stable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST introduce dedicated entities — Notification, NotificationPreference, and DeliveryAttempt — with no notification state added to Topic, User, or any other existing entity, and zero writes to the signal sources it consumes (trend scores, timeline entries, article links).
- **FR-002**: Targeting MUST be strictly follow-based: notifications are created only for users who follow the topic (per the platform's follow model); hidden topics produce zero creations; unfollow or hide immediately stops future creations; no other targeting source exists.
- **FR-003**: Trigger conditions MUST come from a single-sourced, configurable, deterministic rulebook with the rule identity and version recorded on each notification: (a) topic activity — the topic becomes significantly active per the trend-scoring categories; (b) new coverage — the topic receives important new relevant coverage per the relevance-gated article links (significance thresholds with conservative defaults defined in the plan; first-coverage and multi-article cases covered).
- **FR-004**: When multiple triggers fire in one evaluation cycle, a documented deterministic precedence applies (topic activity before new coverage as the default); at most one notification per (user, topic) is created per cycle; suppressed triggers are counted in run diagnostics.
- **FR-005**: Deduplication MUST be deterministic: each notification carries a dedupe identity derived from (user, topic, type, reason reference); the identity is unique at the data layer; replayed, overlapping, or restarted evaluations create zero duplicates.
- **FR-006**: Cooldown periods MUST be configurable per (user, topic) across types: creations inside an active cooldown window are suppressed deterministically and counted; the boundary comparison rule is documented and fixture-tested; configuration changes apply prospectively only.
- **FR-007**: Notification preferences MUST be explicit per-user, per-type enablement stored in dedicated preference entities with idempotent updates, deterministic documented defaults when no preference exists, and immediate effect on future creations only (never retroactive).
- **FR-008**: The delivery lifecycle MUST be defined and recorded: status pending → dispatched | failed with created/dispatched/last-attempt timestamps in UTC; failed is terminal after exhausting retries; read state is separate user data, idempotently updatable, and never conflated with delivery status.
- **FR-009**: Retries MUST be bounded and configured (attempt count and backoff): transient failures only are retried; permanent, authentication, and validation failures fail immediately without retries; every attempt is recorded append-only in DeliveryAttempt (attempt number, attempted-at UTC, outcome, error category, provider identity; unique per notification+attempt); a dispatched notification is never sent again.
- **FR-010**: Delivery MUST go through a provider-neutral abstraction following the platform's provider contract pattern (uniform error taxonomy, timeouts, cancellation, configuration and secure-store credentials); the first implementation ships a deterministic stub provider with contract tests and no vendor-specific adapter; adapters are replaceable without domain or application changes; provider identity is recorded on attempts.
- **FR-011**: The user-facing surface MUST be a versioned, authenticated notifications contract following platform conventions (mapped DTOs, stable error shapes, pagination, additive-only evolution): list own notifications (deterministic order, newest first, filters for read state and type), idempotent read/unread updates, and preferences read/update; strict ownership on every operation.
- **FR-012**: Isolation and privacy MUST hold: all notification operations are strictly per-user; cross-user access fails with non-disclosing semantics; zero notification data appears on public surfaces; administrative visibility is limited to operational job/run outcomes (no notification-content view in the MVP); logging follows the platform's redaction policy (no notification content or destinations in logs).
- **FR-013**: Background processing MUST integrate with the platform scheduler as registered job types (evaluation and dispatch) with recorded runs, counts, and trigger sources per platform rules; both passes are idempotent and convergent; per-cycle creation and dispatch batches are bounded with deterministic ordering and no loss.
- **FR-014**: Caching MUST follow the platform rule that user-specific responses are never stored in shared caches; no user-specific notification data enters shared cache keys.
- **FR-015**: Configuration MUST cover cooldown duration, rulebook thresholds, retry bounds and backoff, batch sizes, provider selection, and retention; no secrets in code or committed configuration (future transport credentials come from secure storage); all timestamps are UTC.
- **FR-016**: The feature MUST be testable offline and deterministically with fixtures covering triggers and precedence, dedupe replays, cooldown boundaries, preference defaults and toggles, retry exhaustion and permanent failures, isolation probes, and batching boundaries — all without any vendor dependency.
- **FR-017**: Out of scope: recommendation logic, engagement optimization or behavioral ranking, open/click tracking, digests and quiet hours, per-topic preference granularity, localization, source-follow notifications, full authentication systems, vendor email/push adapters, UI presentation (deferred to a future frontend integration point), and changes to public reader surfaces.

### Key Entities *(include if feature involves data)*

- **Notification (new)**: per-user notification record — owner, topic reference, type (topic activity | new coverage), display snapshot (public-safe, display-safe fields such as topic name and reason description), reason reference plus rule identity/version, dedupe identity (unique), status (pending | dispatched | failed), created/dispatched/last-attempt timestamps (UTC), read state; retained per the configured retention policy.
- **NotificationPreference (new)**: per-user per-type enablement with updated-at (UTC); absence means documented defaults; idempotent updates.
- **DeliveryAttempt (new, append-only)**: per-notification attempt — attempt number, attempted-at (UTC), outcome, error category, provider identity; unique per (notification, attempt).
- **Delivery provider contract (interface)**: provider-neutral capability (uniform error taxonomy, timeouts, cancellation, secure-store credentials); adapters registered via dependency injection; MVP ships a deterministic stub provider.
- **Trigger rulebook (configuration)**: single-sourced deterministic rules mapping activity and coverage signals to creations, with recorded identity/version; conservative defaults in the plan.
- **API surface**: versioned authenticated notifications contract (list, read state, preferences) with platform conventions; strictly ownership-scoped; additive evolution.
- **Scheduler integration**: evaluation and dispatch registered as job types with recorded runs and counts (platform job records).
- **Fixtures & harnesses**: trigger/precedence/dedupe/cooldown-boundary/retry/isolation/batching fixtures driven by the stub provider.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Targeting correctness: 100% of fixture cases create notifications only for followers of non-hidden topics; zero creations for unfollowed or hidden topics; zero writes to signal sources.
- **SC-002**: Deduplication and cooldown: zero duplicates for identical reasons across replayed evaluations; cooldown boundary cases match the documented semantics in 100% of fixtures; suppressions are counted.
- **SC-003**: Preferences: defaults, toggles, and idempotency verified in 100% of fixtures; disabled types produce zero creations in all subsequent evaluations.
- **SC-004**: Delivery: 100% of sends produce attempt records; retry counts equal configured bounds exactly; permanent failures are never retried; dispatched notifications are never re-sent; all stub-provider modes (success, transient, permanent, timeout) are covered offline.
- **SC-005**: Isolation and privacy: 100% of cross-user probes fail with non-disclosing semantics; audits find zero notification data on public surfaces.
- **SC-006**: Determinism and scope: repeated identical runs are convergent with no state drift; audits find zero recommendation, engagement-optimization, or implicit-tracking logic.
- **SC-007**: API conformance: golden contract tests pass for 100% of notification endpoints (authentication errors, pagination, ordering, idempotent read state); additive evolution verified with unknown-field tolerance.
- **SC-008**: Scale boundaries: batched creation and dispatch respect configured batch sizes with no loss and no duplication across cycles in stress fixtures.

## Assumptions

- **Follows dependency**: targeting consumes the platform's explicit follow model as-is (topic follows with followed/hidden states); the authentication boundary flagged by that feature remains a dependency — no new authentication system is introduced here; unfollow/hide semantics govern targeting only.
- **Signal sources are read-only**: trigger signals come from existing trend scoring (activity) and relevance-gated coverage links; the rulebook consumes them read-only and never recomputes or writes them; thresholds have conservative defaults defined in the plan.
- **Notifications are user data**: records are retained per a configurable retention policy (defaults in the plan); delivery attempts are append-only; unfollow/unhide does not delete existing notification history.
- **No revocation**: created or pending notifications are not revoked when follows or preferences change afterwards — creation-time intent governs; the alternative (cancelling pending items) is a flagged future policy option.
- **Content scope**: notification display snapshots use public-safe fields (topic name, reason descriptions) only; embedding AI-generated text is future work subject to attribution and labeling rules.
- **MVP scope**: domain + application contracts + authenticated API only; no vendor email/push adapter (deterministic stub provider); transport configuration and credentials arrive with future adapters under the secure-storage rules.
- **UI deferred**: no user interface in this feature; a future frontend integration consumes the notifications contract (the topic detail page's reserved extension seam is the intended entry point).
- **Future seams (explicitly not implemented)**: quiet hours, digests or batching windows, per-topic preference granularity, per-user global caps, localization, and source-follow notifications.
- **Crash-window duplicate delivery** is an accepted, documented risk with mitigation hooks (stable dedupe-safe identity available to providers).
- **Operational visibility**: job runs and failures appear through the platform's existing operational surfaces; no notification-content admin view in the MVP.
- **Out of scope**: recommendation logic, engagement optimization or behavioral ranking, open/click tracking, full authentication or profile systems, and any changes to public reader surfaces beyond the defined contract.

**Dependencies**: Feature 001 (core model — new entity mappings designed in the plan; Topic and User untouched); Feature 002 (provider contract pattern — uniform error taxonomy, timeouts, cancellation, DI registration, secure configuration); Feature 006/007 (coverage signal — relevance-gated article links, read-only); Feature 009 (activity signal — trend score categories, read-only); Feature 013 (scheduler — two new job types with recorded runs); Feature 014 (contract conventions — versioned DTOs, error shapes, pagination, additive evolution); Feature 016 (user-specific data never shared-cached); Feature 018 (reserved topic-detail extension seam for future UI); Feature 023 (logging redaction policy); Feature 024 (follow model and the flagged authentication boundary, consumed as-is); Project Constitution v1.0.0 (Principle II — any future embedding of AI text in notifications follows attribution/labeling rules; Principle III — explicit, minimal scope, no recommendation creep; Principle IV — configuration over code, credentials from secure storage, never in logs).
