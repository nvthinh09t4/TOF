# Feature Specification: Related Topics (Deterministic Discovery)

**Feature Branch**: `020-related-topics`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement related-topic discovery as an extension feature over Topic, TopicAlias, Article, and clustering/relevance data. The MVP should identify related topics using deterministic signals such as shared aliases, shared relevant articles, shared entities/keywords, topic clusters, and categories where available. Do not modify Topic with a permanent list of related topic IDs. Compute or persist relationships using a dedicated extension model when persistence is required. Provide an abstraction so future semantic/AI similarity can replace or supplement the deterministic strategy. Define ranking, maximum result count, duplicate prevention, and handling of weak relationships."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterministic related-pair discovery from existing signals (Priority: P1)

A repeatable pass discovers which topics are related by combining deterministic signals — shared alias forms, shared relevant articles (relevance-band gated), shared significant keywords/name tokens, same-cluster membership, and shared trend categories (weak, corroboration-required) — and persists qualifying pairs in a dedicated extension entity with a deterministic score, evidence breakdown, and strategy identity. The Topic entity gains nothing: no related-topic ID lists anywhere on it.

**Why this priority**: This is the feature's core value and the user's central constraint (extension model, no Topic changes).

**Independent Test**: Fixture topics with scripted aliases, links, clusters, and categories; run discovery and verify exactly the expected pairs per the rulebook with correct evidence and zero writes to any source entity.

**Acceptance Scenarios**:

1. **Given** two topics sharing a strong signal (alias form, relevant article, or cluster membership), **When** discovery runs, **Then** one pair row is stored with its score, evidence breakdown, method identity, and computed-at (UTC).
2. **Given** a pair whose only connection is a weak signal (shared category alone), **When** discovery runs, **Then** the pair does not qualify (corroboration required).
3. **Given** any discovery run, **When** records are audited, **Then** Topic, alias, article, link, relevance, and cluster records are unchanged (read-only inputs; zero writes).

---

### User Story 2 - Ranking, result caps, duplicate prevention, and weak-relationship policy (Priority: P1)

Related topics per topic are ranked deterministically (score, then defined tie-breakers), capped by a configurable maximum result count, and free of duplicates: pairs use a canonical ordering with a unique constraint, self-pairs are impossible, and concurrent runs cannot create duplicate rows. Weak relationships are handled explicitly: below-threshold pairs are neither stored nor returned, previously stored pairs that fall below the threshold are removed deterministically on recompute, and near-threshold pairs are counted in diagnostics for tuning.

**Why this priority**: The user explicitly requires ranking, caps, duplicate prevention, and weak-relationship handling; wrong-tier or duplicated results would undermine the feature's purpose.

**Independent Test**: Ranking fixtures with ties and caps; concurrent-run simulations; threshold boundary pairs (in/out) and post-change below-threshold pairs; verify order, caps, single rows, removals, and diagnostics counts.

**Acceptance Scenarios**:

1. **Given** a topic with many related candidates, **When** results are queried, **Then** they return ranked with the defined tie-breakers, capped by the configured maximum, with identical order across repeated runs.
2. **Given** concurrent discovery runs, **When** both process the same pair, **Then** exactly one row exists (canonical pair identity + unique constraint) and no run fails.
3. **Given** a pair that falls below the threshold after an input change, **When** recomputation runs, **Then** the stale row is removed deterministically and the pair appears in diagnostics as pruned.
4. **Given** any topic, **When** results are queried, **Then** it never appears in its own results and never twice.

---

### User Story 3 - Idempotent, incremental, convergent recomputation (Priority: P2)

Discovery runs as a repeatable background pass that recomputes only affected pairs (input fingerprints skip unchanged ones), converges to zero delta when nothing changed, updates exactly the pairs whose inputs changed, is safe under concurrent runs and restarts, and records its outcomes (evaluated, stored, updated, pruned, near-threshold) via ProcessingJob runs.

**Why this priority**: The platform's background-processing discipline (idempotency, convergence, observability) must hold for this derived dataset too.

**Independent Test**: Run repeatedly (zero delta), change one input (exactly the affected pairs update), interrupt and restart mid-run (safe convergence), overlap concurrent runs (no duplicates/failures).

**Acceptance Scenarios**:

1. **Given** a completed pass and unchanged inputs, **When** it reruns, **Then** zero pairs are added, updated, or removed.
2. **Given** a new shared article link between two topics, **When** the next pass runs, **Then** exactly the affected pair(s) are recomputed and updated with new evidence and score.
3. **Given** interruption or concurrent runs, **When** processing resumes, **Then** the dataset converges to the same deterministic state with no duplicates or failures.

---

### User Story 4 - Replaceable strategy and a stable consumer contract (Priority: P3)

The scoring strategy sits behind a replaceable contract with a versioned method identity; the deterministic implementation is the MVP, and a future semantic/AI similarity strategy can replace or supplement it (through the provider abstraction when AI capability is involved) without changing stored models or consumers. Results are consumable through an application-level query contract (ranked, capped related topics per topic); public API exposure follows the established additive pattern when a UI feature needs it — nothing here couples consumers to persistence.

**Why this priority**: Explicitly required forward-compatibility; keeps future AI similarity additive rather than disruptive.

**Independent Test**: Register an alternative stub strategy; verify it drives the same storage/ranking/pruning machinery with its own method identity and zero consumer changes; verify the application-level contract serves deterministic results.

**Acceptance Scenarios**:

1. **Given** two strategy implementations, **When** one is selected by configuration, **Then** discovery, ranking, pruning, and idempotency behave identically through the contract with the selected method identity recorded.
2. **Given** the application-level query contract, **When** consumers request related topics, **Then** they receive ranked results with cap applied — no persistence coupling.
3. **Given** the future addition of a semantic/AI strategy, **When** it is added through the contract, **Then** no stored model or consumer contract changes are required (the seam suffices).

---

### Edge Cases

- Generic/ambiguous topics (common phrases) → conservative thresholds prevent mass qualification; behavior deterministic and tunable.
- Very short topic forms → minimum-length safeguards in token rules; no fuzzy fallbacks.
- Shared-article signal when relevance results are missing → the article is not counted for that pair (deterministic gap handling; no guessing).
- Same-cluster members also sharing articles → signals corroborate (higher score) but still one pair row (no duplication across signal sources).
- Relevance classification shifts a shared article below the exposure band → the article stops counting; affected pairs recompute and may prune deterministically.
- Topic added to a cluster after pairs exist → cluster signal appears; affected pairs update on the next pass.
- Category changes (score version shifts) → weak signal updates only; corroboration requirements may flip a pair in/out — deterministic per rulebook.
- Threshold or weight configuration change → next pass recomputes affected pairs; pruned pairs are removed and counted; results remain deterministic.
- Concurrent passes / overlap with matching, relevance, clustering, scoring passes → unique pair constraints and fingerprints resolve races; convergence holds.
- Restart mid-pass → safe re-run from the same inputs; no partial inconsistent rows (pair rows are written atomically per pair).
- A topic becoming inactive or retired (future lifecycle) → pairs involving it are pruned deterministically (derived data only; no historical obligation).
- Enormous candidate sets → bounded batches with indexed lookups; near-threshold counts and run counts keep volumes observable.
- Duplicate-looking evidence (two aliases matching, multiple shared articles) → evidence aggregated per signal class deterministically (one weighted contribution per class, counts captured).
- Semantic/AI strategy (future) scoring differently → its method identity is recorded; deterministic strategies' rows and consumers remain valid per configuration.
- Results requested for a topic with no qualifying pairs → a clean empty result (no errors, no fabrication).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST be additive: it introduces a dedicated extension entity for related-pair data and MUST NOT add related-topic ID lists (or any relatedness state) to Topic; Topic, TopicAlias, Article, link, relevance, and cluster records MUST remain read-only inputs.
- **FR-002**: Signals MUST be deterministic and single-sourced in a rulebook: (a) shared alias forms (normalized equality); (b) shared relevant articles (relevance-band gated; count-aware); (c) shared significant keywords/name tokens (conservative minimum-length and boundary rules; the same deterministic approximation established for clustering — true entity extraction remains future work); (d) same-cluster membership; (e) shared trend category (weak signal, corroboration-required — it can never qualify a pair alone).
- **FR-003**: Scoring MUST combine signal evidence deterministically (fixed-point, weights per signal class in the rulebook) with a recorded evidence breakdown per pair; identical inputs MUST always produce identical scores; every stored pair records the strategy identity and version.
- **FR-004**: Pair identity MUST be canonical (ordered pair of topic identifiers): one row per pair maximum, enforced by a unique constraint; self-pairs are impossible; concurrent runs cannot create duplicates; relationships are symmetric (one score per pair).
- **FR-005**: Ranking MUST be deterministic per topic: score descending, then defined tie-breakers (for example: shared-article count, cluster corroboration, most recent shared article, related-topic identifier); a configurable maximum result count caps results; ordering is identical across repeated runs.
- **FR-006**: Weak-relationship policy MUST be explicit: a configurable minimum score threshold gates persistence and results; below-threshold pairs are neither stored nor returned; weak-only signals never qualify (corroboration rule); pairs that fall below the threshold on recomputation are removed deterministically and counted as pruned; near-threshold pairs are counted in diagnostics for tuning.
- **FR-007**: Persistence MUST be derived-state only: each pair row stores score, evidence breakdown, strategy identity/version, computed-at (UTC), and input fingerprint; rows are fully recomputable; updates replace the previous computed state; removals are deterministic — no append-only history obligation exists for this derived dataset (distinct from domain histories).
- **FR-008**: Recomputability MUST run as a repeatable background pass added to the orchestration registry (additive job-type delta): bounded batches, restartable, concurrency-safe, with outcomes (evaluated, stored, updated, pruned, near-threshold) and per-outcome categories recorded via ProcessingJob runs.
- **FR-009**: Incrementality MUST use input fingerprints: unchanged pairs are skipped (no recomputation, no writes); recomputation triggers when material inputs change (new/removed relevant links, alias changes, cluster membership changes, category changes, rulebook version changes); convergence yields a zero-change delta when inputs are stable.
- **FR-010**: The strategy MUST sit behind a replaceable contract: the deterministic implementation is the MVP; a future semantic/AI similarity strategy can replace or supplement it (using the provider abstraction when AI capability is involved) producing the same stored artifacts with its own recorded method identity — no changes to stored models or consumers.
- **FR-011**: Results MUST be consumable through an application-level query contract (per-topic ranked related topics with caps applied, method identity visible); public API exposure is explicitly deferred and, when introduced with a future UI feature, will follow the additive versioned-contract pattern of the public API feature (no persistence coupling for consumers).
- **FR-012**: Determinism and idempotency MUST hold end to end: identical inputs produce identical pairs, scores, rankings, and pruning decisions; repeated passes after convergence produce a zero-change delta in 100% of runs.
- **FR-013**: Out of scope: UI rendering of related topics (a future frontend feature consumes the application-level contract via the topic detail page's reserved extension seam), notifications, personalization, recommendations (relatedness is context-free — no user or behavioral signals), and any changes to clustering, matching, relevance, scoring, or article semantics; no AI-based similarity in the MVP.
- **FR-014**: All timestamps MUST be UTC; no window arithmetic is involved (constitution Principle V); configuration covers weights, thresholds, caps, corroboration rules, and batch sizes (constitution Principle IV); no secrets are involved.
- **FR-015**: The feature MUST be testable offline and deterministically: fixtures cover each signal class, corroboration and threshold boundaries (in/out), ranking ties and caps, duplicate/concurrency scenarios, pruning on input change, fingerprint skipping, strategy-swap contract tests, and read-only audits of source entities.
- **FR-016**: Operations MUST be observable: pass runs record counts and outcome categories via ProcessingJob records (per the background-processing feature); failures follow the platform's error taxonomy and never leave partially inconsistent pair states (writes are atomic per pair).

### Key Entities

This feature introduces one derived-state extension entity, a rulebook, a strategy contract, and an assembly pass; all existing entities remain untouched.

- **RelatedTopicPair (new; derived state)**: one row per canonical (ordered) topic pair: score, evidence breakdown per signal class, strategy identity and version, computed-at (UTC), input fingerprint; unique pair constraint; fully recomputable — updates replace prior computed state, removals are deterministic; no historical obligation.
- **Relatedness Rulebook (configuration)**: single-sourced weights per signal class, corroboration rules (weak signals may never qualify alone), minimum thresholds, ranking tie-breakers, maximum result caps, token rules, and batch sizes — conservative defaults in the plan.
- **Related-topics strategy contract (application-service)**: produces scored related-topic candidates with evidence from a target topic and its signal context; the deterministic MVP implementation; the seam for future semantic/AI similarity (via the provider abstraction when AI is involved).
- **Related-topics assembly pass**: a repeatable background job type (added to the orchestration registry — additive delta) recomputing affected pairs via fingerprints; bounded, restartable, concurrency-safe, recorded via ProcessingJob runs with counts and outcomes.
- **Application-level query contract (consumer-facing)**: ranked, capped related topics for a topic with strategy identity visible — the only interface consumers use (public API exposure deferred to a future additive extension).
- **Read-only inputs (consumed)**: Topic and TopicAlias (trend collection), Article and Source (ingestion/deduplication), topic–article links and relevance results (matching/relevance), cluster memberships (clustering), trend categories/score records (scoring) — none modified.
- **Fixtures**: scripted signal corpora per class, threshold/corroboration boundaries, ranking ties and caps, concurrency and interruption scenarios, pruning cases, strategy-swap suites, and read-only audit harnesses.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Signal fixtures yield exactly the expected pairs per the rulebook (100%), with correct evidence breakdowns; category-only weak pairs never qualify.
- **SC-002**: Duplicate prevention: canonical pair identity yields exactly one row per pair in 100% of scenarios including concurrent runs; self-pairs never appear.
- **SC-003**: Ranking and caps: result order is deterministic with the defined tie-breakers and respects the configured maximum in 100% of fixtures, identical across repeated runs.
- **SC-004**: Weak-relationship policy: below-threshold pairs are absent from storage and results in 100% of boundary fixtures; pairs falling below threshold are pruned deterministically on recompute and counted; near-threshold counts appear in diagnostics.
- **SC-005**: Idempotency and incrementality: unchanged reruns produce a zero-change delta in 100% of runs; a single input change updates exactly the affected pairs; interruption/concurrency converges to the identical deterministic state.
- **SC-006**: Strategy seam: an alternative stub strategy passes the same contract suite with its own method identity recorded (100%); swapping requires zero consumer or stored-model changes; a future semantic strategy needs no model redesign.
- **SC-007**: Read-only and boundary audits: zero writes to Topic/alias/article/link/relevance/cluster records across all runs; a schema audit confirms zero related-ID lists or relatedness fields on Topic (100% clean).

## Assumptions

- **Derived-state semantics**: unlike domain histories (observations, scores, summaries, timeline), related-pair rows are derived state — recomputable, replaceable, and removable; the platform's append-only rules do not apply to this dataset.
- **Symmetric relationships**: one score per pair, viewed from both topics; per-topic ranking applies tie-breakers deterministically; the maximum-result cap is per topic.
- **Cluster signal scope**: members of the same cluster are related; cluster-to-cluster relationships are not modeled in the MVP.
- **Category signal weakness**: shared category alone can never qualify a pair (corroboration required); category contributes only as a weighted bonus.
- **Entity/keyword approximation**: "shared entities/keywords" is implemented as conservative significant-token overlap (the established clustering approximation); true entity extraction remains future work feeding the same signal slot.
- **Consumer path**: results are consumed via the application-level contract; the topic detail page's related-topics section is a future frontend feature that will receive an additive public API extension — nothing in this feature couples consumers to persistence.
- **No personalization**: no user context, behavioral signals, or recommendation semantics — relatedness is context-free and deterministic.
- **Configuration**: weights, thresholds, corroboration rules, caps, and cadence are configurable with conservative defaults fixed in the plan; no AI or probabilistic scoring in the MVP.
- **Operations**: the pass integrates with the existing background-processing registry (additive job type) and follows its retry/isolation/recovery semantics.

**Dependencies**: Feature 001 (core model — new derived entity mapping to be designed in the plan; Topic untouched); Feature 003 (topics/aliases as signal inputs); Features 004/005 (article identity); Feature 006 (topic–article links); Feature 007 (relevance gating for the shared-article signal); Feature 008 (cluster memberships as a signal); Feature 009 (categories as weak signals); Feature 013 (background processing — new job type, ProcessingJob observability); Feature 014 (the additive public-contract pattern that any future API exposure must follow); Feature 018 (topic detail page — the reserved related-topics extension seam this feature's contract will eventually serve); Project Constitution v1.0.0 (Principles I — replaceable strategies, III — no UI/personalization scope creep, IV — configuration-driven rulebook, V — UTC and no window math); deterministic offline fixtures for testing.
