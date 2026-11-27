# NuGet preview release checklist

Checklist for preparing the **`SmartEnum`** preview package (`1.0.0-preview.1`).
This document does **not** claim the package is published on NuGet.org.

## Version control

- [ ] `PackageId` is `SmartEnum` (unchanged).
- [ ] `Version` is `1.0.0-preview.1` (unchanged for this preview).
- [ ] Working tree matches the intended release commit; no unrelated local edits.
- [ ] `git diff --check` is clean.

## Package metadata

Verify in `src/SmartEnum.Packaging/SmartEnum.Packaging.csproj` and the packed `.nuspec`:

- [ ] Authors / description / MIT license expression
- [ ] `PackageProjectUrl` → repository URL
- [ ] `RepositoryUrl` / `RepositoryType` / `PublishRepositoryUrl`
- [ ] `PackageTags`: `smartenum;enum;dotnet;source-generator;roslyn`
- [ ] `Copyright`: `Copyright (c) Fatih Deniz`
- [ ] `PackageIcon`: `package-icon.png` (from `assets/branding/package-icon.png`)
- [ ] `PackageReadmeFile`: `README.md`
- [ ] `PackageReleaseNotes` present and accurate for a preview
- [ ] `net10.0` dependency group is empty (no runtime package dependencies)

## Local pack

From the repository root (SDK from `global.json`):

```powershell
dotnet restore SmartEnum.slnx
dotnet build SmartEnum.slnx -c Release --no-restore
dotnet pack src/SmartEnum.Packaging/SmartEnum.Packaging.csproj -c Release --no-build -o ./artifacts/nuget
```

Expected outputs:

- `artifacts/nuget/SmartEnum.1.0.0-preview.1.nupkg`
- `artifacts/nuget/SmartEnum.1.0.0-preview.1.snupkg`

## nupkg validation

Must contain:

- [ ] `lib/net10.0/SmartEnum.dll`
- [ ] `lib/net10.0/SmartEnum.xml`
- [ ] `analyzers/dotnet/cs/SmartEnum.Generators.dll`
- [ ] `analyzers/dotnet/cs/SmartEnum.Generators.pdb`
- [ ] `README.md`
- [ ] `package-icon.png`

Must **not** contain:

- [ ] `SmartEnum.Packaging.dll`
- [ ] `Microsoft.CodeAnalysis*.dll`
- [ ] Core PDB (`lib/net10.0/SmartEnum.pdb`)
- [ ] Any `SmartEnum.Core` package identity / assets

## snupkg validation

- [ ] Exact entries: `SmartEnum.nuspec`, `lib/net10.0/SmartEnum.pdb`, `[Content_Types].xml`
- [ ] Does **not** contain generator PDB, DLLs, icon, or README files
- [ ] nuspec `id` = `SmartEnum`, `version` = `1.0.0-preview.1`
- [ ] `packageType` name is exactly `SymbolsPackage`
- [ ] nuspec does **not** include authors, owners, requireLicenseAcceptance, license, licenseUrl, icon, or readme
- [ ] Core PDB magic is `BSJB` (portable PDB)
- [ ] Packed `lib/net10.0/SmartEnum.dll` CodeView PDB ID matches the snupkg PDB
- [ ] SourceLink / GitHub document URLs present when built from a git checkout with repository metadata

## Test requirements

- [ ] `dotnet test tests/unit/SmartEnum.Tests/SmartEnum.Tests.csproj -c Release`
- [ ] `dotnet test tests/generators/SmartEnum.Generators.Tests/SmartEnum.Generators.Tests.csproj -c Release`
- [ ] `dotnet test tests/integration/SmartEnum.Generator.IntegrationTests/SmartEnum.Generator.IntegrationTests.csproj -c Release`
- [ ] `dotnet test tests/integration/SmartEnum.Package.IntegrationTests/SmartEnum.Package.IntegrationTests.csproj -c Release`
  (includes R3 consumer restore isolation and package archive layout asserts)

## NuGet.org pre-check (future publish only)

Do **not** publish from this checklist until an explicit publish milestone says so.

- [ ] Package id / version conflict check on NuGet.org
- [ ] Metadata and README review for public consumers
- [ ] Icon and license expression review
- [ ] Symbol package pairing (`.nupkg` + `.snupkg`) confirmed
- [ ] SourceLink navigation smoke-tested in an IDE against a published symbol package

## API key / trusted publishing (future)

- [ ] Decide between NuGet.org API key and Trusted Publishing (OIDC)
- [ ] Store secrets only in GitHub Actions secrets / environment protection—never in the repo
- [ ] Restrict publish workflow to `main` tags or a manual `workflow_dispatch` with environment approval
- [ ] No publish step in the default CI path until authorized

## Publication status for this preview

**`SmartEnum` `1.0.0-preview.1` is not published to NuGet.org from this repository yet.**
CI may pack and upload build artifacts for review; that is not a public feed publish.
