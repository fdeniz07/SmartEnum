namespace SmartEnum.Generator.IntegrationTests;

public sealed class RegistryHardeningTests
{
    [Fact]
    public void Static_constructor_can_read_registry_after_field_initializers()
    {
        _ = ReentrantStatus.One;

        Assert.Null(ReentrantStatus.StaticCtorException);
        Assert.Equal(2, ReentrantStatus.StaticCtorItemCount);
        Assert.Equal(2, ReentrantStatus.Registry.Items.Count);
    }

    [Fact]
    public void Runtime_duplicate_name_fails_registry_and_repeats_the_same_exception()
    {
        SmartEnumValidationException first = Assert.Throws<SmartEnumValidationException>(
            static () => _ = RuntimeDuplicateStatus.Registry);
        SmartEnumValidationException second = Assert.Throws<SmartEnumValidationException>(
            static () => _ = RuntimeDuplicateStatus.Registry);

        Assert.Same(first, second);
        Assert.Contains("duplicate Name", first.Message, StringComparison.Ordinal);
        Assert.Equal(first.Message, second.Message);
    }
}
