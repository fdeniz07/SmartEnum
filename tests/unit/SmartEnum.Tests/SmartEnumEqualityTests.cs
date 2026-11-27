using System.Reflection;

namespace SmartEnum.Tests;

public sealed class SmartEnumEqualityTests
{
    [Fact]
    public void Same_value_is_equal_when_names_and_references_differ()
    {
        Status twin = Status.CreateUnregistered();

        Assert.NotSame(Status.Pending, twin);
        Assert.NotEqual(Status.Pending.Name, twin.Name);
        Assert.True(Status.Pending.Equals(twin));
        Assert.True(twin.Equals(Status.Pending));
        Assert.True(Status.Pending == twin);
        Assert.False(Status.Pending != twin);
        Assert.Equal(Status.Pending.GetHashCode(), twin.GetHashCode());
    }

    [Fact]
    public void Different_values_are_not_equal()
    {
        Assert.False(Status.Pending.Equals(Status.Approved));
        Assert.False(Status.Pending == Status.Approved);
        Assert.True(Status.Pending != Status.Approved);
    }

    [Fact]
    public void Unregistered_instance_stays_outside_the_registry()
    {
        Status twin = Status.CreateUnregistered();

        Assert.Equal(2, Status.List.Count);
        Assert.DoesNotContain(Status.List, static item => item.Name == "Twin");
        Assert.Same(Status.Pending, Status.FromValue(1));
        Assert.True(Status.Pending == twin);
    }

    [Fact]
    public void Object_equality_members_are_sealed()
    {
        MethodInfo equals = typeof(Status).GetMethod(nameof(object.Equals), [typeof(object)])!;
        MethodInfo hashCode = typeof(Status).GetMethod(nameof(object.GetHashCode), Type.EmptyTypes)!;

        Assert.True(equals.IsFinal);
        Assert.True(hashCode.IsFinal);
        Assert.Equal(typeof(SmartEnum<Status, int>), equals.DeclaringType);
        Assert.Equal(typeof(SmartEnum<Status, int>), hashCode.DeclaringType);
    }

    [Fact]
    public void Derived_runtime_type_with_the_same_value_is_equal()
    {
        DerivedStatus derived = new();

        Assert.True(Status.Pending.Equals(derived));
        Assert.True(derived.Equals(Status.Pending));
        Assert.True(Status.Pending == derived);
        Assert.Equal(Status.Pending.GetHashCode(), derived.GetHashCode());
        Assert.IsNotType<DerivedStatus>(Status.Pending);
    }

    [Fact]
    public void Null_comparisons_follow_the_operator_contract()
    {
        Status? none = null;
        Status? alsoNone = null;

        Assert.False(Status.Pending == none);
        Assert.False(none == Status.Pending);
        Assert.True(Status.Pending != none);
        Assert.True(none != Status.Pending);
        Assert.True(none == alsoNone);
        Assert.False(none != alsoNone);
        Assert.False(Status.Pending.Equals(null));
    }

    [Fact]
    public void HashSet_and_dictionary_use_value_equality()
    {
        HashSet<Status> set = [Status.Pending];
        Assert.Contains(Status.CreateUnregistered(), set);
        Assert.Single(set);

        Dictionary<Status, string> map = new()
        {
            [Status.Pending] = "pending",
        };

        Assert.Equal("pending", map[Status.CreateUnregistered()]);
    }

    [Fact]
    public void Custom_value_uses_equality_comparer()
    {
        Assert.True(PriceStatus.Ten == PriceStatus.CreateUnregisteredTen());
        Assert.False(PriceStatus.Ten == PriceStatus.Twenty);
        Assert.Equal(PriceStatus.Ten.GetHashCode(), PriceStatus.CreateUnregisteredTen().GetHashCode());
    }

    [Fact]
    public void Different_domain_is_not_equal_as_object()
    {
        Assert.False(Status.Pending.Equals((object)OtherStatus.Only));
        Assert.False(OtherStatus.Only.Equals((object)Status.Pending));
    }

    public class Status : SmartEnum<Status, int>, Internal.ISmartEnumGenerated<Status>
    {
        public static readonly Status Pending = new("Pending", 1);
        public static readonly Status Approved = new("Approved", 2);
        private static readonly Status[] Instances = [Pending, Approved];

        protected Status(string name, int value)
            : base(name, value)
        {
        }

        public static Status CreateUnregistered() => new("Twin", 1);

        static ReadOnlySpan<Status> Internal.ISmartEnumGenerated<Status>.GetInstances()
            => Instances;
    }

    public sealed class DerivedStatus : Status
    {
        public DerivedStatus()
            : base("Derived", 1)
        {
        }
    }

    public readonly record struct Money(decimal Amount);

    public sealed class PriceStatus : SmartEnum<PriceStatus, Money>, Internal.ISmartEnumGenerated<PriceStatus>
    {
        public static readonly PriceStatus Ten = new("Ten", new Money(10m));
        public static readonly PriceStatus Twenty = new("Twenty", new Money(20m));
        private static readonly PriceStatus[] Instances = [Ten, Twenty];

        private PriceStatus(string name, Money value)
            : base(name, value)
        {
        }

        public static PriceStatus CreateUnregisteredTen() => new("Alt", new Money(10m));

        static ReadOnlySpan<PriceStatus> Internal.ISmartEnumGenerated<PriceStatus>.GetInstances()
            => Instances;
    }

    public sealed class OtherStatus : SmartEnum<OtherStatus, int>, Internal.ISmartEnumGenerated<OtherStatus>
    {
        public static readonly OtherStatus Only = new("Only", 1);
        private static readonly OtherStatus[] Instances = [Only];

        private OtherStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<OtherStatus> Internal.ISmartEnumGenerated<OtherStatus>.GetInstances()
            => Instances;
    }
}
