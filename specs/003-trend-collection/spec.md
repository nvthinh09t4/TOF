# Feature Specification: Trend Collection (Google Trends via Provider Abstraction)

**Feature Branch**: `003-trend-collection`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement the trend collection feature using the provider abstraction defined by the core architecture. The initial implementation must collect trending topics from Google Trends and normalize the provider response into the existing Topic, TopicAlias, and TopicTrendObservation core entities. The system must preserve observation timestamps and provider information and support identifying trends for the last 5 hours, today, and yesterday. Do not create a Google-specific domain model. Google Trends-specific fields that are not part of the stable core should be isolated behind the provider implementation or controlled metadata. Handle duplicate keywords, normalization, missing data, provider failures, rate limits, timeouts, cancellation, and partial results. The feature must be idempotent so repeated collection of the same provider observation does not create unintended duplicates. Do not implement trend scoring, clustering, article matching, or topic lifecycle logic in this feature. Provide the data required by those later features through the core model."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Collect and persist normalized trend observations (Priority: P1)

A background collection run fetches trending topic samples from the trend provider (initially Google Trends) through the provider abstraction, and normalizes every valid sample into the core model: Topics (created or matched), TopicAliases where surface forms differ, and one TopicTrendObservation per topic per provider per observation window, with observation timestamps (UTC) and provider identity preserved.

**Why this priority**: This is the feature's core value — the raw trend signal that every later feature (article matching, summaries, scoring, views) consumes. Without it there is no trending-news data at all.

**Independent Test**: Run collection against a recorded Google Trends fixture (offline, via the abstraction); verify topics and observations are persisted with correct UTC timestamps and provider identity, and that a consumer can identify the trending topics collected.

**Acceptance Scenarios**:

1. **Given** a provider response containing trend samples, **When** a collection run processes it, **Then** every valid sample results in a Topic (created or matched), a TopicTrendObservation with the observation time (UTC) and provider identity, and the run is recorded as a processing job with its counts.
2. **Given** a trend sample whose surface form differs from an existing topic's name (casing, whitespace, diacritics), **When** it is normalized, **Then** it resolves to the existing topic and the differing surface form is recorded as an alias where appropriate.
3. **Given** stored observations, **When** topics are identified for the last 5 hours, today, or yesterday, **Then** the correct topics are returned based solely on stored UTC observation timestamps interpreted by the shared time-window module.

---

### User Story 2 - Idempotent, duplicate-safe collection (Priority: P1)

Repeated collection of the same provider observation never creates unintended duplicates: re-running the same window, re-processing a partially overlapping window, or receiving duplicate keywords within one response all result in the same stored state, enforced by the core model's uniqueness rules — not by ad-hoc application checks.

**Why this priority**: Collection runs repeatedly on a schedule; without data-layer idempotency every re-run would corrupt trend counts, article matching, and future scoring.

**Independent Test**: Run collection twice against the identical fixture, and once against a fixture with intra-response duplicate keywords and cross-run overlapping windows; verify zero new duplicate rows and stable counts.

**Acceptance Scenarios**:

1. **Given** a completed collection for a window, **When** the identical collection runs again, **Then** no duplicate topics, aliases, or observations are created and the run completes successfully with zero additions.
2. **Given** a provider response containing the same keyword multiple times, **When** it is processed, **Then** exactly one observation per topic, provider, and window is stored.
3. **Given** two overlapping collection windows, **When** both are processed, **Then** observations that belong to the same topic, provider, and observation window are stored exactly once.

---

### User Story 3 - Resilient collection under failures and interruption (Priority: P2)

Collection behaves predictably under provider failures, rate limits, timeouts, cancellation, and partial results, using the provider abstraction's error taxonomy and isolation rules: valid data that was collected is preserved, failures are categorized and recorded in the run's processing-job diagnostics, and nothing hangs or corrupts the store.

**Why this priority**: External trend providers fail, throttle, and slow down routinely; collection must degrade gracefully (constitution failure-isolation constraints) and be safely re-runnable thanks to US2.

**Independent Test**: Simulate timeouts, throttling responses, malformed batches, hangs, and cancellation during collection; verify each ends in a categorized run outcome with partial data preserved, no duplicate or corrupt rows, and bounded duration.

**Acceptance Scenarios**:

1. **Given** the provider times out or is throttled, **When** the run ends, **Then** the run records the categorized failure (after the abstraction's bounded retries) and any previously normalized observations remain stored.
2. **Given** a batch where some items are malformed, **When** it is processed, **Then** valid items are persisted (partial success), invalid items are reported in diagnostics, and the run is recorded as partially successful.
3. **Given** an in-flight collection, **When** cancellation is requested, **Then** collection stops promptly, already-normalized observations remain consistent, and the run is recorded as cancelled.
4. **Given** the provider returns an empty result set, **When** the run completes, **Then** it is recorded as a successful run with zero new observations (not an error).

---

### User Story 4 - Forward-compatible data for windows and later features (Priority: P3)

The data collected by this feature is sufficient for the three presentation windows (last 5 hours, today, yesterday) and for later features (article matching, summarization, scoring, clustering) without re-collection or schema changes — while this feature itself implements none of those later capabilities.

**Why this priority**: The MVP roadmap depends on this data; keeping the collection feature focused prevents scope creep, but the outputs must be complete for downstream consumers.

**Independent Test**: Using only stored observations from this feature, verify that (a) topics can be identified for each of the three windows, and (b) the raw fields later features need (topic identity, observation times, rank/score when provided, provider identity, window labels) are all present.

**Acceptance Scenarios**:

1. **Given** observations spanning multiple days, **When** a consumer identifies trending topics per window, **Then** no re-collection is needed and results are deterministic.
2. **Given** Google-specific response fields that are not part of the stable core, **When** they are retained, **Then** they exist only as controlled metadata and no Google-specific entities exist; **Given** a field becomes needed for queries, **When** promoted, **Then** it becomes a strongly typed core column in a future change.

---

### Edge Cases

- Same keyword appearing multiple times in one provider response (and with different surface forms) → one topic, one observation per provider per window; variant surface forms recorded as aliases.
- Keyword normalization collisions: casing, extra whitespace, diacritic/Unicode variants (Vietnamese text) → deterministic normalization ensures a single topic identity.
- Missing data: absent rank/score stored as absent (never fabricated); absent observation time falls back to the run's timestamp, recorded as such; empty result sets are successful runs with zero observations.
- Malformed or schema-changed provider payload → mapping failure classified; raw sample retained only as diagnostic metadata; previously collected data unaffected.
- Rate limiting (e.g., 429 responses) → treated as transient with the abstraction's bounded backoff; if exhausted, the run ends with a categorized failure and diagnostics, and partial data is preserved.
- Timeout or hang mid-collection → bounded by timeout plus retry budget; partial results persisted.
- Cancellation mid-run → stops promptly; persisted observations remain consistent; run recorded as cancelled.
- Overlapping or concurrent collection runs → uniqueness rules make writes idempotent; no duplicates, no data loss.
- Trend disappears between runs → earlier observations remain (append-only history); collection never deletes observations.
- Clock and window boundaries: all timestamps stored UTC; window boundaries (last 5 hours, today, yesterday) come only from the shared time-window module using the deployment's reference timezone (constitution Principle V).
- Provider returns very large result sets → configurable result limits keep runs bounded.
- Provider locale/region/language settings change → affects only which trends are returned; the stored data model is unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Collection MUST run as a background processing job (recorded as a ProcessingJob per run) and MUST interact with the trend provider exclusively through the provider abstraction's trend-collection contract; the core MUST NOT reference Google-specific types, endpoints, or payload shapes. The initial active provider is Google Trends (the first adapter).
- **FR-002**: Every valid provider sample MUST be normalized into the core entities: a Topic (created or matched by normalized name), a TopicAlias when the surface form differs from the topic's stored naming forms, and exactly one TopicTrendObservation per topic, provider, and observation window.
- **FR-003**: Each observation MUST preserve its observation time in UTC — the provider's observation time when supplied, otherwise the collection run's timestamp, recorded as the fallback case — and MUST carry the provider identity and observation-window label (e.g., the provider's cadence) so signals from different providers and cadences never conflate.
- **FR-004**: Stored data MUST support identifying trending topics for the last 5 hours, today, and yesterday based solely on stored UTC timestamps interpreted by the shared time-window module; collection MUST NOT implement its own window arithmetic (constitution Principle V).
- **FR-005**: Collection MUST be idempotent at the data layer: re-collecting an identical provider observation MUST NOT create duplicate topics, aliases, or observations; uniqueness rules (topic + provider + observation window) MUST guarantee this even under concurrent or overlapping runs.
- **FR-006**: Keyword normalization MUST be deterministic (trim, whitespace collapsing, case folding, Unicode/diacritic handling) so keyword variants consistently resolve to a single topic identity.
- **FR-007**: Duplicate keywords within a single provider response MUST be deduplicated deterministically before persistence so that at most one observation per topic, provider, and window is stored, retaining the most complete sample.
- **FR-008**: Missing data MUST be handled deterministically: absent optional fields (rank/score and related fields) are stored as absent and never fabricated; empty provider results are recorded as successful runs with zero observations; missing observation times use the run timestamp per FR-003.
- **FR-009**: Provider failures MUST follow the abstraction's error taxonomy: timeouts, rate limits, and transient failures are handled with bounded retries inside the adapter; exhausted or permanent failures end the run with a categorized outcome recorded in the run's ProcessingJob diagnostics; raw provider exceptions MUST NOT surface into domain code, and secrets MUST never appear in logs.
- **FR-010**: Partial results MUST be preserved: when a response is partially valid, valid items are persisted (partial-success outcome) and invalid items are reported in diagnostics; a run is never marked fully successful when part of its data was rejected.
- **FR-011**: Cancellation MUST stop collection promptly; observations normalized before cancellation MUST remain persisted and consistent; the run is recorded as cancelled.
- **FR-012**: No Google-specific domain model may be introduced: no GoogleTrend-style entities; Google-specific fields not part of the stable core may be retained only as controlled metadata per the core model's metadata policy (never as queried/filtered data).
- **FR-013**: This feature MUST NOT implement trend scoring, clustering, article matching, or topic lifecycle logic; it MUST provide the raw, queryable data those features require (topic identity, observation timestamps, window labels, rank/score when provided, provider identity).
- **FR-014**: Collection scheduling (cadence/enablement), provider settings (locale/region/language, result limits), and timeouts/retry budgets MUST be configurable without code changes; credentials MUST come from environment variables or a secure store (constitution Principle IV).
- **FR-015**: Collection MUST be testable offline: adapter mapping runs against recorded Google Trends fixtures; the collection flow runs against deterministic stub providers; contract tests per the provider abstraction apply to the Google Trends adapter.
- **FR-016**: Collection run records MUST capture at least: run start and end (UTC), provider identity, requested window(s) and cadence label, counts (received, normalized, persisted, deduplicated, rejected), and final outcome category (success, partial success, failed, cancelled).

### Key Entities

This feature introduces no new persisted entities; it maps provider data into the core model from the core domain & persistence model and produces run records via ProcessingJob.

- **Trend Collection Run**: A ProcessingJob of the trend-collection type; attributes: provider identity, requested observation window and cadence label, status, attempt/outcome information, counts (received/normalized/persisted/deduplicated/rejected), diagnostics reference, UTC timings.
- **Normalized Trend Sample**: The neutral adapter output consumed by collection; fields: topic surface name(s), normalized-name input, rank/score when provided, observation time (or fallback flag), provider identity, observation-window/cadence label, optional controlled metadata.
- **Topic / TopicAlias / TopicTrendObservation (core concepts used)**: Topic = matched-or-created by normalized name; TopicAlias = recorded when surface variants differ; TopicTrendObservation = exactly one per topic + provider + window with UTC observation time, window label, optional rank/score, optional metadata.
- **Google Trends Adapter**: The first implementation of the trend-collection contract; isolates Google payload shapes, translates errors into the abstraction's taxonomy, and normalizes samples. No Google-specific entities exist anywhere else.
- **Recorded Fixtures & Stub Trend Providers**: Offline deterministic inputs (recorded Google Trends responses, scripted stubs) used for adapter mapping tests and collection-flow tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A collection run against a recorded Google Trends fixture maps 100% of valid samples to Topic and TopicTrendObservation records with UTC observation times and provider identity; zero Google-specific fields exist outside controlled metadata.
- **SC-002**: Running the identical collection twice produces zero duplicate topics, aliases, or observations (duplicate count = 0 on unique business keys), and intra-response duplicate keywords yield exactly one observation each.
- **SC-003**: A fixture set of keyword variants (casing, whitespace, diacritics) resolves to single topic identities with 100% consistency across repeated runs.
- **SC-004**: 100% of simulated failure scenarios (timeout, throttling, malformed batch, empty result, cancellation) end in the correct categorized run outcome with diagnostics; no run exceeds its timeout plus bounded retry budget; partial batches always persist their valid items.
- **SC-005**: Topics trending in each of the three presentation windows are correctly identifiable from stored observations using the shared window module (boundary-crossing fixture data returns exactly the expected topic sets — 100% match).
- **SC-006**: Dependency verification shows zero direct references from core/domain code to the Google Trends adapter or any Google payload type (0 violations), and secret scans of code and logs return zero credentials.
- **SC-007**: Cancellation stops a run within the configured grace period, and post-cancellation data-consistency checks pass 100% (no partial or corrupt records).

## Assumptions

- **Builds on existing features**: the core domain & persistence model (feature 001) provides Topic, TopicAlias, TopicTrendObservation, ProcessingJob, uniqueness rules, and the metadata policy; the provider abstraction (feature 002) provides the trend-collection contract, error taxonomy, timeout/retry/isolation behavior, registration, and stub/contract-test patterns. This feature adds the first concrete adapter (Google Trends) and the collection flow built on them.
- **Google Trends access method** (official endpoint or alternative) is an adapter implementation detail decided in the plan phase; it MUST remain replaceable per the abstraction, and its failure modes MUST map into the standard taxonomy.
- **Observation time semantics**: the provider's sample time is preferred; when the provider does not supply a reliable time, the run timestamp is used and flagged; both stored UTC.
- **Provider cadence mapping**: how the provider's own windows and cadences relate to platform windows is documented in the plan; this feature stores the provider cadence label per observation so future providers and cadences remain separable.
- **Scheduling**: periodic collection runs at a configurable cadence in the modular monolith's background job runner; no external schedulers or distributed constructs.
- **Retention and append-only history**: observations are never deleted by this feature; disappearing trends simply stop producing new observations.
- **Out of scope (explicit user constraint)**: trend scoring, clustering, article matching, topic lifecycle logic, presentation/UI, and notifications.
- **Result limits**: per-run and per-window result limits are configurable; the default is chosen in the plan phase to keep runs bounded.
- **Locale settings**: provider locale/region/language parameters are configuration; changing them does not change the stored data model.
- **Multilingual text**: keywords pass through with no lossy transformation; normalization is the deterministic rule set defined for the core model.

**Dependencies**: Feature 001 (core domain & persistence model — target entities, uniqueness enforcement, metadata policy, ProcessingJob); Feature 002 (provider abstraction — trend-collection contract, error taxonomy, timeouts/retries/isolation, DI registration, stubs and contract tests); Project Constitution v1.0.0 (Principles I, II, III, IV, V — especially the time-window single source of truth and configuration-driven providers); recorded Google Trends fixtures for offline testing.
