# Feature Specification: Article Deduplication (Deterministic Identity and Source Linking)

**Feature Branch**: `005-article-deduplication`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement article deduplication using the existing Article and Source core model. The system must detect duplicate articles using canonical URLs, normalized URLs, content hashes, and other deterministic metadata when appropriate. Handle the case where the same article is discovered through multiple RSS sources without creating duplicate Article records. Preserve relationships between an Article and all relevant Sources rather than discarding source information. Define database constraints and indexes required to make deduplication reliable under concurrent ingestion. The solution must be deterministic, idempotent, and safe for repeated background processing. Do not introduce speculative AI-based duplicate detection in the MVP. Keep an extension point for future semantic duplicate detection without changing the Article core model."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prevent duplicate Article records across sources (Priority: P1)

The same article discovered through multiple RSS sources — or repeatedly through URL variants — results in exactly one Article record. Every discovering source is preserved as a relationship carrying its discovery URL and first-seen time, and deduplication runs inline within ingestion before persistence.

**Why this priority**: This is the feature's core value. Duplicate Article records corrupt every downstream view and feature (topic matching, summaries, scores); the platform must never create them in the first place.

**Independent Test**: Ingest fixtures of the same article arriving via multiple sources and URL variants (canonical-URL, normalized-URL, and content-hash matches); verify exactly one Article with one discovery link per source and full attribution preserved.

**Acceptance Scenarios**:

1. **Given** an article already stored, **When** the same item arrives via a second source under a different URL, **Then** no new Article is created and a discovery link attaches the second source with its discovery URL and first-seen time.
2. **Given** the same item arriving twice from the same source (URL variants), **When** it is processed, **Then** one article and one discovery link for that source exist, and re-processing changes nothing further.
3. **Given** an article discovered via several sources over time, **When** it is queried, **Then** its primary attribution is the earliest discovery and every discovering source's relationship is retained.

---

### User Story 2 - Reliable under concurrent ingestion (Priority: P1)

Database constraints and indexes on the identity keys (canonical URL, normalized URL, content hash, and article-source discovery pairs) make deduplication reliable when ingestion runs concurrently: racing inserts cannot create duplicate records, and any constraint conflict resolves deterministically by re-matching and linking instead of failing the run.

**Why this priority**: Ingestion is scheduled to run concurrently and retried; idempotency and race-safety must be enforced by the data layer (constitution: uniqueness guarantees), not by ad-hoc application checks.

**Independent Test**: Run parallel ingestion processes on identical items with interleaved URL and hash variants; verify exactly one Article per identity in every scenario, deterministic outcomes, and zero ingestion failures caused by deduplication races.

**Acceptance Scenarios**:

1. **Given** two concurrent runs persisting the same canonical URL, **When** both attempt insertion, **Then** exactly one Article exists and the other run resolves to it and proceeds (link-only work).
2. **Given** the same content under different URLs arriving concurrently, **When** both runs persist, **Then** the content-hash identity yields one Article and the second run records its source as a discovery link.
3. **Given** any deduplication constraint conflict, **When** it occurs, **Then** it is handled by deterministic re-resolution — never surfaced as a crash or unhandled failure.

---

### User Story 3 - Repeatable, convergent background deduplication (Priority: P2)

A repeatable deduplication pass over stored Articles applies the same deterministic rules for historical data and rule evolution. Repeated passes are idempotent — after convergence, further passes make zero changes — and existing duplicates are merged: the canonical Article is elected deterministically, discovery links migrate to it, duplicates are retired via lifecycle state, and every merge is recorded. No record is ever hard-deleted.

**Why this priority**: The user requires safety for repeated background processing; convergence also lets identity rules evolve later without manual data surgery.

**Independent Test**: Seed duplicate groups (including merge chains), run the pass repeatedly; verify deterministic merges, zero changes on the second pass, and fully preserved provenance.

**Acceptance Scenarios**:

1. **Given** duplicate Articles exist, **When** the pass runs, **Then** the canonical Article is elected by the deterministic rule, discovery links migrate to it (conflicting links collapse to the earliest first-seen), the duplicate is retired via lifecycle state, and a merge record is written.
2. **Given** a merge chain, **When** the pass runs, **Then** all members resolve to a single terminal canonical Article deterministically.
3. **Given** a completed pass, **When** it runs again, **Then** zero changes occur.
4. **Given** ingestion running concurrently with the pass, **When** both complete, **Then** the results converge with no lost discovery links and no duplicates.

---

### User Story 4 - Deterministic metadata matching with a future extension seam (Priority: P3)

Detection uses only deterministic signals in a defined precedence; conservative metadata matching (normalized title plus publication date) applies only when stronger keys are absent and never merges records with conflicting strong evidence. The detection logic sits behind a replaceable internal matcher contract, so a future semantic detector can add stages and candidate records referencing Articles and reuse the same linking/merge machinery — without changing the Article core model. The MVP ships no AI-based matching.

**Why this priority**: Keeps the MVP deterministic and bounded while protecting the future roadmap; explicitly required as the extension point.

**Independent Test**: Run a fixture corpus covering strong-key matches, weak-key-only matches, and conflicting-evidence pairs (which must never merge); register an alternative matcher stub and verify it plugs into the same pipeline with zero Article model changes.

**Acceptance Scenarios**:

1. **Given** two records with matching normalized title and publication date but conflicting URL and hash evidence, **When** the pass evaluates them, **Then** they are not merged.
2. **Given** records with no hash where URLs differ but conservative metadata equality holds, **When** the pass evaluates them (sweep context only), **Then** they may merge under the recorded weak rule, with the rule captured in the merge record.
3. **Given** an alternative matcher registration, **When** it is used, **Then** it feeds the same candidate/link/merge machinery without changing the Article core model.

---

### Edge Cases

- Same canonical URL with content updated later (developing story) → still exactly one Article; deduplication never mutates content or creates a new record for the same URL.
- Hash present on one side only, normalized URL matching → match (identity resolves via the strongest available key).
- Insufficient content to compute a hash → no hash is fabricated; matching falls back to URL keys (and, in sweep context only, conservative metadata rules).
- Weak metadata equality but conflicting strong keys (different URL and hash) → never merged (weak signals cannot override strong evidence).
- A retired (merged) duplicate receives a new discovery → resolved to the terminal canonical Article idempotently.
- A discovery link that already exists (same article and source) → no-op; earliest discovery URL and first-seen time retained.
- Link conflicts during merge (both duplicates already linked to the same source) → collapsed deterministically to the earliest first-seen link.
- Merge chains created by rule evolution → converge to a single terminal canonical on the next pass.
- Redirects or URL variants that canonicalization cannot fully align, but content hashes match → hash identity wins; still one Article.
- Case or encoding variants of the same URL → resolved by deterministic URL normalization.
- Concurrent pass and ingestion races → mediated by constraints; deterministic re-resolution; no data loss.
- Feed edits shifting a content hash for an already-stored article → same canonical URL keeps identity; no churn, no new records.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Deduplication MUST run (a) inline within ingestion before persistence for newly fetched items, and (b) as a repeatable background pass over stored Articles; both modes MUST record their outcomes (scanned, matched, links added, merged, skipped) via ProcessingJob records with UTC timings and an outcome category.
- **FR-002**: Detection precedence MUST be deterministic: canonical URL, then normalized URL, then content hash, then conservative deterministic metadata (normalized title plus publication date) only when stronger keys are absent. The rules MUST be defined once in a shared identity rulebook used by both ingestion and the pass.
- **FR-003**: On a detection match, ingestion MUST NOT create a new Article; it MUST record (idempotently) the Article–Source discovery relationship with the discovery URL and first-seen time (UTC).
- **FR-004**: The Article–Source discovery relationship MUST allow at most one link per (article, source) pair; the earliest discovery is the article's primary attribution (derived deterministically); relationships to ALL discovering sources MUST be preserved, never discarded.
- **FR-005**: Data-layer constraints and indexes MUST make deduplication reliable under concurrent ingestion: unique canonical URL; unique normalized URL when present; unique content hash when computed from sufficient content (uniqueness filtered so absent values do not conflict); unique (article, source) discovery pair; plus supporting indexes for hash lookups, normalized-URL lookups, discovery-by-source, discovery-by-article, merge-record lookups, and pass batches by lifecycle state. Constraint conflicts MUST resolve via deterministic re-resolution (re-match and link) and MUST NOT fail ingestion runs.
- **FR-006**: URL normalization and content-hash inputs MUST be defined once, deterministic, and stable across runs; if the rules evolve, the repeatable pass MUST converge historical data without manual data surgery.
- **FR-007**: The background pass MUST be idempotent and convergent: after one full pass, repeated passes make zero changes; it MUST be safe to run concurrently with ingestion.
- **FR-008**: Merging existing duplicates MUST be deterministic: the canonical Article is elected by earliest creation time (tie-break by identifier); discovery links migrate to the canonical (conflicting links collapse to earliest first-seen); the duplicate is retired via lifecycle state (never hard-deleted); a merge record (duplicate → terminal canonical, matched rule/key, decided-at UTC) is written; chains resolve to the terminal canonical.
- **FR-009**: Attribution MUST survive merging: all discovery links and merge provenance remain queryable; no source information, URL, or timing is discarded.
- **FR-010**: All identity, discovery, and merge timestamps MUST be stored in UTC; deduplication MUST NOT perform window arithmetic (constitution Principle V).
- **FR-011**: Deduplication MUST NOT mutate article content in the MVP: the same canonical URL is the same identity regardless of later content changes; content refresh is out of scope.
- **FR-012**: No AI-based, probabilistic, or semantic matching may be introduced in the MVP: no embeddings, similarity scores, vector stores, or ML dependencies in deduplication.
- **FR-013**: Extension point: detection logic MUST sit behind a replaceable internal matcher contract feeding the same candidate/link/merge machinery; a future semantic detector MUST be able to add stages and candidate records referencing Articles without changing the Article core model.
- **FR-014**: Weak metadata matching MUST be conservative and sweep-scoped: exact equality after deterministic normalization, only when strong keys are absent, never when strong keys conflict, with the rule used recorded in the merge record for traceability.
- **FR-015**: Deduplication pass enablement, cadence, and batch sizes MUST be configurable without code changes; no hardcoded source or rule lists outside the identity rulebook; no new secrets are required (constitution Principle IV).
- **FR-016**: Deduplication MUST be testable offline and deterministically: fixtures cover multi-source duplicates, URL variants, hash-only matches, weak-only matches, conflicting evidence, merge chains, and concurrency scenarios; tests run without network access.

### Key Entities

This feature evolves the core model with one explicit relationship and supporting records; the Article entity's definition is intentionally unchanged so the future semantic extension point never requires modifying it.

- **Article (core; unchanged)**: identity anchors remain canonical URL and content hash; lifecycle state is used to retire merged duplicates; no new fields are required by this feature or its future semantic extension.
- **Article–Source Discovery Link (new relationship)**: one per (article, source): discovery URL and first-seen time (UTC); repeatedly refreshed idempotently; the mechanism that preserves relationships between an Article and all relevant Sources; earliest link = primary attribution.
- **Article Merge Record**: duplicate → terminal canonical Article, matched rule/key, decided-at (UTC); preserves merge provenance; chains follow the terminal canonical.
- **Deduplication Run**: ProcessingJob records for inline and pass executions: counts (scanned/matched/links added/merged/skipped), UTC timings, outcome category.
- **Matcher Contract**: the replaceable internal strategy that produces deterministic decisions from the identity rulebook; the extension seam for future semantic matching (implementations are interchangeable, the Article model is not touched).
- **Identity Rulebook**: the single shared definition of URL normalization, content-hash inputs, and weak-metadata equality rules, used identically by ingestion and the deduplication pass.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A fixture of one article discovered through 2–5 different sources yields exactly 1 Article with one discovery link per source; 100% of discovery URLs and first-seen times are preserved.
- **SC-002**: Repeated identical ingestion plus deduplication passes produce zero duplicate Articles and zero further changes after convergence (post-convergence delta = 0).
- **SC-003**: Concurrency simulations (parallel ingests of the same URL and content variants) yield exactly 1 Article in 100% of scenarios, with zero ingestion failures caused by deduplication races.
- **SC-004**: Pre-seeded duplicate groups (including chains) merge deterministically: canonical election matches the defined rule in 100% of cases and every group resolves to a single terminal canonical; zero hard deletes occur.
- **SC-005**: The conflicting-evidence corpus yields zero false merges; weak-rule merges occur only when strong keys are absent (100% policy adherence across fixtures).
- **SC-006**: Constraint review confirms every deduplication-critical key is enforced by a constraint or index (checklist 100% complete), and the race corpus produces identical outcomes across repeated runs.
- **SC-007**: An alternative matcher stub integrates through the contract with zero Article model changes, and the build contains zero AI/vector/ML dependencies for deduplication.

## Assumptions

- **Policy supersession**: this feature refines feature 004's cross-source behavior — the same article found in multiple sources no longer produces separate Article records; instead one Article carries discovery links to all sources. Feature 004's spec/policy requires a corresponding update, to be reconciled during plan/analyze; the user's explicit instruction is the source of truth for this change.
- **Core model evolution**: feature 001's Article-to-single-Source reference evolves into the Article–Source discovery relationship (EF mapping updated in the plan phase); Article identity anchors (canonical URL, content hash) and the metadata policy are unchanged.
- **Deterministic only**: matching is exclusively rule-based in the MVP; probability thresholds, similarity scoring, and semantic detection are deferred behind the matcher seam.
- **Rule details**: the conservative URL normalization ruleset, content-hash inputs, and the "sufficient content" threshold for hashing are defined in the plan phase; conservative means no transformation that could merge clearly distinct URLs or content.
- **Identity over time**: the same canonical URL remains the same article even when the publisher later changes its content; content refresh/mutation is out of scope.
- **Primary attribution**: derived from earliest discovery (deterministic), not stored as a mutable flag.
- **Concurrency approach**: unique constraints are the backstop; conflicts are resolved in code by re-resolution; no reliance on locks or serializable transactions; no distributed coordination (modular monolith only).
- **Merging**: only lifecycle-state retirement is used; no hard deletes anywhere; provenance (links and merge records) is retained indefinitely.
- **Weak matching**: deliberately conservative and sweep-scoped to avoid false merges; exact equality rules are defined once in the identity rulebook.
- **Future semantic detection**: only the seam is built now; its future artifacts (candidates/decisions) will reference Articles without modifying them.
- **Out of scope**: article content updates, relevance, topic matching, summarization, scoring, presentation/UI, and implementation of semantic deduplication itself.

**Dependencies**: Feature 001 (core domain & persistence model — Article, Source, uniqueness and metadata policies, lifecycle states; requires the Article–Source discovery relationship delta); Feature 002 (error taxonomy, isolation, background-processing patterns for the pass); Feature 004 (ingestion pipeline that hosts inline deduplication; its cross-source retention policy is superseded); Project Constitution v1.0.0 (Principles I–V, especially idempotency, attribution, and UTC/time handling); deterministic offline fixtures for testing.
