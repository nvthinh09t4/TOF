# Feature Specification: Summary Validation & Content Policy (Deterministic, Reusable)

**Feature Branch**: `012-summary-validation`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Define validation and content rules for AI-generated news summaries. The system must ensure that generated summaries remain grounded in the supplied source content, avoid unsupported claims, avoid hallucinated facts, avoid reproducing long passages from source articles, and preserve source attribution. Define validation rules for article summaries and multi-article topic summaries. Define behavior when generated output fails validation, is incomplete, contains unsupported claims, or does not conform to the expected structured format. The policy must be implemented as a reusable application-level validation component rather than being embedded directly inside a specific AI provider. Do not implement generic content moderation or misinformation detection in this feature."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enforce grounding and unsupported-claim prevention (Priority: P1)

Before any generated summary can be stored, a reusable validation component checks the candidate against exactly the source material it was given: numbers, dates, and quoted text in the output must be traceable to the supplied sources; significant proper-name facts must appear in the source corpus; and any source or publisher named in the output must be one of the supplied sources. Violations are unsupported-claim (or fabricated-attribution) failures — the candidate is never stored.

**Why this priority**: This operationalizes the constitution's non-negotiable rule (grounded, attributable summaries); without it, hallucinated facts could reach readers.

**Independent Test**: Feed fixture candidates (valid, fabricated number, fabricated quote, unsupported proper name, referenced outlet not among sources) through the component; verify correct pass/fail verdicts with evidence, and that failing candidates are never stored.

**Acceptance Scenarios**:

1. **Given** a candidate containing a number or date not traceable to the supplied sources, **When** validation runs, **Then** the verdict is a categorized unsupported-claim failure naming the offending fragment, and nothing is stored.
2. **Given** a candidate quoting text that does not match the source material (beyond allowed normalization), **When** validation runs, **Then** it fails as a fabricated-quote violation.
3. **Given** a candidate naming a publisher not among the supplied sources, **When** validation runs, **Then** it fails as a fabricated-attribution violation (source attribution preserved).
4. **Given** a fully grounded candidate, **When** validation runs, **Then** it passes and becomes eligible for storage by the summarization workflow.

---

### User Story 2 - Structural conformance, completeness, and no long verbatim passages (Priority: P1)

Validation also enforces the expected structured format declared by the prompt version (required fields/sections, length bounds), completeness (not empty, not truncated, nothing incomplete), and originality limits (no long verbatim passages reproduced from sources: consecutive-token run caps and an overlap ratio limit, with only short quotes permitted within configured limits). Every failure is categorized with evidence sufficient to debug or tune.

**Why this priority**: Explicitly required by the user; structural garbage, truncated output, or copy-pasted passages must never be stored as a summary.

**Independent Test**: Fixture candidates that are structurally non-conforming, truncated, over-length, or contain long copied passages; verify categorized failures; verify in-limit short quotes still pass.

**Acceptance Scenarios**:

1. **Given** a candidate missing a required field or exceeding length bounds, **When** validation runs, **Then** it fails structural conformance with the field/limit identified (no force-fitting).
2. **Given** a truncated or empty candidate, **When** validation runs, **Then** it fails completeness with evidence.
3. **Given** a candidate reproducing a source passage beyond the configured run cap or overlap ratio, **When** validation runs, **Then** it fails the originality rule; a candidate containing only short in-limit quotes passes (boundary semantics defined in the rulebook).

---

### User Story 3 - Defined behavior for failing candidates (Priority: P2)

When a candidate fails validation (unsupported claims, incomplete text, structural non-conformance, or any other rule), the workflow behavior is fully defined: the candidate is never stored, the verdict is recorded with rule identity and evidence in run diagnostics, and an optional bounded regeneration policy may request fresh attempts with unchanged inputs (never hidden prompt mutation or repair heuristics); attempts and outcomes are recorded, and persistent failure leaves no summary rather than a degraded one.

**Why this priority**: The user explicitly asks for failure behavior; combined with reuse/invalidation (feature 010), this guarantees stored summaries are always validated summaries.

**Independent Test**: Run failing fixtures through the full flow; verify zero storage, complete diagnostics, bounded regeneration behavior, and that prompts/stored data are never mutated by failures.

**Acceptance Scenarios**:

1. **Given** a failing candidate, **When** the flow completes, **Then** no summary record exists for that target and the run records the failure category, rule, and evidence.
2. **Given** regeneration enabled with a configured attempt bound, **When** attempts are exhausted with failures, **Then** the target ends as a categorized failure with all attempts recorded — no degraded summary is stored.
3. **Given** any validation failure, **When** audits run, **Then** no prompt content or stored summary was modified as part of the failure handling.

---

### User Story 4 - Scope-specific rules for article and topic summaries (Priority: P2)

Rules are defined once in a single rulebook with scope-specific parameters: article-summary validation grounds against exactly the single supplied article, while topic-summary validation grounds against the union of the supplied (relevant, deduplicated) articles, with attribution membership checked against that supplied set and scope-specific thresholds where appropriate.

**Why this priority**: The user requires rules for both scopes; topic summaries span multiple sources and need union-corpus grounding and stricter attribution handling.

**Independent Test**: Scope fixtures (single-source article candidates; multi-source topic candidates including cross-source numbers, quotes attributed to the wrong supplied source, and attributions outside the set) verify scope-correct verdicts.

**Acceptance Scenarios**:

1. **Given** an article-level candidate, **When** validation runs, **Then** grounding is checked only against the single supplied article.
2. **Given** a topic-level candidate, **When** validation runs, **Then** numbers/quotes/proper names are checked against the union of supplied articles and attribution names against the supplied source set.
3. **Given** the same candidate against different scopes/corpora, **When** verdicts are compared, **Then** each verdict matches its scope's rulebook parameters deterministically.

---

### User Story 5 - Reusable, provider-independent validation component with an extension seam (Priority: P3)

Validation is a reusable application-level component invoked by the summarization workflow before storage — it lives in the application layer, depends on no AI provider, prompt internals, or domain entity, and runs fully offline and deterministically. A future stricter validator (for example, AI-assisted claim checking) can supplement it through the same contract without touching providers, prompts, or stored data models; generic moderation and misinformation detection are explicitly not part of this component.

**Why this priority**: Explicitly required architecture; it keeps policy out of providers and makes future strengthening additive.

**Independent Test**: Exercise the component directly in unit tests (no provider, no network); register an alternative stub validator through the contract and verify it plugs in with zero changes elsewhere.

**Acceptance Scenarios**:

1. **Given** the validation component, **When** it is tested offline, **Then** it requires no provider, no network, and no prompt internals — only candidate, corpus, output definition, and configuration.
2. **Given** an alternative validator registered through the contract, **When** it runs in the flow, **Then** verdicts flow through the same diagnostics path with zero changes to providers, prompts, or entities.
3. **Given** the component's scope, **When** it is audited, **Then** it contains no moderation, toxicity, or misinformation-detection logic.

---

### Edge Cases

- Fabricated numbers/dates: approximated or rounded values beyond configured tolerance fail; normalization covers separators, units, and number-format variants (rules fixed in the plan phase).
- Dates in different formats (numeric vs written forms) → normalized matching; unparseable or unmatched dates fail.
- Quotes with altered wording fail; quote-mark style, whitespace, and diacritic normalization are allowed equivalences.
- Proper-name detection edge cases (common words capitalized, locale heuristics for Vietnamese) → deterministic tokenization with configured exemption lists; no fuzzy fallbacks.
- Vietnamese diacritic variants → normalization equality; content preserved losslessly through validation.
- Overlap exactly at the configured boundary (run cap or ratio) → inclusive/exclusive semantics defined once in the rulebook.
- Short quotes within limits pass; adjacent quotes chained into a longer run are counted as one run and may exceed the cap (deterministic counting rule).
- Truncation signals (mid-word/mid-sentence endings, dangling markers) fail completeness; graceful complete endings pass.
- Unknown/extra fields in the candidate → handled per rulebook (reject or ignore deterministically); required-schema violations always fail.
- Corpus union for topic summaries contains only deduplicated supplied articles (feature 005 guarantees); duplicate passages across articles count once for overlap counting.
- No supplied sources (or empty corpus) → validation cannot run; the workflow must not even generate (feature 010 rule), and a defensive invocation fails deterministically.
- Multiple simultaneous violations → all are reported (no silent short-circuit), with per-rule evidence.
- Regeneration attempts also failing → bounded attempts recorded; final categorized failure; nothing stored.
- Rulebook/threshold changes → apply to new validations only; previously stored (validated) summaries are unaffected in the MVP (a future revalidation pass is separate work).
- Provider formatting noise (markdown artifacts, wrapping) → deterministic normalization rules either fold it away or fail structural conformance; never stored as-is.
- Attribution referencing another outlet or an unsupplied source → fail; references to supplied sources pass.
- Candidate exceeding maximum length → structural failure (no truncate-and-store).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Validation MUST be implemented as a reusable application-level component (contract owned by this feature) that the summarization workflow invokes before storage; it MUST NOT be embedded in AI providers, provider adapters, prompts, or core entities, and it MUST operate independently of any specific AI provider.
- **FR-002**: The component MUST accept: the candidate generated output, the exact supplied source corpus (grounding set), the expected structured output definition of the prompt version used, and the rulebook configuration; it MUST return a pass verdict or one or more categorized failures, each with rule identity and evidence (offending fragment/field).
- **FR-003**: Structural conformance rules MUST verify the candidate against the expected structured output definition (required fields/sections present, types respected, length bounds within configured minimums/maximums); non-conforming candidates MUST fail — never be force-fitted, truncated, or stored.
- **FR-004**: Completeness rules MUST reject empty, truncated, or incomplete candidates using deterministic signals defined in the rulebook (for example: missing required components, abrupt endings); complete outputs MUST pass.
- **FR-005**: Grounding rules MUST be deterministic: numbers, dates, and percentages in the output MUST be traceable to the supplied corpus under configured normalization/tolerance rules; quoted text MUST match source text under allowed normalization (quote style, whitespace, diacritics); significant proper-name facts MUST appear in the corpus (configured exemptions); untraceable content MUST fail as unsupported claims.
- **FR-006**: Attribution integrity rules MUST hold: any source or publisher named in the output MUST belong to the supplied source set; attribution statements referencing anything outside the supplied sources MUST fail (fabricated attribution); source attribution MUST never be removed, altered, or obscured by generated content.
- **FR-007**: Originality rules MUST prevent long verbatim reproduction: consecutive-token runs versus the corpus MUST stay within configured caps, and an overall overlap ratio limit MUST apply as a secondary rule; short quotes are permitted within configured limits; violations MUST fail with the offending passage identified; boundary semantics are defined once.
- **FR-008**: Scope-specific rules MUST be defined in one rulebook: article-level validation grounds against exactly the single supplied article; topic-level validation grounds against the union of the supplied relevant, deduplicated articles and checks attribution membership against that set; thresholds MAY differ per scope and are configuration.
- **FR-009**: Failure behavior MUST be defined and enforced: failing candidates MUST NOT be stored; verdicts (category, rule, evidence, attempts) MUST be recorded in the run's ProcessingJob diagnostics; an optional regeneration policy MAY retry with bounded, configured attempts using unchanged inputs only — repair heuristics that mutate prompts or inputs are out of scope; persistent failure MUST end with no summary rather than a degraded one.
- **FR-010**: This feature MUST NOT implement generic content moderation (toxicity/safety classification) or misinformation detection (semantic truth verification, AI claim checking); validation is deterministic and pattern/evidence-based in the MVP, and a future stricter validator MAY supplement it solely through the component contract.
- **FR-011**: Determinism and reproducibility MUST hold: identical candidate, corpus, output definition, and configuration MUST produce identical verdicts and evidence across runs; no randomness, no network access, no provider involvement.
- **FR-012**: All thresholds and knobs (overlap caps, quote limits, numeric tolerances, numeric/date normalization rules, proper-name exemption lists, minimum/maximum lengths, regeneration attempt bounds) MUST be configurable without code changes and single-sourced in the rulebook (constitution Principle IV); no secrets are involved.
- **FR-013**: Explainability MUST hold: every failure names its rule and provides evidence (offending fragment, field, or measurement versus threshold) sufficient to debug generation or tune thresholds.
- **FR-014**: Integration boundaries MUST be respected: the summarization feature (010) owns orchestration, reuse/invalidation, and storage decisions; prompt management (011) owns the expected output definition consumed here; the provider abstraction (002) owns provider calls; this feature owns only the validation rules and the component.
- **FR-015**: The component MUST be testable offline and deterministically: fixtures cover valid candidates, unsupported numbers/dates/quotes/names, fabricated attributions, long overlaps and in-limit quotes, truncation, structural violations, scope-specific cases, boundary semantics, multi-violation reporting, and regeneration exhaustion; no network access is required.
- **FR-016**: This feature MUST NOT modify stored summaries, prompts, entities, or provider code; it MUST NOT perform language translation, summarization, moderation, or misinformation detection; verdicts are diagnostics of the run, not new business entities.

### Key Entities

This feature introduces a validation component and rulebook plus verdict diagnostics; no new persisted business entities are required and nothing existing is modified.

- **Validation Component (application-service contract)**: the reusable, provider-independent validator invoked before storage; consumes candidate, corpus, output definition, and rulebook; returns pass or categorized failures with evidence; the MVP implementation is deterministic; the seam through which future stricter validators can supplement it.
- **Validation Rulebook**: the single-sourced configuration of rules and thresholds per scope (structural, completeness, grounding/traceability, attribution, originality/overlap, regeneration attempt bounds) with defined boundary semantics.
- **Validation Verdict**: the component's result shape — pass, or failures each carrying rule identity, category, and evidence — recorded in the run's ProcessingJob diagnostics (not a new stored business entity).
- **Read-only inputs (consumed)**: the candidate provider output, the supplied grounding corpus (stored articles from features 004/005/006/007), the expected structured output definition (feature 011), and the summarization workflow context (feature 010). None are written by this feature.
- **Fixtures**: deterministic offline corpora and candidate outputs covering every rule category, scope, boundary case, and failure mode used by component tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of fixture candidates containing untraceable numbers/dates/quotes/proper names or unsupplied-source attributions fail with the correct failure categories, and zero such candidates are ever stored.
- **SC-002**: 100% of long-verbatim fixtures (beyond configured run caps or overlap ratios) fail originality; 100% of in-limit short-quote fixtures pass (boundary semantics respected).
- **SC-003**: 100% of structural and completeness fixtures (missing fields, over/under length, truncated, empty) fail correctly; 100% of conforming complete candidates pass.
- **SC-004**: Determinism: identical candidate, corpus, definition, and configuration produce identical verdicts and evidence across 100% of reruns; the component runs with zero network and zero provider dependencies.
- **SC-005**: Scope correctness: article-level fixtures ground only against the single article and topic-level fixtures against the supplied union with attribution membership enforced — 100% correct verdicts per scope.
- **SC-006**: Failure behavior: 100% of failing candidates leave no stored summary, diagnostics record rule + evidence for every failure, and regeneration (when enabled) is bounded, records all attempts, and never mutates prompts or inputs.
- **SC-007**: Reusability and boundary compliance: an alternative validator stub integrates through the contract with zero changes to providers, prompts, or entities; an audit finds zero moderation/misinformation logic and zero provider coupling in the component.

## Assumptions

- **Deterministic-only scope**: the MVP validates traceable patterns (numbers, dates, quotes, proper names, attribution membership) and originality mechanics; it does NOT attempt semantic entailment or truth verification — stated limitation, with future AI-assisted claim checkers supplementing through the contract (feature 010's provider abstraction remains the only AI path).
- **Grounding corpus**: validation uses exactly the sources supplied to generation (the grounding set from feature 010), not the whole article store; corpus articles are already deduplicated (005) and relevance-gated (007) upstream.
- **Text-analysis approach**: tokenization, numeric/date normalization, quote matching, and proper-name detection are deterministic rulebook algorithms fixed in the plan phase; locale-aware heuristics for Vietnamese are configured once; no NLP/AI dependencies.
- **Verdicts as diagnostics**: failures are recorded in run diagnostics (ProcessingJob records per the established pattern); no new business entity is stored for verdicts in the MVP.
- **Regeneration policy**: bounded, configurable attempts with unchanged inputs only; the MVP default attempt count is fixed in the plan phase (including possibly zero); no feedback-loop prompt mutation (no hidden behavior).
- **Stored summaries**: previously stored summaries were validated when generated; rulebook changes apply to new validations only; retroactive revalidation is separate future work.
- **Boundaries**: no generic moderation/misinformation detection, no language checks, no presentation, no prompt changes, no provider changes, no workflow duplication (all explicitly out of scope).
- **Architecture**: application-layer component in the modular monolith; pure/deterministic execution; no network, no secrets; UTC timestamps appear only in run records (constitution Principle V applies to those records).

**Dependencies**: Feature 010 (ai-summarization — owns invocation timing, regeneration attempts, reuse/invalidation, and storage decisions; this feature supplies the rules and component it calls); Feature 011 (prompt management — supplies the expected structured output definition and the prompt-version traceability that validation verdicts reference); Feature 001 (AiSummary and source links — read-only context); Feature 002 (provider boundary — validation never lives in adapters); Feature 004/005/006/007 (the grounded, deduplicated, relevant article corpus used as the validation corpus); Project Constitution v1.0.0 (Principle II — this feature operationalizes grounded/attributable summaries; Principle III — deterministic MVP without moderation scope creep; Principle IV — configurable rulebook; Principle V — UTC in run records); deterministic offline fixtures for testing.
