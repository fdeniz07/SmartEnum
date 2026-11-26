using SmartEnum.Internal;

namespace SmartEnum.Generator.IntegrationTests.StaticInit;

public sealed class StaticInitializationRegressionTests
{
    [Fact]
    public void Split_partial_files_expose_exact_static_instances_without_nulls()
    {
        ReadOnlySpan<SplitInitStatus> instances = Discover<SplitInitStatus>();

        Assert.True(SplitInitStatus.ExplicitStaticConstructorRan);
        Assert.Equal(3, instances.Length);
        // A null item binds to DoesNotContain's empty ReadOnlySpan overload once
        // the domain implements IEquatable<TEnum>, so check each entry directly.
        Assert.All(instances.ToArray(), static item => Assert.NotNull(item));
        Assert.Same(SplitInitStatus.Pending, instances[0]);
        Assert.Same(SplitInitStatus.Approved, instances[1]);
        Assert.Same(SplitInitStatus.Rejected, instances[2]);
    }

    [Fact]
    public void Foreign_static_constructor_capture_has_no_nulls_and_matches_declared_instances()
    {
        // Touching the probe type runs its static constructor, which calls GetInstances
        // before this test body references Pending/Approved/Rejected by name.
        SplitInitStatus?[] captured = SplitInitStatusProbe.CapturedFromForeignStaticConstructor;

        Assert.Equal(3, captured.Length);
        Assert.All(captured, static item => Assert.NotNull(item));
        Assert.Same(SplitInitStatus.Pending, captured[0]);
        Assert.Same(SplitInitStatus.Approved, captured[1]);
        Assert.Same(SplitInitStatus.Rejected, captured[2]);
    }

    [Fact]
    public void Explicit_static_constructor_runs_before_instance_enumeration()
    {
        _ = Discover<SplitInitStatus>();
        Assert.True(SplitInitStatus.ExplicitStaticConstructorRan);
    }

    private static ReadOnlySpan<TEnum> Discover<TEnum>()
        where TEnum : class, ISmartEnumGenerated<TEnum>
        => TEnum.GetInstances();
}
