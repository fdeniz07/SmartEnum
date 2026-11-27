namespace SmartEnum.Package.IntegrationTests;

[Collection(nameof(PackageConsumerCollection))]
public sealed class RuntimeProbeTests
{
    private readonly PackageConsumerFixture _fixture;

    public RuntimeProbeTests(PackageConsumerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Console_probe_exercises_list_lookup_and_contains_apis()
    {
        Assert.True(
            _fixture.ProbeExitCode == 0,
            "Console probe failed."
            + Environment.NewLine
            + $"ExitCode={_fixture.ProbeExitCode}"
            + Environment.NewLine
            + "STDOUT:"
            + Environment.NewLine
            + _fixture.ProbeStdout
            + Environment.NewLine
            + "STDERR:"
            + Environment.NewLine
            + _fixture.ProbeStderr);

        Assert.Contains("PROBE_OK", _fixture.ProbeStdout, StringComparison.Ordinal);
        Assert.DoesNotContain("PROBE_FAIL", _fixture.ProbeStderr, StringComparison.Ordinal);
    }
}
