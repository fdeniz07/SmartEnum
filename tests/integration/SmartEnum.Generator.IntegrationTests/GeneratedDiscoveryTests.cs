using SmartEnum.Internal;

namespace SmartEnum.Generator.IntegrationTests;

public sealed class GeneratedDiscoveryTests
{
    [Fact]
    public void Generated_GetInstances_returns_declared_instances_in_order()
    {
        ReadOnlySpan<OrderStatus> instances = GetInstancesViaDispatch<OrderStatus>();

        Assert.Equal(2, instances.Length);
        Assert.Same(OrderStatus.Pending, instances[0]);
        Assert.Same(OrderStatus.Approved, instances[1]);
    }

    [Fact]
    public void Static_abstract_dispatch_works_through_generic_type_parameter()
    {
        string[] names = DiscoverNames<OrderStatus>();

        Assert.Equal(["Pending", "Approved"], names);
    }

    private static ReadOnlySpan<TEnum> GetInstancesViaDispatch<TEnum>()
        where TEnum : class, ISmartEnumGenerated<TEnum>
        => TEnum.GetInstances();

    private static string[] DiscoverNames<TEnum>()
        where TEnum : SmartEnum.SmartEnum<TEnum, int>, ISmartEnumGenerated<TEnum>
    {
        ReadOnlySpan<TEnum> instances = TEnum.GetInstances();
        string[] names = new string[instances.Length];
        for (int i = 0; i < instances.Length; i++)
        {
            names[i] = instances[i].Name;
        }

        return names;
    }
}
