# Feature Specification: Article Relevance Evaluation (Deterministic, Recalculable)

**Feature Branch**: `007-article-relevance-evaluation`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement article relevance evaluation between Topics and Articles. Do not modify the stable Topic or Article entities unnecessarily. Store feature-specific relevance information in a dedicated extension entity such as ArticleRelevance. The MVP should calculate deterministic relevance using normalized keywords, aliases, title/description matching, and other available metadata. Store the relevance score, evaluation method, and evaluation timestamp so results can be recalculated and compared over time. Define thresholds for relevant, weakly relevant, and irrelevant articles. Provide an extension point for future AI-assisted relevance evaluation without coupling the domain to a specific AI provider. The feature must be independently recalculable and idempotent."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterministically evaluate relevance of topic–article pairs (Priority: P1)

An evaluation pass computes a deterministic relevance score for every linked topic–article pair using normalized topic keywords and aliases against the article's title, description, and available metadata, and stores the result in a dedicated **ArticleRelevance** extension entity — score, relevance category, evaluation method identity, and evaluation timestamp — without modifying Topic, Article, or the topic–article relationship records.

**Why this priority**: This is the feature's core value: it turns raw matches into ranked, comparable relevance data that later views and features can trust, and it is the prerequisite for everything else in this feature.

**Independent Test**: Run the pass over a fixture corpus of linked pairs; verify exactly one evaluation record per pair with score, category, method, and UTC timestamp, and that no Topic, Article, or link records were written.

**Acceptance Scenarios**:

1. **Given** a linked, active topic–article pair, **When** the pass runs, **Then** an ArticleRelevance record is stored with the deterministic score, relevance category, method identity, and evaluated-at time (UTC).
2. **Given** a completed evaluation run, **When** it finishes, **Then** Topic, Article, and topic–article link records are unchanged (zero writes outside ArticleRelevance and run records).
3. **Given** a pair with no usable text for evaluation, **When** it is processed, **Then** it is excluded and counted in run diagnostics rather than given a fabricated score.

---

### User Story 2 - Thresholds for relevant, weakly relevant, and irrelevant (Priority: P1)

Relevance scores map deterministically into three configurable bands — relevant, weakly relevant, and irrelevant — with defined boundary semantics (which band owns a boundary score). The category computed at evaluation time is stored on the evaluation record, so historical results reflect the thresholds active when they were produced.

**Why this priority**: Explicitly required; downstream consumers (views, filtering) depend on a stable, auditable classification rather than raw numbers alone.

**Independent Test**: Fixtures spanning the score range, including exact boundary scores; verify each maps to the defined band and that the stored category matches the configuration in force at evaluation time.

**Acceptance Scenarios**:

1. **Given** a score exactly at a band boundary, **When** it is classified, **Then** the defined inclusive/exclusive rule assigns the deterministic band.
2. **Given** thresholds are reconfigured, **When** the next pass runs, **Then** new evaluations use the new bands while existing records retain their original categories (history is not rewritten).
3. **Given** identical inputs and configuration, **When** classification reruns, **Then** the band is identical every time.

---

### User Story 3 - Recalculable history with idempotent convergence (Priority: P2)

Evaluation results are append-only history: re-evaluating a pair under a changed method, changed inputs, or changed configuration adds exactly one new record; re-evaluating unchanged pairs adds nothing. Any pair can be recalculated independently or in bulk, repeated runs converge to zero delta, and the latest evaluation per pair is deterministically resolvable for comparisons over time.

**Why this priority**: The user explicitly requires recalculation, comparison over time, and idempotency; history is what makes method comparisons and tuning possible later.

**Independent Test**: Re-run the pass unchanged (expect zero new records); change a material input (a topic alias) and re-run (expect exactly one new record for affected pairs); verify latest-per-pair resolution and that prior records are untouched.

**Acceptance Scenarios**:

1. **Given** an unchanged pair (same inputs, method, and configuration), **When** it is re-evaluated, **Then** no new record is created (idempotent).
2. **Given** a material change (new alias, rulebook version, or threshold set), **When** the pass reruns, **Then** exactly one new evaluation record is added for each affected pair and prior records remain intact and comparable.
3. **Given** multiple evaluation records for a pair, **When** the current relevance is queried, **Then** the latest record is resolved deterministically (evaluated-at, with a defined tie-break).
4. **Given** any single pair, **When** it is recalculated on demand, **Then** the result equals what a full pass would produce (same inputs — same outcome).

---

### User Story 4 - Replaceable evaluator with a provider-neutral AI seam (Priority: P3)

The evaluation algorithm sits behind a replaceable evaluator contract; the deterministic evaluator is the only MVP implementation. A future AI-assisted evaluator can replace or supplement it — integrating through the existing AI provider abstraction rather than coupling the domain to any specific AI provider — and its results land in the same ArticleRelevance records with their own method identity.

**Why this priority**: Required as an extension point only; building any AI capability now would violate the deterministic MVP and YAGNI.

**Independent Test**: Register an alternative stub evaluator through the contract; verify it produces ArticleRelevance records through the same pipeline with zero core-model changes and that no AI/ML dependencies exist in the MVP.

**Acceptance Scenarios**:

1. **Given** an alternative evaluator registered, **When** evaluation runs, **Then** records are produced with the alternative's method identity through the same storage, idempotency, and history machinery.
2. **Given** the MVP build, **When** dependencies are audited, **Then** zero AI/ML/provider-specific packages are referenced by the domain, and method identity — not provider types — is the only trace of evaluators.

---

### Edge Cases

- Pair whose article has no usable title/description → evaluation excluded and counted (no fabricated score); the pair remains eligible once usable text exists.
- Boundary traps (topic keyword as substring of an unrelated word) → the same phrase/word-boundary discipline as matching applies; substring-only occurrences do not inflate scores.
- Score exactly on a band boundary → deterministic inclusive/exclusive rule; identical outcome on every run.
- Threshold change only (inputs and method identical) → the configuration fingerprint changes, producing exactly one new record per affected pair; old records keep their original categories.
- Method version change (e.g., deterministic v2 or a future AI method) → new records with the new method identity; both versions remain comparable in history.
- Topic alias added after evaluation → affected pairs are re-evaluated on the next pass (exactly one new record each); unaffected pairs change nothing.
- Retired duplicate articles (from deduplication) → excluded from evaluation; their pairs are not re-evaluated.
- Pair whose link was never created (article and topic unrelated) → not evaluated in the MVP (evaluation applies to linked pairs); the entity references the pair directly so a future pre-link workflow needs no remodel.
- Concurrent evaluation passes, or evaluation overlapping with matching/ingestion → unique evaluation-identity constraints prevent duplicates; conflicts resolve deterministically.
- Very long article text → bounded normalized excerpt with deterministic truncation; scores remain reproducible.
- Missing configuration (no threshold set) → fail fast with an actionable configuration error at startup, never a silent default drift.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Relevance results MUST be stored in a new dedicated **ArticleRelevance** extension entity referencing the topic–article pair; it MUST carry at minimum: the relevance score, the relevance category, the evaluation method identity (with version), the evaluated-at time (UTC), and the input/configuration fingerprint used for idempotency. Topic, Article, and topic–article relationship records MUST NOT be modified by this feature.
- **FR-002**: Evaluation MUST run as background processing (recorded as a ProcessingJob per run) over linked, active topic–article pairs; runs MUST also be triggerable per single pair on demand, producing identical results to a full pass for the same inputs.
- **FR-003**: Scoring MUST be deterministic and reproducible: normalized topic keywords and aliases are matched against the article's title, description, and available metadata using the established phrase/word-boundary discipline and defined evidence weights; identical inputs MUST always produce an identical score (fixed-point arithmetic, no floating-point drift).
- **FR-004**: The relevance category MUST be one of three defined bands — relevant, weakly relevant, irrelevant — mapped from the score by configurable thresholds with explicit boundary semantics; the category computed at evaluation time is stored on the record and never recomputed retroactively.
- **FR-005**: Every evaluation record MUST identify its method (evaluator identity plus version), so results from different evaluators — including future AI-assisted ones — remain distinguishable and comparable over time.
- **FR-006**: Evaluation history MUST be append-only: re-evaluation adds a new record when any material input (article text, topic names/aliases), the method version, or the configuration (threshold set) has changed since the last evaluation, and adds nothing otherwise; prior records MUST never be modified or deleted.
- **FR-007**: Idempotency MUST be enforced at the data layer: the evaluation identity (pair + method version + input fingerprint + configuration fingerprint) MUST be uniquely constrained, so repeated runs — including concurrent runs — cannot create duplicate records; conflicts MUST resolve deterministically (skip or converge), never fail the run.
- **FR-008**: The latest evaluation per pair MUST be deterministically resolvable (evaluated-at with a defined tie-break), and records MUST be efficiently retrievable by pair recency, by method, and by category (indexes).
- **FR-009**: Evaluation MUST be independently recalculable: restartable mid-run, safe to run concurrently with ingestion, deduplication, and matching passes, and convergent — after all inputs stabilize, further runs produce a zero-change delta.
- **FR-010**: The evaluator MUST sit behind a replaceable evaluation-strategy contract; the deterministic evaluator is the only MVP implementation; a future AI-assisted evaluator MUST be able to replace or supplement it through the contract — integrating AI providers exclusively via the existing provider abstraction — without changing the core model or the consumers.
- **FR-011**: This feature MUST NOT implement topic clustering, trend scoring, link creation or removal (matching owns links), presentation decisions, or summarization; relevance categories only annotate pairs for later consumers.
- **FR-012**: Thresholds, evidence weights, method versions, batch sizes, and pass cadence MUST be configurable without code changes; the evaluation rulebook is single-sourced and shares its normalization discipline with matching (constitution Principles IV and consistency requirements); no secrets are required by this feature.
- **FR-013**: All timestamps MUST be stored in UTC; no window arithmetic (constitution Principle V), and no network access during evaluation (pure local computation over stored data).
- **FR-014**: Relevance evaluation MUST be testable offline and deterministically: fixtures cover score computation, boundary traps, band boundaries, history accumulation, recalculation convergence, method/version changes, and concurrency; results are reproducible across runs.

### Key Entities

This feature is purely additive: one new extension entity plus its supporting rulebook and run records; Topic, Article, and the topic–article relationship remain untouched.

- **ArticleRelevance (new extension entity)**: one record per evaluation: topic reference, article reference (pair identity), relevance score, relevance category (relevant / weakly relevant / irrelevant), method identity and version, evaluated-at (UTC), input/configuration fingerprint. Append-only history; latest per pair resolved deterministically; unique constraint on the evaluation identity (pair + method version + input fingerprint + configuration fingerprint).
- **Topic / Article / topic–article relationship (core; read-only here)**: evaluation reads topic names/aliases and article text/metadata; no writes of any kind; no new fields on these entities.
- **Relevance Evaluator contract (application-service/strategy)**: given a pair's normalized inputs and configuration, produces score, category, and method identity; the deterministic implementation is the MVP; the seam through which future AI-assisted evaluation integrates via the provider abstraction.
- **Relevance Rulebook**: the single shared definition of normalization, phrase/boundary semantics, keyword/alias evidence weights, and fixed-point score composition; consistent with the matching rulebook's normalization discipline.
- **Threshold Set**: the versioned configuration defining the three bands and boundary semantics; its version participates in the evaluation identity so threshold changes produce new records rather than rewrites.
- **Evaluation Run**: a ProcessingJob record per pass: counts (pairs evaluated, records created, skipped-unchanged, excluded, category distribution), outcome category, UTC timings, diagnostics.
- **Fixtures**: deterministic offline corpora of linked pairs, texts, thresholds, and methods used for score, band, history, and idempotency tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The fixture corpus yields exactly one relevance evaluation record per linked active pair with score, category, method identity, and UTC timestamp; zero writes occur to Topic, Article, or link records.
- **SC-002**: Scores are reproducible across 100% of reruns for identical inputs, and boundary fixtures map to their defined bands with 100% consistency (including exact-boundary cases).
- **SC-003**: Re-running an unchanged pass produces zero new records (delta = 0); changing a material input, method version, or threshold set produces exactly one new record per affected pair in 100% of scenarios.
- **SC-004**: History integrity holds: after any number of recalculations, prior evaluation records are unchanged, fully comparable across methods, and latest-per-pair resolution is deterministic in 100% of queries.
- **SC-005**: Concurrency simulations (parallel passes; evaluation overlapping matching/ingestion) produce no duplicate evaluation records and no unhandled errors in 100% of simulated races.
- **SC-006**: Single-pair recalculation equals the full-pass result for the same inputs in 100% of fixture comparisons, and post-stabilization passes converge to zero changes.
- **SC-007**: An alternative evaluator stub integrates through the contract with zero core-model changes; the MVP build contains zero AI/ML/provider-specific dependencies in the domain, and method identity is the only evaluator trace in records.

## Assumptions

- **Scope**: evaluation applies to linked, active pairs produced by topic–article matching; retired duplicate articles are excluded; unlinked pairs are not evaluated in the MVP, and the entity references the pair directly so a future pre-link evaluation workflow needs no remodel.
- **Additive-only**: no changes to Topic, Article, or the topic–article relationship — the 006 matching provenance stays as-is; relevance lives exclusively in ArticleRelevance.
- **Immutability**: article text is fixed at ingestion in the MVP, so material input changes are expected mainly from topic alias additions and rulebook/threshold version changes; the fingerprint mechanism captures them all.
- **History model**: append-only records; recalculations never overwrite; "latest" is derived (evaluated-at with deterministic tie-break), not stored as a mutable flag.
- **Arithmetic**: scores use fixed-point integers to guarantee reproducibility; evidence weights and score composition are fixed in the plan phase and configurable afterwards.
- **Defaults**: conservative thresholds and weights are fixed in the plan phase (favoring precision); boundary semantics (inclusive/exclusive) are defined in the rulebook.
- **Normalization consistency**: the evaluation rulebook shares the matching rulebook's normalization and boundary discipline (single-sourced rules; one place to change).
- **Future AI evaluation**: integrates through the evaluator contract plus the existing provider abstraction; records carry the AI method identity; no schema redesign anticipated; failures of a future AI evaluator would not disturb deterministic history (their own records, own method identity).
- **Consumers**: future views and filtering read the latest record per pair; presentation gating and ranking remain out of scope for this feature.
- **Architecture**: single-module background pass in the modular monolith; no distributed constructs; no network access during evaluation.

**Dependencies**: Feature 001 (core domain & persistence model — Topic, Article, topic–article relationship, metadata policy; new ArticleRelevance mapping to be designed in the plan); Feature 002 (background-processing patterns and the AI provider abstraction used by the future seam); Feature 003 and Feature 004 (topics/aliases and article corpus as evaluation inputs); Feature 005 (retired duplicates excluded); Feature 006 (linked pairs that this feature evaluates; its rulebook's normalization discipline is shared); Project Constitution v1.0.0 (Principles I — replaceable/provider-neutral strategies, III — deterministic-only MVP, IV — configuration, V — UTC/time handling); deterministic offline fixtures for testing.
