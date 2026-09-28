# Feature Specification: Topic Detail Page (React Web App)

**Feature Branch**: `018-topic-detail-page`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Build the topic detail page using the stable trend API. Display: topic name; topic-level AI summary; relevant news articles; article summaries where available; publication time; source name; link to the original article; trend information where available. Clearly distinguish generated summaries from original source information. Support topics with no articles, articles without AI summaries, temporary API failures, and partial data. Do not implement topic timeline, related topics, personalization, or notifications here; provide reusable extension areas for those later features."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Display the topic's full context: name, trend info, AI summary, and attributed articles (Priority: P1)

Opening a topic (from the trending page or a direct link) loads its details from the versioned public API and renders: the topic name and trend information (where available), the topic-level AI summary (clearly labeled as AI-generated and visually distinct from source content), and the list of relevant news articles — each with its title, publication time where present, source name, a link to the original article, and its article-level AI summary where one exists. AI-generated text and original source information are never visually or semantically confusable.

**Why this priority**: This is the page's reason to exist — the attributed, grounded reading experience the whole platform pipeline was built to deliver.

**Independent Test**: With API fixtures across field combinations (summary present/absent, article summaries present/absent, trend info present/absent), render the page and verify every field's display rules, AI labeling, attribution, and distinction between generated and source content.

**Acceptance Scenarios**:

1. **Given** a topic with a summary and articles, **When** the page renders, **Then** the header shows the topic name and trend information where available, the summary block is labeled AI-generated with its generation time, and articles render with title, publication time, source name, original link, and article summary where available.
2. **Given** any AI-generated block, **When** it renders, **Then** it is visually and semantically distinguished from original source information (labels, distinct treatment), and source content is never styled as generated content or vice versa.
3. **Given** an article without a summary or without a publication time, **When** it renders, **Then** the absent parts are omitted gracefully (no placeholders, no fabricated values).
4. **Given** trend information fields present in the contract, **When** they render, **Then** they appear as descriptive context (labels/categories, window context, freshness) with no client-side computation.

---

### User Story 2 - Resilient experience: no articles, no summaries, failures, partial data (Priority: P1)

Every non-happy path has a designed experience: topics with no attributable articles still render their header and trend context with a clear no-news state (and no summary, per platform rules); articles without AI summaries render without them; temporary API failures show an error state with working retry; refresh keeps current content visible on failure (stale-while-error, consistent with the trending page); and any partial data (absent optional fields) degrades gracefully.

**Why this priority**: The user explicitly requires these states; a news platform frequently encounters thin topics and unreliable networks.

**Independent Test**: State fixtures (no articles, no topic summary, no article summaries, error responses, refresh failure after success, unknown topic id, absent optional fields) verify each state's rendering and behavior.

**Acceptance Scenarios**:

1. **Given** a topic with no articles, **When** the page renders, **Then** the header and trend context render, the summary area explains the no-news state, and the articles section shows a clear empty state (no error, no fabricated content).
2. **Given** an unknown topic id, **When** the page loads, **Then** a not-found state renders with navigation back to the trending page.
3. **Given** a first-load failure, **When** the error state renders, **Then** retry refetches and resolves on success; **Given** a refresh failure after successful load, **Then** existing content remains with a non-blocking notice.
4. **Given** any absent optional field, **When** the page renders, **Then** only that field's area is affected (graceful degradation), and the rest of the page stays fully usable.

---

### User Story 3 - Responsive, accessible reading experience (Priority: P2)

A long-form reading page that works comfortably on mobile and desktop: single-column reading layout on small screens, comfortable measure on large screens, touch-friendly controls, semantic structure (headings, lists, article elements, outbound-link semantics), keyboard operability with visible focus, and screen-reader announcements for state changes — consistent with the trending page's accessibility bar.

**Why this priority**: The user requires a usable desktop/mobile page; accessibility and responsiveness are baseline quality for reader-facing content.

**Independent Test**: Viewport fixtures plus keyboard-only and semantic/ARIA checks across all states; verify external links expose clear target semantics and controls remain operable.

**Acceptance Scenarios**:

1. **Given** mobile widths, **When** the page renders, **Then** content stacks in one readable column with no horizontal scrolling and touch-friendly controls.
2. **Given** desktop widths, **When** the page renders, **Then** the reading measure stays comfortable (bounded line length) with the article list laid out for scanning.
3. **Given** keyboard-only use, **When** navigating header, summary, article links, retry, refresh, and load-more, **Then** all are reachable and operable with visible focus, and status changes are announced.

---

### User Story 4 - Reusable components and clean extension areas for future features (Priority: P3)

The page is assembled from presentational, prop-driven, contract-typed components shared with the existing frontend library (status/skeleton components, strings/formatting module) plus new reusable ones (article list item, AI-labeled summary block). The layout exposes documented, empty extension seams — stable insertion points for future topic timeline, related topics, personalization hooks, and notifications — implemented without any feature logic, so those future features attach without reworking existing sections.

**Why this priority**: Explicitly required by the user (extension areas; no timeline/related/personalization/notifications now) and promised by the component-library design.

**Independent Test**: Render the article item and AI summary block in a second context (search-style list fixture) without modification; add a fixture section through an extension seam and verify existing sections and components require zero changes.

**Acceptance Scenarios**:

1. **Given** the article list item and AI summary block components, **When** used in a different context with compatible contract data, **Then** they render correctly without modifications.
2. **Given** the page layout, **When** a future section is inserted through a documented extension seam (fixture test), **Then** no existing section component is modified and the page remains fully functional.
3. **Given** component sources, **When** audited, **Then** zero data fetching and zero entity/provider references exist (fetching stays in page-level layers).

---

### Edge Cases

- Topic with no articles → header and trend context render; summary area explains why no summary exists (platform rule: no attributable news, no summary); articles section shows the no-news empty state; nothing fabricated.
- Article with no summary → summary area omitted for that item only; title/meta/attribution intact.
- Topic summary absent but articles exist → summary area shows an explanatory empty state (not an error) while articles render normally.
- Unknown/removed topic id → not-found state with a link back to trending (no crash, no raw error).
- Temporarily failing API → error state with retry on first load; stale-while-error on refresh — consistent with the trending page conventions.
- Partial data (absent optional fields such as trend info, publication time, or freshness) → only the affected element is omitted; layout remains stable (no layout shift).
- API adds new optional fields (additive evolution) → page tolerates and ignores unknown data without breakage.
- Very long topic names or article titles → wrapping/truncation rules keep the layout intact without hiding meaning (titles prioritize wrapping over truncation).
- Very long summaries → bounded blocks with comfortable reading measure; no layout breakage; content is never silently truncated with characters lost mid-word.
- Large article lists with many pages → load-more appends progressively in API order; no duplicates or skips.
- External links (original articles) → open safely (new context semantics), marked as external, and never styled as internal navigation.
- Language/diacritics (Vietnamese) → render correctly throughout; formatting utilities locale-aware.
- Browser back/forward navigation → returning to the page re-renders consistently from fetched data (no partial DOM state).
- Deep link to a topic that becomes unavailable later → not-found handling applies identically to in-app navigation.
- Summary generation between visits → the page reflects the latest validated summary on next load/refresh (append-only history stays server-side; the page never shows superseded or unvalidated content).
- Reduced-motion preference → skeletons/transitions respect user settings.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The page MUST be built with React and TypeScript in the existing frontend application, consuming ONLY the versioned public API contract (topic details and its paginated relevant-articles list); it MUST NOT reference internal entities, provider fields, or non-public data, and it MUST be deep-linkable by topic identifier.
- **FR-002**: The header MUST display the topic name and, where available from the contract, trend information (descriptive trend label/category, window context, freshness) — rendered as descriptive context only, with no client-side window arithmetic or computation (constitution Principle V).
- **FR-003**: The topic-level AI summary MUST render only when the API serves one (validated summaries only, per platform rules), MUST be labeled as AI-generated with its generation time, and MUST receive a visually and semantically distinct treatment; when absent, an explanatory inline state MUST render (including the no-news case), never a fabricated or empty summary.
- **FR-004**: The articles list MUST render each item with: title; publication time when present; source name; an outbound link to the original article URL; and the article-level AI summary when available — in exactly the API's order, paginated via load-more with no duplicates or skips and no client re-sorting.
- **FR-005**: Generated content and original source information MUST be clearly distinguished at all times: AI summaries carry persistent labels and distinct typography/surfaces; article titles/source metadata use source styling; no generated text may be presented as quoted source material or vice versa.
- **FR-006**: Attribution MUST be guaranteed on every article item: source name and original article link are always present (constitution Principle II); outbound links carry external semantics (new context, safe attributes) and visible indication that they leave the platform.
- **FR-007**: Empty and thin topics MUST be supported: topics with no articles render header/trend context plus a no-news empty state for the articles section and the explanatory summary absence; nothing is fabricated.
- **FR-008**: Failure handling MUST include: first-load error state with working retry; refresh that keeps content visible on failure (stale-while-error, consistent with the trending page); not-found handling for unknown/removed topics with navigation back to trending.
- **FR-009**: Partial data MUST degrade gracefully: every optional field's absence affects only its element; layout stays stable; unknown additive contract fields are tolerated without runtime failures.
- **FR-010**: Responsive design MUST follow the established breakpoints: single-column reading layout on small screens without horizontal scrolling and touch-friendly controls; comfortable reading measure on large screens; article list optimized for scanning.
- **FR-011**: Accessibility MUST meet the trending page's WCAG AA bar: semantic structure (headings, lists, article semantics), keyboard operability of all interactive elements with visible focus, screen-reader announcements for state changes, sufficient contrast, and reduced-motion respect; AI labels MUST also be conveyed non-visually (not by color alone).
- **FR-012**: Components (article list item, AI summary block, plus shared status/skeleton components from the existing library) MUST be presentational, prop-driven, contract-typed, and free of fetching logic; they MUST be reusable by future pages (search results, related topics, personalized feeds) without modification.
- **FR-013**: Extension areas MUST be provided as documented, empty insertion seams in the layout for future features (topic timeline, related topics, personalization hooks, notifications); implementing them later MUST NOT require modifying existing section components (verified via a fixture section).
- **FR-014**: The page MUST NOT implement out-of-scope features: topic timeline, related topics, personalization, notifications, authentication, writes, or real-time updates.
- **FR-015**: Formatting and copy MUST use the shared strings/formatting module (centralized strings, locale-aware time and number formatting, correct Vietnamese diacritics); no scattered user-visible literals.
- **FR-016**: Performance and behavior MUST remain smooth: skeleton-first loading with stable layout, bounded DOM growth across load-more pages, no layout shift on content arrival, and respect for HTTP caching semantics (conditional refresh; 304 causes no visual disruption).
- **FR-017**: Client-side business logic MUST NOT duplicate API semantics: no re-sorting, no recomputation, no manipulation of summaries; the typed client is the single gateway to backend data, and non-fatal errors are logged for diagnostics without exposing internals or secrets to users.

### Key Entities

This feature introduces frontend artifacts bound to the public API contract, reusing the existing frontend library; no backend entities or persistence are in scope.

- **Topic detail page (container)**: owns fetching (topic details + paginated articles), state orchestration (loading/error/not-found/refresh/partial), and composition of sections; the only layer with data-fetching responsibilities; deep-linkable by topic identifier.
- **Topic header component**: topic name plus optional trend-information block (descriptive label/category, window context, freshness) sourced entirely from contract fields.
- **AI summary block component (reusable, new)**: labeled, stylistically distinct generated-summary presentation with generation time and an explanatory absence state.
- **Article list & article list item components (reusable, new)**: title, optional publication time, source name, original-article outbound link (external semantics), optional AI-labeled article summary; prop-driven and fetch-free.
- **Shared components (reused from the trending page feature)**: skeletons, empty states, error-with-retry, refresh notice, load-more controls, strings/formatting module.
- **Extension regions**: documented empty insertion seams in the page layout for future timeline, related topics, personalization, and notification features (no logic implemented).
- **Typed API client additions**: contract-bound calls for topic details and its paginated article list (reusing the existing client layer and its caching semantics).
- **Fixture & test suite**: API fixtures per field/state combination, viewport and keyboard/a11y checks, a second-context reuse fixture, and an extension-seam fixture.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With API fixtures across all field combinations, the page renders every displayable element per its presence rule (100%); zero fabricated or placeholder values anywhere.
- **SC-002**: Attribution guarantee: 100% of article items display a source name and a working original-article link with external semantics; an audit finds zero items missing attribution.
- **SC-003**: Distinction guarantee: 100% of AI-generated blocks carry persistent labels (visual and non-visual) and distinct treatment; an audit confirms generated text is never styled or announced as source material and source metadata is never labeled as generated.
- **SC-004**: State coverage: loading, not-found, first-load error with working retry, refresh keep-stale-on-error, no-articles, no-summary, and partial-field scenarios all render their designed states in 100% of state fixtures.
- **SC-005**: Pagination and ordering: load-more appends in API order with zero duplicates and zero skips across multi-page fixtures (100%).
- **SC-006**: Responsive and accessible: at defined breakpoints no horizontal scrolling occurs and layouts match the specified rules; keyboard-only operation and automated semantic/ARIA audits pass in 100% of tested states; contrast targets and reduced-motion respect verified.
- **SC-007**: Reusability and extension seams: article item and AI summary components render correctly in a second context without modification (100% of reuse fixtures); inserting a fixture section through a documented seam requires zero changes to existing components (structural check passes 100%).

## Assumptions

- **Contract sufficiency (014)**: the topic details contract already exposes name, trend-signal context, topic summary view, and attributed article items (title, publication time, original URL, source, AI-labeled summary); any additional optional fields needed for trend information follow the additive-evolution reconciliation pattern established with the trending page feature (feature 017) — no new endpoints are expected.
- **Frontend consistency (017)**: the page reuses the trending page's stack, component library, state conventions (skeleton-first, stale-while-error refresh, retry), accessibility bar, and strings/formatting module; routing/path scheme (for example `/topics/:id`) is fixed in the plan.
- **Summary rules**: only validated, stored summaries are ever served by the API; the page shows exactly what is served — including the no-news case where no summary exists (constitution Principle II); it never requests regeneration or manipulates summary content.
- **Partial data semantics**: "partial" means absent optional fields or empty article sets within a successful response — whole-response failures use the error/retry path.
- **Extension areas**: implemented as empty, documented component seams (no visible placeholders required); future features (timeline, related topics, personalization, notifications) are separate specs that plug in without reworking existing sections.
- **Navigation**: entry comes from trending-page cards or deep links; the page provides accessible back navigation to trending; browser history behavior relies on standard routing.
- **No client persistence**: no localStorage/service-worker caching in the MVP; HTTP caching semantics from the API feature are respected.
- **Out of scope**: topic timeline, related topics, personalization, notifications, auth, writes, real-time updates, and any client-side computation of trend semantics.

**Dependencies**: Feature 014 (versioned public contract — topic details and paginated relevant-articles endpoints, AI-labeling fields, HTTP caching semantics); Feature 017 (frontend stack, shared components/status patterns, strings/formatting module, accessibility bar); Feature 016 (server-side caching improves perceived performance; nothing frontend-specific required); Features 006/007/010-012 (data provenance: relevance-gated attributed articles and validated, labeled summaries — consumed indirectly via the API); Project Constitution v1.0.0 (Principle II — attribution and AI labeling on every displayed item; Principle V — trend context from the API only, no client window math); API response fixtures, viewport fixtures, a reuse-context fixture, and an extension-seam fixture for testing.
