# SmartEnum — Usage and Development Notes

This document tracks how the project is expected to be used and developed. It is intentionally conservative while the public API is still under design.

## Current status

- Repository initialized with an MIT license.
- No NuGet package has been published.
- No public API is finalized.
- No implementation or integration project is available yet.

## Intended package selection

### Core only

Reference `SmartEnum` when you need the core Smart Enum functionality and do not need framework integration.

### JSON support

Reference `SmartEnum.SystemTextJson` only when the application needs System.Text.Json integration.

### EF Core support

Reference `SmartEnum.EntityFrameworkCore` only when the application needs Entity Framework Core persistence integration.

Exact installation commands will be added after package IDs and the first stable release are confirmed.

## API design checklist

Before implementation, settle the following topics:

- Generic type model and inheritance/composition approach.
- Declaration and registration of known instances.
- Identity, equality, and hash code semantics.
- Name/value lookup APIs and duplicate handling.
- Behavior for unknown values and invalid input.
- Enumeration/discovery of declared instances.
- Thread-safety and initialization guarantees.
- Whether extensibility or closed-set semantics are required.
- JSON representation and converter behavior.
- EF Core conversion, query behavior, and supported versions.

## Development environment

Initial toolchain target:

- Windows development environment is supported.
- .NET 10 SDK.
- C# 14.
- Git and GitHub.
- Visual Studio / Visual Studio Code.

Use the repository-level `global.json` once added to select the intended .NET SDK consistently.

## Local clone

```powershell
git clone https://github.com/fdeniz07/SmartEnum.git
cd SmartEnum
```

The solution and build/test commands will be documented here after the initial project scaffolding is in place.

## Compatibility policy

The core package targets .NET 10 initially. Additional target frameworks, if any, must be justified by consumer demand and tested in CI.

Integration package versions must align with explicitly supported framework versions. The project will document the support matrix before publishing integration packages.

## Release workflow

A release checklist will be maintained before the first NuGet publication. It should cover:

- Semantic version and release notes.
- Package metadata, symbols, and license/readme inclusion.
- Automated tests and packaging validation.
- NuGet package inspection.
- Published package smoke test.
- Documentation and compatibility matrix.
