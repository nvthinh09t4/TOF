# Feature Specification: Multilingual Support (English & Vietnamese First)

**Feature Branch**: `026-multilingual-support`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Design multilingual support across the existing core and application layers. Initially support English and Vietnamese. Use existing language fields where available rather than duplicating language information unnecessarily. Define language detection for Articles, language-aware Topic aliases, AI summary language selection, API language behavior, and frontend localization. Do not create separate Topic entities for each language. A Topic represents the underlying concept while aliases and localized content represent language-specific forms. Keep the design extensible for additional languages."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterministic Article language detection (Priority: P1)

Every ingested Article receives a language value using the existing language field: an explicit declaration (feed or source language) wins when present; otherwise a deterministic, offline content analysis resolves English or Vietnamese; ambiguous or too-short content is recorded as unknown rather than guessed. Detection never blocks ingestion, results are persisted once and re-derivable by a repeatable backfill pass, and no duplicate language fields are introduced.

**Why this priority**: Article language is the foundation every other multilingual behavior consumes (search filtering, summary grounding context, display logic).

**Independent Test**: Detection fixtures (English, Vietnamese with and without diacritics, mixed-language text, very short text, declared-feed cases) against the documented precedence rules; verify stored values, unknown handling, idempotent re-runs, and zero ingestion failures caused by detection.

**Acceptance Scenarios**:

1. **Given** an article whose feed declares a language, **When** ingestion stores it, **Then** the declared language is recorded without content guessing.
2. **Given** an undeclared article with sufficient English or Vietnamese text, **When** detection runs, **Then** the dominant language is recorded deterministically per the documented rules.
3. **Given** ambiguous or too-short content, **When** detection runs, **Then** the value is recorded as unknown (never a guess), and a later re-detection pass can fill it deterministically.

---

### User Story 2 - Language-aware topic aliases, one Topic per concept (Priority: P1)

A Topic remains a single entity per underlying concept across all languages — explicitly no per-language Topic entities, ever. Language-specific forms live in aliases (optionally tagged with their language) and in localized display content. Alias matching becomes language-aware on top of the existing normalization discipline, topics can carry aliases in multiple languages simultaneously, and API display names for a topic are elected deterministically for the requested language (requested-language alias → platform default → canonical).

**Why this priority**: The single-topic invariant is the core constraint the whole design must preserve; display names and cross-language discovery depend on it.

**Independent Test**: Fixtures with one topic carrying English and Vietnamese aliases; verify no duplicate topics are ever created, cross-language search finds the same topic, display-name election matches the documented chain, and alias uniqueness holds per language.

**Acceptance Scenarios**:

1. **Given** a topic with aliases in both supported languages, **When** display is requested in either language, **Then** the documented election chain picks the alias deterministically (fallbacks never split the topic).
2. **Given** a new alias in a language the topic lacks, **When** it is added per the existing alias discipline, **Then** the same single topic gains the alias (zero new topics) and subsequent displays may elect it.
3. **Given** a search term expressed in one language, **When** it matches an alias in another language, **Then** the same underlying topic is found and returned with a requested-language display name.

---

### User Story 3 - AI summary language selection (Priority: P1)

The platform generates summaries in a configurable set of summary languages (initially English and Vietnamese). Each summary is produced in exactly one language, recorded with that language as part of its reuse identity, so generation, reuse, invalidation, and validation are fully independent per language. Grounding, attribution, and validation rules apply identically in every language; a failed or missing summary in one language never blocks or damages another.

**Why this priority**: Readers must be able to consume generated content in their language; per-language identity keeps reuse and cost deterministic.

**Independent Test**: Fixtures where grounding exists for one/two languages; verify per-language generation and reuse, independent invalidation, no mixed-language summaries (validation audits), and per-language failure isolation.

**Acceptance Scenarios**:

1. **Given** sufficient grounding and two configured summary languages, **When** summarization runs, **Then** each language yields its own summary record sharing the same grounding with independent identities.
2. **Given** a valid summary in one language, **When** the other language's grounding changes, **Then** only the affected language's summary is re-evaluated (the other is reused untouched).
3. **Given** summary text that fails validation in one language, **Then** that language's summary is not stored while the other language's summary remains intact.

---

### User Story 4 - API language behavior for localized reads (Priority: P2)

The versioned public API gains additive language behavior: callers may request a language explicitly (validated against the supported set; unsupported values fail deterministically), with a documented fallback to the standard request language preference and then to the platform default. Responses serve localized display content where it exists (topic display names, summary selection) with a deterministic fallback chain, while identifiers, codes, and structural fields remain language-neutral. Absent any language signal, responses match today's behavior exactly (backward compatible); cached variants are strictly separated per language.

**Why this priority**: All reader surfaces (including the frontend) need a defined negotiation contract before localization can be surfaced.

**Independent Test**: Golden tests for explicit parameter, preference fallback, platform default, and unsupported values; fallback-chain tests for display names and summaries; backward-compatibility snapshots with no language signal; cache-variant separation checks.

**Acceptance Scenarios**:

1. **Given** an explicit supported language, **When** any localized read is served, **Then** display content in that language is used where available and the deterministic fallback chain applies where not, with the served language disclosed.
2. **Given** no language signal, **When** any existing endpoint is called, **Then** the response is identical to today's behavior (documented default applied internally).
3. **Given** an unsupported language value, **Then** the request fails with the standard validation error shape and a language-neutral code.

---

### User Story 5 - Frontend localization (Priority: P2)

The web frontend localizes its entire interface through the existing strings module extended into en/vi resource sets: a language switcher, persisted user choice, deterministic selection (saved choice → browser preference → platform default), and every API request carrying the chosen language. Dates and times format per locale while window boundaries and labels continue to come from the API unchanged; document language attributes are set per rendered content; WCAG AA is maintained in both languages; source article text is never translated client-side.

**Why this priority**: Readers experience multilingual support through the UI; it depends on the API contract from the previous story.

**Independent Test**: Switch-language fixtures across all screens (chrome completeness audit), request-inspection tests (language parameter on every call), fallback renders for missing resource entries, locale formatting checks, and a11y verification in both languages.

**Acceptance Scenarios**:

1. **Given** the user switches to Vietnamese or English, **When** any screen renders, **Then** all interface chrome appears in the chosen language with saved state surviving reloads, and all API requests carry it.
2. **Given** generated content (e.g., summaries) available in the chosen language, **Then** it is displayed as-is with its language disclosed; source article text is never translated.
3. **Given** a missing resource entry in a locale, **Then** the platform-default string renders (no broken keys, no crashes) and the gap is detectable.

---

### User Story 6 - Extensible language model (Priority: P3)

Adding a language is a configuration-and-content operation: extending the supported-language configuration, providing its resource set (and summary language enablement where desired), and entering aliases as data. No schema redesign, no new per-language entities, and no contract redesign are required; a deterministic checklist and a fixture (a stubbed third language added through configuration and resources only) prove the extension path end-to-end.

**Why this priority**: Explicit extensibility requirement; it guards the design against hard-coded two-language assumptions.

**Independent Test**: A stubbed third language exercised through the full stack in tests (config + resources only, zero code/schema changes), plus audits that no logic hard-codes exactly two languages.

**Acceptance Scenarios**:

1. **Given** a new language enabled through configuration and resources, **When** fixture data exercises reads and summaries, **Then** the language behaves per the same rules without any schema or entity changes.
2. **Given** any multilingual logic, **When** audited, **Then** it operates on the configured language set (no two-language hard-coding).

---

### Edge Cases

- Article with English boilerplate (feed chrome) but a Vietnamese body → the dominant-language rule uses the weighted content profile (body over boilerplate per plan detail); result is deterministic and fixture-covered.
- Vietnamese title written without diacritics (common in practice) → the profile recognizes diacritic-less Vietnamese via stopword evidence; if still ambiguous, the value is unknown (never guessed).
- Mixed-language article (e.g., Vietnamese text with English technical terms) → one dominant language stored per documented threshold and tie rule; no per-fragment language is ever modeled.
- Feed declares a wrong language → the declared value wins precedence (explicit beats inferred); a corrected declaration plus the repeatable re-detection pass converge deterministically, with counts visible in run records.
- Topic has aliases in both languages, one language's alias missing for display → fallback chain (requested language → platform default → canonical) serves deterministically with the served language disclosed.
- Same literal alias text valid in both languages (proper nouns) → a single alias row, no duplication; language tag does not create duplicate alias identities.
- Summary exists for the platform default but not the requested language → fallback serves the available summary with its language disclosed; nothing is translated at read time.
- Summary generation/validation fails in one language → that language's summary is absent (normal "no summary" behavior) while other languages are unaffected.
- Unsupported or malformed language value in an API request → standard validation error shape with a language-neutral code; no partial localized output.
- Frontend language switch while a request is in flight → responses are tagged by their request language; late responses for a superseded language never render as the new language (deterministic discard/refetch rule in the plan).
- Browser preference outside the supported set → deterministic fallback to the platform default (documented, no error).
- Missing frontend resource entry → platform-default string renders with the gap detectable; no broken placeholders or crashes.
- Cache warm in one language, cold in another → variants never bleed; each language has independent entries with bounded sizes.
- Third language added later → existing content (aliases, summaries for enabled languages) behaves per the same rules; languages disabled by configuration stop being offered without deleting data.
- Search with a language filter while requesting another display language → filtering uses stored article language; results display in the requested language; unknown-language articles are excluded by the filter per existing search rules.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Supported languages MUST be configuration (initially English and Vietnamese) with a configurable platform default (Vietnamese initially, given the product's primary audience), standardized language values, and an extensible set — adding or disabling a language never requires schema or contract redesign.
- **FR-002**: Existing language fields MUST be used rather than duplicated: Article language detection results live in the existing Article language field; summary language uses the existing summary language dimension; further language designations are added only where none exists (alias language tagging if absent) and are mapped once in the plan — never storing the same language fact in multiple places.
- **FR-003**: The single-topic invariant MUST hold absolutely: one Topic entity per underlying concept across all languages; no per-language Topic entities ever; language-specific forms live exclusively in aliases and localized content; language NEVER affects topic identity, linking, clustering, or merge behavior.
- **FR-004**: Article language detection MUST be deterministic and offline with documented precedence: explicit declaration (feed-level or source-level language) → content-based profile analysis (English vs Vietnamese, robust to missing diacritics and mixed content via documented weighting) → unknown when evidence is insufficient; detection never blocks or fails ingestion; results are persisted once and idempotent across re-runs.
- **FR-005**: A repeatable re-detection/backfill pass MUST fill unknown or missing language values and apply corrected declarations/configuration deterministically (registered as a job type with recorded runs and counts; convergent; prospective only — no history rewriting beyond the stored language value).
- **FR-006**: Topic aliases MUST support an optional language designation while remaining valid without one; alias matching stays language-aware on top of the existing normalization discipline (diacritics, case, markup); aliases of the same topic may coexist in multiple languages; alias uniqueness gains the language dimension deterministically (exact rule in the plan as a refinement of the existing uniqueness constraint); the alias creation discipline is unchanged (never silently creates topics or duplicates).
- **FR-007**: Topic display names MUST be elected deterministically per requested language via the documented chain (alias tagged with the requested language → platform default language form → canonical name) with deterministic tie-breaks; election is a read-time computation (no stored per-language name copies beyond aliases/display rules).
- **FR-008**: AI summaries MUST be generated per configured summary language set (initially English and Vietnamese), one language per summary record, with the language part of the reuse/invalidation identity so generation, reuse, invalidation, and regeneration are fully independent per language.
- **FR-009**: Summary grounding, attribution, and validation rules MUST apply identically in every language (constitution principle II): every summary remains grounded in stored source articles with attributions; quoting/title handling is documented (original-language quotes allowed within validation limits; translated text never fabricates); per-language validation outcomes are isolated (one language's failure never blocks another).
- **FR-010**: Summary display selection MUST follow a documented fallback chain (requested language → platform default → any available) with the served language always disclosed to the caller so interfaces can indicate it; no machine translation occurs at read time.
- **FR-011**: The public API MUST gain additive language behavior following platform contract conventions: an explicit language parameter (validated against the supported set; deterministic validation errors with language-neutral codes), fallback to the standard request language preference mechanism, then the platform default; localized reads (topic display names, summary selection) use the fallback chain; identifiers, codes, and structure remain language-neutral; with no language signal the responses match current behavior exactly.
- **FR-012**: Caching MUST treat language as part of the key for localized projections: variants are strictly isolated (zero cross-language leakage), validators/ETags vary by language, and bounded entry counts account for the language dimension per the platform caching rules; user-independence of shared caches is preserved.
- **FR-013**: Search MUST be language-aware through topic identity: a term matching an alias in any language finds the same underlying topic, results display in the requested language, and the existing language filter continues to use stored article language with existing unknown-handling rules (unknown excluded).
- **FR-014**: The frontend MUST localize its full interface through the existing strings module extended into resource sets per supported language, with a language switcher, persisted choice, deterministic selection (saved choice → browser preference → platform default), and every API request carrying the chosen language; dates/times format per locale while window boundaries and labels continue to come only from the API (constitution principle V); rendered content language is declared via document language attributes; WCAG AA holds in every supported language; source article text is never translated client-side.
- **FR-015**: Extensibility MUST be verified: a third language exercised through configuration and resources only (stub fixture, zero schema or code changes) plus audits that no multilingual logic hard-codes exactly two languages; a documented add-a-language checklist covers configuration, resources, summary enablement, and alias data entry.
- **FR-016**: The feature MUST be testable offline and deterministically: fixtures for detection precedence (including diacritic-less and mixed Vietnamese), display-name election, per-language summary reuse/invalidation, fallback chains, API negotiation outcomes, backward-compatibility snapshots, cross-language cache isolation, and locale switching — without any external service.
- **FR-017**: Out of scope: languages beyond English and Vietnamese as content (extension is proven by fixture only), machine translation at read time, per-language Topic entities, server-side account language preference persistence (client-side plus request parameter now; account-level is a future seam), notification content localization (future seam over the notification snapshot design), admin console localization (deferred; admin surfaces remain as-is initially), right-to-left or non-Latin script support beyond the extensibility seam, and content moderation by language.

### Key Entities *(include if feature involves data)*

This feature uses existing entities wherever possible and introduces no per-language domain duplication.

- **Article (used)**: its existing language field is the single home for detection results (declared → detected → unknown); a repeatable backfill pass maintains it; no duplicate language state elsewhere.
- **Topic (used, unchanged)**: exactly one entity per concept across all languages — the invariant the design protects; language never appears as a partitioning dimension.
- **TopicAlias (refined)**: gains an optional language designation where none exists (mapped once in the plan); matching/normalization becomes language-aware; uniqueness refined with the language dimension; still never auto-creates topics or duplicates.
- **AiSummary (used, activated)**: the existing language dimension in its reuse/invalidation identity becomes actively used for per-language generation, reuse, validation, and display selection; no new summary entity.
- **Language configuration (no entity)**: supported language set, platform default, summary language enablement, and detection thresholds as configuration — the extension point for additional languages.
- **API language contract**: additive negotiation rules (explicit parameter → request preference → platform default), fallback chains for localized content with served-language disclosure, and language-keyed caching/validators.
- **Frontend localization resources**: per-language resource sets extending the existing strings module, language switcher state, and locale formatting rules.
- **Fixtures & harnesses**: detection golden sets, election-chain cases, per-language summary reuse cases, negotiation golden tests, cache-isolation checks, and a stubbed third-language extension fixture.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Detection correctness: 100% of the curated golden set (English, Vietnamese with/without diacritics, mixed, short, declared) yields the documented outcome; zero ingestion failures attributable to detection; repeat runs are idempotent.
- **SC-002**: Single-topic invariant: audits and fixtures show zero per-language topic duplication; cross-language alias search resolves to the same topic in 100% of cases; display-name election matches the documented chain in 100% of fixtures.
- **SC-003**: Summary language independence: generation, reuse, invalidation, and validation are per-language in 100% of fixtures; validation audits find zero mixed-language summaries; fallback chains serve exactly as documented in 100% of cases.
- **SC-004**: API behavior: negotiation outcomes (explicit, preference fallback, default, unsupported) match golden expectations in 100% of tests; no-language-signal responses are byte-identical to current behavior (backward compatibility); zero cross-language cache leakage in isolation checks.
- **SC-005**: Frontend localization: 100% of audited interface strings localize in both languages; every API request carries the chosen language (request inspection); missing-resource fallback renders platform-default strings with zero broken keys; WCAG AA verified in both languages; window boundaries remain API-sourced.
- **SC-006**: Extensibility: the stubbed third language passes the full exercise through configuration and resources only with zero schema or code changes; audits find zero two-language hard-coding.
- **SC-007**: Determinism and scope: repeated identical runs converge with no state drift; scope audits confirm no read-time machine translation, no per-language entities, and no duplicated language storage.

## Assumptions

- **Initial and default languages**: English and Vietnamese are the initial supported languages; the platform default is Vietnamese (configurable), reflecting the product's primary audience; both are LTR scripts.
- **Existing fields**: the Article language field exists from the ingestion feature; the summary language dimension exists from the summarization feature; the only possible one-time mapping addition is an optional alias language designation (mapped in the plan as a refinement) — no other schema growth is expected.
- **Alias tags are optional**: untagged aliases remain valid and are treated as language-unknown (matched language-agnostically); tagging improves election quality but is never required for correctness.
- **Detection method**: deterministic, offline character/stopword-profile analysis for the initial pair (no AI dependencies); an AI-based or provider-based detector is a future seam behind the same deterministic interface boundary.
- **Summary costs bounded**: per-language summaries respect the existing reuse/invalidation and single-flight rules; generation budgets remain governed by the summarization feature's configuration (no new cost model).
- **Frontend preference is client-side**: saved choice plus per-request language parameter; a server-side account-level language preference is a future seam (extending the personalization feature), not part of this specification.
- **Quotes and titles**: original-language quoted material is permitted within the existing validation limits; translated presentation of quotes is a plan-level detail that must not fabricate content (principle II governs).
- **Admin surfaces**: admin console localization is deferred; admin APIs/UI keep their current language behavior initially.
- **Notifications**: notification content localization is a future seam integrating with this feature; snapshots continue to behave as specified by the notifications feature for now.
- **Out of scope**: additional languages as delivered content, read-time machine translation, per-language Topic entities, RTL/non-Latin scripts beyond the extensibility seam, and language-based content moderation.

**Dependencies**: Feature 001 (core model — Article language field as the single storage point; optional alias language refinement mapped in the plan); Feature 004 (ingestion — declaration capture (feed/source language) and detection hook at ingestion); Feature 006 (matching — normalization discipline extended with language-aware alias matching; alias creation discipline unchanged); Feature 007/008 (relevance/clustering — language never affects linking, clustering, or topic identity); Feature 010 (summarization — the language dimension becomes active for per-language generation/reuse); Feature 011 (prompts — per-language prompt versions selected through the existing versioned mechanism); Feature 012 (validation — per-language validation with isolated outcomes); Feature 013 (scheduler — re-detection/backfill job type); Feature 014 (contract conventions — additive language parameter, fallback chains, served-language disclosure, backward compatibility); Feature 015 (search — cross-language alias resolution; language filter semantics preserved); Feature 016 (caching — language-keyed variants with strict isolation and bounded entries); Feature 017/018 (frontend — strings module extended into full localization with the switcher and request language propagation); Feature 023 (observability — language-neutral logs and metrics; redaction unchanged); Feature 025 (notifications — localization identified as future seam); Project Constitution v1.0.0 (Principle II — grounding/attribution/validation in every language; Principle III — explicit minimal scope, no translation features beyond the defined contracts; Principle IV — language sets and thresholds as configuration; Principle V — window labels/boundaries remain API-sourced with locale formatting only at the display edge).
