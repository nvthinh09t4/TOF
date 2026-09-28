# Feature Specification: Prompt Management (Versioned, Traceable)

**Feature Branch**: `011-prompt-management`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Design prompt management for AI-generated article and topic summaries. Prompts must be versioned and separated from business logic. Support different prompt templates for article summaries and topic summaries, with clearly defined input variables and expected structured outputs. Prompt versions must be persisted or otherwise reproducibly identifiable so every AiSummary can be traced to the prompt version that generated it. Allow future model/provider changes without changing the core AiSummary entity. Define how prompts are selected, activated, tested, and replaced. Do not implement the AI provider itself or duplicate the summarization workflow from the ai-summarization feature."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Versioned prompt definitions separated from business logic (Priority: P1)

Article-summary and topic-summary prompts are registered as versioned definitions in a prompt registry — template body, declared input variables, and expected structured output — persisted as data and changed without modifying application code. Every version has a reproducible identity (template key plus content hash) and is immutable from creation.

**Why this priority**: This is the feature's foundation: the summarization workflow (feature 010) explicitly excluded prompt content from code, so prompts must exist as versioned, external artifacts before any generation can run.

**Independent Test**: Register fixture versions for both use cases; verify they persist with full definitions and immutable content-addressed identities, and that no prompt text exists in application code.

**Acceptance Scenarios**:

1. **Given** an article-summary template and a topic-summary template, **When** versions are registered, **Then** each version is persisted with its body, declared variables, expected output definition, content hash, and creation time (UTC).
2. **Given** a persisted version, **When** any modification is attempted, **Then** it is rejected — changes always create a new version (immutability).
3. **Given** the same template content registered twice, **When** identity is compared, **Then** the content hash makes the identity identical and re-registration is idempotent.

---

### User Story 2 - Traceability: every AiSummary resolves to its exact prompt version (Priority: P1)

Every summary stores the identity of the prompt version that generated it; prompt management guarantees that this identity always resolves to exactly one immutable version record, so any summary — past or present — can be traced back to the exact prompt text used, even after replacements or provider changes. Versions referenced by any summary can never be edited or deleted.

**Why this priority**: Explicitly required by the user and by the summarization feature's metadata contract (provider, model, prompt version on every summary).

**Independent Test**: Generate summaries referencing fixture versions; attempt edits/deletions of used versions (must fail); resolve summary → version → content for every record (must succeed 100%).

**Acceptance Scenarios**:

1. **Given** a summary referencing a prompt version, **When** traceability is queried, **Then** the exact immutable version content is returned, reproducibly identified by its content hash.
2. **Given** a version referenced by at least one summary, **When** retention rules are applied, **Then** it can never be modified or deleted (guarded).
3. **Given** a replaced (superseded) version, **When** historical summaries reference it, **Then** resolution still succeeds and remains unambiguous.

---

### User Story 3 - Selection and activation per use case (Priority: P2)

Exactly one version is active per use case (article-summary, topic-summary), resolved from append-only activation records: activating a version records the reason note, previous version, and UTC timestamp; rollback means activating a previous version; concurrent activations resolve deterministically. When no version is active (and none is pinned), the summarization workflow is disabled with an actionable error — never a hidden default.

**Why this priority**: The user requires defined selection and activation; the summarization feature requires deterministic selection and fail-fast behavior when nothing is configured.

**Independent Test**: Activate fixture versions per use case; verify deterministic resolution; roll back; simulate concurrent activations; verify history, single active version, and fail-fast behavior with none configured.

**Acceptance Scenarios**:

1. **Given** multiple validated versions for a use case, **When** one is activated, **Then** it becomes the deterministic selection for that use case and the activation is recorded with the previous version and UTC timestamp.
2. **Given** an active version, **When** the previous version is re-activated (rollback), **Then** the activation history records the change and selection follows it deterministically.
3. **Given** no active version for a use case, **When** selection is requested, **Then** an actionable configuration error is produced (no hidden default prompt is ever used).
4. **Given** concurrent activation requests, **When** they complete, **Then** the outcome is deterministic (latest activation wins with a defined tie-break) and exactly one version is active.

---

### User Story 4 - Validation and testing before activation (Priority: P2)

A version can only be activated after passing deterministic, offline validation: the template parses, its placeholders exactly match the declared input variables, required/optional declarations are consistent, the body is non-empty, the expected structured output definition is well-formed, and deterministic rendering tests pass with sample variable fixtures. Failing versions are rejected with actionable diagnostics.

**Why this priority**: The user explicitly asks how prompts are tested; activation gating is the MVP's quality mechanism, and the output definition feeds the summarization feature's structured response validation.

**Independent Test**: Register invalid fixture versions (mismatched variables, empty body, malformed output definition) and verify rejection; register a valid version and verify end-to-end activation; verify rendering is byte-identical across reruns.

**Acceptance Scenarios**:

1. **Given** a template whose placeholders do not match its declared variables, **When** it is registered, **Then** validation fails with actionable diagnostics and the version cannot be activated.
2. **Given** a validated version, **When** rendering runs with sample variable fixtures repeatedly, **Then** outputs are byte-identical across runs (deterministic rendering).
3. **Given** a rendering request missing a required variable, **When** rendering runs, **Then** it fails deterministically with a clear error (no silent empty substitution).

---

### User Story 5 - Replacement without core changes (Priority: P3)

Replacing a prompt means registering a new version and activating it: previous versions become superseded but remain resolvable forever, no in-place edits occur, no code changes are needed, and the core AiSummary entity is untouched. Prompt content stays provider-neutral, so future model/provider changes require neither prompt rewrites nor summary-model changes.

**Why this priority**: Explicitly required forward-compatibility; it is the operational payoff of the versioning design.

**Independent Test**: Replace an active prompt with a new version; verify old summaries still resolve to the old version, new generations use the new one, and zero code/model changes were required.

**Acceptance Scenarios**:

1. **Given** an active version, **When** a new validated version is registered and activated, **Then** subsequent selections use the new version while all historical references remain valid.
2. **Given** a provider/model change, **When** generation runs with unchanged prompt versions, **Then** prompts require no modification and the AiSummary model definition shows zero diffs.

---

### Edge Cases

- Template placeholders not matching declared variables (extra, missing, or misspelled placeholders) → registration rejected with diagnostics; never activated.
- Empty or whitespace-only template body, or unparseable template syntax → rejected.
- Expected structured output definition malformed or incomplete → rejected; activation gated on a well-formed definition.
- Missing required variable at render time → deterministic error; no silent empty substitution or partial render.
- Same content re-registered under the same template → identical content-addressed identity; idempotent (no duplicate version).
- Activation of a version that belongs to a different use case → rejected.
- Concurrent activations for one use case → deterministic single-winner resolution; history shows all attempts.
- Rollback to a previously active version → new activation record; no history rewritten.
- Summary referencing a version of a retired template → resolution still succeeds (immutability + retention guarantee).
- In-flight generation during a prompt replacement → completes with the version it started with; its summary remains traceable; subsequent runs use the newly activated version.
- Provider-neutrality violation in a template (provider-specific syntax) → flagged during validation where detectable; provider adaptation remains adapter territory (feature 002).
- Direct pinning of a specific version (tests/rollouts) vs. active-pointer selection → both deterministic; pinning never mutates activation history.
- Very long template bodies or schemas → bounded limits validated at registration; deterministic rejection beyond limits.
- Non-ASCII prompt content (Vietnamese) → preserved losslessly through storage and rendering.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Prompt management MUST be a standalone capability in which prompt definitions are versioned, persisted data separated from business logic; no prompt content may live as hidden constants in code. It MUST NOT implement any AI provider (owned by the provider abstraction) and MUST NOT duplicate the summarization workflow (orchestration, reuse/invalidation, response validation, and summary storage remain owned by the ai-summarization feature).
- **FR-002**: Distinct template use cases MUST be supported for article summaries and topic summaries; each template declares its use-case key, purpose, input variables (name, required/optional, meaning), and expected structured output definition.
- **FR-003**: Prompt versions MUST be immutable records with reproducible identities: template key, version label, content hash, template body, declared variables, expected output definition, creation time (UTC), and change notes; identical content yields an identical identity (idempotent re-registration); changes create new versions, never mutations.
- **FR-004**: Traceability MUST be guaranteed: every prompt-version identity stored on any summary resolves to exactly one immutable version record forever; versions referenced by at least one summary MUST NOT be modifiable or deletable (enforced), so every AiSummary remains explainable to its exact prompt text.
- **FR-005**: Rendering MUST be deterministic: given a version and variable values, rendering produces the exact input material consumed by the summarization workflow; missing or invalid required variables produce explicit errors (no silent defaults); identical inputs render byte-identically.
- **FR-006**: Selection MUST be deterministic: exactly one active version per use case, resolved from append-only activation records; explicit pinning by version identity is supported for tests/rollouts; when neither an active nor a pinned version exists, selection fails fast with an actionable configuration error (never a hidden default prompt).
- **FR-007**: Activation MUST be explicit and append-only: each activation records the version, previous version, reason note, and activation time (UTC); rollback is a new activation of a previous version; concurrent activations resolve deterministically (single winner via a defined tie-break) without corrupting history.
- **FR-008**: Registration validation MUST be deterministic and offline: template parseability, placeholder-to-declared-variable consistency, required/optional consistency, non-empty body, and well-formed expected output definition; failing versions are rejected with actionable diagnostics and cannot be activated.
- **FR-009**: Activation gating MUST require passing rendering tests: deterministic offline tests render each version with sample variable fixtures; a version that has not passed validation and rendering tests MUST NOT be activatable.
- **FR-010**: Replacement MUST follow the register-then-activate lifecycle: superseded versions remain resolvable forever (never deleted, never edited); replacing prompts requires no code changes and no AiSummary model changes.
- **FR-011**: Prompt content MUST be provider-neutral: no provider-specific syntax or model-specific formatting inside versions; provider adaptation belongs exclusively to the provider abstraction; future model/provider changes MUST NOT require prompt changes or core-model changes (version metadata may optionally note validated models as informational only).
- **FR-012**: The expected structured output definition declared per version MUST describe the required fields/sections of provider responses so the summarization feature can validate responses against it; changing the expected structure requires a new version (never an in-place edit).
- **FR-013**: Version and activation records MUST be retained permanently (no deletion tooling in the MVP); immutability and retention guarantees MUST be enforced at the data layer, not only in application logic.
- **FR-014**: Creating, validating, activating, retiring, and pinning prompt versions MUST require only data/configuration operations (no code changes, no secrets); all timestamps are UTC; no time-window arithmetic is involved (constitution Principle V).
- **FR-015**: The capability MUST be testable offline and deterministically: fixtures cover both use cases, valid and invalid templates, rendering determinism, activation/rollback/concurrency, idempotent re-registration, and traceability resolution; no network access is required.
- **FR-016**: This feature MUST NOT implement response-content validation policy, summary reuse/invalidation logic, or any summarization orchestration; those remain in their dedicated specs. It provides definitions, validation of definitions, rendering, selection, activation, and traceability only.

### Key Entities

This feature introduces new persisted records plus a validation/rendering contract; the core AiSummary entity is untouched (it merely references version identities).

- **PromptTemplate**: the logical prompt for a use case (article-summary, topic-summary): stable key, purpose description, declared input variables (name, required/optional, meaning), and expected structured output definition; owns an ordered set of versions.
- **PromptVersion (immutable)**: one immutable version record: template key, version label, content hash, template body, declared variables, expected output definition, created-at (UTC), change notes, informational validated-model metadata; identity is reproducible via the content hash; referenced by summaries as their prompt version identity.
- **PromptActivation (append-only)**: one record per activation event: use case/template, activated version, previous version, reason note, activated-at (UTC); the current active version is resolved deterministically from these records; rollbacks are new records.
- **Definition Validation & Rendering contract**: the deterministic rules validating templates (structure, variables, output definition, limits) and rendering versions with variable values into the exact input material consumed by the summarization workflow; errors are explicit and actionable.
- **AiSummary (core; referenced only)**: stores prompt-version identity per summary (per the ai-summarization feature); this feature guarantees resolvability and immutability of those identities and never modifies the entity.
- **Fixtures**: deterministic offline corpora (valid/invalid templates, variable sets, activation scenarios, concurrent activation cases) used for validation, rendering, activation, and traceability tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Registered fixture versions for both use cases persist with full definitions and content-addressed identities; a traceability audit resolves 100% of summary prompt references to exactly one immutable version record, and no prompt text exists in application code (audit = 0 findings).
- **SC-002**: Immutability holds in 100% of scenarios: zero modifications or deletions of used versions occur; historical summaries resolve to their exact prompt content after any number of replacements.
- **SC-003**: Validation exactness: 100% of invalid fixture templates (mismatched variables, empty bodies, malformed output definitions) are rejected with diagnostics; 100% of valid ones register and pass activation gating.
- **SC-004**: Rendering determinism: identical inputs render byte-identically in 100% of reruns; missing required variables produce explicit errors in 100% of cases (zero silent substitutions).
- **SC-005**: Activation lifecycle correctness: exactly one active version per use case at all times when configured; rollbacks and concurrent activations produce deterministic single-winner outcomes with complete append-only history in 100% of simulated scenarios.
- **SC-006**: Separation and neutrality: producing a new version, activation, or rollback requires zero code changes (verified by workflow); an audit finds zero provider-specific syntax in versions and zero provider dependencies in this feature.
- **SC-007**: Fail-fast behavior: with no active or pinned version, the summarization workflow is disabled with an actionable error in 100% of scenarios (no hidden default prompts anywhere).

## Assumptions

- **Storage (001 conventions)**: templates, versions, and activations persist in the platform database using the modular-monolith conventions from the core model; administration is done through data/configuration operations and seed scripts in the MVP — an admin UI is out of scope.
- **Selection scope**: one active version per use case in the MVP; per-language or per-segment variants are a future key extension that will not require AiSummary changes (identities already carry template key + version).
- **Rendering model**: deterministic template substitution with declared variables; the exact placeholder syntax and rendering rules are fixed in the plan phase; provider-specific formatting remains exclusively in provider adapters.
- **Output definition representation**: the expected structured output definition (fields/sections) uses a schema representation fixed in the plan phase; the summarization feature consumes it for structural response validation (editorial content policy remains a separate spec).
- **Testing definition**: "tested" means deterministic validation plus rendering tests plus activation gating in the MVP; prompt evaluation/quality scoring (A-B comparisons, model benchmarks) is out of scope and can build on versioning later.
- **Versioning scheme**: version labels increase monotonically per template; identity includes the content hash so identical content is idempotent and reproducible; versions are immutable from creation (not only from activation).
- **Retention**: versions and activations are retained indefinitely; no deletion tooling exists in the MVP; immutability/retention are enforced at the data layer.
- **Pinning**: explicit version pinning (by identity) is supported for tests and controlled rollouts; the default selection path remains the activated version; pinning is configuration, never mutation of history.
- **Boundaries**: the AI provider itself (feature 002 abstraction and its adapters), summarization orchestration and reuse/invalidation (feature 010), and content-validation policy (dedicated future spec) are explicitly out of scope.
- **No secrets/timestamps**: no credentials are involved; all timestamps are UTC; no window arithmetic (constitution Principle V).

**Dependencies**: Feature 010 (ai-summarization — the consumer contract: prompt-version identity stored on every summary, deterministic selection, fail-fast when unconfigured, and structural response validation against the declared output definition); Feature 001 (persistence conventions and the AiSummary entity to be referenced, never modified); Feature 002 (provider-neutral boundary: prompts render into the neutral input material defined by the AI provider contract; provider adaptation stays in adapters); Project Constitution v1.0.0 (Principles I — replaceable/provider-neutral integration, III — deterministic MVP with no hidden behavior, IV — data/configuration over code, V — UTC timestamps); deterministic offline fixtures for testing.
