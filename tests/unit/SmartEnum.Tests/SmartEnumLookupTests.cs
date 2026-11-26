using System.Reflection;

namespace SmartEnum.Tests;

public sealed class SmartEnumLookupTests
{
    [Fact]
    public void FromName_returns_ordinal_match()
    {
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Pending, SmartEnumRegistryTests.ValidStatus.FromName("Pending"));
        Assert.True(SmartEnumRegistryTests.ValidStatus.TryFromName("Pending", out SmartEnumRegistryTests.ValidStatus? found));
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Pending, found);
        Assert.True(SmartEnumRegistryTests.ValidStatus.ContainsName("Pending"));
    }

    [Fact]
    public void Name_lookup_is_case_sensitive()
    {
        Assert.Throws<KeyNotFoundException>(() => SmartEnumRegistryTests.ValidStatus.FromName("pending"));
        Assert.Throws<KeyNotFoundException>(() => SmartEnumRegistryTests.ValidStatus.FromName("PENDING"));
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName("pending", out SmartEnumRegistryTests.ValidStatus? miss));
        Assert.Null(miss);
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsName("PENDING"));
    }

    [Fact]
    public void Name_lookup_does_not_trim()
    {
        Assert.Throws<KeyNotFoundException>(() => SmartEnumRegistryTests.ValidStatus.FromName(" pending "));
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName(" pending ", out SmartEnumRegistryTests.ValidStatus? miss));
        Assert.Null(miss);
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsName(" pending "));
    }

    [Fact]
    public void FromName_rejects_null_empty_and_whitespace()
    {
        Assert.Throws<ArgumentNullException>(() => SmartEnumRegistryTests.ValidStatus.FromName(null!));

        ArgumentException empty = Assert.Throws<ArgumentException>(() => SmartEnumRegistryTests.ValidStatus.FromName(""));
        Assert.IsType<ArgumentException>(empty);
        Assert.Equal("name", empty.ParamName);

        ArgumentException whitespace = Assert.Throws<ArgumentException>(() => SmartEnumRegistryTests.ValidStatus.FromName("   "));
        Assert.IsType<ArgumentException>(whitespace);
        Assert.Equal("name", whitespace.ParamName);
    }

    [Fact]
    public void Try_and_contains_name_reject_null_empty_and_whitespace_without_throwing()
    {
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName(null, out SmartEnumRegistryTests.ValidStatus? nullResult));
        Assert.Null(nullResult);
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName("", out SmartEnumRegistryTests.ValidStatus? emptyResult));
        Assert.Null(emptyResult);
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName("   ", out SmartEnumRegistryTests.ValidStatus? whitespaceResult));
        Assert.Null(whitespaceResult);
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsName(null));
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsName(""));
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsName("   "));
    }

    [Fact]
    public void Missing_name_is_not_an_argument_error()
    {
        Assert.Throws<KeyNotFoundException>(() => SmartEnumRegistryTests.ValidStatus.FromName("Missing"));
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName("Missing", out SmartEnumRegistryTests.ValidStatus? miss));
        Assert.Null(miss);
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsName("Missing"));
    }

    [Fact]
    public void Integer_value_lookup_uses_equality()
    {
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Pending, SmartEnumRegistryTests.ValidStatus.FromValue(1));
        Assert.True(SmartEnumRegistryTests.ValidStatus.TryFromValue(1, out SmartEnumRegistryTests.ValidStatus? found));
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Pending, found);
        Assert.True(SmartEnumRegistryTests.ValidStatus.ContainsValue(1));

        Assert.Throws<KeyNotFoundException>(() => SmartEnumRegistryTests.ValidStatus.FromValue(0));
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromValue(0, out SmartEnumRegistryTests.ValidStatus? miss));
        Assert.Null(miss);
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsValue(0));
    }

    [Fact]
    public void String_value_null_follows_the_reference_type_contract()
    {
        Assert.Throws<ArgumentNullException>(() => PaymentMethod.FromValue(null!));
        Assert.False(PaymentMethod.TryFromValue(null, out PaymentMethod? result));
        Assert.Null(result);
        Assert.False(PaymentMethod.ContainsValue(null));
    }

    [Fact]
    public void String_value_lookup_uses_equality()
    {
        Assert.Same(PaymentMethod.Card, PaymentMethod.FromValue("card"));
        Assert.False(PaymentMethod.TryFromValue("missing", out PaymentMethod? miss));
        Assert.Null(miss);
        Assert.True(PaymentMethod.ContainsValue("cash"));
        Assert.False(PaymentMethod.ContainsValue("missing"));
    }

    [Fact]
    public void Record_struct_value_matches_by_equality()
    {
        Assert.Same(UniqueMoneyStatus.Ten, UniqueMoneyStatus.FromValue(new Money(10m)));
        Assert.False(UniqueMoneyStatus.ContainsValue(new Money(30m)));
    }

    [Fact]
    public void Value_lookup_distinguishes_hash_collisions()
    {
        Assert.Same(CollidingStatus.Left, CollidingStatus.FromValue(new CollidingValue(1)));
        Assert.Same(CollidingStatus.Right, CollidingStatus.FromValue(new CollidingValue(2)));
        Assert.False(CollidingStatus.TryFromValue(new CollidingValue(3), out CollidingStatus? miss));
        Assert.Null(miss);
    }

    [Fact]
    public void List_is_the_registry_collection()
    {
        IReadOnlyList<SmartEnumRegistryTests.ValidStatus> list = SmartEnumRegistryTests.ValidStatus.List;

        Assert.Same(SmartEnumRegistryTests.ValidStatus.Registry.Items, list);
        Assert.Same(list, SmartEnumRegistryTests.ValidStatus.List);
        Assert.Equal(2, list.Count);
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Pending, list[0]);
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Approved, list[1]);

        IList<SmartEnumRegistryTests.ValidStatus> mutable = Assert.IsAssignableFrom<IList<SmartEnumRegistryTests.ValidStatus>>(list);
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutable.Add(SmartEnumRegistryTests.ValidStatus.Pending));
    }

    [Fact]
    public void Domains_do_not_share_lookup_indexes()
    {
        Assert.Throws<KeyNotFoundException>(() => SmartEnumRegistryTests.ValidStatus.FromName("Only"));
        Assert.False(SmartEnumRegistryTests.ValidStatus.TryFromName("Only", out _));
        Assert.Same(SmartEnumRegistryTests.OtherDomainStatus.Only, SmartEnumRegistryTests.OtherDomainStatus.FromName("Only"));
        Assert.Same(SmartEnumRegistryTests.OtherDomainStatus.Only, SmartEnumRegistryTests.OtherDomainStatus.FromValue(9));
        Assert.False(SmartEnumRegistryTests.ValidStatus.ContainsValue(9));
    }

    [Fact]
    public void Failed_initialization_propagates_from_lookup_and_stays_cached()
    {
        Assert.False(SmartEnumRegistryTests.DuplicateNameStatus.TryFromName(null, out SmartEnumRegistryTests.DuplicateNameStatus? ignored));
        Assert.Null(ignored);
        Assert.False(SmartEnumRegistryTests.DuplicateNameStatus.ContainsName(""));
        Assert.False(SmartEnumRegistryTests.DuplicateNameStatus.ContainsName("   "));

        SmartEnumValidationException first = Assert.Throws<SmartEnumValidationException>(
            () => SmartEnumRegistryTests.DuplicateNameStatus.TryFromName("Same", out _));
        SmartEnumValidationException second = Assert.Throws<SmartEnumValidationException>(
            () => SmartEnumRegistryTests.DuplicateNameStatus.ContainsName("Same"));
        SmartEnumValidationException third = Assert.Throws<SmartEnumValidationException>(
            () => SmartEnumRegistryTests.DuplicateNameStatus.FromName("Same"));

        Assert.Same(first, second);
        Assert.Same(first, third);
    }

    [Fact]
    public void Private_name_dictionary_is_reused_across_lookups()
    {
        SmartEnumRegistry<SmartEnumRegistryTests.ValidStatus, int> registry = SmartEnumRegistryTests.ValidStatus.Registry;
        FieldInfo nameField = Assert.Single(
            typeof(SmartEnumRegistry<SmartEnumRegistryTests.ValidStatus, int>).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            static field => field.FieldType == typeof(Dictionary<string, SmartEnumRegistryTests.ValidStatus>));

        Assert.False(nameField.IsPublic);
        Dictionary<string, SmartEnumRegistryTests.ValidStatus> first = Assert.IsType<Dictionary<string, SmartEnumRegistryTests.ValidStatus>>(nameField.GetValue(registry));

        _ = SmartEnumRegistryTests.ValidStatus.FromName("Pending");
        _ = SmartEnumRegistryTests.ValidStatus.FromName("Approved");

        Dictionary<string, SmartEnumRegistryTests.ValidStatus> second = Assert.IsType<Dictionary<string, SmartEnumRegistryTests.ValidStatus>>(nameField.GetValue(registry));
        Assert.Same(first, second);
        Assert.Equal(2, first.Count);
        Assert.Same(SmartEnumRegistryTests.ValidStatus.Pending, first["Pending"]);
    }

    public readonly record struct Money(decimal Amount);

    public readonly struct CollidingValue : IEquatable<CollidingValue>
    {
        public CollidingValue(int amount) => Amount = amount;

        public int Amount { get; }

        public bool Equals(CollidingValue other) => Amount == other.Amount;

        public override bool Equals(object? obj) => obj is CollidingValue other && Equals(other);

        public override int GetHashCode() => 1;
    }

    public sealed class PaymentMethod : SmartEnum<PaymentMethod, string>, Internal.ISmartEnumGenerated<PaymentMethod>
    {
        public static readonly PaymentMethod Card = new("Card", "card");
        public static readonly PaymentMethod Cash = new("Cash", "cash");
        private static readonly PaymentMethod[] Instances = [Card, Cash];

        private PaymentMethod(string name, string value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<PaymentMethod> Internal.ISmartEnumGenerated<PaymentMethod>.GetInstances()
            => Instances;
    }

    public sealed class UniqueMoneyStatus : SmartEnum<UniqueMoneyStatus, Money>, Internal.ISmartEnumGenerated<UniqueMoneyStatus>
    {
        public static readonly UniqueMoneyStatus Ten = new("Ten", new Money(10m));
        public static readonly UniqueMoneyStatus Twenty = new("Twenty", new Money(20m));
        private static readonly UniqueMoneyStatus[] Instances = [Ten, Twenty];

        private UniqueMoneyStatus(string name, Money value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<UniqueMoneyStatus> Internal.ISmartEnumGenerated<UniqueMoneyStatus>.GetInstances()
            => Instances;
    }

    public sealed class CollidingStatus : SmartEnum<CollidingStatus, CollidingValue>, Internal.ISmartEnumGenerated<CollidingStatus>
    {
        public static readonly CollidingStatus Left = new("Left", new CollidingValue(1));
        public static readonly CollidingStatus Right = new("Right", new CollidingValue(2));
        private static readonly CollidingStatus[] Instances = [Left, Right];

        private CollidingStatus(string name, CollidingValue value)
            : base(name, value)
        {
        }

        static ReadOnlySpan<CollidingStatus> Internal.ISmartEnumGenerated<CollidingStatus>.GetInstances()
            => Instances;
    }
}
