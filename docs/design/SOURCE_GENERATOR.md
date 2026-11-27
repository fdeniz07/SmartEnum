# SmartEnum Source Generator — Architecture (Milestone 1)

| Field | Value |
| --- | --- |
| Status | Prototype implemented |
| Scope | Compile-time instance discovery only |
| Related ADR | [ADR-002](../architecture/decisions/ADR-002-instance-discovery.md) |
| Turkish counterpart | [SOURCE_GENERATOR.tr.md](SOURCE_GENERATOR.tr.md) |

This document describes the Source Generator architecture validated in Milestone 1. It distinguishes **Accepted** decisions from **provisional** technical choices. Unapproved product proposals are not marked Accepted.

Lookup, equality, and diagnostics SMARTENUM003 through SMARTENUM005 were implemented after Milestone 1. Those facts are recorded below. This document does not change ADR-002.

## 1. Context and problem

A Smart Enum domain type declares a finite set of instances:

```csharp
public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
{
    public static readonly OrderStatus Pending = new("Pending", 1);
    public static readonly OrderStatus Approved = new("Approved", 2);

    private OrderStatus(string name, int value) : base(name, value) { }
}
```

Later Core APIs need to enumerate those instances without:

- runtime reflection,
- a mutable global registry,
- or `ModuleInitializer` side effects.

Milestone 1 proves that compile-time discovery + static abstract interface dispatch works end-to-end.

## 2. Accepted architectural decisions

| Decision | Status |
| --- | --- |
| Use an incremental Roslyn Source Generator (`IIncrementalGenerator`) | **Accepted** |
| Discover instances at compile time | **Accepted** |
| Reject reflection-based discovery | **Accepted** |
| Reject `ModuleInitializer` registration | **Accepted** |
| Reject a global mutable registry for discovery | **Accepted** |
| Require `partial` domain declarations | **Accepted** |
| Emit `ISmartEnumGenerated<TSelf>` implementation | **Accepted** |
| Use static abstract interface dispatch (`TEnum.GetInstances()`) | **Accepted** |
| Generator project must not `ProjectReference` Core | **Accepted** |
| Core has zero external runtime package dependencies | **Accepted** |
| Prioritize top-level, non-generic domain classes in Milestone 1 | **Accepted** (scope limit) |

## 3. Why compile-time discovery was selected

Compile-time discovery:

- moves correctness checks to the build,
- produces deterministic generated source,
- avoids runtime type scanning,
- aligns with trimming/AOT *intent* (validation of AOT remains a later milestone).

## 4. Why reflection was rejected

Reflection-based field scanning:

- is brittle under trimmer/AOT,
- defers failures to runtime,
- adds measurable startup cost,
- makes “missing instance” bugs harder to catch in CI without exhaustive runtime tests.

## 5. Why partial declarations are required

The generator adds a second `partial` type declaration that:

- lists the generated interface on the domain type,
- implements `GetInstances()`,
- preserves namespace and accessibility.

Without `partial`, the generator cannot legally augment the user type. Non-partial domains produce diagnostic **SMARTENUM001**.

## 6. Why static abstract interface dispatch was selected

Static abstract members on interfaces (C# generic math style) allow:

```csharp
static ReadOnlySpan<TEnum> GetAll<TEnum>()
    where TEnum : ISmartEnumGenerated<TEnum>
    => TEnum.GetInstances();
```

Benefits:

- no shared registry lookup,
- dispatch is resolved through the generic constraint,
- works across assemblies when the interface is public.

## 7. Why ModuleInitializer was rejected

`ModuleInitializer` registration typically writes into a global table during assembly load. Problems:

- initialization ordering across assemblies,
- harder reasoning about when instances become visible,
- still a runtime registry model,
- weaker build-time validation than generator diagnostics.

## 8. Core contract (prototype)

```csharp
public abstract class SmartEnum<TEnum, TValue> : IEquatable<TEnum>
    where TEnum : SmartEnum<TEnum, TValue>, ISmartEnumGenerated<TEnum>
    where TValue : notnull
```

Technical interface:

```csharp
namespace SmartEnum.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISmartEnumGenerated<TSelf>
    where TSelf : ISmartEnumGenerated<TSelf>
{
    static abstract ReadOnlySpan<TSelf> GetInstances();
}
```

### Public API consequence

`EditorBrowsable` only affects IDE presentation. The interface remains **public API**. Callers can reference it. This is intentional for cross-assembly static abstract constraints.

## 9. Generator behavior

Project: `src/SmartEnum.Generators` (`netstandard2.0`, Roslyn component).

### Responsibilities

The generator does not implement lookup or equality.

Generator:

- Compile-time discovery of `public static readonly` domain fields.
- The generated `ISmartEnumGenerated<T>` implementation.
- The static instance array.
- Diagnostics SMARTENUM001 through SMARTENUM005.

Core (`src/SmartEnum`):

- Registry initialization and validation.
- Lookup: `List`, `FromName`, `FromValue`, `TryFromName`, `TryFromValue`, `ContainsName`, `ContainsValue`.
- Equality, `GetHashCode()`, and the `==` / `!=` operators.
- Runtime exceptions, including `SmartEnumValidationException`, `KeyNotFoundException`, `ArgumentNullException`, and `ArgumentException`.

### Discovery

1. Syntax predicate: class/record with a base list.
2. Semantic filter: base type identity is `SmartEnum.SmartEnum<TEnum, TValue>` where `TEnum` is the declaring type (namespace + metadata identity, not stringly matching alone).
3. Require `partial` (else SMARTENUM001). A non-partial domain does not receive generated discovery source.
4. Collect `public static readonly` fields whose type equals the domain type.
5. Ignore unrelated static fields (`string`, `int`, etc.).
6. Report SMARTENUM002 for `public static` domain-typed fields that are not `readonly`. Those fields are left out of the generated array.
7. Report SMARTENUM003 and SMARTENUM004 when the constructor arguments named `name` and `value` are duplicate compile-time constants.
8. Report SMARTENUM005 when no valid instance field remains. Generation still emits an empty array. The diagnostic severity is Error, so the consumer compilation fails.

Verified declaration shape: a top-level, non-generic `partial` class. Nested types and open generic domains are not a supported claim.

### Diagnostic catalogue

Every descriptor is category `SmartEnum.Generators`, severity Error, and enabled by default. Messages below are the `messageFormat` strings in `SmartEnumDiagnostics.cs`.

| ID | Title | When it is reported | Message |
| --- | --- | --- | --- |
| SMARTENUM001 | SmartEnum domain type must be partial | The domain inherits `SmartEnum<,>` and is not `partial`. | `Type '{0}' inherits from SmartEnum<,> and must be declared partial so the source generator can emit instance discovery` |
| SMARTENUM002 | SmartEnum instance field must be static readonly | A `public static` field of the domain type is not `readonly`. | `Field '{0}' on SmartEnum domain '{1}' must be 'static readonly' to participate in compile-time instance discovery` |
| SMARTENUM003 | Duplicate SmartEnum Name | Two valid fields pass the same compile-time constant to the parameter named `name`. Comparison is ordinal. | `SmartEnum domain '{0}' declares duplicate Name '{1}' on fields '{2}' and '{3}'` |
| SMARTENUM004 | Duplicate SmartEnum Value | Two valid fields pass the same non-null compile-time constant to the parameter named `value`. | `SmartEnum domain '{0}' declares duplicate Value '{1}' on fields '{2}' and '{3}'` |
| SMARTENUM005 | SmartEnum domain has no instances | The domain has no valid `public static readonly` instance. | `SmartEnum domain '{0}' has no valid public static readonly instances` |

A `name` or `value` argument that is not a compile-time constant is skipped by SMARTENUM003 and SMARTENUM004. Runtime registry validation still rejects duplicate names and values. The selected constructor does not have to be public. Parameters are recognized by the names `name` and `value`. Constants are read with `SemanticModel.GetConstantValue`.

### Emission

Verified shape (collection expression directly typed as `ReadOnlySpan<T>` is invalid — CS9203). Generated code uses a static array backing field:

```csharp
// <auto-generated/>
#nullable enable

namespace Demo;

public sealed partial class OrderStatus : global::SmartEnum.Internal.ISmartEnumGenerated<global::Demo.OrderStatus>
{
    private static readonly global::Demo.OrderStatus[] _smartEnumInstances = [Pending, Approved];

    static global::System.ReadOnlySpan<global::Demo.OrderStatus> global::SmartEnum.Internal.ISmartEnumGenerated<global::Demo.OrderStatus>.GetInstances()
        => _smartEnumInstances;
}
```

Properties of generated code:

- fully qualified type names,
- preserved namespace,
- preserved accessibility / `sealed`,
- `#nullable enable`,
- deterministic ordering: source file path, then source position inside that file. One file follows declaration order. Multiple files follow ordinal file-path order,
- no reflection, no `ModuleInitializer`, no global state.

## 10. Advantages and disadvantages

### Advantages

- Build-time validation.
- Simple runtime path.
- Clear project boundary (Generator ↛ Core).
- Snapshot-testable output.

### Disadvantages

- `partial` requirement.
- Public technical interface.
- Packaging complexity for NuGet consumers (provisional).
- Limited domain shapes in Milestone 1.

## 11. Alternatives considered

See ADR-002. Summary: reflection, manual registry, and `ModuleInitializer` were rejected in favor of generator + static abstract dispatch.

## 12. Security implications

- No runtime evaluation of discovered member names beyond compile-time symbols.
- Diagnostics reduce the chance of silently incomplete catalogs for checked invalid shapes.
- Generated identifiers are taken from compiler symbols, not free-form user input streams.

## 13. Performance implications

- Incremental generator pipeline avoids full-compilation rescans when possible.
- Runtime discovery is O(1) access to a static array / span.
- No reflection caches.

## 14. AOT and trimming implications

**Accepted intent:** avoid reflection discovery to stay AOT/trim friendly.

**Not claimed in Milestone 1:** Native AOT publish success, trim warnings zero, or linker configuration completeness. Those require dedicated validation milestones.

## 15. Generator limitations

- Top-level, non-generic domains are the verified path.
- Nested types and open generic domains are not claimed supported.
- The generator does not generate lookup, equality, or serialization. Lookup and equality are implemented on Core.
- Diagnostics SMARTENUM001 through SMARTENUM005 are implemented.
- The generator is referenced by integration tests as an Analyzer `ProjectReference`. NuGet bundling is provisional.

## 16. Test strategy

| Suite | Project | Focus |
| --- | --- | --- |
| Unit | `tests/unit/SmartEnum.Tests` | Construction, registry, lookup, and equality |
| Generator | `tests/generators/SmartEnum.Generators.Tests` | Driver-based generation, diagnostics, Verify snapshots |
| Integration | `tests/integration/SmartEnum.Generator.IntegrationTests` | Real compile with Analyzer reference + static abstract dispatch |

Stack:

- xUnit v3 via `xunit.v3.mtp-v2` (Microsoft.Testing.Platform; required on .NET 10 SDK `dotnet test`)
- `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.CSharp.Workspaces` for the test host
- `CSharpGeneratorDriver` in `tests/generators/SmartEnum.Generators.Tests/GeneratorTestHelpers.cs`
- `Verify.XunitV3` for generated source snapshots (`*.verified.txt`)

## 17. Project dependency boundaries

```text
SmartEnum (Core)          — no PackageReference runtime deps; no ProjectReference to Generators
SmartEnum.Generators      — Roslyn packages only (PrivateAssets); NO ProjectReference to Core
Generator.Tests           — references Core + Generators (normal refs for driver tests)
Integration.Tests         — references Core + Generators as Analyzer
```

## 18. Future extension considerations (provisional)

- Bundle the generator into the Core NuGet package as an analyzer.
- Nested and open-generic domain support, with tests, before claiming that support.
- Dedicated Native AOT / trim validation.

Lookup and equality are already implemented in Core. The generator does not generate those members. Duplicate name and value diagnostics SMARTENUM003 and SMARTENUM004 are already implemented for compile-time constants.

## 19. Provisional vs accepted — quick checklist

| Topic | Classification |
| --- | --- |
| Compile-time discovery | Accepted |
| No reflection discovery | Accepted |
| Partial required | Accepted |
| Static abstract dispatch | Accepted |
| No ModuleInitializer discovery | Accepted |
| CS9203-safe static array emission | Provisional implementation detail (correctness-driven) |
| Diagnostic IDs SMARTENUM001–SMARTENUM005 | Implemented. 001 and 002 come from this discovery design. 003, 004, and 005 were accepted in ADR-003. |
| NuGet packaging topology | Provisional |
| Nested/generic domains | Out of Milestone 1 claim |
| Native AOT proven | Not claimed |
