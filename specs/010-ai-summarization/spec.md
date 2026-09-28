# Feature Specification: AI-Powered Summarization (Grounded, Reusable)

**Feature Branch**: `010-ai-summarization`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Implement AI-powered summarization using the existing AiSummary core entity and IAiProvider abstraction. Support summarizing individual Articles and producing topic-level summaries from multiple relevant Articles. AI-generated content must be concise, factual, source-grounded, and clearly separated from original source content. Avoid unnecessary duplicate AI calls by reusing valid existing summaries and defining deterministic invalidation/reprocessing rules. Support provider failures, timeouts, retries, token/input limits, malformed responses, and structured output validation. Store provider, model, prompt version, language, timestamps, and other required metadata using the existing AiSummary model or controlled extension data. Do not couple the domain to OpenAI or any specific AI provider. Do not implement prompt management or content validation policy as hidden behavior; those concerns should be defined by their dedicated specs."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate article-level summaries (Priority: P1)

An article that has been ingested can be summarized individually: the generation flow passes the article's stored content through the AI provider abstraction, validates the structured response, and stores a concise, factual, source-grounded summary in the AiSummary entity — with provider, model, prompt version, language, and timestamp metadata — clearly separated from the original article content.

**Why this priority**: Article-level summaries are the building block of reader value and of topic-level summarization; the grounding and separation rules are non-negotiable (constitution Principle II).

**Independent Test**: Run generation for fixture articles; verify each stored summary references its source article, carries full metadata, contains only AI-generated text in the summary entity, and that no Article content record was modified.

**Acceptance Scenarios**:

1. **Given** an article with usable content, **When** generation runs, **Then** a summary record is stored with the summary text, source article link, provider, model, prompt version, language, and generated-at time (UTC).
2. **Given** a completed generation, **When** records are audited, **Then** the original Article and Source records are unchanged and the AI text exists only in the summary record.
3. **Given** an article with unusable or empty content, **When** it is processed, **Then** no summary is generated and it is counted in diagnostics.

---

### User Story 2 - Generate topic-level summaries from multiple relevant articles (Priority: P1)

A topic with attributable articles gets a topic-level summary grounded in a deterministically selected set of its relevant articles (via topic–article links gated by relevance): the summarized content is derived strictly from those stored sources, the derivation links are retained, and no summary is ever produced for a topic without attributable articles.

**Why this priority**: Topic-level summaries are the core reader-facing deliverable of the platform; the constitution forbids summaries that are not traceable to stored sources.

**Independent Test**: Run generation for fixture topics with varied article sets; verify conciseness, grounding (every summary re-derivable from the linked sources), retained derivation links, and correct no-op behavior for topics without articles.

**Acceptance Scenarios**:

1. **Given** a topic with several relevant linked articles, **When** generation runs, **Then** one topic-level summary is stored with derivation links to the articles used and full generation metadata.
2. **Given** a topic with no attributable articles, **When** it is evaluated, **Then** no summary is attempted or stored, and the topic is counted as skipped-no-sources.
3. **Given** a stored topic summary, **When** it is audited, **Then** its text is derived solely from the linked stored sources (no external or unsourced content) and all derivation links resolve.

---

### User Story 3 - Reuse valid summaries and avoid unnecessary AI calls (Priority: P1)

Before any provider call, the flow checks for an existing valid summary for the same target and generation parameters; valid summaries are reused with zero AI calls and zero new records, and deterministic invalidation rules define exactly when a summary is stale (grounding set changes, prompt version, provider/model, language, or generation parameters) and must be regenerated.

**Why this priority**: The user explicitly requires avoiding duplicate AI calls and defining deterministic invalidation/reprocessing rules; reuse also controls cost directly.

**Independent Test**: Generate a summary; rerun unchanged (expect zero calls, zero new records); change grounding, prompt version, model, language, or parameters one at a time and rerun (expect exactly one regeneration per affected identity).

**Acceptance Scenarios**:

1. **Given** a valid existing summary and unchanged inputs, **When** generation runs again, **Then** the summary is reused: no AI call and no new record.
2. **Given** a grounding change (a relevant article added or removed, or source content changed), **When** generation reruns, **Then** exactly one new summary record is produced and the previous record remains as history.
3. **Given** a prompt version, provider/model, language, or generation-parameter change, **When** generation reruns, **Then** the change invalidates per the rulebook and exactly one regeneration occurs per affected identity.
4. **Given** concurrent or overlapping generation triggers for the same identity, **When** they run, **Then** at most one provider call occurs per identity and no duplicate records are stored.

---

### User Story 4 - Robust generation: failures, limits, malformed responses (Priority: P2)

Generation survives provider failures, timeouts (with bounded retries from the provider abstraction), token/input limits (deterministic input construction and reduction), malformed provider responses (structured output validation), and cancellation: nothing invalid is ever stored, every failure is categorized in run diagnostics, and one failing target never blocks the rest of the run.

**Why this priority**: AI providers fail and misbehave routinely; the platform must degrade gracefully and never store unvalidated AI output.

**Independent Test**: Simulate provider timeouts, errors, refusals, malformed payloads, and oversized inputs; verify categorized outcomes, zero partial storage, bounded retries, deterministic input reduction, and run isolation.

**Acceptance Scenarios**:

1. **Given** a provider timeout or failure (after bounded retries), **When** the run ends, **Then** the affected target is categorized as failed with diagnostics and no summary record exists for it.
2. **Given** a response missing required structure or derivation references, **When** validation runs, **Then** the response is rejected, categorized, and nothing is stored.
3. **Given** a grounding set exceeding input budgets, **When** inputs are constructed, **Then** the set is reduced by the deterministic rulebook ordering and the provider receives identical inputs across identical reruns.
4. **Given** one failing target among many, **When** the run completes, **Then** other targets complete normally (isolation) and counts reflect per-outcome categories.

---

### User Story 5 - Provider neutrality with a versioned, swappable abstraction (Priority: P3)

The summarization flow depends only on the AI provider abstraction; providers are registered and selected through configuration, provider/model identity is stored as data on each summary, and swapping or adding providers requires no domain changes. Prompt content is referenced by version and supplied externally (owned by its dedicated spec), never embedded as hidden behavior in this feature.

**Why this priority**: Explicitly required (no OpenAI coupling) and it is the precondition for cost/quality experiments later without code rewrites.

**Independent Test**: Swap stub providers through configuration and rerun generation; verify identical flow behavior, correct stored provider identity, zero domain changes, and that no prompt text or content-validation policy exists in this feature's code.

**Acceptance Scenarios**:

1. **Given** two different stub providers registered, **When** each is configured and generation runs, **Then** records store the respective provider/model identity and the flow behaves identically.
2. **Given** the feature's codebase, **When** audited, **Then** no provider-specific types, no embedded prompt templates, and no content-validation policy logic exist; prompts are referenced by version only.

---

### Edge Cases

- Topic with no attributable articles → no summary attempted (constitution Principle II); counted as skipped-no-sources.
- Article with empty/boilerplate-only content → excluded from summarization with diagnostics; no fabricated summary.
- Grounding set changes after summarization (new relevant article arrives, an article is retired by deduplication, relevance classification shifts) → deterministic invalidation and exactly one regeneration; prior records kept.
- Provider returns a structurally valid but empty/too-long response → structured validation rejects per rulebook limits; categorized failure; nothing stored.
- Provider refusal or safety-blocked response → categorized failure (policy specifics owned by the dedicated content-validation spec); nothing stored.
- Oversized single article content → deterministic excerpt limits produce stable, reproducible provider inputs.
- Tie in deterministic grounding selection ordering → defined tie-breakers (score, recency, identifier) ensure identical inputs across reruns.
- Same knowledge summarized for article-level and topic-level scopes → separate identities and records; no cross-scope reuse.
- Language configuration change → invalidation of all affected summaries (new records); historical records preserve their original language.
- Concurrent generation for the same identity (two runs racing) → single-flight behavior; at most one provider call; deterministic outcome.
- Prompt version not configured → generation is disabled with an actionable configuration error (fail fast); no hidden default prompt is used.
- Provider swap mid-life → previous summaries remain valid per the rulebook only if provider is not part of their validity identity; otherwise regeneration occurs deterministically (rulebook-defined).
- Non-ASCII content (Vietnamese) → preserved without lossy transformation through input construction and storage.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Summarization MUST call AI exclusively through the existing provider abstraction (the AI summarization provider contract); the domain MUST NOT reference OpenAI or any provider-specific type, endpoint, or SDK — provider and model identity are stored as data on every result.
- **FR-002**: Two summary scopes MUST be supported: article-level (grounded in exactly one stored source article) and topic-level (grounded in one or more relevant stored articles selected from the topic's links and relevance results); every summary MUST retain at least one stored source article link, and topics without attributable articles MUST NOT be summarized.
- **FR-003**: Summary content MUST be derived strictly from the supplied stored source material (concise, factual, source-grounded); derivation links to all grounding articles MUST be stored and resolvable; no unsourced or external content may be introduced.
- **FR-004**: Every summary record MUST store at minimum: scope, target references (topic and/or article), the summary text, provider, model, prompt version identity, language, generated-at time (UTC), and the input fingerprint (grounding set + generation parameters); additional metadata may use controlled extension data of the AiSummary model.
- **FR-005**: AI-generated text MUST be clearly separated from original source content: stored only in the summary entity, never merged into Article or Source records, and always distinguishable by consumers through its stored provider/model/prompt metadata.
- **FR-006**: Before any provider call, the flow MUST check for an existing valid summary for the same identity; a valid summary MUST be reused with zero AI calls and zero new records.
- **FR-007**: Invalidation MUST be deterministic and rulebook-defined: a summary is stale when the grounding set changes (articles added/removed or source content changed), or the prompt version, provider/model, language, or generation parameters change; unchanged inputs remain valid (no wall-clock TTL in the MVP).
- **FR-008**: Regeneration MUST be append-only: a stale summary triggers exactly one new record per affected identity; previous records MUST remain queryable as history; the latest valid summary MUST be deterministically resolvable.
- **FR-009**: Duplicate AI calls MUST be prevented under concurrency: at most one in-flight generation per identity, enforced with data-layer constraints (no distributed locks), with deterministic conflict resolution and no duplicate records.
- **FR-010**: Provider failures, timeouts, retries, and cancellation MUST follow the provider abstraction's taxonomy: retries are bounded and adapter-internal; failures MUST NOT store partial or invalid content; each target's outcome MUST be categorized in the run's ProcessingJob diagnostics; one target's failure MUST NOT block others.
- **FR-011**: Provider responses MUST pass structured output validation before storage (required structure, non-empty bounded text, and derivation references for the supplied sources where the contract requires them); malformed or invalid responses MUST be rejected with categorized failure and stored nowhere; validation is structural and grounding-shape only — editorial content policy belongs to its dedicated spec.
- **FR-012**: Token and input limits MUST be managed deterministically: grounding selection and excerpt construction follow configured budgets and a deterministic ordering with defined tie-breakers (relevance band, score, recency, identifier); identical inputs produce identical provider inputs; hard-limit breaches MUST fail categorized rather than truncate unpredictably; output limits are enforced via the provider contract configuration.
- **FR-013**: Prompt management MUST NOT be implemented or hidden here: prompts are referenced by version identity and supplied through external configuration/registry owned by their dedicated spec; this feature MUST NOT embed prompt templates as hidden behavior, and generation MUST fail fast with an actionable error when no prompt reference is configured (no hidden defaults); content-validation policy MUST NOT be implemented here either.
- **FR-014**: Provider selection and registration MUST be configuration-driven through the existing abstraction (dependency injection), so swapping or adding AI providers requires zero domain changes; offline tests MUST run with deterministic stub providers and zero network access.
- **FR-015**: Each run MUST record counts (targets evaluated, reused, generated, failed, skipped-no-sources, invalidated) with per-outcome categories and UTC timings via ProcessingJob; repeated runs MUST be idempotent and convergent (zero delta after stabilization).
- **FR-016**: This feature MUST NOT modify matching, relevance, clustering, or scoring records; retired (merged) duplicate articles MUST NOT be used as grounding; presentation, notifications, source quality, translations, prompt management, and content-validation policy are out of scope.

### Key Entities

This feature consumes the core model and evolves the AiSummary entity minimally (a documented delta for the plan phase); no other entity changes.

- **AiSummary (core; used and evolved)**: stores one generated summary per record: scope (article-level | topic-level), target references (source article for article-level; topic plus grounding set for topic-level), summary text, provider, model, prompt version, language, generated-at (UTC), input fingerprint, controlled extension data. Every record retains at least one stored source article link; history is append-only; the latest valid record per identity is determined by fingerprint equality with current inputs.
- **Grounding Rulebook**: the single shared definition of source selection (active articles only; relevance-band gating; deterministic ordering and tie-breakers), input budgets and excerpt limits, fingerprint composition, and invalidation rules (configuration-backed).
- **Generation Run**: a ProcessingJob record per pass with counts, per-target outcome categories, UTC timings, and diagnostics.
- **AI provider abstraction (existing; consumed)**: the AI summarization provider contract from the provider abstraction feature — called with topic/article material and generation parameters; returns summary text plus provider/model identity; never bypassed and never replaced with provider-specific code.
- **Read-only inputs (core concepts used)**: Article and Source records, topic–article links, relevance evaluations (for grounding selection), and Topic records — read only; this feature writes only AiSummary records and run records.
- **Fixtures & stub providers**: deterministic offline corpora (articles, topics, links, relevance states) and stub AI providers with scripted outcomes (success, malformed, timeout, refusal) used for reuse, invalidation, validation, and failure tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The fixture corpus yields stored article-level and topic-level summaries with complete metadata (provider, model, prompt version, language, generated-at) and at least one source link each; all runs together perform zero writes to Article, Source, link, relevance, cluster, or score records (100%).
- **SC-002**: A grounding audit shows zero summaries without at least one stored source link, zero summaries for topics lacking attributable articles, and 100% of stored summaries re-derivable from their linked sources.
- **SC-003**: Reuse and invalidation exactness: unchanged inputs and parameters produce zero AI calls and zero new records in 100% of reruns; each material change (grounding, prompt version, provider/model, language, parameters) produces exactly one regeneration per affected identity per the rulebook.
- **SC-004**: Duplicate-call prevention: concurrent triggers for one identity result in at most one in-flight provider call and zero duplicate records in 100% of simulations; provider call counts in fixtures match the expected minimal counts.
- **SC-005**: Failure handling: simulated timeouts, provider errors, refusals, and malformed responses produce categorized outcomes with diagnostics, bounded retries, zero partial storage, and full run isolation in 100% of scenarios.
- **SC-006**: Deterministic limits: oversized grounding sets and content are reduced by the documented ordering with identical provider inputs across identical reruns in 100% of cases; hard-limit breaches fail categorized (zero unpredictable truncation).
- **SC-007**: Provider neutrality and separation: swapping stub providers via configuration requires zero domain changes; the build contains zero provider-specific dependencies; an audit finds no embedded prompt templates or content-validation policy; AI-generated text never appears in source entities (100%).

## Assumptions

- **AiSummary evolution (001 delta)**: the core entity gains a scope discriminator and an article reference for article-level summaries; topic-level summaries keep the topic reference plus derivation links; at least one source link is enforced for both scopes; the plan phase updates the mapping.
- **Grounding selection**: topic-level grounding uses active linked articles gated by a configurable relevance band; deterministic ordering with tie-breakers (relevance band, score, recency, identifier) produces stable provider inputs; retired duplicates are never grounded.
- **Validity identity**: a summary is valid when its stored fingerprint (grounding set with content hashes + provider + model + prompt version + language + generation parameters) equals the current desired inputs; invalidation is material-change only (no wall-clock TTL in the MVP; a TTL rule could be added later as configuration without model changes).
- **Concurrency**: single-flight per identity is enforced with data-layer constraints (for example, a unique in-flight identity or equivalent pattern); no distributed locks; deterministic conflict resolution.
- **History**: summary records are append-only; superseded records remain queryable for provider/model/prompt comparisons; latest-valid is derived, never stored as a mutable flag.
- **Prompts**: prompt content and versioning are owned by a dedicated prompt-management spec; this feature consumes version references through configuration; when no prompt reference is configured, generation is disabled with an actionable error — never a hidden default.
- **Validation scope**: structured output validation here is structural and grounding-shape only (required fields, bounded text, derivation references); editorial, safety, and content-policy validation is owned by a dedicated spec.
- **Language**: the generation language is configured and stored per record; language changes invalidate affected summaries; multilingual handling (translations, aliases) belongs to future specs; non-ASCII content is preserved losslessly.
- **Provider scope**: no specific AI provider is designated by this feature; providers implement the existing abstraction and are selected by configuration; offline tests use stub providers exclusively.
- **Out of scope**: prompt management, content-validation policy, presentation/notifications, source quality, translations, changes to matching/relevance/clustering/scoring.
- **Failure semantics**: retries are bounded and adapter-internal per the provider abstraction; cancellation is honored; nothing is stored unless validation passes; run records capture per-target outcome categories.

**Dependencies**: Feature 001 (core domain & persistence model — AiSummary scope evolution delta and Article read-only access); Feature 002 (AI provider abstraction — the AI summarization provider contract, error taxonomy, retries, isolation, registration, and stub patterns); Feature 003 and Feature 004 (topic and article corpus); Feature 005 (deduplicated article identity; retired duplicates excluded from grounding); Feature 006 (topic–article links for grounding selection); Feature 007 (relevance evaluations gating grounding selection); Project Constitution v1.0.0 (Principles I — provider-neutral replaceable integration, II — grounded summaries with source links, III — no hidden prompt/policy behavior, IV — configuration-driven providers and secrets, V — UTC timestamps); deterministic offline fixtures and stub providers for testing.
