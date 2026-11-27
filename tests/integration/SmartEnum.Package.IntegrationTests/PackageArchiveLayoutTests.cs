using System.IO.Compression;
using System.Xml.Linq;

namespace SmartEnum.Package.IntegrationTests;

[Collection(nameof(PackageConsumerCollection))]
public sealed class PackageArchiveLayoutTests
{
    private static readonly string[] RequiredNupkgEntries =
    [
        "lib/net10.0/SmartEnum.dll",
        "lib/net10.0/SmartEnum.xml",
        "analyzers/dotnet/cs/SmartEnum.Generators.dll",
        "analyzers/dotnet/cs/SmartEnum.Generators.pdb",
        "README.md",
        "package-icon.png",
    ];

    private static readonly string[] ExpectedNupkgAssembliesAndSymbols =
    [
        "analyzers/dotnet/cs/SmartEnum.Generators.dll",
        "analyzers/dotnet/cs/SmartEnum.Generators.pdb",
        "lib/net10.0/SmartEnum.dll",
    ];

    private static readonly string[] ExpectedSnupkgEntries =
    [
        "[Content_Types].xml",
        "lib/net10.0/SmartEnum.pdb",
        "SmartEnum.nuspec",
    ];

    private static readonly string[] ForbiddenSymbolMetadata =
    [
        "authors",
        "owners",
        "requireLicenseAcceptance",
        "license",
        "licenseUrl",
        "icon",
        "readme",
    ];

    private readonly PackageConsumerFixture _fixture;

    public PackageArchiveLayoutTests(PackageConsumerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Nupkg_contains_shipping_assets_and_excludes_forbidden_entries()
    {
        string[] entries = ReadEntryNames(_fixture.PackagePath);

        foreach (string required in RequiredNupkgEntries)
        {
            Assert.Contains(required, entries, StringComparer.Ordinal);
        }

        Assert.DoesNotContain(
            entries,
            static name => name.Contains("Packaging.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            entries,
            static name => name.Contains("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        Assert.DoesNotContain(
            entries,
            static name => name.Contains("SmartEnum.Core", StringComparison.Ordinal));
        Assert.DoesNotContain(
            entries,
            static name => name.Equals("lib/net10.0/SmartEnum.pdb", StringComparison.Ordinal)
                || name.EndsWith("/SmartEnum.pdb", StringComparison.Ordinal)
                || name.Equals("SmartEnum.pdb", StringComparison.Ordinal));

        string[] dllAndPdb = entries
            .Where(static name =>
                name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedNupkgAssembliesAndSymbols, dllAndPdb);
    }

    [Fact]
    public void Snupkg_contains_exactly_the_core_symbol_payload()
    {
        string snupkgPath = GetSnupkgPath();
        string[] entries = ReadEntryNames(snupkgPath)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = ExpectedSnupkgEntries
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, entries);
        Assert.DoesNotContain(
            entries,
            static name => name.Equals("analyzers/dotnet/cs/SmartEnum.Generators.pdb", StringComparison.Ordinal));
        Assert.DoesNotContain(
            entries,
            static name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        Assert.Single(entries, static name => name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Snupkg_nuspec_declares_symbols_package_without_nupkg_only_metadata()
    {
        XElement metadata = ReadNuspecMetadata(GetSnupkgPath());
        XNamespace ns = metadata.Name.Namespace;

        Assert.Equal(PackageConsumerFixture.PackageId, metadata.Element(ns + "id")?.Value);
        Assert.Equal(PackageConsumerFixture.PackageVersion, metadata.Element(ns + "version")?.Value);

        XElement packageTypes = Assert.Single(metadata.Elements(ns + "packageTypes"));
        XElement packageType = Assert.Single(packageTypes.Elements(ns + "packageType"));
        Assert.Equal("SymbolsPackage", (string?)packageType.Attribute("name"));

        foreach (string localName in ForbiddenSymbolMetadata)
        {
            Assert.False(
                metadata.Elements().Any(element => element.Name.LocalName == localName),
                $"Symbol nuspec unexpectedly contains <{localName}>.");
        }
    }

    [Fact]
    public void Core_portable_pdb_matches_packed_core_dll()
    {
        byte[] dllBytes = ReadEntryBytes(_fixture.PackagePath, "lib/net10.0/SmartEnum.dll");
        byte[] pdbBytes = ReadEntryBytes(GetSnupkgPath(), "lib/net10.0/SmartEnum.pdb");
        PortablePdbIdentity.AssertDllMatchesPortablePdb(dllBytes, pdbBytes);
    }

    [Fact]
    public void Nuspec_net10_dependency_group_is_empty()
    {
        XElement metadata = ReadNuspecMetadata(_fixture.PackagePath);
        XNamespace ns = metadata.Name.Namespace;
        XElement? group = metadata
            .Descendants(ns + "group")
            .SingleOrDefault(element =>
                string.Equals(
                    (string?)element.Attribute("targetFramework"),
                    "net10.0",
                    StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(group);
        Assert.Empty(group.Elements(ns + "dependency"));
    }

    private string GetSnupkgPath()
    {
        string snupkgPath = Path.ChangeExtension(_fixture.PackagePath, ".snupkg");
        Assert.True(File.Exists(snupkgPath), $"Missing symbol package: {snupkgPath}");
        return snupkgPath;
    }

    private static XElement ReadNuspecMetadata(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry nuspecEntry = Assert.Single(
            archive.Entries,
            static entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        using Stream stream = nuspecEntry.Open();
        XDocument document = XDocument.Load(stream);
        XElement? metadata = document.Root?.Elements().SingleOrDefault(element => element.Name.LocalName == "metadata");
        Assert.NotNull(metadata);
        return metadata;
    }

    private static string[] ReadEntryNames(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        return archive.Entries
            .Select(static entry => entry.FullName.Replace('\\', '/'))
            .ToArray();
    }

    private static byte[] ReadEntryBytes(string packagePath, string entryName)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.FullName.Replace('\\', '/'), entryName, StringComparison.Ordinal));
        Assert.NotNull(entry);
        using Stream stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
