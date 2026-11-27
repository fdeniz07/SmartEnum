# SmartEnum

**Lightweight Smart Enum for .NET**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

SmartEnum replaces a traditional .NET `enum` when a closed set of values needs a **name**, a **typed value**, and ordinary C# behavior—lookup, equality, and room for domain methods—without reflection or manual registration.

A consumer declares a `partial` type that inherits `SmartEnum<TEnum, TValue>`. An incremental Roslyn source generator discovers the static instances at compile time. Core validates that set once, then serves lookup and comparison APIs.

## Key features

- Strongly typed named values with a small public API surface
- Compile-time instance discovery (no reflection, no assembly scanning)
- Validated immutable registry and discovery-ordered `List`
- Name/value lookup: `From*`, `TryFrom*`, `Contains*`
- Value-based equality (`==` / `!=` / `Equals`)
- Core assembly with **zero runtime package dependencies**
- Single NuGet package that ships Core + the source generator together
- .NET 10 / C# 14, MIT licensed

## Installation / package status

The shipping package id is **`SmartEnum`**. It is produced by `src/SmartEnum.Packaging` and includes:

| Package content | Location |
| --- | --- |
| Core runtime | `lib/net10.0/SmartEnum.dll` |
| XML documentation | `lib/net10.0/SmartEnum.xml` |
| Source generator | `analyzers/dotnet/cs/SmartEnum.Generators.dll` |
| Generator symbols | `analyzers/dotnet/cs/SmartEnum.Generators.pdb` (inside the main nupkg) |
| Package readme | `README.md` |
| Package icon | `package-icon.png` |

Core portable symbols ship in a companion **`.snupkg`** as `lib/net10.0/SmartEnum.pdb` (not inside the main nupkg). SourceLink uses the .NET 10 SDK built-in repository metadata—no explicit `Microsoft.SourceLink.GitHub` package reference.

There is **no** separately distributed `SmartEnum.Generators` package. One `PackageReference` to `SmartEnum` restores both the runtime types and the analyzer.

> **Publication status:** The package layout is implemented in this repository (preview version `1.0.0-preview.1`). It is **not** claimed to be published on NuGet.org from this documentation. Until a public feed listing is available, build from source or consume a locally packed nupkg. Preview release checklist: [docs/release/nuget-preview-checklist.md](docs/release/nuget-preview-checklist.md).

Intended consumer reference (when the package is available on your feed):

```xml
<PackageReference Include="SmartEnum" Version="1.0.0-preview.1" />
```

Consumers do **not** manually implement `ISmartEnumGenerated<T>` and do **not** need a second generator package reference.

Walkthrough with full API notes: [docs/USAGE.md](docs/USAGE.md).

## Quick Start

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

`partial` is required: the source generator emits a second partial declaration that implements `ISmartEnumGenerated<OrderStatus>` and provides the discovered instances. You declare the type, the static fields, and the constructor—not `GetInstances()`.

Practical usage:

```csharp
OrderStatus pending = OrderStatus.FromName("Pending");
OrderStatus processing = OrderStatus.FromValue(2);

bool found = OrderStatus.TryFromName("Completed", out OrderStatus? completed);
bool known = OrderStatus.ContainsName("Pending");

var all = OrderStatus.List; // Pending, Processing, Completed

bool same = OrderStatus.Pending == pending; // true
```

## Lookup examples

```csharp
OrderStatus pending = OrderStatus.FromName("Pending");
OrderStatus alsoPending = OrderStatus.FromValue(1);

bool ok = OrderStatus.TryFromName("Pending", out OrderStatus? match);
bool missing = OrderStatus.TryFromName("Missing", out _);
bool hasName = OrderStatus.ContainsName("Processing");
bool hasValue = OrderStatus.ContainsValue(3);
```

Name comparison is **ordinal** (case-sensitive, no trim). `TryFromName` / `ContainsName` return `false` for null, empty, and whitespace without throwing. `FromName` / `FromValue` throw on invalid or missing input. Details: [docs/USAGE.md](docs/USAGE.md).

## Equality and comparison

Equality is **domain type + `Value`** (`EqualityComparer<TValue>.Default`). `Name`, runtime subtype, and registry membership are not part of equality.

```csharp
bool same = OrderStatus.Pending == OrderStatus.FromValue(1); // true
bool different = OrderStatus.Pending == OrderStatus.Completed; // false
bool notSame = OrderStatus.Pending != OrderStatus.Completed; // true
```

Different domains do not support `==` / `!=` between each other (CS0019). `Equals(object?)` returns `false` across domains.

## How source generation works

1. You declare a top-level `partial` class inheriting `SmartEnum<TEnum, TValue>` with `public static readonly` instances.
2. `SmartEnum.Generators` (shipped under `analyzers/dotnet/cs` in the `SmartEnum` package) discovers those fields at compile time.
3. It emits `ISmartEnumGenerated<TEnum>` and a static array of instances (diagnostics SMARTENUM001–SMARTENUM006).
4. The first operation that actually accesses the registry (`Registry`, `List`, or a lookup that reaches the catalog) validates the set (non-empty, no nulls, unique names and values) and publishes an immutable catalog. Early-return input checks—such as `TryFromName` / `ContainsName` on null, empty, or whitespace, `TryFromValue` / `ContainsValue` on null, and the pre-registry argument checks on `FromName` / `FromValue`—do **not** initialize the registry.

There is **no** reflection-based discovery, **no** runtime assembly scanning, **no** manual registration API, and **no** production `ModuleInitializer` side effect. Design notes: [docs/design/SOURCE_GENERATOR.md](docs/design/SOURCE_GENERATOR.md).

## Supported domain declarations and limitations

**Supported (verified):** top-level, non-generic, `partial` classes.

**Not supported today:**

- Nested domain types (diagnostic **SMARTENUM006**; no source is generated)
- Generic domain types
- Non-`partial` domains (**SMARTENUM001**)

## Package architecture

| Project | Role |
| --- | --- |
| `src/SmartEnum` | Core runtime (`IsPackable=false`; restore id `SmartEnum.Core` only to avoid solution ambiguity) |
| `src/SmartEnum.Generators` | Roslyn generator (`IsPackable=false`) |
| `src/SmartEnum.Packaging` | Produces the shipping `SmartEnum` nupkg |

Core and Generator do **not** `ProjectReference` each other. Packaging gathers `GetTargetPath` / documentation outputs into one package. Decision record: [ADR-006](docs/architecture/decisions/ADR-006-single-nuget-package-distribution.md).

Planned (not in this repository): optional `SmartEnum.SystemTextJson` and `SmartEnum.EntityFrameworkCore` integration packages. JSON and EF Core conversion are not implemented in Core.

## Build and test

```powershell
git clone https://github.com/fdeniz07/SmartEnum.git
cd SmartEnum
dotnet restore SmartEnum.slnx
dotnet build SmartEnum.slnx -c Release --no-restore
dotnet test SmartEnum.slnx -c Release --no-build
```

`global.json` pins SDK `10.0.404` (`rollForward`: `latestFeature`, `allowPrerelease`: `false`). Tests use Microsoft.Testing.Platform. CI runs on GitHub Actions (`.github/workflows/ci.yml`).

## Repository layout

```text
SmartEnum/
├── assets/branding/                       # Logo, symbol, package icon
├── src/
│   ├── SmartEnum/                         # Core runtime
│   ├── SmartEnum.Generators/              # Source generator
│   └── SmartEnum.Packaging/               # Shipping NuGet package project
├── tests/
│   ├── unit/SmartEnum.Tests/
│   ├── generators/SmartEnum.Generators.Tests/
│   └── integration/
│       ├── SmartEnum.Generator.IntegrationTests/
│       └── SmartEnum.Package.IntegrationTests/
├── docs/
│   ├── USAGE.md
│   ├── release/nuget-preview-checklist.md
│   ├── architecture/decisions/            # ADRs (including ADR-006)
│   └── design/SOURCE_GENERATOR.md
├── .github/workflows/ci.yml
├── global.json
├── LICENSE
├── README.md
└── SmartEnum.slnx
```

## Documentation

| Document | Content |
| --- | --- |
| [docs/USAGE.md](docs/USAGE.md) | Consumer usage, lookup, equality, limitations |
| [docs/release/nuget-preview-checklist.md](docs/release/nuget-preview-checklist.md) | Preview pack / symbol validation checklist |
| [docs/design/SOURCE_GENERATOR.md](docs/design/SOURCE_GENERATOR.md) | Generator design and diagnostics |
| [docs/architecture/decisions/README.md](docs/architecture/decisions/README.md) | Accepted ADRs |
| [ADR-006](docs/architecture/decisions/ADR-006-single-nuget-package-distribution.md) | Single NuGet package distribution |

## License

SmartEnum is licensed under the [MIT License](LICENSE).

## Acknowledgements

SmartEnum is an independent implementation. It does not copy source code from Ardalis.SmartEnum or other Smart Enum libraries.
