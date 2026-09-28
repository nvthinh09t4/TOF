# Data Model: Core Domain & Persistence Model (Feature 001)

This document is the physical/logical contract delivered by feature 001. It reflects the spec as clarified on 2026-09-28.

## Global conventions

- **Primary keys**: `BIGINT IDENTITY` (`Id`), clustered, never reused (R-003).
- **Timestamps**: `datetime2(0)` UTC everywhere; `CreatedAtUtc`/`UpdatedAtUtc` on all top-level records set by a TimeProvider-driven interceptor (R-012). Display conversion happens only at the presentation edge (constitution V).
- **Strings**: `nvarchar` with default collation, except uniqueness/normalized keys which use `Latin1_General_100_BIN2` (R-004).
- **JSON**: nullable `nvarchar(max)` + `CHECK (ISJSON(...) = 1)`; never unique/indexed/filtered (R-009).
- **Deletes**: all foreign keys `DeleteBehavior.Restrict`; lifecycle via state columns only (R-010).
- **Concurrency**: `rowversion` on mutable records per R-011.
- **Table names (pluralized)**: Topics, TopicAliases, TopicTrendObservations, Articles, ArticleSourceLinks, Sources, AiSummaries, AiSummarySourceLinks, TopicArticleLinks, Users, ProcessingJobs, ProcessingJobAttempts.

## Enums (stored as tinyint via value conversions)

| Enum | Values | Notes |
|---|---|---|
| `WindowType` | `Last5Hours = 1`, `Today = 2`, `Yesterday = 3` | Boundaries computed by the shared time-window module (future feature); stored as data |
| `SourceState` | `Enabled = 1`, `Disabled = 2` | `Retired` is reserved for feature 021 (additive value) |
| `ArticleLifecycleState` | `Active = 1`, `Inactive = 2` | Retirement/merge-away semantics arrive with feature 005 (additive) |
| `ProcessingJobState` | `Pending = 1`, `Running = 2`, `Succeeded = 3`, `Failed = 4` | Triggers/counts/correlation columns are feature 013 deltas (additive) |
| `ProcessingJobAttemptOutcome` | `Succeeded = 1`, `Failed = 2` | Append-only attempt history |

## Record catalog

### Topic — `Topics`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| DisplayName | nvarchar(200) NOT NULL | As provided by the pipeline |
| NormalizedName | nvarchar(200) NOT NULL, BIN2 collation | Uniqueness anchor (normalized per R-004) |
| FirstSeenAtUtc | datetime2(0) NOT NULL | Earliest observation/mention time |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | Interceptor-managed |
| RowVersion | rowversion | Optimistic concurrency |

Constraints/indexes: `UX_Topics_NormalizedName` unique. Relationships: 1–* TopicAliases, 1–* TopicTrendObservations, 1–* TopicArticleLinks, 1–* AiSummaries (topic-scoped); all Restrict.

### TopicAlias — `TopicAliases`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| TopicId | bigint FK → Topics | NOT NULL, Restrict |
| Value | nvarchar(200) NOT NULL | Surface form as provided |
| NormalizedValue | nvarchar(200) NOT NULL, BIN2 collation | Normalized per R-004 |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |
| RowVersion | rowversion | |

Constraints/indexes: `UX_TopicAliases_NormalizedValue` **global unique** — an alias surface form resolves to exactly one topic (FR-003 "resolvable to a single topic"); per-topic uniqueness follows. Lookup index for alias resolution is the unique index itself. Note: polysemous surface forms are not modeled as aliases in the MVP; feature 026 adds the language dimension to this key (delta).

### TopicTrendObservation — `TopicTrendObservations`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| TopicId | bigint FK → Topics | NOT NULL, Restrict |
| ProviderKind | nvarchar(32) NOT NULL | e.g., `trend` (R-008) |
| ProviderKey | nvarchar(64) NOT NULL | e.g., `google-trends` |
| WindowType | tinyint NOT NULL | `WindowType` enum |
| WindowStartUtc / WindowEndUtc | datetime2(0) NOT NULL | Window instance descriptor (R-007) |
| ObservedAtUtc | datetime2(0) NOT NULL | Latest sample time for the instance |
| Rank | int NULL | When supplied |
| Score | decimal(9,4) NULL | When supplied |
| MetadataJson | nvarchar(max) NULL | ISJSON check; provider-specific extras |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |
| RowVersion | rowversion | Re-sample updater must retry on conflict |

Constraints/indexes: `UX_TopicTrendObservations_WindowInstance` unique (TopicId, ProviderKind, ProviderKey, WindowType, WindowStartUtc); `IX_TopicTrendObservations_TopicId_WindowStartUtc` (topic history ranges); `IX_TopicTrendObservations_ProviderKind_ProviderKey_ObservedAtUtc` (provider/time queries). Semantics: re-sampling the same instance refreshes provided fields; prior instances are append-only.

### Article — `Articles`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| CanonicalUrl | nvarchar(2048) NOT NULL, BIN2 collation | Uniqueness anchor (canonicalized per R-005) |
| Title | nvarchar(500) NULL | Feeds may omit |
| ContentHash | char(64) NULL | SHA-256 hex (R-006) |
| PublicationAtUtc | datetime2(0) NULL | When published, when available |
| LifecycleState | tinyint NOT NULL | `ArticleLifecycleState`, default Active |
| MetadataJson | nvarchar(max) NULL | ISJSON; ingestion metadata |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |
| RowVersion | rowversion | |

Constraints/indexes: `UX_Articles_CanonicalUrl` unique; `IX_Articles_ContentHash` (duplicate detection). Relationships: *–* Topics via TopicArticleLinks; 1–* ArticleSourceLinks; referenced by AiSummarySourceLinks.

### ArticleSourceLink — `ArticleSourceLinks`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| ArticleId | bigint FK → Articles | NOT NULL, Restrict |
| SourceId | bigint FK → Sources | NOT NULL, Restrict |
| OriginalUrl | nvarchar(2048) NOT NULL | Exact URL as seen in that source's feed (provenance/verification) |
| DiscoveredAtUtc | datetime2(0) NOT NULL | Earliest discovery for the pair |
| IsPrimary | bit NOT NULL | True = primary attribution (earliest discovery per article) |

Constraints/indexes: `UX_ArticleSourceLinks_ArticleId_SourceId` unique; `UX_ArticleSourceLinks_PrimaryPerArticle` **filtered unique** on (ArticleId) WHERE IsPrimary = 1 (at most one primary per article); `IX_ArticleSourceLinks_SourceId` (source→articles traversal). Semantics (FR-005 clarified): one article per canonical identity keeps a link for every discovered source; the earliest discovery holds the primary flag; links are never deleted (attribution permanence).

### Source — `Sources`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| SourceKey | nvarchar(512) NOT NULL | Unique source identifier (e.g., feed URL) |
| Kind | nvarchar(32) NOT NULL | e.g., `rss`; kind-agnostic |
| DisplayName | nvarchar(200) NOT NULL | |
| State | tinyint NOT NULL | `SourceState` (Enabled/Disabled) |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |
| RowVersion | rowversion | |

Constraints/indexes: `UX_Sources_SourceKey` unique. Semantics: adding/removing/disabling is data/config-only (FR-006); retained for attribution when disabled (US4); operational state and `Retired` are feature 021 deltas.

### AiSummary — `AiSummaries`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| ScopeTopicId | bigint FK → Topics NULL | Exactly one of the two scope references is set |
| ScopeArticleId | bigint FK → Articles NULL | (XOR; enforced by CHECK constraint) |
| Content | nvarchar(max) NOT NULL | Generated text only here |
| ProviderKey | nvarchar(64) NOT NULL | AI provider identity as data (R-008) |
| ModelKey | nvarchar(128) NOT NULL | Model identity |
| GeneratedAtUtc | datetime2(0) NOT NULL | |
| MetadataJson | nvarchar(max) NULL | ISJSON; generation metadata |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |
| RowVersion | rowversion | |

Constraints/indexes: `CK_AiSummaries_ScopeXor` — `(ScopeTopicId IS NULL AND ScopeArticleId IS NOT NULL) OR (ScopeTopicId IS NOT NULL AND ScopeArticleId IS NULL)`; `IX_AiSummaries_ScopeTopicId`; `IX_AiSummaries_ScopeArticleId`. Grounding: ≥1 row in AiSummarySourceLinks, enforced transactionally (R-010) + audited (SC-005); the check is universal — topic-scoped summaries link their grounding articles, article-scoped summaries link their anchor article.

### AiSummarySourceLink — `AiSummarySourceLinks`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| AiSummaryId | bigint FK → AiSummaries | NOT NULL, Restrict |
| ArticleId | bigint FK → Articles | NOT NULL, Restrict |

Constraints/indexes: `UX_AiSummarySourceLinks_AiSummaryId_ArticleId` unique. The summary + its first link are written atomically; links cannot be deleted.

### TopicArticleLink — `TopicArticleLinks`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| TopicId | bigint FK → Topics | NOT NULL, Restrict |
| ArticleId | bigint FK → Articles | NOT NULL, Restrict |
| LinkedAtUtc | datetime2(0) NOT NULL | |

Constraints/indexes: `UX_TopicArticleLinks_TopicId_ArticleId` unique (at most one link per pair, FR-007); `IX_TopicArticleLinks_ArticleId` (reverse traversal). Feature 006 adds provenance columns (rule, matched text, confidence, matched-at) — additive.

### User — `Users`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| IdentityReference | nvarchar(256) NOT NULL | Operator-provisioned, auth-agnostic (clarified FR-009); credentials never stored here |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |

Constraints/indexes: `UX_Users_IdentityReference` unique.

### ProcessingJob — `ProcessingJobs`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| JobType | nvarchar(64) NOT NULL | e.g., `trend-collection` |
| State | tinyint NOT NULL | `ProcessingJobState` |
| StartAtUtc / FinishAtUtc | datetime2(0) NULL | UTC timing |
| DiagnosticsReference | nvarchar(1024) NULL | Pointer to diagnostics detail |
| CreatedAtUtc / UpdatedAtUtc | datetime2(0) NOT NULL | |
| RowVersion | rowversion | |

Constraints/indexes: `IX_ProcessingJobs_JobType_State_CreatedAtUtc` (jobs by type/state and time). Feature 013 adds trigger/counts/correlation columns — additive.

### ProcessingJobAttempt — `ProcessingJobAttempts`

| Field | Type | Notes |
|---|---|---|
| Id | bigint PK | |
| ProcessingJobId | bigint FK → ProcessingJobs | NOT NULL, Restrict |
| AttemptNumber | int NOT NULL | 1-based ordering |
| StartedAtUtc / FinishedAtUtc | datetime2(0) NULL | UTC timing |
| Outcome | tinyint NULL | `ProcessingJobAttemptOutcome` once finished |
| DiagnosticsReference | nvarchar(1024) NULL | |

Constraints/indexes: `UX_ProcessingJobAttempts_ProcessingJobId_AttemptNumber` unique (append-only; prior attempts are never overwritten).

## Deterministic rules (delivered as `HomNayLive.Domain/Normalization`)

1. **NameNormalizer** (topics + aliases): trim → collapse whitespace runs → Unicode NFC → lowercase (invariant). Diacritics preserved; no locale-dependent behavior.
2. **UrlCanonicalizer**: lowercase scheme/host; punycode hosts; drop default ports; drop fragment; strip tracking params (configurable list); sort remaining query params; collapse duplicate path slashes; strip trailing slash except root; max length 2048.
3. **ContentHasher**: SHA-256 lowercase hex over NFC/whitespace-normalized, case-preserved text; output `char(64)`.
4. **Window descriptor semantics**: identity per (topic, provider, window instance); refresh-on-resample (provided fields overwrite; absent fields unchanged); boundaries supplied by the shared time-window module — never computed in persistence.

## Relationship matrix

| Relationship | Cardinality | Delete behavior |
|---|---|---|
| Topic → TopicAlias | 1–* | Restrict |
| Topic → TopicTrendObservation | 1–* | Restrict |
| Topic ↔ Article (TopicArticleLink) | *–* (≤1 per pair) | Restrict both sides |
| Article → ArticleSourceLink ← Source | *–* via link (≥1 per article; ≤1 primary) | Restrict both sides |
| AiSummary → scope target (Topic XOR Article) | *–1 (exactly one) | Restrict |
| AiSummary ↔ Article (AiSummarySourceLink) | *–* (≥1 per summary) | Restrict |
| ProcessingJob → ProcessingJobAttempt | 1–* | Restrict |

## Requirement → enforcement map (key items)

| Requirement | Mechanism |
|---|---|
| FR-002 topic uniqueness | `UX_Topics_NormalizedName` + NameNormalizer pipeline |
| FR-003 alias resolvability | Global unique `UX_TopicAliases_NormalizedValue` |
| FR-004 window identity / refresh | `UX_TopicTrendObservations_WindowInstance` + upsert semantics |
| FR-005 canonical identity + attribution | `UX_Articles_CanonicalUrl` + ArticleSourceLink (original URL, primary flag) |
| FR-008 summary grounding | Scope XOR CHECK + transactional ≥1 link write + restricted deletes + SC-005 audit |
| FR-011 UTC | Interceptor timestamps + datetime2(0) throughout |
| FR-014 indexes | Constraint/index set above |
| FR-015 integrity | All FKs Restrict; no cascades |
| FR-016 idempotency | Uniqueness constraints + upsert paths |
| SC-003 variant fixtures | Unit tests over NameNormalizer/UrlCanonicalizer |
| SC-005 / SC-007 audits | Audit queries in quickstart.md |

## Cross-feature deltas (informational — owned by their features)

- **004**: ingestion uses canonical URL + hash; original per-source URL stored on ArticleSourceLink.
- **005**: adds NormalizedUrl column, cross-source hash uniqueness policy, merge records; article retirement values.
- **006**: adds provenance columns to TopicArticleLinks.
- **013**: adds trigger/counts/correlation columns to ProcessingJobs.
- **019/020/021/024/025/026/027**: add their own entities/columns around this core without modifying these definitions (additive enum values where noted).
