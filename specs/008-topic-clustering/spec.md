# Feature Specification: Topic Clustering (Deterministic, Extension Entities)

**Feature Branch**: `008-topic-clustering`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement topic clustering as an extension of the stable Topic model. Multiple Topics or trend keywords may refer to the same real-world event or story. The system must be able to group related topics without changing the fundamental Topic entity. Use dedicated extension entities such as TopicCluster and TopicClusterMember rather than adding cluster-specific fields directly to Topic. The MVP should use deterministic rules based on normalized names, aliases, shared articles, entities, and other available signals. Define rules for creating, updating, merging, and separating clusters. Do not implement AI semantic clustering yet, but provide an extension point for a future AI-based strategy. Clustering must not destroy the original Topic or TrendObservation records because historical trend observations must remain available."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Group related topics into clusters (Priority: P1)

A deterministic pass groups Topics that refer to the same real-world event or story into clusters stored in new extension entities (**TopicCluster** and **TopicClusterMember**), leaving the Topic entity and every existing record (aliases, trend observations, links, relevance) untouched. Each membership records its provenance, and every topic is either unclustered or in at most one active cluster.

**Why this priority**: This is the feature's core value — several trend keywords about one story must be understood as one event — and the user's primary constraint that the stable Topic model must not change.

**Independent Test**: Run the pass over a fixture set of topics connected by qualifying signals; verify the expected cluster and memberships with provenance, and that Topic, alias, observation, link, and relevance records were not written.

**Acceptance Scenarios**:

1. **Given** two topics whose normalized names/aliases are equivalent forms, **When** the pass runs, **Then** they appear as members of one cluster with recorded provenance.
2. **Given** two topics that share enough relevant articles, **When** the pass runs, **Then** they are deterministically grouped under the shared-article signal.
3. **Given** a topic with no qualifying connection to any other topic, **When** the pass runs, **Then** it remains unclustered (never forced into a cluster).
4. **Given** a completed pass, **When** it reruns with unchanged inputs, **Then** nothing changes.

---

### User Story 2 - Deterministic signals, thresholds, and rulebook (Priority: P1)

Clustering uses deterministic signals defined in a single rulebook: normalized name/alias equivalence (whole-form equality), shared relevant articles (topics linked to the same article at or above a configured relevance band), and conservative significant-token overlap rules (minimum token lengths, boundary discipline, stopword handling). Acceptance semantics, thresholds, and cluster naming (a deterministic member election) are all defined once, recorded as evidence, and reproducible.

**Why this priority**: The user requires deterministic rules; trust, debuggability, and tuning all depend on recorded, reproducible evidence.

**Independent Test**: Fixtures per signal class and across threshold boundaries; verify exact grouping outcomes, recorded evidence per membership, deterministic cluster naming, and full reproducibility across reruns.

**Acceptance Scenarios**:

1. **Given** boundary traps (short tokens inside unrelated words), **When** rules evaluate, **Then** no connection is accepted (no fuzzy fallbacks).
2. **Given** identical inputs, **When** the pass runs repeatedly, **Then** cluster membership, naming, and evidence are identical every time.
3. **Given** a threshold or weight configuration change, **When** the next pass runs, **Then** the change applies deterministically in a single reconciliation outcome.
4. **Given** any accepted connection, **When** it is recorded, **Then** the fired rule and its evidence (matched forms, shared article references, token overlap) are stored on the membership.

---

### User Story 3 - Deterministic cluster lifecycle: create, update, merge, separate (Priority: P2)

Reconciliation semantics define what happens as data changes: a new qualifying topic connecting to an existing cluster becomes a member (update); a connection bridging two active clusters merges them into a single terminal cluster (the other is retired via lifecycle state with a merged-into pointer, memberships migrated with history); losing connectivity partitions deterministically and separates disconnected members (memberships closed with reasons, history preserved); nothing is ever deleted, and historical trend observations remain fully available regardless of clustering actions.

**Why this priority**: The user explicitly requires create/update/merge/separate rules and the non-destructiveness of topics and trend observations.

**Independent Test**: A scripted fixture timeline (grow → bridge → rule change) verifying deterministic create/update/merge/split outcomes and preserved history at every step.

**Acceptance Scenarios**:

1. **Given** a qualifying new topic, **When** the pass runs, **Then** exactly one active membership is added to the relevant cluster (idempotent on rerun).
2. **Given** an accepted connection bridging two active clusters, **When** the pass runs, **Then** a single terminal cluster remains, the other cluster is retired with a merged-into pointer, and all membership history is queryable.
3. **Given** merge chains across passes, **When** clusters are queried, **Then** every chain resolves to one terminal cluster deterministically.
4. **Given** a rule change that disconnects members, **When** the pass runs, **Then** the cluster partitions deterministically, separated memberships are closed with reasons, and new memberships/clusters are created only where the rules require.
5. **Given** any lifecycle action, **When** history is queried, **Then** all prior memberships, retired clusters, topics, aliases, and trend observations remain available and unchanged.

---

### User Story 4 - Replaceable strategy with an AI clustering seam (Priority: P3)

The clustering algorithm sits behind a replaceable strategy contract; the deterministic rulebook implementation is the only MVP implementation. A future AI-based semantic clustering strategy can plug in through the same contract (and, when it needs AI capabilities, through the existing provider abstraction), producing the same cluster/membership artifacts with its own method identity — without changing the core model or requiring any AI capability to access existing data.

**Why this priority**: Required as an extension point only; building AI clustering now would violate the deterministic MVP.

**Independent Test**: Register an alternative stub strategy; verify it drives the same reconciliation, constraint, and history machinery with zero core-model changes, and that the MVP build contains no AI/ML dependencies.

**Acceptance Scenarios**:

1. **Given** an alternative clustering strategy registered, **When** the pass runs, **Then** artifacts are produced with the strategy's method identity through the same storage and history machinery.
2. **Given** the MVP build, **When** dependencies and the Topic entity definition are audited, **Then** there are zero AI/ML dependencies and zero changes to the Topic model.

---

### Edge Cases

- Identical normalized topic names cannot occur (core uniqueness), but aliases can collide across topics — name/alias equivalence remains a strong deterministic signal in both directions.
- Very short topic names or tokens → minimum-length safeguards in token rules; punctuation-only forms never connect.
- Generic or ambiguous names → conservative thresholds prevent mass grouping; behavior stays deterministic and tunable.
- Shared-article signal when relevance results are missing for a pair → the article is not counted for that pair (deterministic data-gap handling; no guessing).
- A topic qualifying for two clusters at once (bridge) → merge semantics; a topic never holds two active memberships.
- Cluster reduced to one member (dissolution threshold) → the cluster dissolves deterministically and the membership closes with a reason; single-member clusters are never maintained.
- Retired (merged) clusters receiving new signals later → signals resolve to the terminal cluster; retired clusters are never resurrected and chains stay flattened.
- Rule change causing many merges/splits at once → one deterministic reconciliation outcome per pass; no partial intermediate states escape a run.
- Concurrent passes or overlap with matching/relevance passes → unique constraints prevent duplicate active memberships; conflicts resolve deterministically.
- Trend observations of member topics after any merge/split → remain present and unchanged; clustering never touches observation history.
- Cluster naming after membership changes (merge/split) → the canonical name is recomputed by the deterministic election at reconciliation time; no hidden rename history is required in the MVP.
- A topic leaving all clusters (correction) → it simply becomes unclustered; prior membership rows remain closed and queryable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Clustering MUST be additive: two new extension entities — **TopicCluster** and **TopicClusterMember** — carry all cluster data; no cluster-specific fields may be added to Topic, and Topic, TopicAlias, TopicTrendObservation, Article, links, and relevance records MUST NOT be modified by this feature.
- **FR-002**: Clustering MUST run as background processing (recorded as a ProcessingJob per run) and MUST be repeatable and restartable; each run records counts (topics evaluated, pairs connected, clusters created/updated/merged/split/dissolved, memberships added/closed) with UTC timings and an outcome category.
- **FR-003**: Signals MUST be deterministic and defined once in a single rulebook: (a) normalized name/alias whole-form equivalence; (b) shared relevant articles — an article counts only when it is linked to both topics and the relevance evaluation meets a configured band; (c) conservative significant-token overlap rules (minimum token lengths, boundary discipline, stopword handling); thresholds and boundary semantics are configurable and single-sourced.
- **FR-004**: Pair acceptance semantics MUST be deterministic: any qualifying rule acceptance connects a topic pair; clusters are the connected components of accepted pairs; the combined evidence for each membership MUST be recorded.
- **FR-005**: Membership constraints MUST be enforced at the data layer: at most one ACTIVE membership per topic; unclustered is a valid state; single-member components dissolve rather than form clusters.
- **FR-006**: Cluster identity MUST include a deterministic canonical display name derived from a rulebook-defined member election, and a method identity with version; the election and naming MUST be identical across identical runs.
- **FR-007**: Membership provenance MUST record: fired rule, evidence (matched forms, shared article references/counts, token overlap), confidence, method identity, and added-at time (UTC); results MUST be reproducible.
- **FR-008**: Creation rule: a connected component of two or more topics not covered by any active cluster MUST produce a new cluster with its memberships, deterministically.
- **FR-009**: Update rule: a qualifying connection to an existing cluster MUST add the membership idempotently (no duplicate active memberships); unchanged inputs MUST add nothing (zero-delta convergence).
- **FR-010**: Merge rule: an accepted connection bridging two active clusters MUST merge them into one terminal cluster — the canonical cluster is elected deterministically (earliest created, tie-break by identifier); the other is retired via lifecycle state with a merged-into pointer (never deleted); merged chains always resolve to the terminal cluster; memberships migrate with full history (closures with reasons plus new registrations, all timestamped in UTC).
- **FR-011**: Separation rule: recomputation partitions members per the current rulebook; disconnected members MUST be separated deterministically (memberships closed with reasons; new clusters or memberships created only where the rules require); the surviving cluster retains its deterministic core; clusters below two members dissolve.
- **FR-012**: Nothing may be destroyed: topics, aliases, trend observations, articles, links, relevance records, clusters, and membership history MUST NOT be deleted; every historical trend observation MUST remain available regardless of clustering actions.
- **FR-013**: Clustering MUST be idempotent, convergent, and concurrency-safe: unique constraints (one active membership per topic; membership identity) prevent duplicates under concurrent or overlapping runs; conflicts resolve deterministically and never fail a run; after inputs stabilize, repeated runs produce a zero-change delta.
- **FR-014**: The algorithm MUST sit behind a replaceable clustering-strategy contract; the deterministic implementation is the sole MVP strategy; a future AI-based semantic strategy MUST integrate through the contract (and the provider abstraction for any AI capability) producing the same artifacts with its own method identity, without core-model changes.
- **FR-015**: This feature MUST NOT implement AI semantic clustering, trend scoring, link or relevance changes, presentation decisions, or manual curation tooling; it consumes matching (links) and relevance results as signals only.
- **FR-016**: Thresholds, token rules, relevance-band gating, batch sizes, and pass cadence MUST be configurable without code changes; all timestamps are UTC with no window arithmetic (constitution Principle V); behavior MUST be testable offline with deterministic fixtures covering signals, creation/update/merge/split, idempotency, concurrency, and history.

### Key Entities

This feature is purely additive: two new extension entities plus a rulebook and run records; the stable Topic model is untouched.

- **TopicCluster (new extension entity)**: one cluster per real-world grouping of topics: deterministic canonical display name (rulebook election), lifecycle state (active/retired), method identity and version, merged-into pointer for retired clusters (chains flatten to a terminal cluster), created/updated timestamps (UTC), optional metadata.
- **TopicClusterMember (new extension entity)**: one row per membership episode: cluster reference and topic reference; provenance (fired rule, evidence — matched forms, shared article references, token overlap — confidence, method identity); added-at and ended-at timestamps (UTC); closure reason (for example: cluster-merged, separated, dissolved); at most one ACTIVE membership per topic (data-layer constraint); closed rows are permanent history.
- **Clustering Strategy contract (application-service/strategy)**: given topics, aliases, links, and relevance results, produces connection decisions and reconciliation artifacts; the deterministic rulebook strategy is the MVP; the seam through which future AI semantic clustering integrates.
- **Cluster Rulebook**: the single shared definition of signals, thresholds, boundary semantics, acceptance rules, canonical naming/election, dissolution, and merge/split semantics (configuration-backed).
- **Clustering Run**: a ProcessingJob record per pass with counts, outcome category, UTC timings, and diagnostics.
- **Read-only inputs (core concepts used)**: Topic, TopicAlias, TopicTrendObservation, topic–article links, and ArticleRelevance results — read as signals; never written by this feature.
- **Fixtures**: deterministic offline corpora for signal rules, threshold boundaries, merges, splits, dissolution, idempotency, concurrency, and history verification.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Signal fixtures produce exactly the expected clusters and memberships with recorded provenance, and all runs together perform zero writes to Topic, alias, observation, link, or relevance records (100%).
- **SC-002**: Identical inputs produce identical cluster membership, naming, and evidence across reruns in 100% of cases; after convergence, repeated passes show a zero-change delta.
- **SC-003**: Merge fixtures resolve to a single terminal cluster per chain with the retired cluster and full membership history queryable; zero deletions occur (100%).
- **SC-004**: Split fixtures produce the deterministic partition with separated memberships closed with reasons and correct new memberships/clusters (100%).
- **SC-005**: Concurrency simulations (parallel passes, overlap with matching and relevance passes) yield at most one active membership per topic, no duplicates, and no unhandled errors in 100% of scenarios.
- **SC-006**: After every cluster lifecycle scenario, 100% of TopicTrendObservation records remain present and unchanged.
- **SC-007**: An alternative clustering strategy stub integrates with zero core-model changes; the build contains zero AI/ML dependencies; a schema audit shows zero diffs on the Topic entity definition.

## Assumptions

- **Group semantics**: a topic is either unclustered or in at most one cluster in the MVP; clusters require at least two members; unclustered is a first-class valid state.
- **Clustering scope**: clusters group Topics (trend keywords are Topics per the trend-collection feature); no separate keyword entity exists.
- **Signal availability**: the shared-article signal depends on matching links and relevance evaluations; pairs lacking relevance results simply do not fire this signal (deterministic gap handling); true entity extraction is a future capability — the MVP approximates entity-like evidence with significant name/alias tokens, and the strategy seam allows richer signals later without schema changes.
- **Input immutability**: topics, aliases, and articles are append-only or immutable in the MVP, so connectivity mostly grows; splits arise from rule or configuration changes and corrections, and are still fully specified.
- **Determinism**: elections, canonical naming, partitions, merge winners, and dissolution are all rulebook-defined with no randomness; identical runs are byte-identical in outcome.
- **Non-destructiveness**: clusters and memberships are never deleted; retirement and closures are lifecycle states with reasons; all history remains queryable; trend observations are entirely out of scope for modification.
- **Reconciliation semantics**: each pass recomputes connections and maps results onto existing clusters — set-equal components are no-ops, supersets add memberships, bridges merge with retirement, shrinkage splits or dissolves; the plan phase fixes the exact algorithm while this spec fixes the semantics.
- **Naming**: the canonical cluster name comes from the deterministic member election at reconciliation time; manual naming/curation is out of scope.
- **Defaults**: conservative thresholds and token rules are fixed in the plan phase and configurable afterwards.
- **Concurrency approach**: unique constraints are the backstop; conflicts resolve in code deterministically; no locks or distributed coordination; single-module background pass.
- **Out of scope**: AI semantic clustering (seam only), trend scoring, link/relevance modification, presentation decisions, and manual curation tooling.

**Dependencies**: Feature 001 (core domain & persistence model — Topic, TopicAlias, TopicTrendObservation read-only; new extension entity mappings to be designed in the plan); Feature 002 (background-processing patterns and the AI provider abstraction for the future strategy seam); Feature 003 (topic inventory as clustering input); Feature 005 (deduplicated article identity underlying the shared-article signal); Feature 006 (topic–article links as signal input); Feature 007 (relevance evaluations gating the shared-article signal); Project Constitution v1.0.0 (Principles I — replaceable strategies, II — attribution and preservation, III — deterministic MVP, IV — configuration, V — UTC/time handling); deterministic offline fixtures for testing.
