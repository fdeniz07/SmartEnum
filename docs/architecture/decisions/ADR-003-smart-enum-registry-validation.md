# ADR-003 — SmartEnum Registry Validation and Uniqueness

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-11-26 |
| Deciders | SmartEnum maintainers |
| Related | [ADR-002](ADR-002-instance-discovery.md), `docs/design/SOURCE_GENERATOR.md` |
| Turkish | [ADR-003-smart-enum-registry-validation.tr.md](ADR-003-smart-enum-registry-validation.tr.md) |

## Context

ADR-002 established compile-time discovery of `public static readonly` Smart Enum instances via an incremental Source Generator and `ISmartEnumGenerated<TSelf>.GetInstances()`.

Discovery alone does not guarantee a usable catalog:

- Domains may declare zero valid instances.
- Two instances may share the same `Name` or `Value`.
- Generated spans may theoretically contain null entries under unsafe initialization scenarios.
- Some `Name`/`Value` expressions cannot be evaluated at compile time.

Consumers need a **validated Registry** that is either fully initialized or not observable at all.

## Decision

### Accepted — dual-layer validation

1. **Compile-time (Generator)** validates uniqueness and emptiness when values are reliably constant-foldable.
2. **Runtime (Core Registry)** always re-validates the discovered instance set on Registry initialization, even when the Generator already validated constants.

### Accepted — Generator diagnostics (Error severity)

| ID | Meaning |
| --- | --- |
| SMARTENUM003 | Duplicate `Name` (Ordinal, case-sensitive) when compile-time evaluable |
| SMARTENUM004 | Duplicate `Value` when compile-time evaluable |
| SMARTENUM005 | Domain has no valid Smart Enum instances |

### Accepted — compile-time evaluation rules

- Use Roslyn `SemanticModel.GetConstantValue` (and equivalent constant folding) only.
- **Do not** invent evaluation of arbitrary runtime expressions.
- When `Name` or `Value` cannot be reliably evaluated, **skip** uniqueness diagnostics for that pair and leave the check to Runtime.
- Name comparison for compile-time duplicates: **Ordinal, case-sensitive** (`StringComparison.Ordinal` / string equality).
- Report both conflicting declarations where possible (primary + additional location).

### Accepted — Runtime Registry validation

On every Registry initialization, validate:

1. Empty instance collection → fail.
2. Null instance entries → fail.
3. Duplicate `Name` using `StringComparer.Ordinal` → fail.
4. Duplicate `Value` using `EqualityComparer<TValue>.Default` → fail.

Rules:

- Validation runs **before** the Registry becomes publicly accessible.
- Failed initialization must not leave a partially constructed Registry observable.
- Initialization must be **thread-safe**.
- No reflection, no `ModuleInitializer`, no global cross-type registry, no Core runtime package dependencies.

### Accepted — API shape (additive)

- Preserve `SmartEnum<TEnum, TValue>` and `ISmartEnumGenerated<TSelf>`.
- Expose a validated `SmartEnumRegistry<TEnum, TValue>` accessed via `SmartEnum<TEnum, TValue>.Registry`.
- Discovery remains Source Generator + static abstract dispatch (`TEnum.GetInstances()`).

## Consequences — positive

- Fail-fast uniqueness for constant declarations at build time.
- Runtime safety net for non-constant expressions and adversarial/manual `ISmartEnumGenerated` stubs.
- Clear Error diagnostics for empty domains.
- Thread-safe, all-or-nothing Registry publication.

## Consequences — negative / trade-offs

- Some duplicates are only detectable at runtime (non-constant expressions).
- Runtime validation adds a one-time cost on first Registry access per closed domain type.
- Generator cannot cover every `TValue` expression shape without false positives.

## Alternatives considered

| Alternative | Outcome |
| --- | --- |
| Compile-time only | Rejected — incomplete for runtime-computed names/values |
| Runtime only | Rejected — loses early CI feedback for constant duplicates |
| Global mutable registry | Rejected — conflicts with ADR-002 |
| Reflection scanning | Rejected — conflicts with ADR-002 |

## Security implications

- Deterministic Ordinal name comparison avoids culture-dependent equality surprises.
- No reflection reduces trimmer/AOT attack/compatibility surface for discovery/validation paths.

## Performance implications

- Generator checks are proportional to discovered instance fields.
- Runtime validation is O(n) over instances, once per type under thread-safe lazy initialization.

## Provisional (not accepted as product policy yet)

- Public lookup helpers (`FromName` / `FromValue`) beyond validated enumeration.
- Exact exception type hierarchy beyond a dedicated validation exception.
- Whether empty-domain generation still emits an empty `GetInstances()` body (implementation may emit while reporting SMARTENUM005; compilation still fails).

## References

- ADR-002 Instance Discovery
- Milestone 1.2 implementation notes in repository history / PR description when published
