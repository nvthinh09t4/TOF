# Implementation Plan: Provider Abstraction Layer

**Branch**: `002-provider-abstraction-layer` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

## Summary

This feature delivers the replaceable integration boundary for the HomNay Live platform. It introduces a small, capability-focused provider layer that isolates trend collection, news ingestion, and AI summarization behind neutral contracts. Provider-specific payloads and SDK touchpoints are confined to adapters and test stubs, while the domain and application core depend only on neutral request/result types and provider identities.

## Technical Context

- Language/runtime: C# / .NET 10
- Project type: provider abstraction library inside the modular monolith
- Primary dependencies: `Microsoft.Extensions.DependencyInjection.Abstractions`
- Test strategy: offline xUnit tests, deterministic provider stubs, no network dependencies
- Key goal: zero core-domain dependency on provider SDKs or provider shapes; only capability contracts and normalization boundaries remain in the core boundary

## Architectural Decisions

1. Capability-first contracts: `ITrendProvider`, `INewsProvider`, `IAiSummaryProvider`
2. Neutral request/result records: `TrendProviderRequest`, `TrendObservationCandidate`, `NewsProviderRequest`, `NewsArticleCandidate`, `AiSummaryRequest`, `AiSummaryResult`
3. Shared failure taxonomy: `ProviderErrorCategory` and `ProviderFailure`
4. Uniform execution envelope: `ProviderResultEnvelope<T>` carrying outcomes, attempt counts, and duration
5. DI composition root registration through `AddHomNayLiveProviders(...)`
6. Deterministic adapter stubs in `src/HomNayLive.Providers/Stubs/` for offline tests and future provider onboarding validation

## Project Structure

```text
src/
├── HomNayLive.Providers/
│   ├── Contracts/
│   │   └── ProviderContracts.cs
│   ├── Stubs/
│   │   └── DeterministicProviders.cs
│   ├── ServiceCollectionExtensions.cs
│   └── HomNayLive.Providers.csproj

tests/
├── HomNayLive.Providers.Tests/
│   ├── ProviderContractTests.cs
│   └── HomNayLive.Providers.Tests.csproj
```

## Constitution Check

- Replaceable integrations: pass — provider implementations are behind externally registered interfaces and neutral contracts.
- Failure isolation: pass — all provider calls carry cancellation support, timeout configuration, retry metadata, and a uniform error taxonomy.
- Configuration over code: pass — registration is configuration-driven and provider activation is performed through DI setup.
- MVP simplicity: pass — no provider-specific domain entities or distributed abstractions were introduced.

## Validation

- `dotnet test HomNayLive.slnx --nologo` passes after the implementation is complete.
- The test suite remains offline and deterministic via stub providers.
