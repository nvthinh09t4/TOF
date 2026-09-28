# Coding Conventions & Best Practices

This repository (WritingUniverse) is a **spec-driven** project: every feature is planned in `specs/NNN-<feature>/` (`spec.md`, `plan.md`, `tasks.md`, `quickstart.md`) before implementation. Always read the relevant spec/plan/tasks before writing feature code, and keep changes consistent with what the spec defines.

## Repository layout

- `backend/` — .NET 9 modular-monolith solution (`WritingUniverse.slnx`)
  - `src/WritingUniverse.Api` — host/startup (Program.cs, DI composition, Swagger)
  - `src/WritingUniverse.Auth` — Identity, JWT, Google OAuth, RBAC policies
  - `src/WritingUniverse.Universe` — business module (Universes, Worlds, Entities, Relationships, Stories, Epics, Chapters)
  - `src/WritingUniverse.Shared` — shared types (mostly unused; keep lean)
  - `tests/` — `*.UnitTests` (xUnit + EF InMemory) and `*.IntegrationTests` (WebApplicationFactory + JWT)
- `frontend/` — React 19 + TypeScript + Vite + Oxlint + Playwright e2e
  - `src/features/<feature>/` with `api/`, `components/`, `hooks/`, `pages/`, `types/`
  - `e2e/` — Playwright specs (mocked API via `page.route`)
- `specs/` — feature specifications, plans, task lists, quickstart validation matrices

## Backend (.NET / C#) conventions

### Formatting (enforced by `backend/.editorconfig` + `dotnet format`)
- 4-space indentation; UTF-8; LF line endings; ~120 character max line length.
- **Allman braces**; braces are **required** on every `if`/`foreach`/`for`/`while`/`try`/`catch`/`finally` block (no braceless single-statement bodies — `csharp_prefer_braces = true:error`).
- File-scoped namespaces; system `using` directives sorted first.
- Nullable reference types are treated as errors (CS86xx raised to `error` severity) — do not ship warnings.
- Long signatures and long `HandlerResult.Failure(...)` calls: one argument per line.
- LINQ chains: one operator per line.
- Run `dotnet format WritingUniverse.slnx` and verify no changes before finishing backend work.

### Architecture
- **Modular monolith**: each module exposes `XxxModuleExtensions.AddXxxModule(...)` registered in `Program.cs`. New handlers must be registered there as `AddScoped`.
- Feature folders inside each module: `Api/Controllers`, `Application/Commands`, `Application/Queries`, `Application/DTOs`, `Application/Services`, `Domain/Entities`, `Domain/Enums`, `Infrastructure/Persistence`.
- **Handler-based CQRS**: one handler class per operation.
  - Commands: `CreateXCommandHandler`, `UpdateXCommandHandler`, `DeleteXCommandHandler`, plus domain actions (`PublishStoryCommandHandler`, `ReorderEpicsCommandHandler`).
  - Queries: `ListXQueryHandler`, `GetXQueryHandler`.
  - Signature: `HandleAsync(...)` returning `HandlerResult<T>`; take `CancellationToken ct = default` as the last parameter and pass it through to EF calls.
  - Keep handlers thin: validation via `ResourceValidation`, persistence via `DbContext`, mapping via `XxxMapping.ToResponse(...)`.

### Controllers
- One controller per aggregate/area (`UniverseController`, `StoryController`) — do not bloat a single controller.
- `[ApiController]`, `[Route("api/...")]`, `[Authorize(Policy = "WriterOnly")]` for writer-scoped endpoints.
- **Thin**: parse route/query params, call one handler, return via the `Respond(...)` helper or `CreatedAtAction` / `NoContent()`.
- Declare `[ProducesResponseType]` for **every** status code the endpoint can return (200/201/204/400/401/403/404/409/500 as applicable).
- Route constraints: `{universeId:guid}`, `{storyId:guid}`, etc.

### Domain
- Entities use **private setters** + a public constructor that assigns `Id = Guid.NewGuid()`, `CreatedAt = UpdatedAt = DateTime.UtcNow`, `Version = 1`.
- Behavior lives in domain methods (`Publish()`, `Unpublish()`, `Update(...)`, `BumpVersion()`) that bump `Version` and `UpdatedAt` — never mutate state from handlers directly.
- Enums stored via `HasConversion<string>()` so they serialize as strings in responses.
- Optimistic concurrency: `long Version` marked `IsConcurrencyToken()`; stale writes return `409` (`ErrorCodes.StaleUpdate`).

### Errors & responses
- Handlers return `HandlerResult<T>` with `Success(value, statusCode)` / `Failure(statusCode, title, detail, code)`.
- Stable machine-readable error codes: add constants to `ErrorCodes.cs` (snake_case, e.g. `duplicate_story_title`), and surface them in Problem Details `code`.
- Ownership scoping: every handler validates the resource belongs to `ownerId`; foreign/missing resources return **404** (never reveal existence).
- Resource validation limits live in `ResourceValidation` (titles ≤ 200, descriptions ≤ 2000, content ≤ 1,000,000).

### Naming
- PascalCase for types/methods; `I` prefix for interfaces; `Async` suffix on async methods.
- DTOs: `XxxRequest` (input) / `XxxResponse` (output), positional `record`s with an XML `<summary>`.
- XML `<summary>` doc comment on every public handler, query, DTO, and domain class.

## Frontend (React / TypeScript) conventions

### Structure & naming
- Feature folders under `src/features/<feature>/`:
  - `api/<feature>Api.ts` — all fetch calls for the feature (object literal of methods).
  - `types/<feature>Types.ts` — shared types.
  - `hooks/useXxx.ts` — data hooks.
  - `pages/XxxPage.tsx` / `XxxRoutes.tsx` — route components.
  - `components/XxxForm.tsx`, `XxxList.tsx`, `DeleteXxxDialog.tsx` — presentational components.
- Components and types: PascalCase. Hooks and functions: camelCase. File names match the exported symbol.
- Prefer `import type { ... }` for type-only imports (enforced by `verbatimModuleSyntax`).
- No router library — derive route params from `window.location.pathname` (see existing pages); navigation via `<a href>` / `window.location.href`.

### API layer
- Centralize `fetch` through a small `request<T>(path, init)` helper that:
  - prefixes `/api`, injects `Authorization: Bearer <token>` from `sessionStorage.getItem('auth_token')`,
  - sets `Content-Type: application/json`,
  - throws an `Error` with the Problem Details `detail`/`error` message on non-OK,
  - returns `undefined as T` for `204 No Content`.
- Endpoint methods mirror the backend routes (nested paths in full, e.g. `/universes/{universeId}/stories/{storyId}/epics`).

### Hooks
- Data hooks return `{ result, error, loading, refresh }` using `useState` + `useEffect`; always provide a fallback error message (e.g. `'Unable to load Stories.'`).

### Pages & state
- Pages own state (lists, selected/edit/delete targets, `busy` flag), call API methods, then `refresh()` the relevant hooks.
- Reorder interactions swap array items and call the reorder endpoint with the parent's `version`.
- Guard every async handler with `try/catch/finally`; surface errors via `<p role="alert">`.

### Types
- Response types mirror backend DTOs (`Story`, `Epic`, `Chapter`); input types are `XxxInput`; reuse `PageResponse<T>` from `universe-world/types/universeWorldTypes`.
- String-literal unions for enums (e.g. `StoryStatus = 'Draft' | 'Published' | 'Unpublished'`).

## Testing conventions

### Backend unit tests (`*.UnitTests`)
- xUnit; class per handler: `CreateXCommandTests`, `UpdateXCommandTests`, etc.
- Descriptive behavior-first test names (e.g. `CreatesStoryWithTrimmedTitleOptionalDescriptionAndDraftStatus`).
- Use EF **InMemory** provider; construct handlers directly (`new CreateXCommandHandler(db)`).
- Cover: happy path + validation/not-found/unauthorized/duplicate/concurrency cases, asserting `StatusCode` and `ErrorCodes`.

### Backend integration tests (`*.IntegrationTests`)
- WebApplicationFactory fixture (e.g. `UniverseWorldApiFactory`); real JWT login for writer scoping.
- Class per area: `StoryEpicChapterApiTests`, `UniverseWorldApiTests`, `EntityRelationshipApiTests`.
- Verify ownership/security (`Security.*`), concurrency (`Concurrency.*`), visibility (`Visibility.*`) with dedicated folders.
- Relational fixtures use in-memory SQLite as a SQL Server proxy; **do not** set `nvarchar(max)` column types explicitly (SQLite rejects `(max)`).

### E2E (Playwright, `frontend/e2e/`)
- Mock the API with `page.route`; seed auth via `page.addInitScript` writing `auth_token`/`auth_role`/`auth_email` to `sessionStorage`.
- Keep mock-created ids aligned with hardcoded route segments (e.g. `u1`, `s1`, `ep1`) or navigation breaks.
- Ship an `-accessibility.spec.ts` per feature covering keyboard-accessible controls and `role="alert"`/`role="status"` messaging.

## Git workflow

- Work on feature branches: `feature/<NNN>-<feature-name>` (e.g. `feature/004-story-epic-chapter`).
- Commit focused changes; keep specs, backend, frontend, and tests in the same feature branch.

## Verify before finishing

```powershell
# Backend
cd backend
dotnet build WritingUniverse.slnx
dotnet test WritingUniverse.slnx
dotnet format WritingUniverse.slnx   # must report no changes

# Frontend
cd frontend
npm run build
npm run lint
npx playwright test
```

## Quality bar (best practices)

1. **Convention over configuration** — match the existing codebase patterns exactly; consistency trumps personal preference.
2. **Explicit over clever** — readable, debuggable code; avoid over-abstraction.
3. **Fail fast, fail loud** — validate early, return meaningful Problem Details; never swallow exceptions.
4. **Test what matters** — every feature ships unit + integration + e2e coverage per the spec quickstart matrix.
5. **Security by default** — scope every operation to the authenticated writer; foreign resources → 404; stale writes → 409.
6. **No warnings** — nullable-reference warnings are errors on the backend; TS `noUnusedLocals`/`noUnusedParameters` and Oxlint must pass on the frontend.
