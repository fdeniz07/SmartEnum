namespace SmartEnum.Generator.IntegrationTests;

public sealed class RegistryIntegrationTests
{
    [Fact]
    public void Generated_domain_registry_exposes_validated_instances()
    {
        SmartEnumRegistry<OrderStatus, int> registry = OrderStatus.Registry;

        Assert.Equal(2, registry.Items.Count);
        Assert.Same(OrderStatus.Pending, registry.Items[0]);
        Assert.Same(OrderStatus.Approved, registry.Items[1]);
    }

    [Fact]
    public void Generated_domain_lookup_uses_registry_order_and_ordinal_names()
    {
        Assert.Same(OrderStatus.Registry.Items, OrderStatus.List);
        Assert.Same(OrderStatus.Pending, OrderStatus.List[0]);
        Assert.Same(OrderStatus.Approved, OrderStatus.List[1]);
        Assert.Same(OrderStatus.Pending, OrderStatus.FromName("Pending"));
        Assert.Same(OrderStatus.Approved, OrderStatus.FromValue(2));
        Assert.False(OrderStatus.TryFromName("pending", out OrderStatus? miss));
        Assert.Null(miss);
        Assert.False(OrderStatus.ContainsName("PENDING"));
    }
}
