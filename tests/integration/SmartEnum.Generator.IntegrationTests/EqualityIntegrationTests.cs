using SmartEnum;

namespace SmartEnum.Generator.IntegrationTests;

public sealed class EqualityIntegrationTests
{
    [Fact]
    public void Generated_domain_comparisons_use_value_equality()
    {
        OrderStatus pending = OrderStatus.Pending;
        OrderStatus approved = OrderStatus.Approved;
        Assert.True(pending == OrderStatus.Pending);
        Assert.False(pending == approved);
        Assert.True(pending != approved);

        OrderStatus? none = null;
        OrderStatus? alsoNone = null;
        Assert.False(OrderStatus.Pending == none);
        Assert.False(none == OrderStatus.Pending);
        Assert.True(none == alsoNone);
        Assert.False(none != alsoNone);

        SmartEnum<OrderStatus, int> left = OrderStatus.Pending;
        SmartEnum<OrderStatus, int> right = OrderStatus.Approved;
        SmartEnum<OrderStatus, int>? missing = null;
        Assert.True(left == OrderStatus.Pending);
        Assert.False(left == right);
        Assert.True(left != right);
        Assert.False(left == missing);
        Assert.False(missing == left);

        Assert.False(OrderStatus.Pending.Equals((object)PayStatus.Card));
        Assert.False(PayStatus.Card.Equals((object)OrderStatus.Pending));
        Assert.Equal(OrderStatus.Pending.GetHashCode(), OrderStatus.Pending.GetHashCode());
    }
}
