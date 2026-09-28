# Feature Specification: Topic–Article Matching (Deterministic)

**Feature Branch**: `006-topic-article-matching`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement matching between existing Topics and crawled Articles. Use Topic, TopicAlias, Article, and the existing topic/article relationship model from the core data model. The MVP should use deterministic matching based on normalized topic names, aliases, article titles, descriptions, and other available article metadata. Define matching rules, normalization rules, confidence/relevance thresholds, and how multiple topics can relate to the same article. Do not create duplicate Topic records merely because an article uses a different spelling or phrase when an existing alias can represent the variation. Keep the matching algorithm isolated behind an application service or strategy so it can later be replaced or supplemented with AI-assisted relevance evaluation. Do not implement topic clustering or trend scoring in this feature."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Match crawled articles to existing topics (Priority: P1)

Background matching passes link stored articles to existing topics using deterministic rules over normalized topic names/aliases and article text (title, description, and available metadata). Accepted matches are recorded in the core topic–article relationship — at most one link per pair — so every topic can list its attributable articles.

**Why this priority**: This feature is the connection between trends and news; without it, trending topics have no articles and the reader-facing value of everything built so far is zero.

**Independent Test**: Run the pass over a fixture corpus of articles, topics, and aliases; verify links are created exactly per the rulebook with provenance recorded, and that a topic's article list is resolvable from the relationship data.

**Acceptance Scenarios**:

1. **Given** an article whose title or description contains a topic's normalized name or an alias as an eligible phrase, **When** the pass runs, **Then** a link is created for that pair with the matched rule, matched text, confidence, and matched-at time recorded.
2. **Given** an article with no eligible match, **When** the pass runs, **Then** no link is created and the article is counted as evaluated without a match.
3. **Given** a link already exists for a pair, **When** the pass runs again, **Then** nothing changes (idempotent).
4. **Given** several topics qualify for one article, **When** the pass runs, **Then** each qualifying topic receives its own link (per-pair uniqueness still enforced).

---

### User Story 2 - Defined normalization, rules, and thresholds (Priority: P1)

Match decisions follow a single documented rulebook: deterministic text normalization (case, whitespace, Unicode/diacritics including Vietnamese, markup and entity cleanup), phrase and word-boundary matching rules applied to name and alias forms against title/description/metadata, deterministic confidence scoring from defined evidence, and configurable thresholds below which no link is created. Scores are reproducible and every rejection is recorded with its score for tuning.

**Why this priority**: The user explicitly requires matching rules, normalization rules, and confidence/relevance thresholds; auditability and reproducibility make the deterministic matcher trustworthy and tunable.

**Independent Test**: Fixtures across normalization variants, boundary traps, field variants (title vs description), and threshold boundaries; verify rule outcomes, reproducible scores, and correct accept/reject behavior under different configurations.

**Acceptance Scenarios**:

1. **Given** topics whose names are substrings of unrelated words (e.g., a short keyword inside a longer word), **When** matching runs, **Then** boundary rules prevent false matches (substring-only occurrences never match).
2. **Given** identical inputs evaluated in repeated runs, **When** the pass runs, **Then** confidence scores and decisions are identical.
3. **Given** a candidate scoring below the active threshold, **When** it is evaluated, **Then** no link is created and the rejection is counted with its score in run diagnostics.
4. **Given** a threshold or weight configuration change, **When** the next pass runs, **Then** the change applies deterministically to new matches while existing links remain unaffected (links are append-only in the MVP).

---

### User Story 3 - Alias discipline and multi-topic relations (Priority: P2)

Variant spellings and phrases resolve through existing normalized names and aliases: matching never creates Topic or TopicAlias records and never duplicates topics because of spelling or phrasing variation. One article may relate to several topics and one topic to many articles, with at most one link per pair; when name/alias collisions give several topics equal claim, a defined deterministic precedence picks the winner and records it.

**Why this priority**: Protects core-model identity (uniqueness rules) and delivers correct multi-topic semantics, per the user's explicit alias requirement and the core model's many-to-many design.

**Independent Test**: Fixtures where articles use variant spellings and phrases; verify resolution through existing identities, zero topic/alias writes, deterministic collision winners, and multi-topic link sets.

**Acceptance Scenarios**:

1. **Given** a variant phrase covered by an existing alias, **When** the pass runs, **Then** the article links via that alias and no Topic or TopicAlias record is created (zero writes).
2. **Given** a variant not covered by any existing name or alias, **When** the pass runs, **Then** no link is created and no speculative topic is added.
3. **Given** a collision where one topic's alias equals another topic's name, **When** both are candidates, **Then** the defined precedence rule selects the same winner on every run and the decision is recorded.

---

### User Story 4 - Replaceable matcher strategy with a future AI seam (Priority: P3)

The matching algorithm sits behind an application-service/strategy contract. The deterministic matcher is the only MVP implementation; a future AI-assisted relevance evaluator can replace it or supplement it (for example, as an additional stage that proposes links or adjusts confidence) without changing the core model or its consumers. No AI capability is implemented in this feature.

**Why this priority**: The roadmap calls for AI-assisted relevance later; the seam prevents a rewrite and satisfies the replaceability principle without speculating on AI design now.

**Independent Test**: Register an alternative matcher stub through the contract; verify it plugs into the same link/threshold/idempotency machinery with zero core-model changes and that no AI/ML dependencies exist in the MVP build.

**Acceptance Scenarios**:

1. **Given** an alternative matcher implementation registered through the contract, **When** the pass runs, **Then** the same candidate evaluation, threshold gating, linking, and idempotency machinery is used.
2. **Given** the MVP configuration, **When** matching runs, **Then** no AI/ML components are loaded and no network calls are made (matching is pure local computation over stored data).

---

### Edge Cases

- Topic names appearing as substrings of unrelated words → word-boundary semantics prevent matches; short-name safeguards (minimum lengths, boundary requirements) defined in the rulebook.
- Very short or punctuation-only topic names → deterministic exclusion or strict boundary requirements; never fuzzy fallbacks.
- Article missing title or description → only eligible fields are searched; an article with no usable text yields no match and is counted.
- Markup, HTML entities, smart quotes, full-width characters in article text → normalization cleans noise without lossy transformation of diacritics (Vietnamese preserved).
- Alias shared across topics, or an alias equal to another topic's name → precedence and tie-break rules from the rulebook; winner recorded; identical on every run.
- Generic or ambiguous topic phrases (common words) → boundary rules plus thresholds limit noise; behavior remains deterministic and tunable, never random.
- One article about several trends → one link per qualifying topic (the many-to-many design purpose).
- Retired (merged) duplicate articles from deduplication → excluded from matching; active deduplicated articles only.
- Overlapping or concurrent matching passes and concurrent ingestion → per-pair uniqueness and idempotency guarantee no duplicate links and no errors escaping the run.
- Rulebook or threshold changes → the repeatable pass re-evaluates and adds newly qualifying links only (append-only in the MVP; link retraction is out of scope).
- Cross-language text (Vietnamese article, English topic) → no cross-language matching in the MVP; aliases may carry translations in a future multilingual feature.
- Topic phrase embedded among extra words → phrase-inclusion rules define acceptance exactly (whole-phrase occurrence within an eligible field).
- Topics with no articles or articles with no topics → valid states; no errors, correct counts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Matching MUST run as background processing (recorded as a ProcessingJob per run) over stored, active, deduplicated articles and existing topics; matching MUST NOT create, update, or delete Topic, TopicAlias, or Article records — its only writes are links and provenance on the topic–article relationship (plus run records).
- **FR-002**: Detection MUST use deterministic rules over normalized text: each topic's normalized name and all its alias forms are matched against the article's title, description/content excerpt, and other available article metadata; normalization (case, whitespace, Unicode/diacritics, markup/entity cleanup) is defined once in a shared rulebook and used identically everywhere.
- **FR-003**: Phrase and word-boundary semantics MUST define what counts as an occurrence: a topic form must occur as an eligible whole phrase within a searched field; substring-only occurrences inside unrelated words MUST NOT match; short-name safeguards are part of the rulebook.
- **FR-004**: Each candidate match MUST receive a deterministic confidence score derived from defined evidence (for example: title vs description, exact topic name vs alias, boundary strength), reproducible for identical inputs; evidence weights and the score mapping are part of the rulebook.
- **FR-005**: Configurable thresholds MUST gate link creation: candidates scoring below the threshold are rejected, counted with their scores in run diagnostics; rejections and near-misses MUST be recorded so thresholds can be tuned deterministically.
- **FR-006**: Accepted matches MUST create at most one link per topic–article pair (pair uniqueness enforced at the data layer); every link MUST record provenance: the matched rule, the matched surface form/text, the confidence score, and the matched-at time (UTC).
- **FR-007**: One article MAY relate to multiple topics and one topic to multiple articles; all qualifying topics are linked (the core many-to-many design is the intended semantics).
- **FR-008**: Alias discipline: variant spellings and phrases MUST resolve through existing normalized names and aliases; matching MUST NOT create topics or aliases and MUST NOT link through speculative new identities; variants without an existing representation produce no link.
- **FR-009**: Collision and precedence rules MUST be deterministic and recorded: exact topic-name matches take precedence over alias matches; remaining ties break by a defined deterministic rule (for example, earliest-created topic); winners are identical across repeated runs.
- **FR-010**: Matching passes MUST be idempotent and convergent: repeated runs create no duplicate links; after convergence, additional runs make zero changes unless inputs or rules changed; passes MUST be safe to run concurrently with ingestion, deduplication, and each other, and restartable after interruption.
- **FR-011**: A repeatable backfill/re-evaluation pass MUST cover historical data (initial population and re-evaluation after rule or threshold changes); each run MUST record counts (articles evaluated, candidates scored, links created, below threshold, excluded) and an outcome category via its ProcessingJob record.
- **FR-012**: This feature MUST NOT implement topic clustering, trend scoring, article relevance ranking, or AI-assisted matching; it MUST NOT mutate articles or topics; link retraction and topic lifecycle actions are out of scope for the MVP.
- **FR-013**: The matching algorithm MUST sit behind a replaceable application-service/strategy contract; the deterministic matcher is the sole MVP implementation, and a future AI-assisted relevance evaluator MUST be able to replace or supplement it through the contract (for example, an additional stage proposing links or confidence adjustments) without changing the core model or its consumers.
- **FR-014**: All timestamps MUST be stored in UTC; matching MUST NOT perform window arithmetic (constitution Principle V) and MUST NOT call external providers or make network requests (pure local computation over stored data).
- **FR-015**: Thresholds, evidence weights, minimum name lengths, batch sizes, and pass cadence MUST be configurable without code changes; the rulebook is single-sourced (constitution Principle IV); no secrets are required by this feature.
- **FR-016**: Matching MUST be testable offline and deterministically: fixtures cover normalization variants, boundary traps, field variants, threshold boundaries, alias collisions, multi-topic articles, idempotency, and concurrency; scores and decisions are reproducible across runs; tests run without network access.

### Key Entities

This feature reads the core model and writes only the topic–article relationship; the Article entity remains unchanged.

- **Topic–Article relationship (core; used and extended)**: at most one link per (topic, article) pair; extended by this feature with match provenance — matched rule, matched surface form/text, confidence score, matched-at (UTC). The plan phase updates the core model's mapping for these fields; existing consumers are unaffected.
- **Topic / TopicAlias / Article (core; read-only here)**: matching reads normalized names, aliases, titles, descriptions, and metadata, and never writes these records; alias discipline guarantees no topic duplication for spelling or phrasing variation.
- **Topic–Article Matcher (application-service/strategy contract)**: given an article and candidate topics/aliases, produces candidate decisions (topic, matched rule, evidence, confidence); the deterministic implementation is the MVP; the seam allows future AI-assisted relevance evaluation to replace or supplement it.
- **Match Rulebook**: the single shared definition of text normalization, phrase/boundary semantics, evidence weights, score mapping, precedence/tie-break rules, and threshold defaults (configuration-backed).
- **Matching Run**: a ProcessingJob record of one pass: counts (evaluated, scored, linked, below threshold, excluded), outcome category, UTC timings, diagnostics.
- **Fixtures**: deterministic offline article/topic/alias corpora used for rule, threshold, collision, and idempotency tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The fixture corpus yields 100% of rulebook-matchable pairs as exactly one link each with full provenance (rule, matched text, confidence, matched-at); zero links are created for below-threshold or boundary-violating cases.
- **SC-002**: Variant spelling and phrasing fixtures resolve through existing names/aliases in 100% of cases, and all matching runs together perform zero Topic or TopicAlias writes.
- **SC-003**: Post-convergence repeated runs produce a delta of zero; concurrent ingestion + deduplication + matching scenarios produce no duplicate links and no unhandled errors in 100% of simulations.
- **SC-004**: Multi-topic fixtures link one article to all qualifying topics with per-pair uniqueness holding in 100% of scenarios.
- **SC-005**: Collision and boundary fixtures produce identical winners and rejections across repeated runs (100% deterministic).
- **SC-006**: Confidence scores are reproducible for identical inputs in 100% of reruns; configuration changes (thresholds/weights) deterministically alter decisions on the next pass without touching existing links.
- **SC-007**: An alternative matcher stub integrates through the contract with zero core-model changes; the build contains zero AI/ML dependencies, and matching code has no coupling to clustering or scoring features.

## Assumptions

- **Scope boundaries**: matching runs only between existing topics and stored articles; topic creation stays with trend collection; no speculative candidate/pending-match entities in the MVP (near-miss counts live in run diagnostics); a future AI stage may add candidate records referencing core entities without changing them.
- **Article stability**: article text is fixed at ingestion in the MVP (no content updates), so match decisions are stable; rulebook changes are absorbed by the repeatable pass; links are append-only (retraction deferred to a future feature).
- **Relationship evolution**: match provenance (rule, matched text, confidence, matched-at) is a documented evolution of the core model's topic–article relationship (plan phase updates the EF mapping); consumers of the relationship are unaffected.
- **Identity guarantees**: core-model uniqueness makes duplicate normalized topic names impossible; alias/name collisions are resolved by the deterministic precedence rules, not by creating new identities.
- **Defaults**: conservative default thresholds and weights favor precision over recall initially (fewer false links); defaults and evidence weights are fixed in the plan phase and configurable afterwards.
- **Scale**: matching processes active topics and articles in bounded, configurable batches with indexed candidate lookups; no full cross-product scans at MVP scale (candidate selection strategy is a plan-phase decision with deterministic semantics).
- **Language**: cross-language matching is out of scope; aliases may carry translations in a future multilingual feature.
- **Architecture**: single-module background pass in the modular monolith; no distributed constructs; no network access during matching.
- **Consumers**: topic views (future) read links plus provenance; article relevance ranking and trend scoring remain separate future features built on this data.

**Dependencies**: Feature 001 (core domain & persistence model — Topic, TopicAlias, Article, topic–article relationship with pair uniqueness; provenance delta to be mapped in the plan); Feature 002 (background-processing, error taxonomy, and isolation patterns); Feature 003 (topics and aliases originate from trend collection); Feature 004 and Feature 005 (article corpus; retired duplicate articles are excluded from matching); Project Constitution v1.0.0 (Principles I — replaceable strategies, II — attribution integrity, III — YAGNI, IV — configuration, V — UTC/time handling); deterministic offline fixtures for testing.
