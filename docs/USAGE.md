# SmartEnum — Usage

Consumer guide for the implemented Core API and the Source Generator shipped in the **`SmartEnum`** NuGet package.

Examples align with `tests/integration/SmartEnum.Generator.IntegrationTests` and the isolated package consumer covered by `tests/integration/SmartEnum.Package.IntegrationTests`.

## Package installation

Add a single package reference. The package contains the Core runtime, XML docs, the generator under `analyzers/dotnet/cs`, and the package icon. There is no separate `SmartEnum.Generators` package to install.

```xml
<PackageReference Include="SmartEnum" Version="1.0.0-preview.1" />
```

> **Publication caveat:** Preview packaging is implemented in this repository (`1.0.0-preview.1`). This document does **not** claim the package is already on NuGet.org. Use a feed that hosts the packed nupkg, or pack locally as shown below. Contributor checklist: [release/nuget-preview-checklist.md](release/nuget-preview-checklist.md).

### Pack and consume from a local feed

Work from the repository root. Pack the shipping project (`src/SmartEnum.Packaging/SmartEnum.Packaging.csproj`) into a local feed folder:

```powershell
dotnet pack src/SmartEnum.Packaging/SmartEnum.Packaging.csproj -c Release -o ./artifacts/local-feed
```

This writes:

- `SmartEnum.1.0.0-preview.1.nupkg` — runtime, XML docs, generator DLL + generator PDB, README, package icon
- `SmartEnum.1.0.0-preview.1.snupkg` — Core portable PDB (`lib/net10.0/SmartEnum.pdb`) for SourceLink

to `./artifacts/local-feed` relative to the repo root. Consume the **nupkg** from your feed; keep the snupkg alongside when you publish symbols later.

Place (or create) a `nuget.config` in the **repository root**. Relative package source paths are resolved against that config file’s directory, so `./artifacts/local-feed` means `<repo-root>/artifacts/local-feed`. Keep other required sources available (for example nuget.org for test SDK packages); do not add `<clear />` unless you also configure every source the consumer needs:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="smartenum-local" value="./artifacts/local-feed" />
  </packageSources>
</configuration>
```

Restore the consumer explicitly with that config (still from the repo root, adjusting the project path as needed):

```powershell
dotnet restore path/to/Consumer.csproj --configfile ./nuget.config
```

Use the same `PackageReference` as above. The package already includes the generator under `analyzers/dotnet/cs`; no second reference is required.

You do **not** manually implement `ISmartEnumGenerated<T>`. You do **not** add a second analyzer/`ProjectReference` to the generator when consuming the NuGet package.

For contributors working inside this repo without a local feed, integration tests that use an Analyzer `ProjectReference` remain a development convenience—they are not the NuGet consumer model. Prefer the package path described above for real applications.

### Build requirements

`global.json` selects SDK `10.0.404` (`rollForward`: `latestFeature`, `allowPrerelease`: `false`). The repo language version is C# 14. Core has **zero** runtime package dependencies.

From the repository root:

```powershell
dotnet test SmartEnum.slnx -c Release
```

## Domain declaration

Declare a **top-level**, **non-generic**, **`partial`** class. Pass `(string name, TValue value)` to the base constructor. The generator discovers `public static readonly` fields whose constructor arguments use parameters named `name` and `value`.

```csharp
using SmartEnum;

namespace Demo;

public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
{
    public static readonly OrderStatus Pending = new("Pending", 1);
    public static readonly OrderStatus Processing = new("Processing", 2);
    public static readonly OrderStatus Completed = new("Completed", 3);

    private OrderStatus(string name, int value)
        : base(name, value)
    {
    }
}
```

`SmartEnum<OrderStatus, int>` constrains `OrderStatus` to implement `ISmartEnumGenerated<OrderStatus>`. The generator supplies that implementation and the discovered instance array. Do not write `GetInstances()` by hand.

`TValue` is `notnull`. `Name` and `Value` are get-only properties on the base type.

## How the source generator works

1. At compile time, the analyzer from the `SmartEnum` package finds eligible `partial` domains.
2. It emits a second partial declaration implementing `ISmartEnumGenerated<TEnum>` with compile-time discovered instances.
3. Diagnostics SMARTENUM001–SMARTENUM006 report declaration problems (for example non-`partial` or nested types).
4. Core does **not** use reflection, runtime assembly scanning, manual registration, or a production `ModuleInitializer` to find instances.

Design detail: [SOURCE_GENERATOR.md](design/SOURCE_GENERATOR.md). Packaging decision: [ADR-006](architecture/decisions/ADR-006-single-nuget-package-distribution.md).

## Registry and List

```csharp
IReadOnlyList<OrderStatus> list = OrderStatus.List;
SmartEnumRegistry<OrderStatus, int> registry = OrderStatus.Registry;

// List and Registry.Items are the same collection instance.
bool sameCatalog = ReferenceEquals(list, registry.Items);
```

The first successful access validates discovered instances and publishes one immutable registry for the domain. Validation failures throw `SmartEnumValidationException`; later accesses rethrow that same exception instance. An empty set, a null entry, a duplicate name, or a duplicate value fails validation.

## Lookup

```csharp
OrderStatus pending = OrderStatus.FromName("Pending");
OrderStatus alsoPending = OrderStatus.FromValue(1);

bool found = OrderStatus.TryFromName("Pending", out OrderStatus? match);
bool missing = OrderStatus.TryFromName("Missing", out OrderStatus? none);
bool hasCompleted = OrderStatus.ContainsName("Completed");
bool hasValue = OrderStatus.ContainsValue(2);
```

Name comparison is **ordinal**. `"pending"` and `"PENDING"` do not match `"Pending"`. Leading or trailing spaces are not trimmed.

### Null and missing-value behavior

| API | Notable behavior |
| --- | --- |
| `FromName(null)` | `ArgumentNullException` |
| `FromName("")` / whitespace | `ArgumentException` |
| `FromName` unknown non-empty name | `KeyNotFoundException` |
| `TryFromName` / `ContainsName` | `false` for null, empty, whitespace, or missing name; does not throw for those inputs |
| `FromValue` with null runtime value | `ArgumentNullException` |
| `FromValue` unregistered value | `KeyNotFoundException` |
| `TryFromValue(null)` / `ContainsValue(null)` | `false` (relevant for reference-type `TValue`, e.g. `string`) |

Only operations that access the registry can surface a cached `SmartEnumValidationException`. Early-return input checks do **not** access or initialize the registry—for example `TryFromName` / `ContainsName` on null, empty, or whitespace names; `TryFromValue` / `ContainsValue` on null; and the pre-registry argument checks on `FromName` / `FromValue`. A real name or value lookup that reaches the registry does not catch validation failures.

## Equality

Equality is the domain type plus `Value`, compared with `EqualityComparer<TValue>.Default`. `Name` is not part of equality. A runtime subtype with the same value is equal. An instance that was never placed in the registry is equal to the registered instance with the same value.

```csharp
OrderStatus pending = OrderStatus.Pending;
OrderStatus completed = OrderStatus.Completed;

bool same = pending == OrderStatus.Pending;
bool different = pending == completed;
bool notSame = pending != completed;

OrderStatus? none = null;
OrderStatus? alsoNone = null;
bool pendingIsNull = pending == none;   // false
bool bothNull = none == alsoNone;       // true
```

`==` and `!=` are declared on `SmartEnum<TEnum, TValue>`. Both operands may be null. `GetHashCode()` combines `typeof(TEnum)` and `Value`, so equal instances work as dictionary keys.

A second domain with the same numeric value is a different type:

```csharp
public sealed partial class PayStatus : SmartEnum<PayStatus, int>
{
    public static readonly PayStatus Card = new("Card", 1);

    private PayStatus(string name, int value)
        : base(name, value)
    {
    }
}
```

```csharp
bool sameObject = OrderStatus.Pending.Equals((object)PayStatus.Card); // false
```

`OrderStatus.Pending == PayStatus.Card` does not compile (CS0019).

## Supported declarations and limitations

**Supported:** top-level, non-generic, `partial` classes inheriting `SmartEnum<TEnum, TValue>`.

**Not supported:**

- Nested domain types → **SMARTENUM006** (no generated source)
- Generic domain types
- Non-`partial` domains → **SMARTENUM001**

## Where this is tested

- `tests/integration/SmartEnum.Generator.IntegrationTests/OrderStatus.cs`
- `tests/integration/SmartEnum.Generator.IntegrationTests/PayStatus.cs`
- `tests/integration/SmartEnum.Generator.IntegrationTests/EqualityIntegrationTests.cs`
- `tests/integration/SmartEnum.Package.IntegrationTests/` (NuGet package consumer)
- `tests/unit/SmartEnum.Tests/SmartEnumLookupTests.cs`
- `tests/unit/SmartEnum.Tests/SmartEnumEqualityTests.cs`
