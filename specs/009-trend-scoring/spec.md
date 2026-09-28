# Feature Specification: Trend Scoring (Versioned, Deterministic)

**Feature Branch**: `009-trend-scoring`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement trend scoring as an extension feature over TopicTrendObservation. Do not add volatile scoring fields directly to the stable Topic entity. Create a dedicated scoring result model that stores calculated score, category, calculation timestamp, and algorithm version. The scoring algorithm should consider available signals such as trend volume, velocity/growth, freshness, observation frequency, and other reliable Google Trends signals. Do not rely solely on percentage growth because low-volume topics can otherwise dominate high-volume topics. Support categories such as Emerging, Rising, Exploding, and Persistent as descriptive classifications rather than subjective rankings. The algorithm must be deterministic, explainable, configurable, and recalculable when new observations arrive. Future scoring algorithms must be able to coexist through algorithm versioning without changing the core Topic model."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compute and store trend scores in a dedicated result model (Priority: P1)

A scoring pass computes a deterministic score for each topic's trend activity from its stored observations and stores it in a dedicated **TopicTrendScore** result entity — score, category, calculation timestamp, and algorithm version — while the Topic entity and every existing record (aliases, observations, links, relevance, clusters) remain untouched.

**Why this priority**: This is the feature's core value, and the user's hard constraint is that volatile scoring data must never live on the stable Topic entity.

**Independent Test**: Run the pass over a fixture corpus of topics and observations; verify exactly one score record per eligible (topic, period, algorithm version) with calculation timestamp and component breakdown, and that no writes occur outside the score entity and run records.

**Acceptance Scenarios**:

1. **Given** a topic with sufficient observations in a scoring period, **When** the pass runs, **Then** a TopicTrendScore record is stored with the deterministic score, category, calculated-at time (UTC), and algorithm version.
2. **Given** any scoring run, **When** it completes, **Then** Topic, alias, observation, link, relevance, and cluster records are unchanged (zero external writes).
3. **Given** a topic with insufficient observations for the period, **When** it is processed, **Then** it is excluded and counted in run diagnostics rather than given a fabricated score.

---

### User Story 2 - Multi-signal, volume-aware deterministic algorithm (Priority: P1)

Scoring combines defined signal classes — trend volume proxy (observation presence and rank/strength values when provided), velocity/growth versus the previous comparable period, freshness (recency of the latest observation), and observation frequency/persistence across periods — with growth contributions weighted against volume baselines, so low-volume percentage spikes cannot dominate high-volume topics. Every stored score carries the component values, inputs, and weights used, and identical inputs always produce identical scores.

**Why this priority**: The user explicitly requires these signals, forbids growth-only scoring, and demands determinism and explainability.

**Independent Test**: Fixture pairs where a low-volume topic shows extreme percentage growth and a high-volume topic shows moderate growth; verify the recorded scores honor the volume-aware rulebook, that breakdowns explain each score, and that reruns reproduce them exactly.

**Acceptance Scenarios**:

1. **Given** a low-volume topic with extreme percentage growth, **When** it is scored, **Then** its growth contribution is dampened by the volume baseline and it does not outrank a genuinely high-volume topic per the rulebook.
2. **Given** any stored score, **When** it is audited, **Then** the record's component breakdown and inputs exactly re-derive the stored score for its algorithm version.
3. **Given** identical inputs, **When** scoring reruns, **Then** the score and breakdown are identical (fixed-point arithmetic, no drift).
4. **Given** observations missing optional rank/strength fields, **When** they are scored, **Then** the defined deterministic fallback (presence-based volume proxy) applies.

---

### User Story 3 - Descriptive categories: Emerging, Rising, Exploding, Persistent (Priority: P2)

Each score record receives exactly one descriptive category assigned by deterministic rulebook rules over the computed signals: **Emerging** (newly appeared, fresh signal with little history), **Rising** (sustained growth beyond a volume floor), **Exploding** (strong growth combined with high recent volume/velocity), and **Persistent** (present across many consecutive periods with stable volume). Categories describe the topic's behavior pattern; they are not subjective rankings across topics. Boundary scores and precedence between competing category rules are defined once and applied identically on every run.

**Why this priority**: Explicitly required; downstream views and consumers need the descriptive classification, and determinism is non-negotiable.

**Independent Test**: Fixture timeline per category pattern (new burst, steady climb, spike, long-lived topic) plus boundary cases; verify exact category assignment, stored with the record, identical on every rerun.

**Acceptance Scenarios**:

1. **Given** a timeline matching the rulebook's Exploding pattern, **When** scoring runs, **Then** the record is categorized Exploding (with the rule recorded).
2. **Given** a score exactly on a category boundary, **When** it is classified, **Then** the defined inclusive/exclusive and precedence rules assign the deterministic category.
3. **Given** a rulebook or threshold change, **When** the next pass runs, **Then** new records use the new rules while existing records keep their original categories (history unchanged).

---

### User Story 4 - Recalculable history when new observations arrive (Priority: P2)

Score results are append-only history: when new observations arrive for a topic's period or when weights/thresholds change, recalculation adds exactly one new record per affected identity; unchanged reruns add nothing. Repeated passes converge to zero delta, scoring is safe under concurrent runs and overlap with collection, and the latest score per identity is deterministically resolvable for comparisons over time.

**Why this priority**: The user explicitly requires recalculation on new observations and a deterministic, idempotent process.

**Independent Test**: Score a fixture; rerun unchanged (zero new records); simulate late-arriving observations and rerun (exactly one new record for the affected identities); verify latest-per-identity resolution and untouched history.

**Acceptance Scenarios**:

1. **Given** a scored identity (topic, period, algorithm version) and no material change, **When** the pass reruns, **Then** no new record is created.
2. **Given** new observations in the period, **When** the pass reruns, **Then** exactly one new score record is added for each affected identity with the new inputs, and prior records remain intact.
3. **Given** multiple records for an identity, **When** the current score is queried, **Then** the latest is resolved deterministically (calculated-at with a defined tie-break).
4. **Given** concurrent scoring passes or overlap with trend collection, **When** both complete, **Then** no duplicate records exist (identity constraint) and no run fails from races.

---

### User Story 5 - Coexisting algorithm versions without touching the core model (Priority: P3)

Scoring sits behind a strategy contract; every result carries its algorithm version, and multiple versions may coexist, each producing its own records under the same entity and constraints. A future scoring algorithm (v2, vN) can be added and run alongside v1 — with its own weights, rules, and category mapping — while consumers choose which version to read. No changes to the core Topic model are ever required, and no existing records are rewritten.

**Why this priority**: Explicitly required as the forward-compatibility guarantee; building additional algorithms now would violate the deterministic MVP.

**Independent Test**: Activate two fixture algorithm versions simultaneously; verify separate records per version, deterministic latest-per-version resolution, and zero Topic schema changes.

**Acceptance Scenarios**:

1. **Given** algorithm versions v1 and v2 both active, **When** scoring runs, **Then** records exist per version with their own scores/categories and consumers can query each version independently.
2. **Given** a new version added, **When** it runs, **Then** existing records of other versions are unchanged, and the Topic model definition diff remains empty.

---

### Edge Cases

- Low-volume extreme-growth topic versus high-volume moderate-growth topic → volume-aware weighting prevents domination (explicit user requirement), verified by fixture expectations.
- Topic with no observations in the period → excluded from scoring and counted; no fabricated score or category.
- Sparse history (a single observation) → eligible only for the categories the rulebook allows with minimal data (for example Emerging); other rules require their minimum data by definition.
- Observations missing rank/strength values → presence-based volume proxy applies deterministically; no guessing at magnitudes.
- Period boundaries and comparable previous periods → computed only via the shared time-window module in UTC (constitution Principle V); "previous comparable period" semantics are rulebook-defined.
- Late-arriving observations after a score was computed → material change → exactly one new record; historical record remains queryable for comparison.
- Category boundary score → inclusive/exclusive boundary semantics plus rule precedence resolve deterministically.
- Weights or thresholds reconfigured → configuration fingerprint changes → exactly one new record per affected identity; old records keep their categories.
- Algorithm versions coexisting → separate identities (version is part of the score identity); no cross-version overwrites.
- Concurrent scoring passes, or scoring overlapping with collection → identity constraint resolves races; deterministic outcome.
- Duplicate observations cannot occur (collection guarantees), so scoring input is already deduplicated; scoring never mutates observations.
- A topic that stops appearing → previously computed records remain; no new records are fabricated for empty periods.
- Very long-lived topic → Persistent classification derives from sustained presence across periods; no ad-hoc date arithmetic outside the shared module.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Scoring MUST be additive: a new dedicated **TopicTrendScore** result entity carries all scoring data; no scoring fields may be added to Topic, and Topic, TopicAlias, TopicTrendObservation, Article, links, relevance, and cluster records MUST NOT be modified by this feature.
- **FR-002**: Scoring MUST run as background processing (recorded as a ProcessingJob per run) over topics' stored observations; runs MUST be repeatable, restartable, and support backfill over historical periods; each run records counts (topics evaluated, records created, skipped-unchanged, excluded, category distribution) with UTC timings and an outcome category.
- **FR-003**: Every score record MUST store at minimum: the topic reference, the scoring period identity with UTC boundaries (as defined by the shared time-window module), the calculated score, the descriptive category, the calculation timestamp (UTC), the algorithm version identity, and the input/configuration fingerprint used for idempotency.
- **FR-004**: Signals MUST include at least: (a) trend volume proxy — observation presence and rank/strength values when provided; (b) velocity/growth versus the previous comparable period; (c) freshness — recency of the latest observation; (d) observation frequency/persistence across periods; all derivations are deterministic with defined fallbacks for missing optional data.
- **FR-005**: Volume-aware combination MUST hold: growth and velocity contributions are weighted/dampened by the volume baseline so that low-volume topics with extreme percentage growth cannot dominate high-volume topics; the exact weighting is a single-sourced rulebook parameter, and combined scores are fixed-point and reproducible.
- **FR-006**: Weights, thresholds, minimum-data requirements, and category rules MUST be configurable without code changes and single-sourced in the scoring rulebook; conservative defaults are chosen in the plan phase.
- **FR-007**: Explainability MUST hold: every stored score is exactly re-derivable from its recorded component values, inputs, weights/config version, and algorithm version; no opaque or estimated components.
- **FR-008**: Categories MUST be the descriptive classifications Emerging, Rising, Exploding, and Persistent, assigned by deterministic rulebook rules (including boundary semantics and precedence when rules overlap) with exactly one primary category per record; categories describe behavioral patterns, not subjective rankings across topics.
- **FR-009**: Recalculation MUST be append-only and trigger on material changes (new observations in the period, algorithm version change, configuration change): exactly one new record per affected identity; unchanged reruns MUST produce a zero-change delta.
- **FR-010**: Idempotency and concurrency MUST be enforced at the data layer: the score identity (topic + period + algorithm version + fingerprint) MUST be uniquely constrained; races resolve deterministically (skip or converge) and never fail a run.
- **FR-011**: Algorithm versioning MUST allow multiple scoring algorithms to coexist: the algorithm identity (with version) is part of every record; versions can be added through the scoring-strategy contract without changing the core model, and consumers can query results per version independently.
- **FR-012**: The latest score per identity (topic, period, version) MUST be deterministically resolvable (calculated-at with a defined tie-break), with indexes supporting lookup by topic/period/version recency and by category.
- **FR-013**: This feature MUST NOT implement presentation ranking, notifications, clustering changes, matching changes, relevance changes, or AI-based scoring; it consumes observations and produces scores only.
- **FR-014**: All timestamps and period boundaries MUST be UTC and MUST be computed only via the shared time-window module (constitution Principle V); scoring MUST NOT implement its own window arithmetic and MUST NOT make network calls (pure local computation).
- **FR-015**: Scoring MUST be testable offline and deterministically: fixtures cover volume-awareness (low-volume vs high-volume), missing-data fallbacks, category patterns and boundaries, recalculation, concurrency, and version coexistence; scores and breakdowns are reproducible across runs, and secret scans are irrelevant (no secrets involved).
- **FR-016**: Scoring period definitions and "previous comparable period" semantics MUST be owned by the shared time-window module and be identical wherever used; scores for different period types are separate identities.

### Key Entities

This feature is purely additive: one new result entity plus its rulebook and run records; the stable Topic model is untouched.

- **TopicTrendScore (new result entity)**: one record per scored identity: topic reference; scoring period identity with UTC boundaries; calculated score (fixed-point); descriptive category (Emerging / Rising / Exploding / Persistent); calculated-at (UTC); algorithm version identity; input/configuration fingerprint; explainability breakdown (signal component values, inputs, weights applied). Append-only history; latest per identity resolved deterministically; unique constraint on the score identity (topic + period + algorithm version + fingerprint).
- **Scoring Strategy contract (application-service/strategy)**: versioned implementations that compute signals, combined score, and category from observations and the rulebook; the deterministic v1 strategy is the MVP; additional versions coexist through the same contract.
- **Scoring Rulebook**: the single shared definition of signal derivations, volume weighting, minimum-data requirements, thresholds, category rules, boundary semantics, precedence, and comparable-period semantics (configuration-backed).
- **Scoring Run**: a ProcessingJob record per pass with counts, outcome category, UTC timings, and diagnostics.
- **Read-only inputs (core concepts used)**: Topic and TopicTrendObservation records (from trend collection) are read as inputs; the shared time-window module supplies period boundaries; nothing outside TopicTrendScore and run records is written.
- **Fixtures**: deterministic offline corpora (topic timelines per category pattern, low/high-volume pairs, late-arriving observations, multi-version scenarios) used for score, category, recalculation, concurrency, and versioning tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The fixture corpus yields exactly one score record per eligible (topic, period, algorithm version) identity with score, category, calculated-at, and breakdown; all runs together perform zero writes to Topic, observation, alias, link, relevance, or cluster records (100%).
- **SC-002**: Volume-bias fixtures meet the documented expectations in 100% of cases: low-volume extreme-growth topics do not outrank genuinely high-volume topics, and each recorded score is fully explained by its stored breakdown and inputs.
- **SC-003**: Scores, breakdowns, and categories are reproducible for identical inputs across 100% of reruns, including exact category-boundary cases.
- **SC-004**: Late-arriving observations or configuration changes produce exactly one new record per affected identity (100%), and unchanged reruns produce a zero-change delta.
- **SC-005**: Two fixture algorithm versions coexist: separate records per version, deterministic latest-per-version resolution, zero record overwrites, and a schema audit shows zero diffs on the Topic entity definition.
- **SC-006**: Concurrency simulations (parallel scoring passes, overlap with collection) produce no duplicate records and no unhandled errors in 100% of scenarios.
- **SC-007**: An explainability audit re-derives 100% of stored scores from their recorded components, inputs, and version metadata, matching the engine's outputs.

## Assumptions

- **Scoring inputs**: scoring consumes TopicTrendObservation history only (trend signals); articles, matching, relevance, and clustering are not inputs to this feature.
- **Volume proxy**: the provider supplies ranks/strength rather than absolute search volume; the volume proxy combines observation presence with rank/strength values when present, defined deterministically in the rulebook (signals remain provider-neutral; Google Trends specifics stay in the collection adapter).
- **Period semantics**: scoring periods and comparable previous periods are defined by the shared time-window module (for example, the presentation windows); each score identity names its period type so different periods never conflate; boundary rules are documented once.
- **Categories**: one primary category per record, chosen by deterministic rule precedence when patterns overlap (for example, Exploding takes precedence over Rising); categories are descriptive labels, never ordinal rankings across topics.
- **Recalculation triggers**: new observations, algorithm version activation, and configuration changes; recalculations are triggered by scheduled passes (configurable cadence) and backfill runs — no event-driven complexity in the MVP.
- **History model**: append-only score records; "latest" is derived (calculated-at with deterministic tie-break); no updates or deletions; comparisons across time and versions remain possible.
- **Arithmetic**: fixed-point integer arithmetic for reproducibility; weights and formulas fixed in the plan phase and configurable afterwards.
- **Versioning**: algorithm versions are strategy implementations; activation is configuration-driven; consumers choose a version (a default active version is configured) — no core-model change is ever required to add one.
- **Insufficient data**: topics with too few observations for a period are excluded and counted; no default or estimated scores are ever stored.
- **Architecture**: single-module background pass in the modular monolith; unique constraints backstop concurrency; no distributed constructs; no network access during scoring; no secrets involved.
- **Out of scope**: presentation ranking, notifications, clustering/matching/relevance changes, and AI-based scoring.

**Dependencies**: Feature 001 (core domain & persistence model — Topic read-only; new TopicTrendScore mapping to be designed in the plan); Feature 002 (background-processing and job patterns); Feature 003 (TopicTrendObservation history — the sole signal source — and its provider cadence/rank fields); Feature 006/007/008 (unchanged; documented as unaffected by FR-001); Project Constitution v1.0.0 (Principles I — versioned replaceable strategies, III — deterministic MVP, IV — configuration, V — shared time-window module and UTC storage); deterministic offline fixtures for testing.
