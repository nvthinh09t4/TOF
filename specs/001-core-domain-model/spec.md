# Feature Specification: Core Domain & Persistence Model

**Feature Branch**: `001-core-domain-model`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Design the core domain and persistence model for the trending-news platform using a stable-core and extension-friendly architecture. The core model must support the initial MVP requirements: trending topics from external trend providers, RSS/news articles, news sources, topic-to-article relationships, AI-generated summaries, users, and background processing jobs. Define the core entities, aggregates, relationships, constraints, indexes, identifiers, timestamps, normalization rules, and EF Core mappings for SQL Server. The core model should include stable concepts such as Topic, TopicAlias, TopicTrendObservation, Article, Source, AiSummary, User, and ProcessingJob. Keep provider-specific data isolated through provider abstractions and optional metadata rather than coupling the domain model to Google Trends, a specific RSS provider, or a specific AI provider. The architecture must allow future features such as topic clustering, trend scoring, article relevance scoring, related topics, personalization, notifications, multilingual support, source quality, and additional trend/news providers to add their own entities and relationships without requiring major changes to the core entities. Prefer composition and explicit relationships over inheritance and excessive polymorphic entities. Do not create speculative entities for every future feature. Use SQL Server and EF Core. Define appropriate unique constraints and indexes for normalized topic names, canonical article URLs, content hashes, source identifiers, topic/article relationships, and time-based trend observations. Use JSON metadata only for provider-specific or non-critical extensible data; frequently queried or indexed business properties must remain strongly typed database columns. Keep the model suitable for a .NET modular monolith and avoid distributed-system concerns. The resulting design should provide a clear foundation for subsequent feature specs without implementing those future features prematurely."

## Clarifications

### Session 2026-09-28

- Q: When the same article is discovered from multiple sources, how should source attribution be modeled? → A: One article per canonical identity keeps source links for every discovered source; the earliest discovery is the primary attribution.
- Q: Should AiSummary support article-level summaries in addition to topic-level summaries? → A: Yes — each summary is scoped to exactly one topic or one article; every summary still requires at least one grounding article link.
- Q: When the same topic+provider observation window is re-sampled with updated values, what should the store do? → A: Exactly one observation record per topic+provider+window instance; re-sampling refreshes that record's latest values, while prior window instances remain append-only history.
- Q: How should processing-job retry attempts be modeled so prior attempts stay distinguishable? → A: A job-run record plus separate append-only attempt records (attempt number, timing, outcome, diagnostics reference).
- Q: What uniquely identifies a User record while no authentication feature exists? → A: A unique, operator-provisioned identity reference string (auth-agnostic); credentials are never stored on the User entity.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Persist the MVP knowledge chain (Priority: P1)

The platform must durably store every MVP domain concept — trending topics observed from external trend providers, news articles and their sources, topic-to-article relationships, AI-generated summaries, users, and background processing jobs — so that every reader-facing view (topics for the last 5 hours, today, and yesterday, with attributed summaries and original source links) can be reconstructed from stored data alone.

**Why this priority**: This is the foundation of the product. Without a trustworthy core model, no reader-facing or operational capability can be built; every subsequent feature spec depends on these entities and relationships.

**Independent Test**: Seed one complete trend cycle (provider observations → topics → articles with sources → summaries with derivation links, plus user and processing-job records), restart the platform, and verify each reader-facing content chain can be reconstructed with source attribution intact.

**Acceptance Scenarios**:

1. **Given** persisted trend observations, **When** the platform queries topics and their articles for any of the three presentation windows, **Then** each topic resolves to its observation history and its linked articles with their sources.
2. **Given** an AI-generated summary, **When** it is stored, **Then** it retains links to the stored articles it was derived from, and a summary with no source link cannot exist.
3. **Given** a background processing job, **When** it completes or fails, **Then** its record persists with its final state and timing information.

---

### User Story 2 - Trustworthy, idempotent data (Priority: P1)

Deterministic normalization and canonicalization rules (topic names, article URLs, content fingerprints) must be enforced by the data store itself, so that repeated ingestion of the same trends, feeds, or items never creates duplicate topics, articles, observations, or links.

**Why this priority**: Duplicates corrupt every downstream view (trend counts, article lists, summaries), and the constitution requires that every displayed item is traceable to a stored, attributable source. Idempotency must be guaranteed by the data model, not by fragile application-only checks.

**Independent Test**: Ingest the same batch twice, including the same article reached via several URL spellings and the same topic under different case/diacritic forms; verify that no additional records are created and that all lookups resolve to the single stored record.

**Acceptance Scenarios**:

1. **Given** a topic already stored under its normalized name, **When** the same topic arrives with different casing, extra whitespace, or diacritic variants, **Then** no new topic is created and lookups resolve to the single existing topic (via alias when appropriate).
2. **Given** an article already stored, **When** the same item arrives again via a syndicating source or a URL variant, **Then** the data store exposes the duplicate through canonical URL or content-fingerprint checks and no duplicate record is created for the same canonical identity.
3. **Given** a processing job that failed and is retried, **When** the retry completes, **Then** both attempts remain distinguishable in the job history.

---

### User Story 3 - Provider isolation and extension seams (Priority: P2)

Provider-specific data (Google Trends or alternatives, RSS/news providers, AI providers, and future sources such as Reddit, X, or YouTube) must be isolated in optional metadata rather than in core entity definitions, so that adding, replacing, or removing a provider never requires redesigning the core model, and future features can attach new entities and relationships around the stable core.

**Why this priority**: The constitution (Principle I) requires replaceable integrations, and the roadmap names several future providers and features (clustering, scoring, related topics, personalization, notifications, multilingual, source quality). The model must make those changes additive, not disruptive.

**Independent Test**: Onboard a hypothetical new trend provider and a hypothetical future feature (e.g., topic clustering) at the data layer using only provider-scoped records, metadata, and new relationships that reference core entities — with zero modifications to existing core entity definitions.

**Acceptance Scenarios**:

1. **Given** the core model, **When** a new trend or news provider is onboarded, **Then** only provider identity data, provider-scoped records, and metadata payloads are added and core entities remain unchanged.
2. **Given** a provider-specific attribute that is not needed for queries, **When** it is stored, **Then** it lives in optional metadata; **Given** that attribute later becomes a queried or constrained business property, **When** it is promoted, **Then** it is represented as a strongly typed column without breaking existing data.
3. **Given** a future feature needs new entities (e.g., clusters, scores), **When** it is specified, **Then** it attaches via new relationships referencing core entities without altering them.

---

### User Story 4 - Lifecycle-independent records (Priority: P3)

Records must survive independently of processing and content lifecycles: trend observations remain append-only history, processing jobs retain their attempt and outcome history, and attribution links stay valid even when a source is disabled or an article is later marked inactive.

**Why this priority**: Operability, audits, and future features (scoring, re-processing) depend on history that is never silently destroyed; destructive cascades would break the constitution's attribution guarantees.

**Independent Test**: Disable a source, mark an article inactive, run a job through failure and retry, and verify all historical records and attribution chains remain fully queryable.

**Acceptance Scenarios**:

1. **Given** a source that is later disabled, **When** historical articles and summaries are queried, **Then** attribution still resolves to the retained source record.
2. **Given** observations collected over several days, **When** a topic's history is queried, **Then** the full append-only observation history is returned.
3. **Given** a destructive delete is attempted on a record participating in attribution, **Then** the data store prevents it in favor of a lifecycle state change.

---

### Edge Cases

- Topic name collisions across casing, whitespace, and diacritic/Unicode forms (relevant for Vietnamese text today, other languages later); alias duplicates within one topic or across topics.
- Article URL variants: tracking parameters, scheme/host casing, redirect chains, AMP or mobile variants; feed items missing publication dates or other metadata.
- The same content syndicated by multiple sources: same content fingerprint under different URLs and sources must be detectable without losing any source's attribution.
- No attributable articles exist for a trend: the topic is displayed without a summary — a summary record must not exist.
- Concurrent ingestion or processing of the same item or time window: uniqueness rules resolve races without duplicates or data loss.
- Provider removal or deprecation: historical provider-scoped data (observations, summaries, metadata) stays readable; removing a provider's data does not break core queries.
- Re-sampling the same topic, provider, and observation window with changed values: the single record for that window refreshes with the latest values; prior window instances remain untouched history.
- Malformed or oversized provider payloads: rejected samples are recorded in processing-job diagnostics rather than corrupting core records.
- Time-zone boundaries: all timestamps stored in UTC; the three presentation windows are computed by the shared window module at the presentation edge, never from stored local times.
- User lifecycle: minimal identity records must not block later deletion/anonymization policies, which will be specified with the feature that needs them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The data store MUST persist the eight core concepts — Topic, TopicAlias, TopicTrendObservation, Article, Source, AiSummary, User, and ProcessingJob — along with their supporting relationship records (article–source links, summary derivation links, job attempts), each with a stable, system-generated identifier that is never reused.
- **FR-002**: Topic identity MUST be anchored on a normalized name form produced by deterministic normalization rules (trim, collapse whitespace, case folding, Unicode normalization including diacritic handling), with a uniqueness rule guaranteeing at most one topic per normalized name.
- **FR-003**: TopicAlias MUST map an alternate surface form to exactly one topic; alias values MUST be unique within a topic and resolvable to a single topic, so variant spellings never create competing topics.
- **FR-004**: TopicTrendObservation MUST record one trend signal state per topic, provider, and observation-window instance: at most one record per (topic, provider, window instance), where re-sampling the same window refreshes that record's latest values and observation time (UTC) instead of appending duplicates, while records for prior window instances remain append-only history; every record carries the provider's identity and the observation time (UTC).
- **FR-005**: Article identity MUST be anchored on a canonical URL (unique), produced by deterministic canonicalization rules (scheme/host normalization, tracking-parameter stripping); a content fingerprint (hash) MUST be stored to support duplicate-content detection across sources; source attribution MUST preserve every discovered source through article–source links on the single article record (at least one source link per article), with the earliest discovery as the primary attribution.
- **FR-006**: Source MUST be identified by a unique source identifier (e.g., feed URL) and carry a source kind and enabled/disabled state; adding, removing, or disabling a source MUST be possible through data and configuration changes only, with no application code changes.
- **FR-007**: Topic-to-article relationships MUST be many-to-many, recorded at most once per topic–article pair, and MUST survive content lifecycle changes without destructive cascades that break attribution.
- **FR-008**: AiSummary MUST be scoped to exactly one target — either one topic or one article (exactly one of the two references is set; topic-scoped and article-scoped summaries never mix in one record) — and MUST retain derivation links to at least one stored article: topic-scoped summaries link their grounding articles, article-scoped summaries link their anchor article; the data store MUST prevent storing a summary without at least one such link. Each summary MUST record the AI provider/model identity that produced it (as a strongly typed value) for traceability.
- **FR-009**: User MUST be persisted as a minimal identity record: stable identifier, a unique operator-provisioned identity reference string (auth-agnostic; the mechanism that authenticates it is external to this model, and credentials MUST NOT be stored on the User entity), and audit timestamps; personalization, notification, and preference data MUST NOT be modeled by this feature.
- **FR-010**: ProcessingJob MUST record the job run (job type, lifecycle state, start/finish timing in UTC, and a reference to failure diagnostics) while every attempt is preserved as a separate append-only attempt record (attempt number, timing in UTC, outcome, diagnostics reference); retries MUST NOT overwrite prior attempt history, and job records MUST outlive individual ingestion or summarization runs.
- **FR-011**: All core records MUST carry created and last-updated audit timestamps stored in UTC; no local-time values are persisted, and display-time conversion happens only at the presentation edge using the shared time-window module.
- **FR-012**: Frequently queried, filtered, sorted, joined, or constrained business properties MUST be strongly typed columns; JSON metadata MAY be used only for provider-specific or non-critical extensible attributes and MUST NOT hold unique keys or indexed/filtered criteria.
- **FR-013**: Core entities MUST NOT embed provider-specific columns or payload shapes; provider identity (which trend, news, or AI provider produced data) MUST be stored as data (stable identifier plus provider kind), so providers can be added, replaced, or removed by data and adapters rather than schema changes.
- **FR-014**: The data store MUST provide efficient access paths (indexes) for at least: normalized topic name lookup; alias lookup; observations by topic and time range; observations by provider and time range; article lookup by canonical URL; article lookup by content fingerprint; source lookup by its unique identifier; article–source link traversal in both directions; topic–article relationship traversal in both directions; summaries by topic and by article; attempts by job run; and jobs by type/state and time.
- **FR-015**: Referential integrity MUST be enforced across all relationships (aliases and observations → topic; article–source links → both article and source; topic–article links → both sides; summaries → their scope target (topic or article) and grounding articles; attempts → job run); deletion of a parent MUST be prevented unless the relationship is explicitly non-historical, in which case the rule MUST be defined in the model.
- **FR-016**: Repeated ingestion or processing operations MUST be idempotent at the data layer: uniqueness constraints MUST guarantee that identical re-ingestion cannot create duplicate topics, aliases, articles, observations, links, or summaries.
- **FR-017**: The model MUST allow future feature entities (topic clustering, trend scoring, article relevance, related topics, personalization, notifications, multilingual metadata, source quality) to attach to core concepts through new relationships without modifying existing core entity definitions; conversely, the model MUST NOT include speculative entities for features that are not currently specified.

### Key Entities

Aggregation boundaries: Topic is the aggregate root for topic-scoped data (aliases, observations, article links, summaries); Article (with its source links) and ProcessingJob (with its attempts) are independently managed records.

- **Topic**: The stable core concept of a trending subject; aggregate root for topic-scoped data. Key attributes: stable identifier, display name, normalized name (uniqueness anchor), first-seen times, audit timestamps. Relationships: one-to-many with aliases, observations, and topic-scoped summaries; many-to-many with articles.
- **TopicAlias**: An alternate surface form (spelling, casing, or diacritic variant; future: translations) that resolves to exactly one topic. Attributes: alias value (unique within its topic), topic reference.
- **TopicTrendObservation**: One trend signal record per topic, provider, and observation-window instance. Attributes: topic reference, provider identity, observed-at (UTC), observation window descriptor, rank/score when supplied, optional provider metadata. Re-sampling the same window refreshes the record's latest values; records for prior window instances are append-only history.
- **Article**: An ingested news item. Attributes: stable identifier, canonical URL (uniqueness anchor), title, content fingerprint (hash), publication timestamp when available (UTC), lifecycle state, optional ingestion metadata. One record per canonical identity; linked to every discovered source via article–source links; participates in the many-to-many link with topics; may be the scope target of article-scoped summaries and is referenced by summary derivation links.
- **ArticleSourceLink**: A discovery record tying an article to each source that carried it. Attributes: article reference, source reference, discovered-at (UTC), primary-attribution flag (the earliest discovery per article). Preserved for attribution regardless of source lifecycle.
- **Source**: A news source (currently an RSS feed; future: other provider channels). Attributes: stable identifier, unique source identifier (e.g., feed URL), source kind, display name, enabled state, audit timestamps. Owns articles; retained for attribution even when disabled.
- **AiSummary**: An AI-generated summary scoped to exactly one target — one topic or one article — and grounded in stored articles. Attributes: stable identifier, scope target (topic XOR article; exactly one is set), summary content, provider/model identity that generated it, generation timestamp (UTC), optional generation metadata. Must have at least one derivation link to a stored article (topic-scoped summaries link their grounding articles; article-scoped summaries link their anchor article).
- **User**: A minimal persisted user identity. Attributes: stable identifier, unique operator-provisioned identity reference (auth-agnostic; credentials are never stored here), audit timestamps. No preferences or personalization data in this feature.
- **ProcessingJob**: A record of one background processing run (trend fetch, feed ingestion, article processing, summarization). Attributes: stable identifier, job type, lifecycle state, start/finish timestamps (UTC), diagnostics reference. Attempts are preserved as separate append-only attempt records. Independent of other aggregates' lifecycles; history retained.
- **ProcessingJobAttempt**: An append-only record of one attempt within a job run. Attributes: run reference, attempt number, start/finish timestamps (UTC), outcome, diagnostics reference; prior attempts are never overwritten.

Relationship summary: Topic 1–\* TopicAlias; Topic 1–\* TopicTrendObservation; Topic \*–\* Article (at most one link per pair); Article \*–\* Source (article–source links; at least one per article; earliest discovery = primary attribution); Topic 1–\* AiSummary (topic-scoped) and Article 1–\* AiSummary (article-scoped; exactly one scope target per summary); AiSummary \*–\* Article (derivation links, at least one); ProcessingJob 1–\* ProcessingJobAttempt (append-only); ProcessingJob references job type and provider identity and is not lifecycle-coupled to the records it processed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of reader-facing MVP content (trending topics with linked articles, sources, and attributed summaries) can be reconstructed from persisted data alone after a platform restart, with every displayed item resolving its original source link.
- **SC-002**: Re-running an identical ingestion cycle produces zero duplicate topics, articles, observations, links, or summaries (duplicate count on unique business keys = 0 across repeated runs).
- **SC-003**: A documented fixture set of topic name variants (casing, whitespace, diacritics) and article URL variants (tracking parameters, casing, syndication) resolves to single stored records with 100% consistency across repeated ingestion runs.
- **SC-004**: Onboarding a new trend/news provider or attaching a future-feature entity requires zero modifications to existing core entity definitions (verified by comparing model definitions before and after: no diffs on core entities).
- **SC-005**: Zero AI summaries exist without at least one linked stored source article — verified by an audit query returning zero violations at any time.
- **SC-006**: Topic trend-history queries for any of the three presentation windows return results within 1 second at 10× the launch-volume baseline.
- **SC-007**: 100% of processing job attempts, including failures, remain queryable for at least the configured retention period (default ≥ 30 days).

## Assumptions

- **Technology (user-mandated)**: SQL Server is the relational store, accessed through EF Core in a .NET modular monolith; a single database with no distributed-system concerns (no sharding, cross-service synchronization, or eventual consistency). Entity-to-schema mappings (EF Core configurations) are designed in the plan phase; this spec defines the data contract.
- **Metadata policy (user-mandated)**: JSON metadata columns are reserved for provider-specific or non-critical extensible attributes; any property that is unique-constrained, indexed, filtered on, sorted, or joined remains a strongly typed column.
- **Integration boundaries (constitution Principle I)**: This feature introduces no new providers. It defines the persistence seams behind the existing trend-provider, news-ingestion, and AI-summarization interfaces: provider identity and provider-scoped data are stored as data (with optional metadata) so adapters can be added or replaced without core-model changes.
- **Time handling (constitution Principle V)**: All persisted timestamps are UTC; the three presentation windows (last 5 hours, today, yesterday) are computed by the shared time-window module from stored UTC timestamps; no stored local times and no ad-hoc date arithmetic.
- **Attribution (constitution Principle II)**: Summaries are always traceable to stored source articles; when no attributable news exists for a trend, no summary record is created and the trend is displayed without one.
- **Configuration over code (constitution Principle IV)**: Adding, removing, or disabling sources requires only data/configuration changes; no source lists or provider selections are hardcoded in the model.
- **Retention defaults**: Trend observations are retained as append-only history; processing records are retained for at least 30 days by default, configurable by operators.
- **MVP simplicity (constitution Principle III)**: No speculative entities are created for future features (clustering, scoring, relevance, related topics, personalization, notifications, multilingual, source quality); only the documented extension seams (provider metadata and attachable relationships) are included.
- **Lifecycle states over deletes**: Records participating in attribution (sources, articles) use lifecycle states (e.g., enabled/disabled, active/inactive) instead of destructive deletes; hard deletion is prevented while attribution links exist.
- **Users**: MVP persistence is a minimal identity record sufficient for future personalization; authentication flows and profile features are out of scope for this feature and will reference these records when specified.

**Dependencies**: Project Constitution v1.0.0 (Principles I–V); the shared time-window module for presentation-range semantics; EF Core migrations as the schema-evolution mechanism.
