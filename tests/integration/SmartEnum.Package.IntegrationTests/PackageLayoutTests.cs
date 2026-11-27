namespace SmartEnum.Package.IntegrationTests;

[Collection(nameof(PackageConsumerCollection))]
public sealed class PackageLayoutTests
{
    private readonly PackageConsumerFixture _fixture;

    public PackageLayoutTests(PackageConsumerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Local_feed_contains_the_shipping_preview_package()
    {
        Assert.True(File.Exists(_fixture.PackagePath), $"Missing package: {_fixture.PackagePath}");
        Assert.Equal(
            $"{PackageConsumerFixture.PackageId}.{PackageConsumerFixture.PackageVersion}.nupkg",
            Path.GetFileName(_fixture.PackagePath));
    }
}
