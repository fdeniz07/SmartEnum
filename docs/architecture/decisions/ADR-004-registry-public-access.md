# ADR-004 — Registry Public Access

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-11-26 |
| Deciders | SmartEnum maintainers |
| Scope | SmartEnum Core public API |
| Related | [ADR-003](ADR-003-smart-enum-registry-validation.md) |
| Turkish | [ADR-004-registry-public-access.tr.md](ADR-004-registry-public-access.tr.md) |

This record documents a decision that is already implemented. It does not change production code.

## Context

ADR-003 accepted a validated `SmartEnumRegistry<TEnum, TValue>`, reached through `SmartEnum<TEnum, TValue>.Registry`. Initialization is a `Lazy<T>` factory with `LazyThreadSafetyMode.ExecutionAndPublication`. Validation runs before publication. A failed initialization caches the same exception and never publishes a partial registry.

After that implementation, two public ways reached the same factory:

- `SmartEnum<TEnum, TValue>.Registry`
- `SmartEnumRegistry<TEnum, TValue>.Instance`

`Registry` was the intended contract. `Instance` was a second public static entry to the same lazy value. Callers could bypass the documented entry point.

The Milestone 1.3 review also recorded a C# constraint: a public member cannot return a less accessible type (CS0050). `Registry` returns `SmartEnumRegistry<TEnum, TValue>`, so the registry type cannot be `internal` unless that return type changes.

## Decision

Accepted — Option A:

- `SmartEnum<TEnum, TValue>.Registry` is the single official public Registry entry point.
- `SmartEnumRegistry<TEnum, TValue>` remains public because it is the return type of the public `Registry` property.
- `SmartEnumRegistry<TEnum, TValue>.Instance` is internal.
- Registry initialization remains `Lazy<T>` with `LazyThreadSafetyMode.ExecutionAndPublication`.
- Exception caching and validation behavior remain unchanged.

`Items` stays a public member of the public registry type. Discovery, uniqueness rules, and the validation exception type are unchanged.

## Options considered

### Option A — One public entry; registry type stays public

Make `Instance` internal. Keep `SmartEnum<TEnum, TValue>.Registry` public. Keep `SmartEnumRegistry<TEnum, TValue>` public as the return type. Leave lazy initialization, exception caching, and validation as specified by ADR-003.

### Option B — Make the registry type internal

Make `SmartEnumRegistry<TEnum, TValue>` internal, matching the preference that registry implementation be internal where technically possible.

This option is not available while `Registry` returns that type. CS0050 forbids a public property from returning a less accessible type. Selecting Option B requires a different public return type, for example exposing only `IReadOnlyList<TEnum>` and dropping the named registry type from the public surface.

## Why Option A was selected

Option A removes the second public entry without reopening the ADR-003 contract.

- Callers keep a named, validated registry object and its `Items` collection.
- The return type of `Registry` does not change, so existing `Domain.Registry` call sites stay source-compatible.
- `Instance` is an implementation detail of the lazy factory. The only production caller is `Registry` inside the SmartEnum assembly.
- Validation, thread safety, and cached failures stay exactly as ADR-003 defined them.
- Option B would hide `SmartEnumRegistry<TEnum, TValue>` only by changing the public return type. That is a larger API break than the access problem requires.

Leaving `Instance` public was the state before this decision. It was rejected because two official entry points make the supported surface ambiguous.

## Public API consequences

| Member | Accessibility |
| --- | --- |
| `SmartEnum<TEnum, TValue>.Registry` | public — the only official entry |
| `SmartEnumRegistry<TEnum, TValue>` | public — return type of `Registry` |
| `SmartEnumRegistry<TEnum, TValue>.Items` | public |
| `SmartEnumRegistry<TEnum, TValue>.Instance` | internal |
| `SmartEnumValidationException` | public — unchanged |

External assemblies cannot name `Instance`. Code inside the SmartEnum assembly can. Non-public reflection can still observe the internal property; that is not a supported API.

Future public members added to `SmartEnumRegistry<TEnum, TValue>` become public API, because the type itself stays public.

## Compatibility implications

- Source compatible for `SmartEnum<TEnum, TValue>.Registry`, the registry type, and `Items`.
- Not source compatible for direct use of `SmartEnumRegistry<TEnum, TValue>.Instance` from another assembly (`CS0122`).
- No change to exception types, exception messages, lazy mode, or validation rules.
- No published stable package had committed `Instance` as a supported public member. The supported contract is `Registry`.

## Consequences and trade-offs

### Positive

- One documented way to obtain the validated registry.
- ADR-003 behavior stays intact: all-or-nothing publication, cached failure, Ordinal name checks, and `EqualityComparer<TValue>.Default` value checks.
- The public type remains usable for `Items` and for later lookup APIs that return through the domain type.

### Trade-offs

- The registry type cannot be hidden without a later, deliberate return-type change.
- `Instance` remains callable inside the SmartEnum assembly. New Core code should go through `Registry`.
- Consumers can still construct references to the public registry type. Encapsulation is at the member level (`Instance`), not at the type level.

## Testing strategy

Tests observe the public contract and the access boundary. They do not change validation semantics.

- A valid domain initializes through `Registry`.
- Repeated `Registry` access returns the same instance.
- Parallel access publishes one registry instance.
- An external test assembly finds no public `Instance` property. The non-public static property is assembly-scoped (`IsAssembly`).
- Existing ADR-003 checks still pass: empty, null, duplicate name, duplicate value, cached failure, immutable `Items`, and independent registries per domain.

## Relationship with ADR-003

ADR-003 decides what a registry guarantees and that consumers reach it through `SmartEnum<TEnum, TValue>.Registry`. It does not require `Instance` to be public.

ADR-004 only narrows who may call the lazy factory. It does not change:

- compile-time diagnostics SMARTENUM003, SMARTENUM004, and SMARTENUM005
- runtime checks for empty, null, duplicate name, and duplicate value
- `LazyThreadSafetyMode.ExecutionAndPublication`
- cached initialization failures
- the public `Items` collection

ADR-002 remains the discovery decision. ADR-005 (Lookup and Equality) is **Accepted**. It remains outside this decision.

## References

- [ADR-003](ADR-003-smart-enum-registry-validation.md) — Registry validation and uniqueness
- `docs/design/REGISTRY.md` — behavior notes for the official entry point
- C# accessibility: a public member cannot expose a less accessible type (CS0050)
