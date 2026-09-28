# Tasks: Provider Abstraction Layer

## Phase 1: Contract foundation

- [x] T001 Define the provider failure taxonomy and execution envelope in `src/HomNayLive.Providers/Contracts/ProviderContracts.cs`
- [x] T002 Define neutral trend/news/AI contract interfaces and request/result records in `src/HomNayLive.Providers/Contracts/ProviderContracts.cs`
- [x] T003 Add DI registration extension in `src/HomNayLive.Providers/ServiceCollectionExtensions.cs`

## Phase 2: Stub implementations

- [x] T004 Add deterministic trend/news/AI stub providers in `src/HomNayLive.Providers/Stubs/DeterministicProviders.cs`
- [x] T005 Add solution wiring for the provider library and provider test project in `HomNayLive.slnx`

## Phase 3: Verification

- [x] T006 Add contract tests for neutral providers and error taxonomy in `tests/HomNayLive.Providers.Tests/ProviderContractTests.cs`
- [x] T007 Run `dotnet test HomNayLive.slnx --nologo` and confirm all tests pass
