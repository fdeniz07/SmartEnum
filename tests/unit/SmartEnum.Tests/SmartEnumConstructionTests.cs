namespace SmartEnum.Tests;

public sealed class SmartEnumConstructionTests
{
    [Fact]
    public void Constructor_sets_name_and_value()
    {
        SampleStatus status = SampleStatus.Pending;

        Assert.Equal("Pending", status.Name);
        Assert.Equal(1, status.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_rejects_invalid_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => new SampleStatus(name!, 1));
    }

    // Domain type used only for construction validation of the Core base.
    // It does not rely on the source generator; the interface is stubbed manually.
    public sealed class SampleStatus(string name, int value) : SmartEnum<SampleStatus, int>(name, value), Internal.ISmartEnumGenerated<SampleStatus>
    {
        public static readonly SampleStatus Pending = new("Pending", 1);
        private static readonly SampleStatus[] Instances = [Pending];

        static ReadOnlySpan<SampleStatus> Internal.ISmartEnumGenerated<SampleStatus>.GetInstances()
            => Instances;
    }
}
