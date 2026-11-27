namespace SmartEnum.Package.IntegrationTests;

[CollectionDefinition(nameof(PackageConsumerCollection), DisableParallelization = true)]
public sealed class PackageConsumerCollection : ICollectionFixture<PackageConsumerFixture>;
