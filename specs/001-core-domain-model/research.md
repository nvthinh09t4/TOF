# Research: Core Domain & Persistence Model (Feature 001)

All Technical Context unknowns are resolved below; no NEEDS CLARIFICATION items remain. Each decision lists rationale and rejected alternatives.

## R-001 — Repository & project layout

- **Decision**: Two production projects — `src/HomNayLive.Domain` (entities + pure deterministic rules; no EF Core reference) and `src/HomNayLive.Persistence` (DbContext, entity configurations, interceptors, migrations) — plus `tests/HomNayLive.Domain.Tests` (offline unit tests) and `tests/HomNayLive.Persistence.Tests` (SQL Server LocalDB integration tests). Root namespace `HomNayLive`; solution `HomNayLive.sln` at the repository root.
- **Rationale**: smallest split that honors the modular-monolith mandate and keeps persistence concerns out of the domain; later features (provider adapters, scheduler, API) add projects without reshaping this one.
- **Alternatives considered**: single merged project (domain becomes EF-coupled — rejected); full Clean Architecture layer split (application/infrastructure/API layering is speculative for the current spec — rejected per Principle III).

## R-002 — Runtime and libraries

- **Decision**: .NET 10 (LTS) with EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`); xUnit for tests.
- **Rationale**: .NET 10 SDK is installed on the dev machine (10.0.302); EF Core 10 matches the runtime; both are the current LTS line.
- **Alternatives considered**: .NET 8/9 (older LTS; no benefit — rejected); Dapper/raw SQL (loses mapping/migration productivity; EF Core is mandated by the spec — rejected).

## R-003 — Identifier strategy (resolves the deferred clarification item)

- **Decision**: `BIGINT IDENTITY` surrogate keys (`long Id`) on every record; identity values are never reused in practice (SQL Server does not recycle them); relationships reference these keys.
- **Rationale**: single-instance SQL Server with heavy time-series joins — clustered-index locality matters for SC-006; creation order is meaningful (earliest-discovery election, future merge rules); public enumeration is a non-issue for anonymous public content.
- **Alternatives considered**: UUIDv7/GUID (portable and non-enumerable but wider clustered keys; no distributed requirement to justify the cost — rejected); natural keys such as canonical URL as PK (wide, mutable when canonicalization is corrected — rejected).

## R-004 — Topic/alias name normalization (FR-002, FR-003)

- **Decision**: one shared pipeline for both topic names and alias values: trim → collapse internal whitespace runs to a single space → Unicode NFC → invariant-culture lowercase. Diacritics are significant content (never stripped); diacritic-free variants are modeled as aliases. Normalized columns use `Latin1_General_100_BIN2` collation so uniqueness is exact byte equality of the normalized form; the application always writes normalized values through the shared `NameNormalizer`.
- **Rationale**: deterministic across platforms and SQL collations; NFC unifies composed/decomposed sequences (the primary Unicode hazard for Vietnamese input); app-side case folding avoids culture-dependent index behavior.
- **Alternatives considered**: case-insensitive collation on the unique index (nondeterministic, varies across SQL versions — rejected); stripping diacritics inside the uniqueness key (destroys Vietnamese semantics and merges distinct words — rejected; alias mechanism covers variants).

## R-005 — Article URL canonicalization (FR-005) and per-source original URL

- **Decision**: `UrlCanonicalizer` applies: scheme and host lowercased; IDN hosts to punycode; default ports removed; fragment dropped; tracking parameters removed (configurable list — initial: `utm_*`, `fbclid`, `gclid`, `mc_cid`, `mc_eid`); remaining query parameters sorted; repeated path slashes collapsed; trailing slash stripped (except root). `Article.CanonicalUrl` is unique (BIN2 collation, ≤2048). The exact URL an article was discovered at in each feed is preserved on `ArticleSourceLink.OriginalUrl`.
- **Rationale**: implements FR-005 deterministically; the per-source original URL preserves provenance/verifiability (Principle II) and satisfies feature 004's original-URL retention on the record where it naturally belongs (one URL per article–source discovery).
- **Alternatives considered**: stripping all query parameters (breaks legitimate query-based article URLs — rejected); preserving fragments/ports (defeats duplicate prevention — rejected).

## R-006 — Content fingerprint (FR-005)

- **Decision**: `ContentHash char(64)` = lowercase hex SHA-256 over the article text after shared text normalization (NFC, trim, collapse whitespace; case preserved); nullable; non-unique index for duplicate-detection queries.
- **Rationale**: deterministic and collision-safe; case-preserving normalization is the conservative default (does not merge case-different content); uniqueness of duplicates remains anchored on canonical URL in this feature.
- **Alternatives considered**: MD5/SHA-1 (obsolete — rejected); making the hash unique now (would reject legitimate boilerplate sharing; feature 005 owns cross-source uniqueness policy — rejected).

## R-007 — Observation window descriptor (FR-004, constitution V)

- **Decision**: `TopicTrendObservation` stores the window descriptor as data: `WindowType` (enum: last-5-hours | today | yesterday), `WindowStartUtc`, `WindowEndUtc` (UTC). Unique identity = (TopicId, ProviderKind, ProviderKey, WindowType, WindowStartUtc). Re-sampling the same window instance refreshes `ObservedAtUtc`, `Rank`, `Score`, and `MetadataJson` (fields provided by the sample overwrite; absent/null fields leave existing values); prior window instances remain append-only history.
- **Rationale**: implements the clarified semantics exactly; boundary computation stays exclusively in the shared time-window module (constitution V) — persistence never computes windows.
- **Alternatives considered**: free-form timestamp buckets (no deterministic identity — rejected); multiple samples per window (explicitly rejected by the clarification answer).

## R-008 — Provider identity as data (FR-013)

- **Decision**: `ProviderKind nvarchar(32)` + `ProviderKey nvarchar(64)` on observations (e.g., `trend` / `google-trends`); `ProviderKey` + `ModelKey nvarchar(128)` on summaries. No provider enums that would require code changes; provider-specific extras go to metadata JSON.
- **Rationale**: providers are added/replaced by data and adapters only (Principle I); short stable strings index efficiently.
- **Alternatives considered**: provider catalog table with FK (extra join and a migration per onboarding; no MVP benefit — rejected).

## R-009 — JSON metadata policy (FR-012)

- **Decision**: nullable `MetadataJson nvarchar(max)` with `CHECK (ISJSON(MetadataJson) = 1)` on TopicTrendObservation, Article, and AiSummary. Metadata never participates in unique constraints, foreign keys, or indexed/filtered predicates.
- **Rationale**: exactly the spec's policy; ISJSON prevents non-JSON garbage from entering extensible fields.
- **Alternatives considered**: key/value metadata tables (queryable but speculative — rejected); omitting metadata entirely (breaks provider isolation — rejected).

## R-010 — Integrity, deletion semantics, and the summary-grounding rule (FR-015, US4)

- **Decision**: every foreign key uses `DeleteBehavior.Restrict` (SQL NO ACTION) — no cascades anywhere; lifecycle changes occur through state columns, never deletes. The "summary requires at least one grounding link" rule is enforced transactionally by the persistence write path: the summary and its first derivation link commit in one transaction, and link rows cannot be deleted (restricted FKs). Integration tests prove a dangling summary cannot be committed, and the SC-005 audit query verifies zero violations.
- **Rationale**: SQL Server cannot express "at least one child row" declaratively, and it cannot defer trigger validation to commit (derivation links necessarily insert after the summary row within the same transaction). Transactional enforcement is the only sound relational approach without triggers; triggers were rejected as they break valid two-step inserts.
- **Alternatives considered**: `AFTER INSERT` trigger on summaries (fails valid same-transaction link inserts — rejected); deferred constraints (not available in SQL Server — rejected).

## R-011 — Concurrency tokens

- **Decision**: `rowversion` on mutable records — Topic, TopicAlias, TopicTrendObservation, Article, Source, AiSummary, ProcessingJob. Append-only records (links, attempts) carry none. The observation upsert path retries on optimistic-concurrency conflicts.
- **Rationale**: protects update/refresh races without locking; append-only rows cannot conflict.
- **Alternatives considered**: pessimistic locking (unnecessary at single-instance MVP — rejected); no tokens (lost updates during re-sampling — rejected).

## R-012 — UTC audit timestamps (FR-011)

- **Decision**: `CreatedAtUtc` / `UpdatedAtUtc` `datetime2(0)` on all top-level records, populated by a `SaveChanges` interceptor using an injected `TimeProvider`; no database defaults for them.
- **Rationale**: testable determinism (fake TimeProvider), one source of truth, no local time anywhere.
- **Alternatives considered**: SQL defaults `SYSUTCDATETIME()` (fights the save pipeline, less controllable in tests — rejected); per-entity constructor timestamps (scattered, inconsistent — rejected).

## R-013 — Testing strategy

- **Decision**: xUnit unit tests for `NameNormalizer`, `UrlCanonicalizer`, and `ContentHasher` (SC-003 fixture sets, fully offline). Integration tests against SQL Server LocalDB (`(localdb)\MSSQLLocalDB`, a fresh database per run) covering: unique constraints (normalized names, alias values, canonical URL, observation window identity, link pairs), restricted deletes, the filtered one-primary-link index, the summary grounding rule, rowversion concurrency, and index presence.
- **Rationale**: only real SQL Server enforces the "data store itself" guarantees the spec demands; LocalDB is offline-capable on Windows dev machines.
- **Alternatives considered**: EF InMemory provider (enforces no SQL constraints — banned for these tests); SQLite (different constraint/collation semantics — rejected); Testcontainers/Docker (heavier operational dependency; may be revisited in CI — deferred).

## R-014 — Migrations and connection configuration

- **Decision**: EF Core migrations live in `HomNayLive.Persistence/Migrations` (initial migration `InitialCoreModel`). The design-time factory reads `HOMNAY_DB_CONNECTION` from the environment, falling back to the LocalDB default; no connection strings or secrets are committed (constitution IV).
- **Rationale**: standard EF workflow; constitution IV compliance; quickstart commands stay simple and reproducible.
- **Alternatives considered**: hand-authored SQL scripts (loses EF tooling benefits — rejected); committed `appsettings` credentials (forbidden — rejected).
