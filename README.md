# SmartEnum

**Lightweight Smart Enum for .NET**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-in%20development-orange)]()

SmartEnum is an open-source library project that aims to provide a lightweight, strongly typed alternative to traditional .NET enums when an application needs behavior or metadata alongside a finite set of values.

The project is being designed for modern .NET applications, with a dependency-free core and optional integration packages.

> **Project status:** Early development. The public API, package boundaries, and behavior are not finalized yet. Examples in this README describe the intended direction and are not a promise of the final API.

## Goals

- Lightweight API with a small conceptual surface.
- Core package with **zero external package dependencies**.
- Strongly typed named values with room for domain behavior and metadata.
- Optional integrations kept in separate packages.
- Modern .NET 10 and C# 14 target.
- Clear documentation, tests, and benchmarks.
- MIT licensed and free to use.

## Package plan

| Package | Responsibility | Dependencies |
| --- | --- | --- |
| `SmartEnum` | Core Smart Enum abstractions and behavior | No external package dependencies |
| `SmartEnum.SystemTextJson` | System.Text.Json serialization support | Core + framework-provided JSON APIs |
| `SmartEnum.EntityFrameworkCore` | Entity Framework Core integration | Core + compatible EF Core package |

The integration packages are optional. Applications that only need the core functionality should not have to reference JSON or Entity Framework Core packages.

Package versions and compatibility details will be documented when the integrations are implemented.

## Intended usage

A typical Smart Enum represents a closed set of named, typed values that can carry domain-specific data or behavior.

The following is **illustrative pseudocode only**. It is not compilable against a released package and does not define the final API:

```csharp
// Illustrative concept — final API is not yet defined.
public sealed class OrderStatus
{
    public static readonly OrderStatus Pending = new("pending");
    public static readonly OrderStatus Paid = new("paid");
    public static readonly OrderStatus Shipped = new("shipped");

    public string Name { get; }

    private OrderStatus(string name) => Name = name;
}
```

The final API will be decided through explicit design work and documented before implementation.

## Planned integrations

### System.Text.Json

The optional `SmartEnum.SystemTextJson` package is planned to provide JSON support without adding a JSON dependency to the core package.

The exact representation (for example, serializing by name or by value), converter registration, null handling, and unknown-value behavior are design topics still to be decided.

### Entity Framework Core

The optional `SmartEnum.EntityFrameworkCore` package is planned to support persistence with EF Core.

Conversion behavior, model configuration, query translation expectations, and supported EF Core versions will be specified before implementation.

## Target framework and language

The initial target is:

- .NET 10
- C# 14

The repository will pin its SDK selection so machines with newer SDKs installed do not silently change the intended build toolchain.

## Repository structure

The planned repository layout is:

```text
SmartEnum/
├── src/
│   ├── SmartEnum/
│   ├── SmartEnum.SystemTextJson/
│   └── SmartEnum.EntityFrameworkCore/
├── tests/
├── samples/
├── docs/
├── .github/
├── LICENSE
├── README.md
└── SmartEnum.sln
```

The directory structure may evolve as the architecture is finalized.

## Getting started

There is no NuGet package available yet.

Once the first package is published, installation instructions and verified, compilable examples will be added here.

For contributors who want to inspect the project during development:

1. Clone the repository.
2. Open `SmartEnum.sln` once the solution has been added.
3. Use the .NET 10 SDK.
4. Run the test suite after the projects are available.

```powershell
git clone https://github.com/fdeniz07/SmartEnum.git
cd SmartEnum
```

## Design principles

The project will follow these principles:

1. **Dependency discipline** — keep the core free of external dependencies.
2. **Explicit behavior** — define lookup, equality, identity, and failure behavior precisely.
3. **Stable public API** — review API design before implementation and avoid unnecessary breaking changes.
4. **Optional integrations** — framework-specific support belongs in separate packages.
5. **Testability** — cover normal behavior, edge cases, and invalid input.
6. **Maintainability** — prefer simple, readable implementation over unnecessary abstraction.

## Contributing

The project is in its early design stage. Before opening a pull request for a feature, please open an issue to discuss the use case and proposed API.

Contribution guidelines, coding conventions, and the development workflow will be documented as the project matures.

## License

SmartEnum is licensed under the [MIT License](LICENSE).

## Acknowledgements

SmartEnum is an independent implementation. It does not copy source code from Ardalis.SmartEnum or other Smart Enum libraries.
