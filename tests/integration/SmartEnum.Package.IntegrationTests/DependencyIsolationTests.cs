using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartEnum.Package.IntegrationTests;

[Collection(nameof(PackageConsumerCollection))]
public sealed class DependencyIsolationTests
{
    private readonly PackageConsumerFixture _fixture;

    public DependencyIsolationTests(PackageConsumerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Consumer_project_has_exactly_one_package_reference_and_no_project_references()
    {
        string csproj = File.ReadAllText(_fixture.ConsumerProjectPath);
        Match packageReference = Assert.Single(
            Regex.Matches(
                csproj,
                """
                <PackageReference\s+Include="(?<id>[^"]+)"\s+Version="(?<version>[^"]+)"\s*/>
                """,
                RegexOptions.CultureInvariant).Cast<Match>());
        Assert.Equal(PackageConsumerFixture.PackageId, packageReference.Groups["id"].Value);
        Assert.Equal(PackageConsumerFixture.PackageVersion, packageReference.Groups["version"].Value);
        Assert.DoesNotContain("<ProjectReference", csproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Restore_graph_contains_smartenum_without_core_or_generators_package_ids()
    {
        Assert.True(File.Exists(_fixture.AssetsFilePath), $"Missing assets file: {_fixture.AssetsFilePath}");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_fixture.AssetsFilePath));
        JsonElement libraries = document.RootElement.GetProperty("libraries");

        bool foundSmartEnum = false;
        foreach (JsonProperty library in libraries.EnumerateObject())
        {
            string name = library.Name;
            if (name.Equals($"SmartEnum/{PackageConsumerFixture.PackageVersion}", StringComparison.OrdinalIgnoreCase))
            {
                foundSmartEnum = true;
            }

            Assert.False(
                name.StartsWith("SmartEnum.Core/", StringComparison.OrdinalIgnoreCase),
                $"Unexpected SmartEnum.Core library entry: {name}");
            Assert.False(
                name.StartsWith("SmartEnum.Generators/", StringComparison.OrdinalIgnoreCase),
                $"Unexpected SmartEnum.Generators library entry: {name}");
        }

        Assert.True(foundSmartEnum, "project.assets.json did not contain SmartEnum/1.0.0-preview.1");
    }

    [Fact]
    public void Consumer_output_does_not_copy_roslyn_assemblies()
    {
        string[] roslynAssemblies = Directory.GetFiles(
            _fixture.ConsumerOutputDirectory,
            "Microsoft.CodeAnalysis*.dll",
            SearchOption.TopDirectoryOnly);
        Assert.Empty(roslynAssemblies);
    }

    [Fact]
    public void Analyzer_path_is_inside_the_fixture_nuget_packages_directory()
    {
        string packagesRoot = Path.GetFullPath(_fixture.PackagesDirectory);
        string analyzerPath = Path.GetFullPath(_fixture.AnalyzerPath);
        Assert.StartsWith(packagesRoot, analyzerPath, StringComparison.OrdinalIgnoreCase);
    }
}
