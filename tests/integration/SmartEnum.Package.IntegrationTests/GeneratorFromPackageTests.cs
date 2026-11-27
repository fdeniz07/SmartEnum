namespace SmartEnum.Package.IntegrationTests;

[Collection(nameof(PackageConsumerCollection))]
public sealed class GeneratorFromPackageTests
{
    private readonly PackageConsumerFixture _fixture;

    public GeneratorFromPackageTests(PackageConsumerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Analyzer_loads_from_the_isolated_nuget_package_cache()
    {
        string packagesRoot = Path.GetFullPath(_fixture.PackagesDirectory);
        string analyzerPath = Path.GetFullPath(_fixture.AnalyzerPath);

        Assert.StartsWith(packagesRoot, analyzerPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            Path.Combine("smartenum", PackageConsumerFixture.PackageVersion, "analyzers", "dotnet", "cs", "SmartEnum.Generators.dll"),
            analyzerPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Path.Combine("src", "SmartEnum.Generators", "bin"),
            analyzerPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generated_source_implements_the_interface_for_all_declared_instances()
    {
        string generated = _fixture.GeneratedSource;

        Assert.Contains("ISmartEnumGenerated", generated, StringComparison.Ordinal);
        Assert.Contains("OrderStatus", generated, StringComparison.Ordinal);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("Processing", generated, StringComparison.Ordinal);
        Assert.Contains("Completed", generated, StringComparison.Ordinal);
        Assert.Contains("GetInstances()", generated, StringComparison.Ordinal);

        string orderStatusSource = File.ReadAllText(Path.Combine(_fixture.ConsumerDirectory, "OrderStatus.cs"));
        Assert.DoesNotContain("ISmartEnumGenerated", orderStatusSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetInstances", orderStatusSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Consumer_build_completes_without_CS0311()
    {
        Assert.DoesNotContain("CS0311", _fixture.BuildStdout, StringComparison.Ordinal);
        Assert.DoesNotContain("CS0311", _fixture.BuildStderr, StringComparison.Ordinal);
    }
}
