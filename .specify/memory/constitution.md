<!--
Sync Impact Report
- Version change: unversioned template → 1.0.0 (initial ratification)
- Modified principles:
  - [PRINCIPLE_1_NAME] → I. Replaceable Integrations (NON-NEGOTIABLE)
  - [PRINCIPLE_2_NAME] → II. Source Attribution and Grounded Summaries (NON-NEGOTIABLE)
  - [PRINCIPLE_3_NAME] → III. MVP Simplicity (YAGNI)
  - [PRINCIPLE_4_NAME] → IV. Configuration Over Code
  - [PRINCIPLE_5_NAME] → V. Consistent Time Windows
- Added sections: Technical Constraints; Development Workflow & Quality Gates;
  Governance (placeholder scaffold filled with concrete content)
- Removed sections: none
- Follow-up TODOs: none
-->

# HomNay Live Constitution

## Core Principles

### I. Replaceable Integrations (NON-NEGOTIABLE)

Every external capability — trend collection (Google Trends or alternatives), news
ingestion (configured RSS feeds), and AI summarization — MUST be accessed through a
small, documented internal interface owned by the core domain. Adding, replacing, or
removing a provider MUST NOT require changes to core domain logic or the presentation
layer; adapters MUST normalize external payloads so provider-specific shapes never
leak into the domain model.

Rationale: the MVP ships with specific sources, but sources and AI providers change.
Replaceability is the difference between a configuration change and a rewrite.

### II. Source Attribution and Grounded Summaries (NON-NEGOTIABLE)

Every trend, news item, and AI summary presented to users MUST name its source and
link to the original article. AI summaries MUST be derived strictly from collected
source material and MUST never appear without their source links; the system MUST NOT
present generated content that is not traceable to at least one stored source item.
When no attributable news exists for a trend, the trend is shown without a summary.

Rationale: readers must be able to verify claims and reach the original publishers;
ungrounded or unattributed AI output destroys trust and misuses source content.

### III. MVP Simplicity (YAGNI)

Implement the smallest system that satisfies the current specification. No speculative
features, no pluggability beyond the seams required by Principle I, no premature
optimization. Any complexity beyond that MUST be justified in the feature plan against
a current requirement; "we might need it later" is not a justification.

Rationale: the MVP exists to validate the model quickly; unneeded complexity slows
every subsequent change.

### IV. Configuration Over Code

The RSS feed list, enabled sources, provider selection, and time-window settings MUST
be changeable through configuration without modifying application code. Secrets and
API keys MUST come from environment variables or a secure store and MUST NOT be
committed to the repository.

Rationale: operators must be able to add or remove feeds and rotate providers without
a release; hermetic tests can rely on configuration fixtures.

### V. Consistent Time Windows

The presentation ranges — last 5 hours, today, yesterday — MUST be defined exactly
once in a shared module with documented boundary rules and the deployment's reference
timezone, and every component that filters, buckets, or labels content MUST use that
module. Timestamps MUST be stored in UTC and converted to the display timezone only at
the presentation edge; no ad-hoc date arithmetic is permitted elsewhere.

Rationale: trend views across the three ranges are only comparable when every part of
the system cuts time identically.

## Technical Constraints

- **Failure isolation**: every external call MUST have a timeout and per-source error
  isolation. A failing or slow source MUST degrade to partial results rather than
  block the whole presentation; failures MUST be reported in logs.
- **Secrets**: API keys and credentials for any provider MUST be provided via
  environment variables or a secure store; they MUST NOT appear in code, committed
  configuration, or logs.
- **Time and locale**: all persisted timestamps are UTC; the reference/display
  timezone is a configurable deployment setting documented in the feature spec.

## Development Workflow & Quality Gates

- Every feature `spec.md` MUST identify which integration interfaces it touches and
  whether it introduces a new provider; `plan.md` MUST record the Constitution Check
  against these principles.
- New or changed providers MUST ship with contract tests written against the provider
  interface; core logic tests MUST run without network access by using stub providers.
- Reviews MUST verify: attribution and original links on every rendered item
  (Principle II), no hardcoded source lists or secrets (Principles I and IV), and time
  bucketing only via the shared window module (Principle V).
- Adding a feed or provider MUST update the configuration documentation in the same
  change.

## Governance

- This constitution supersedes conflicting local practices and conventions. Dependent
  Spec Kit templates and commands (`/speckit-plan`, `/speckit-tasks`,
  `/speckit-analyze`) read this file at runtime and MUST NOT be edited to restate it.
- Amendments MUST be made through `/speckit-constitution` (or an equivalent change to
  `.specify/memory/constitution.md`) and MUST include a Sync Impact Report comment, a
  semantic version bump, and updated dates on this line.
- Versioning policy: MAJOR for backward-incompatible governance changes (principle
  removal or redefinition); MINOR for a new principle or materially expanded guidance;
  PATCH for clarifications, wording, and non-semantic refinements.
- Every plan's Constitution Check MUST verify compliance with each principle;
  violations MUST be recorded in the plan's Complexity Tracking with explicit
  justification, or the feature MUST be revised. Pull requests and reviews MUST verify
  compliance before merge.
- Use the feature `spec.md` and `plan.md` for runtime development guidance.

**Version**: 1.0.0 | **Ratified**: 2026-09-27 | **Last Amended**: 2026-09-27
