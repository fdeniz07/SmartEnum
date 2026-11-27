# SmartEnum

**Lightweight Smart Enum for .NET**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

SmartEnum is an open-source library that replaces a traditional .NET enum when a closed set of values needs a name, a typed value, and ordinary C# behavior.

The Core library and its Source Generator are implemented in this repository. A consumer domain is a `partial` type derived from `SmartEnum<TEnum, TValue>`. The generator discovers the static instances at compile time. Core then validates that set, looks instances up, and compares them.

> **Project status:** Core lookup, registry validation, and equality are implemented. NuGet packaging and the integration packages below are not published. The walkthrough is [docs/USAGE.md](docs/USAGE.md).

## Goals

- Lightweight API with a small conceptual surface.
- Core assembly with **zero external package dependencies**.
- Strongly typed named values with room for domain behavior.
- Source Generator kept in its own project, with no compile-time reference from Core back to the generator.
- .NET 10 and C# 14.
- MIT licensed and free to use.

## Implemented API

```csharp
public abstract class SmartEnum<TEnum, TValue> : IEquatable<TEnum>
    where TEnum : SmartEnum<TEnum, TValue>, ISmartEnumGenerated<TEnum>
    where TValue : notnull
```

`TEnum` is the concrete domain type. `TValue` is the non-nullable underlying value. `ISmartEnumGenerated<TEnum>` is the technical contract the generator implements. Consumers write `partial` and the static fields. They do not write `GetInstances()`.

| Member | Behavior |
| --- | --- |
| `Name` | Canonical instance name. |
| `Value` | Underlying value. |
| `Registry` | Validated, immutable catalog for this domain. The only public registry entry. |
| `List` | The same collection as `Registry.Items`, in discovery order. |
| `FromName` / `FromValue` | Return the registered instance or throw. |
| `TryFromName` / `TryFromValue` | `TryFromName` returns `false` for null, empty, and whitespace names without reading the registry. `TryFromValue` returns `false` for null. Every other value, including an empty or whitespace string, uses the normal value lookup. A miss returns `false`. |
| `ContainsName` / `ContainsValue` | Same classification as the `Try` methods, without the instance. |
| `Equals`, `==`, `!=` | Equal when the domain type and `Value` match. `Name`, the runtime subtype, and registry membership are not part of equality. |

`GetHashCode()` is `HashCode.Combine(typeof(TEnum), Value)`. Name comparison is ordinal. Value comparison uses `EqualityComparer<TValue>.Default`. Two different closed domains do not support `==` or `!=` (CS0019). `Equals(object?)` returns `false` across domains.

A short domain looks like this. The full sample, lookups, and equality cases are in [docs/USAGE.md](docs/USAGE.md).

```csharp
public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
{
    public static readonly OrderStatus Pending = new("Pending", 1);
    public static readonly OrderStatus Approved = new("Approved", 2);

    private OrderStatus(string name, int value)
        : base(name, value)
    {
    }
}
```

## What the generator does

`src/SmartEnum.Generators` is an incremental Roslyn generator. For each eligible `partial` domain it emits `ISmartEnumGenerated<T>` and a static array of the `public static readonly` fields. Diagnostics are SMARTENUM001 through SMARTENUM006. Details are in [docs/design/SOURCE_GENERATOR.md](docs/design/SOURCE_GENERATOR.md).

Core, in `src/SmartEnum`, owns registry validation, lookup, equality, and the runtime exceptions. The generator does not emit those APIs.

Accepted decisions: [docs/architecture/decisions/README.md](docs/architecture/decisions/README.md).

## Packages

| Package | State in this repository | Responsibility |
| --- | --- | --- |
| `SmartEnum` | Implemented. No NuGet package has been published. | Registry, lookup, equality |
| `SmartEnum.Generators` | Implemented. Referenced by tests as an analyzer. Not yet bundled into the Core package. | Compile-time discovery |

These packages are **planned** and are not projects in this repository:

| Planned package | Responsibility |
| --- | --- |
| `SmartEnum.SystemTextJson` | System.Text.Json support, without a JSON dependency in Core |
| `SmartEnum.EntityFrameworkCore` | EF Core persistence, in a separate package |

JSON representation and EF Core conversion are still design topics. They are not implemented.

## Target framework

`global.json` pins SDK `10.0.404`, with `rollForward` set to `latestFeature` and `allowPrerelease` set to `false`. The language version is C# 14. Tests run on Microsoft.Testing.Platform.

## Repository layout

```text
SmartEnum/
├── src/
│   ├── SmartEnum/
│   └── SmartEnum.Generators/
├── tests/
│   ├── unit/SmartEnum.Tests/
│   ├── generators/SmartEnum.Generators.Tests/
│   └── integration/SmartEnum.Generator.IntegrationTests/
├── docs/
├── global.json
├── LICENSE
├── README.md
└── SmartEnum.slnx
```

## Build and test

```powershell
git clone https://github.com/fdeniz07/SmartEnum.git
cd SmartEnum
dotnet test SmartEnum.slnx -p:TreatWarningsAsErrors=true
```

A local consumer references `src/SmartEnum/SmartEnum.csproj` and references `src/SmartEnum.Generators/SmartEnum.Generators.csproj` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

## Design principles

1. **Dependency discipline.** The Core assembly has no external package dependencies.
2. **Explicit behavior.** Lookup, equality, and failure behavior are specified and tested.
3. **Stable public API.** Accepted ADRs stay in force. Packaging and integrations are still open.
4. **Optional integrations.** Framework-specific support belongs in separate packages.
5. **Testability.** Unit, generator, and integration tests cover the implemented contract.
6. **Maintainability.** Prefer a direct implementation over an extra abstraction layer.

## Contributing

Core behavior for discovery, registry validation, lookup, and equality is accepted. Open an issue before proposing a new public API. Contribution guidelines beyond this README are still to be written.

## License

SmartEnum is licensed under the [MIT License](LICENSE).

## Acknowledgements

SmartEnum is an independent implementation. It does not copy source code from Ardalis.SmartEnum or other Smart Enum libraries.
