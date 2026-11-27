# ADR-002 — Compile-Time Instance Discovery

| Field | Value |
| --- | --- |
| Status | **Accepted** (for Milestone 1 prototype scope) |
| Date | 2026-11-26 |
| Deciders | SmartEnum maintainers |
| Related | `docs/design/SOURCE_GENERATOR.md` |

## Context

Smart Enum domain types declare a closed set of named instances as `public static readonly` fields. Core APIs (lookup, enumeration, validation helpers in later milestones) need a reliable way to obtain that set.

Traditional approaches include:

1. Runtime reflection over static fields.
2. Manual registration (`ModuleInitializer`, static constructors, global registries).
3. Compile-time discovery via a Roslyn Source Generator.

Milestone 1 must validate an architecture that remains compatible with trimming and Native AOT **goals**, without claiming those goals are already proven.

## Decision

**Accepted:** Instance discovery is performed at **compile time** by an incremental Roslyn Source Generator (`SmartEnum.Generators`). The generator emits an implementation of a technical static-abstract interface (`SmartEnum.Internal.ISmartEnumGenerated<TSelf>`) on each eligible **partial** domain type.

**Accepted:** Runtime reflection is **not** used for instance discovery.

**Accepted:** `ModuleInitializer`-based registration and a mutable global registry are **rejected** for discovery.

**Accepted:** Domain types that inherit from `SmartEnum<TEnum, TValue>` must be declared `partial`.

**Accepted:** Discovery results are exposed through **static abstract interface dispatch** (`TEnum.GetInstances()`), not through a shared runtime registry.

## Consequences — positive

- Discovery cost moves to compile time; runtime enumeration is a span over a static array.
- No reflection surface for discovery (better alignment with trimming/AOT *intent*).
- No cross-assembly mutable registry and no initialization-order surprises from `ModuleInitializer`.
- Diagnostics can fail the build for invalid declarations (non-partial domain, mutable instance fields).

## Consequences — negative / trade-offs

- Domain types must be `partial`.
- Generator packaging and analyzer wiring must be correct for consumers.
- `ISmartEnumGenerated<T>` is a **public** technical surface (see below); `EditorBrowsable` does not hide it from the public API.
- Nested and complex generic domain shapes are **not** claimed as supported in Milestone 1.

## Public API note — `EditorBrowsable`

`ISmartEnumGenerated<TSelf>` is marked `[EditorBrowsable(EditorBrowsableState.Never)]` to reduce IntelliSense noise. This attribute does **not** make the interface private. It remains a public type in the Core assembly and can be referenced by any consumer. That visibility is required so Core generic constraints and cross-assembly static abstract dispatch can work.

## Alternatives considered

| Alternative | Outcome |
| --- | --- |
| Reflection over `public static readonly` fields | Rejected — fragile under trimming/AOT; startup cost; hard to diagnose missing instances |
| Manual `Register` / global dictionary | Rejected — mutable global state; easy to forget registration |
| `ModuleInitializer` auto-registration | Rejected — ordering hazards; harder to reason about; still a runtime registry |
| Source Generator + static abstract interface | **Accepted** |

## Security implications

- Compile-time discovery reduces reliance on runtime type inspection APIs.
- Generated code uses fully qualified type names and does not execute user-supplied strings.
- Invalid shapes are surfaced as compiler diagnostics rather than silent runtime gaps (for the checked cases in Milestone 1).

## Performance implications

- Generator work is incremental and proportional to changed syntax/semantic candidates.
- Runtime `GetInstances()` returns a `ReadOnlySpan<T>` over a static array (CS9203-safe pattern).
- No per-call reflection.

## AOT and trimming implications

- Architecture is **chosen** to be AOT/trimming-friendly.
- Milestone 1 does **not** claim Native AOT or full trim validation. Those remain later verification stages.

## Provisional technical choices (not yet “Accepted” product policy)

- SMARTENUM001 and SMARTENUM002 are the initial discovery diagnostics from this decision. SMARTENUM003, SMARTENUM004, and SMARTENUM005 were subsequently accepted in ADR-003. All five diagnostics are implemented. The discovery architecture in this ADR is unchanged.
- Final NuGet packaging shape (single package vs Core + Generators).
- Whether `ISmartEnumGenerated<T>` remains in `SmartEnum.Internal` long term.
- Support matrix for nested types and open generic domains.

## References

- `docs/design/SOURCE_GENERATOR.md`
- `docs/design/SOURCE_GENERATOR.tr.md`
- `ADR-002-instance-discovery.tr.md`
