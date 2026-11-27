# ADR-006 — Single NuGet Package Distribution

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-11-27 |
| Deciders | SmartEnum maintainers |
| Scope | NuGet packaging and distribution |
| Related | [ADR-002](ADR-002-instance-discovery.md) |
| Turkish | [ADR-006-single-nuget-package-distribution.tr.md](ADR-006-single-nuget-package-distribution.tr.md) |

This record documents a packaging decision that is already implemented. It does not change production runtime behavior, discovery rules, registry validation, or the public Core API accepted in ADR-002 through ADR-005.

## Context

ADR-002 requires compile-time instance discovery through an incremental Source Generator. Consumers must receive both the Core runtime assembly and the generator so that `partial` domains can satisfy `ISmartEnumGenerated<TSelf>` and use Registry, lookup, and equality APIs.

Before this decision, packaging topology was provisional in ADR-002 and in `docs/design/SOURCE_GENERATOR.md`. Integration tests referenced the generator as an Analyzer `ProjectReference`. That wiring is not how NuGet consumers obtain analyzers.

Two distribution shapes were available:

1. One NuGet package that contains Core and the generator.
2. Separate Core and Generators packages.

The project owner accepted the single-package shape. R2 packaging milestones implemented that shape.

## Decision

**Accepted:** Core and the Source Generator are distributed in one NuGet package whose `PackageId` is `SmartEnum`.

Accepted layout and ownership:

1. Core and the Source Generator ship in the single `SmartEnum` package.
2. A dedicated packaging project, `src/SmartEnum.Packaging/SmartEnum.Packaging.csproj`, owns package production.
3. Core and Generator remain independent projects. Neither takes a `ProjectReference` to the other.
4. The Core assembly is placed at `lib/net10.0/SmartEnum.dll`.
5. The Generator assembly is placed at `analyzers/dotnet/cs/SmartEnum.Generators.dll`.
6. Core XML documentation is placed at `lib/net10.0/SmartEnum.xml`.
7. `README.md` is included at the package root.
8. Roslyn compiler assemblies are not copied into the consumer package.
9. Core and Generator set `IsPackable=false`. Only the Packaging project produces the shipping package.

The first preview version under this decision is `1.0.0-preview.1`.

## Options considered

### Option A — Single `SmartEnum` package

One `PackageReference` restores Core under `lib/` and the generator under `analyzers/dotnet/cs/`. A packaging-only project gathers `GetTargetPath` and documentation outputs without creating a Core ↔ Generator compile-time dependency.

### Option B — Separate Core and Generators packages

Publish `SmartEnum` and `SmartEnum.Generators`. Consumers must reference both, or Core must declare a dependency that pulls the analyzer package.

This option was rejected. Two packages make discovery optional for a careless install, and they split versioning across the pair that ADR-002 treats as one product capability.

### Option C — Pack from the Core project with an Analyzer project reference

Have `SmartEnum.csproj` pack itself and reference the generator with `OutputItemType="Analyzer"`.

This option was rejected. It would give Core a project reference to the generator and contradict the accepted project boundary: Core must not `ProjectReference` Generators, and Generators must not `ProjectReference` Core.

## Why Option A was selected

- Consumers install one package and get runtime types plus automatic generator execution.
- NuGet loads `analyzers/dotnet/cs` for C# without a second reference.
- Packaging stays outside the Core and Generator compile graphs, so the independent-project boundary remains.
- Distribution metadata, README inclusion, and XML documentation live in one shipping project.
- ADR-003’s rule of no Core runtime package dependencies remains intact: the nuspec dependency group for `net10.0` stays empty, and Roslyn packages stay `PrivateAssets` on the generator build only.

## Package contents

| Path | Content |
| --- | --- |
| `lib/net10.0/SmartEnum.dll` | Core runtime |
| `lib/net10.0/SmartEnum.xml` | Core XML documentation |
| `analyzers/dotnet/cs/SmartEnum.Generators.dll` | Source Generator |
| `README.md` | Package readme |

Excluded from the shipping package:

- `SmartEnum.Packaging.dll`
- `Microsoft.CodeAnalysis*.dll` and other Roslyn implementation assemblies
- A second copy of Core or Generator under another package folder

## Relationship with earlier ADRs

- ADR-002 remains the discovery decision. This ADR only records the previously provisional NuGet packaging shape.
- ADR-003 remains the registry validation decision. Core still has no runtime NuGet dependencies.
- ADR-004 and ADR-005 remain the Registry access and Lookup/Equality API decisions. Packaging does not change those members.

## Consequences and trade-offs

### Positive

- One documented install path: `PackageReference` to `SmartEnum`.
- The Source Generator loads automatically from the analyzer asset folder.
- Core and Generator stay separately buildable and separately testable.
- Package identity, version, license, README, and repository metadata are owned in one place.

### Trade-offs and maintenance cost

- Packaging logic lives in a third project and custom MSBuild targets (`GetTargetPath`, `DocumentationProjectOutputGroup`, explicit `PackagePath` values). That surface must stay aligned with SDK pack behavior.
- Core uses a non-shipping `PackageId` (`SmartEnum.Core`) only to avoid solution restore ambiguity with the shipping `PackageId` (`SmartEnum`). Accidental pack enablement of Core would publish the wrong id.
- Future multi-targeting, symbol packages, or additional analyzer assets require coordinated updates in the Packaging project.
- Integration proof that a restore-only consumer works with one `PackageReference` is a packaging verification concern; it is not redefined by this ADR as a Core API change.

## Testing and verification expectations

Packaging verification should confirm:

- Solution pack produces exactly one `SmartEnum` nupkg.
- The archive contains the four shipping assets listed above and no Roslyn or Packaging assemblies.
- The nuspec `net10.0` dependency group is empty.
- Core XML in the package matches the Core documentation output.

Runtime unit tests, generator driver tests, and Analyzer `ProjectReference` integration tests remain valid for behavior. They do not replace package-layout verification.

## References

- `src/SmartEnum.Packaging/SmartEnum.Packaging.csproj`
- `src/SmartEnum/SmartEnum.csproj`
- `src/SmartEnum.Generators/SmartEnum.Generators.csproj`
- `docs/design/SOURCE_GENERATOR.md`
- `ADR-006-single-nuget-package-distribution.tr.md`
