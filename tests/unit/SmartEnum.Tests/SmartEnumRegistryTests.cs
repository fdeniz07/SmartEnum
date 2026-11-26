using System.Collections.Concurrent;

namespace SmartEnum.Tests;

public sealed class SmartEnumRegistryTests
{
    [Fact]
    public void Registry_initialization_succeeds_for_valid_domain()
    {
        SmartEnumRegistry<ValidStatus, int> registry = ValidStatus.Registry;

        Assert.Equal(2, registry.Items.Count);
        Assert.Same(ValidStatus.Pending, registry.Items[0]);
        Assert.Same(ValidStatus.Approved, registry.Items[1]);
        Assert.Same(registry, ValidStatus.Registry);
    }

    [Fact]
    public void Instance_is_not_visible_to_external_assemblies()
    {
        System.Reflection.PropertyInfo? publicInstance = typeof(SmartEnumRegistry<ValidStatus, int>).GetProperty(
            "Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        System.Reflection.PropertyInfo? internalInstance = typeof(SmartEnumRegistry<ValidStatus, int>).GetProperty(
            "Instance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.Null(publicInstance);
        Assert.NotNull(internalInstance);
        Assert.True(internalInstance.GetMethod!.IsAssembly);
    }

    [Fact]
    public void Registry_rejects_duplicate_name()
    {
        SmartEnumValidationException ex = Assert.Throws<SmartEnumValidationException>(
            static () => _ = DuplicateNameStatus.Registry);

        Assert.Contains("duplicate Name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_rejects_duplicate_value()
    {
        SmartEnumValidationException ex = Assert.Throws<SmartEnumValidationException>(
            static () => _ = DuplicateValueStatus.Registry);

        Assert.Contains("duplicate Value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_rejects_empty_instance_collection()
    {
        SmartEnumValidationException ex = Assert.Throws<SmartEnumValidationException>(
            static () => _ = EmptyStatus.Registry);

        Assert.Contains("no instances", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_rejects_null_instance_entries()
    {
        SmartEnumValidationException ex = Assert.Throws<SmartEnumValidationException>(
            static () => _ = NullEntryStatus.Registry);

        Assert.Contains("null instance", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_uses_EqualityComparer_for_custom_TValue()
    {
        SmartEnumValidationException ex = Assert.Throws<SmartEnumValidationException>(
            static () => _ = DuplicateMoneyStatus.Registry);

        Assert.Contains("duplicate Value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Failed_initialization_is_stable_across_repeated_access()
    {
        SmartEnumValidationException first = Assert.Throws<SmartEnumValidationException>(
            static () => _ = DuplicateNameStatus.Registry);
        SmartEnumValidationException second = Assert.Throws<SmartEnumValidationException>(
            static () => _ = DuplicateNameStatus.Registry);

        Assert.Same(first, second);
    }

    [Fact]
    public void Registry_initialization_is_thread_safe_for_valid_domain()
    {
        ConcurrentBag<SmartEnumRegistry<ValidStatus, int>> bags = [];

        Parallel.For(0, 64, _ => bags.Add(ValidStatus.Registry));

        SmartEnumRegistry<ValidStatus, int>[] distinct = bags.Distinct().ToArray();
        Assert.Single(distinct);
        Assert.Equal(2, distinct[0].Items.Count);
    }

    [Fact]
    public void Items_collection_cannot_be_mutated()
    {
        IReadOnlyList<ValidStatus> items = ValidStatus.Registry.Items;
        IList<ValidStatus> list = Assert.IsAssignableFrom<IList<ValidStatus>>(items);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(ValidStatus.Pending));
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    [Fact]
    public void Public_registry_surface_does_not_expose_dictionaries()
    {
        System.Reflection.PropertyInfo[] properties = typeof(SmartEnumRegistry<ValidStatus, int>).GetProperties();

        Assert.DoesNotContain(
            properties,
            static property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>));
    }

    [Fact]
    public void Distinct_domains_have_independent_registries()
    {
        Assert.NotSame(ValidStatus.Registry, OtherDomainStatus.Registry);
        Assert.Single(OtherDomainStatus.Registry.Items);
        Assert.Equal(2, ValidStatus.Registry.Items.Count);
        Assert.Same(OtherDomainStatus.Only, OtherDomainStatus.Registry.Items[0]);
    }

    public readonly record struct Money(decimal Amount);

    public sealed class ValidStatus : SmartEnum<ValidStatus, int>, Internal.ISmartEnumGenerated<ValidStatus>
    {
        public static readonly ValidStatus Pending = new("Pending", 1);
        public static readonly ValidStatus Approved = new("Approved", 2);
        private static readonly ValidStatus[] Instances = [Pending, Approved];

        private ValidStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<ValidStatus> Internal.ISmartEnumGenerated<ValidStatus>.GetInstances()
            => Instances;
    }

    public sealed class DuplicateNameStatus : SmartEnum<DuplicateNameStatus, int>, Internal.ISmartEnumGenerated<DuplicateNameStatus>
    {
        public static readonly DuplicateNameStatus A = new("Same", 1);
        public static readonly DuplicateNameStatus B = new("Same", 2);
        private static readonly DuplicateNameStatus[] Instances = [A, B];

        private DuplicateNameStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<DuplicateNameStatus> Internal.ISmartEnumGenerated<DuplicateNameStatus>.GetInstances()
            => Instances;
    }

    public sealed class DuplicateValueStatus : SmartEnum<DuplicateValueStatus, int>, Internal.ISmartEnumGenerated<DuplicateValueStatus>
    {
        public static readonly DuplicateValueStatus A = new("A", 1);
        public static readonly DuplicateValueStatus B = new("B", 1);
        private static readonly DuplicateValueStatus[] Instances = [A, B];

        private DuplicateValueStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<DuplicateValueStatus> Internal.ISmartEnumGenerated<DuplicateValueStatus>.GetInstances()
            => Instances;
    }

    public sealed class EmptyStatus : SmartEnum<EmptyStatus, int>, Internal.ISmartEnumGenerated<EmptyStatus>
    {
        private static readonly EmptyStatus[] Instances = [];

        private EmptyStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<EmptyStatus> Internal.ISmartEnumGenerated<EmptyStatus>.GetInstances()
            => Instances;
    }

    public sealed class NullEntryStatus : SmartEnum<NullEntryStatus, int>, Internal.ISmartEnumGenerated<NullEntryStatus>
    {
        public static readonly NullEntryStatus Only = new("Only", 1);
        private static readonly NullEntryStatus[] Instances = [null!];

        private NullEntryStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<NullEntryStatus> Internal.ISmartEnumGenerated<NullEntryStatus>.GetInstances()
            => Instances;
    }

    public sealed class DuplicateMoneyStatus : SmartEnum<DuplicateMoneyStatus, Money>, Internal.ISmartEnumGenerated<DuplicateMoneyStatus>
    {
        public static readonly DuplicateMoneyStatus Left = new("Left", new Money(10m));
        public static readonly DuplicateMoneyStatus Right = new("Right", new Money(10m));
        private static readonly DuplicateMoneyStatus[] Instances = [Left, Right];

        private DuplicateMoneyStatus(string name, Money value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<DuplicateMoneyStatus> Internal.ISmartEnumGenerated<DuplicateMoneyStatus>.GetInstances()
            => Instances;
    }

    public sealed class OtherDomainStatus : SmartEnum<OtherDomainStatus, int>, Internal.ISmartEnumGenerated<OtherDomainStatus>
    {
        public static readonly OtherDomainStatus Only = new("Only", 9);
        private static readonly OtherDomainStatus[] Instances = [Only];

        private OtherDomainStatus(string name, int value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<OtherDomainStatus> Internal.ISmartEnumGenerated<OtherDomainStatus>.GetInstances()
            => Instances;
    }
}
