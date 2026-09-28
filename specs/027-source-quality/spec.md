# Feature Specification: Source Quality Metadata (Configurable, Transparent, Versioned)

**Feature Branch**: `027-source-quality`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement configurable source-quality metadata as an extension of the existing Source model. The system may store operational and configurable quality signals such as source type, crawl health, freshness, duplicate behavior, editorial category, and administrator-defined trust configuration. Do not modify Source with a simplistic permanent objective truth score. If a calculated quality score is required, store it as a versioned extension model with transparent inputs and calculation method. Source quality may influence article ranking or filtering, but it must not silently remove sources or articles without an explicit configured rule. Keep this feature independent from content moderation and misinformation detection."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configurable quality metadata as extension entities (Priority: P1)

Quality information lives in dedicated extension entities — never as fields on Source. Administrators configure per-source metadata: source type (from a configurable taxonomy), editorial category (from a configurable list), and an explicit trust configuration (tier or label with recorded rationale). Operational and behavioral signals — crawl health, freshness, duplicate behavior — are consumed from existing data (operational state, publication times, deduplication statistics) rather than duplicated into new storage. The Source model itself remains untouched, with no simplistic permanent objective truth score.

**Why this priority**: Metadata is the foundation every other quality capability consumes; the no-score-on-Source constraint is the user's core design requirement.

**Independent Test**: Profile fixtures (assign/edit each metadata field, defaults for unconfigured sources); audits confirming Source has zero quality fields and existing operational data is reused, not duplicated.

**Acceptance Scenarios**:

1. **Given** an administrator assigns a source type, editorial category, and trust tier with rationale, **When** the profile is queried, **Then** exactly those values are returned with who/when recorded, and nothing changed on the Source model.
2. **Given** a source with no profile metadata, **When** any consumer reads its quality information, **Then** documented neutral defaults apply (never an implicit penalty or assumption).
3. **Given** crawl health, freshness, or duplicate behavior is needed, **When** signals are read, **Then** they come from existing operational and article data (no duplicated stored copies).

---

### User Story 2 - Transparent versioned quality score (Priority: P1)

If a calculated quality score is used, it is an optional, versioned extension model: each score record carries its algorithm identity and version, the full input breakdown (signal values and weights), the calculation timestamp (UTC), and an input fingerprint; scores are append-only, deterministic, and recalculable; algorithm versions coexist; and every stored score can answer "why" — its inputs and method are transparent on inspection and its value reproduces exactly from the stored inputs.

**Why this priority**: The user explicitly requires transparent inputs and calculation method if a score exists; this is the anti-"black-box truth score" safeguard.

**Independent Test**: Reproducibility fixtures (same inputs + method → identical score offline); version-coexistence fixtures (old scores readable and explainable after a version change); fingerprint convergence tests.

**Acceptance Scenarios**:

1. **Given** stored score inputs and its method version, **When** the score is recomputed offline, **Then** the identical value results (full reproducibility).
2. **Given** a stored score, **When** an administrator inspects it, **Then** the input breakdown, weights, method version, and fingerprint are all shown.
3. **Given** a method version change, **When** new scores are calculated, **Then** new and existing scores coexist with their respective versions, each explainable on its own terms.

---

### User Story 3 - Explicit configured influence, no silent removal (Priority: P1)

Source quality may influence article ranking or filtering, but only through named, explicitly configured rules with deterministic identity and a neutral default: with no rules enabled, behavior is exactly as today. Any exclusion of a source or its articles from a surface is traceable to a specific configured rule and its inputs; nothing is ever silently hidden or removed; quality never disables or retires a source (that remains an explicit administrative action) and never removes attribution of any article or source.

**Why this priority**: The user's strongest constraint — no silent removal, explicit rules only — and the guarantee that makes the whole feature safe.

**Independent Test**: Neutral-default fixtures (no rules → identical behavior), rule-application fixtures (ranking/filtering effects with recorded rule identity), and zero-silent-removal audits across every surface.

**Acceptance Scenarios**:

1. **Given** no influence rules are enabled, **When** any surface is composed, **Then** ordering and inclusion are identical to the quality-agnostic behavior (neutral default).
2. **Given** an enabled filtering rule, **When** an article is excluded from that surface, **Then** the exclusion is traceable to the rule and its inputs, is visible to administrators, and affects only the rule's configured scope.
3. **Given** any quality value, **When** attribution is rendered, **Then** every source attribution (including disabled or low-signal sources) remains exactly as preserved today.

---

### User Story 4 - Administrator governance and management (Priority: P2)

All quality management flows through the established admin boundary: editing profile metadata (type, category, trust configuration with rationale, who/when recorded), inspecting calculated scores with their breakdowns, configuring scoring (enablement, thresholds, method version) and influence rules (create, update, enable, disable). Every operation is logged (actor, operation, target, timestamp, outcome), non-administrators are rejected, and effects are prospective — configuration changes apply to subsequent calculations and read compositions with no retroactive rewriting and predictable reversibility.

**Why this priority**: Without governed management, the configured metadata and rules cannot be trusted or audited.

**Independent Test**: Admin-operation fixtures (create/edit/enable/disable, rejection of non-admin callers, complete logging), prospective-effect tests (change → subsequent behavior; disable → neutral restored), and no-leakage audits (zero internal quality exposure on public surfaces).

**Acceptance Scenarios**:

1. **Given** an administrator updates trust configuration or a rule, **When** the change is saved, **Then** it applies to subsequent computations and compositions, is fully logged, and prior records remain explainable under their recorded versions.
2. **Given** a non-administrator, **When** any quality management operation is attempted, **Then** it is rejected by the established authorization boundary with zero effect.
3. **Given** internal quality signals exist, **When** public surfaces are audited, **Then** zero internal signals or trust labels appear.

---

### User Story 5 - Determinism, graceful degradation, and independence from moderation (Priority: P3)

Quality behavior is deterministic: identical inputs and configuration produce identical outcomes across runs, score calculations converge (fingerprint-gated, idempotent), and quality systems can never block ingestion or public reads — when scores, rules, or metadata fail or are absent, surfaces degrade to neutral behavior rather than erroring or blanking. The feature is explicitly independent from content moderation and misinformation detection: it operates on source-level configuration and operational/behavioral signals only, never on article-content truthfulness, and provides no truth or misinformation verdicts of any kind.

**Why this priority**: Determinism and the moderation-independence boundary keep the feature honest and safe; they underpin trust in everything above.

**Independent Test**: Convergence fixtures (repeat runs), degradation fixtures (score/rule failures → neutral composition, ingestion and reads unaffected), and scope audits (no moderation/misinformation/truth-verdict logic; no content-based judgments).

**Acceptance Scenarios**:

1. **Given** identical inputs and configuration, **When** calculations and compositions run repeatedly, **Then** outcomes are identical and convergent (no drift, no duplicates).
2. **Given** a failing or disabled scoring/rule subsystem, **When** ingestion and public reads run, **Then** they proceed unaffected and surfaces render with neutral quality behavior.
3. **Given** a code and behavior audit, **Then** zero content moderation, misinformation detection, or truth-verdict logic exists in this feature.

---

### Edge Cases

- Source with no profile metadata → neutral defaults everywhere (no implicit penalty, no implicit promotion); metadata absence is never a quality statement.
- Two enabled rules affect the same surface for the same article → documented deterministic precedence (rule identity as tie-breaker) with a fixture proving stable, non-ambiguous outcomes.
- Rule references a retired or unknown source → deterministic no-op for that source with a logged warning; composition never errors.
- Administrator edits trust configuration while a score calculation is running → the calculation completes with the configuration version it started from (recorded in its inputs); the edit applies prospectively.
- Source with no recent articles → freshness signal resolves per documented neutral handling; it is never automatically interpreted as low quality.
- Aggregator-style source that legitimately republishes syndicated content → duplicate-behavior signal is interpreted via configuration (source type informs weights) rather than an automatic penalty.
- Crawl health degraded (repeated fetch failures) → treated as an operational signal, not a quality verdict; any effect requires an explicit configured rule.
- Exclusion rule narrows one surface (e.g., search ordering) → other surfaces, topic pages, attribution, and discovery links remain unaffected; re-disabling the rule restores prior behavior.
- Score inputs include an article that is later merged/deduplicated (link model preserved) → the input fingerprint is defined over material inputs only; recomputation is deterministic and convergence-safe.
- Calculated-score history grows large → configurable retention/compaction preserves explainability for retained records and version integrity for references.
- Public read composed while a rule/config change is fresh → freshness tokens invalidate affected cached compositions deterministically; unrelated caches remain untouched (no cross-effect leakage).
- Quality subsystem completely unavailable during ingestion → ingestion proceeds exactly as without the feature (per-source isolation unchanged).
- Administrator tries to express "hide everything from this source everywhere" → only the explicitly configured rule scopes apply; a platform-wide silent removal is impossible by design (deterministic rejection/documentation of unsupported scopes).
- Accuracy-related dispute: stale metadata attributed to the wrong source entry → edits are logged with actor/target/outcome so corrections are traceable; no automatic correction path exists.
- Non-Latin or multilingual source names in taxonomy labels → stored safely; taxonomy label localization is a documented future seam, not implemented here.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Quality information MUST live in dedicated extension entities — a per-source quality profile (latest-state config metadata), an append-only calculated-score model, and persisted influence rules — with the Source model completely unchanged (zero quality fields on Source) and zero modifications to any other existing entity; operational signals are reused from existing data, never duplicated into new storage.
- **FR-002**: The signal catalog MUST support: source type (configurable taxonomy), editorial category (configurable list), administrator-defined trust configuration (explicit tier/label plus recorded rationale, manually assigned), crawl health (reused from the existing operational state), freshness (derived from existing publication data), and duplicate behavior (derived from existing deduplication/aggregation data); the configured-versus-calculated distinction is documented per signal, and derived signals are computed from source data on read/calculation rather than stored as duplicated mutable fields.
- **FR-003**: A simplistic permanent objective truth score MUST NOT be added to Source (or anywhere): quality is configurable metadata plus optional transparent derived scores; administrator trust configuration is always presented as explicit configuration — never as objective truth — and the system never computes a truthfulness verdict.
- **FR-004**: The calculated score MUST be optional (enable/disable by configuration), deterministic (fixed-point rules with documented rounding), versioned (algorithm identity and version recorded per score), append-only and recalculable (fingerprint-gated: unchanged material inputs produce no new records; unique identity per source + version + fingerprint), with algorithm versions coexisting; it writes to no other entity.
- **FR-005**: Score transparency MUST be total: every stored score carries its full input breakdown (signal values and weights), method identity and version, calculation timestamp (UTC), and fingerprint; recomputation from stored inputs reproduces the score exactly offline; every score is explainable to administrators on inspection.
- **FR-006**: Influence on ranking or filtering MUST occur only through named, persisted rules with deterministic identity, a configured scope (surface class), a typed effect, parameters, and an enabled state; with no rules enabled behavior is identical to today (neutral default); effects follow documented deterministic semantics; the applied rule identity is recorded per application (counts in calculation/job records; request-time compositions use the active rule-set version).
- **FR-007**: No silent removal MUST be guaranteed: sources or articles are never hidden or excluded from any surface without an explicit configured rule; every exclusion is traceable to its rule and inputs and visible to administrators; platform-wide silent removal is impossible; quality never disables or retires sources (source lifecycle remains an explicit administrative action under the existing administration rules).
- **FR-008**: Attribution integrity MUST be absolute (constitution principle II): quality of any value never removes or degrades attribution; excluded items on a surface keep their attribution everywhere else; disabled or retired sources' attribution remains exactly as preserved today.
- **FR-009**: Quality management MUST flow through the established administrative boundary: profile editing (type, category, trust configuration with rationale; updated-by/at recorded), score inspection with breakdowns, scoring configuration (enablement, thresholds, method version), and rule management (create/update/enable/disable); every operation is logged (actor, operation, target, timestamp, outcome) and non-administrative callers are rejected with zero effect.
- **FR-010**: Effects MUST be prospective and reversible: configuration, rule, and metadata changes apply to subsequent calculations and read compositions per documented freshness rules; no retroactive history rewriting; disabling scoring or rules deterministically restores neutral behavior; cached surfaces invalidate via freshness signals without cross-effect leakage.
- **FR-011**: Graceful degradation MUST hold: failures, absence, or disablement of scoring, rules, or metadata never block ingestion or public reads; affected surfaces degrade to neutral quality behavior (never errors or blank output); per-source ingestion isolation is unchanged.
- **FR-012**: The feature MUST be independent from content moderation and misinformation detection: signals are source-level (configuration, operational, behavioral) only — never article-content judgments; no flagging, no truth verdicts, no misinformation labels; these domains remain explicitly outside this feature.
- **FR-013**: Extensibility MUST follow the platform pattern: new signals attach behind a signal-provider contract and new rule effects behind a rule contract (strategy seams, no schema redesign for additions within the contracts); taxonomies, categories, thresholds, and weights are configuration.
- **FR-014**: Score recalculation MUST be a registered background job type (platform scheduler delta) with single-flight, idempotent, fingerprint-convergent semantics and recorded runs/counts; rule-dependent recomposition hooks follow the same discipline.
- **FR-015**: Observability MUST follow platform rules (constitution-aligned): administrator operations, rule applications/influences (counts), and score calculation runs are logged and metered with bounded labels; redaction applies (no article content in logs; source identifiers only per policy).
- **FR-016**: The feature MUST be testable offline and deterministically: fixtures for score reproducibility, version coexistence, influence semantics and rule-identity recording, neutral defaults, zero-silent-removal audits, degradation behavior, and no-public-leakage audits.
- **FR-017**: Out of scope: content moderation, misinformation detection, content-quality or truthfulness judgments of any kind, automatic source disabling/retiring, public exposure of internal quality signals or trust labels (a future seam requiring its own transparency policy), per-article quality typing, changes to the source lifecycle or attribution semantics, and localization of taxonomy labels (a future multilingual seam).

### Key Entities *(include if feature involves data)*

All new capability lives in extension entities; existing entities are reused read-only where noted and never modified.

- **Source (unchanged)**: zero quality fields; the no-truth-score prohibition applies to it directly; lifecycle and attribution semantics unchanged.
- **SourceOperationalState (reused)**: supplies the crawl-health signal read-only; not duplicated.
- **Article / deduplication data (reused read-only)**: supplies freshness (publication times) and duplicate-behavior (aggregation/dedup statistics) signals at computation time.
- **SourceQualityProfile (new, latest-state)**: per-source configured metadata — source type (config taxonomy), editorial category (config list), trust configuration (tier/label + rationale), notes; updated-by/at recorded; updates replace (history via logs, consistent with the operational-state pattern).
- **SourceQualityScore (new, append-only)**: source reference, score, algorithm identity/version, calculated-at (UTC), input fingerprint, full input breakdown (signal values + weights); unique per (source, version, fingerprint); recalculable and explainable.
- **SourceQualityInfluenceRule (new, persisted config)**: deterministic identity, name, scope (surface class), typed effect (ranking adjustment or filtering), parameters, enabled state, activation records; effects reference the rule identity on application.
- **Signal & rule contracts (interfaces)**: signal-provider contract (add signals without schema redesign) and rule-effect contract (add rule types deterministically).
- **Taxonomy configuration (no entity)**: source-type list, editorial-category list, thresholds, weights, scoring enablement.
- **Scheduler integration**: score recalculation registered as a job type with recorded runs/counts.
- **Fixtures & harnesses**: reproducibility sets, influence/neutrality cases, zero-silent-removal audits, degradation scenarios, no-leakage audits.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Score transparency: 100% of stored scores reproduce exactly from their stored inputs and method version (offline verification); every score answers "why" with its full breakdown on inspection.
- **SC-002**: No silent removal: audits across all surfaces find zero exclusions without a traceable enabled rule; neutral defaults verified with no rules enabled (behavior identical to today).
- **SC-003**: Attribution integrity: 100% of attributions preserved under every quality configuration tested (including disabled/retired sources and excluded-from-surface items).
- **SC-004**: Influence correctness: rule applications match documented deterministic semantics (including precedence) in 100% of fixtures; the applied rule identity is recorded for 100% of applications; scope isolation verified (no unintended surface affected).
- **SC-005**: Governance: 100% of administrative operations logged with actor/target/outcome; non-administrative callers rejected in 100% of probes; prospective-effect and reversibility (neutral restore) verified; zero internal-signal leakage on public surfaces (audits).
- **SC-006**: Degradation and convergence: zero ingestion or public-read failures attributable to quality systems in failure/disable fixtures; repeated identical runs converge with no drift or duplicate score records.
- **SC-007**: Scope and schema audits: Source model unchanged (zero quality fields); zero moderation, misinformation, or truth-verdict logic; version coexistence verified after a method change (old scores remain readable and explainable).

## Assumptions

- **Operational reuse**: crawl health comes from the existing source operational state; freshness and duplicate behavior are derived at computation time from existing article and deduplication data — this feature adds no duplicated copies of those facts.
- **Trust configuration is manual and explicit**: administrator-assigned tier/label with recorded rationale; it is configuration, never an objective truth claim; no automatic trust adjustments exist.
- **Score is optional**: profile metadata, signals, and rules work without calculated scores; scoring enablement is configuration; when enabled, score history is append-only with configurable retention.
- **Influence scope**: initial rule scopes cover deterministic composed surfaces (e.g., search result ordering, article list ordering) as detailed in the plan; trending composition is neutral by default; no rule can apply platform-wide silent removal (unsupported scopes are rejected/documented).
- **No automatic lifecycle effects**: quality never disables, retires, or deletes sources; all lifecycle actions remain explicit administrative operations under the existing source administration rules.
- **Public exposure deferred**: internal signals and trust labels stay admin-only; any future public disclosure is a separate feature with its own transparency and policy review.
- **Localization seam**: taxonomy label localization interacts with the multilingual feature only as a future seam; labels are data and remain language-neutral identifiers internally.
- **Alerting/dashboards**: operational visibility of quality jobs and rule applications rides the platform's existing observability surfaces; no new alerting system here.
- **Out of scope**: content moderation, misinformation detection, content truthfulness judgments, per-article quality, automatic source lifecycle actions, and any "truth score" presentation.

**Dependencies**: Feature 001 (core model — new extension-entity mappings; Source and all existing entities untouched); Feature 004 (ingestion — operational behavior supplies duplicate/freshness inputs; ingestion isolation never blocked by quality); Feature 005 (deduplication — aggregation statistics as signal input; attribution/discovery links untouched); Feature 006/007 (matching/relevance — ranking influence seams on composed article lists); Feature 009 (versioned-score pattern — algorithm identity, fingerprints, append-only recalculable history as the established precedent); Feature 013 (scheduler — recalculation registered as a job type); Feature 014 (public contract — unchanged; quality never alters public shapes; future disclosure seam flagged); Feature 015 (search — ordering-influence scope); Feature 016 (caching — freshness tokens invalidate affected compositions, no cross-effects); Feature 021 (source administration — operational state reuse, lifecycle authority, admin boundary, logged operations patterns); Feature 022 (admin console — management and inspection surfaces extend the existing console); Feature 023 (observability — logged operations, bounded-label metrics, redaction); Feature 026 (multilingual — taxonomy label localization as future seam); Project Constitution v1.0.0 (Principle II — attribution always preserved; Principle III — explicit minimal scope, no moderation creep, no black-box scores; Principle IV — taxonomies, weights, thresholds, and rule enablement as configuration; no secrets in code or logs).
