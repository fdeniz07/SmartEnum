using SmartEnum;

namespace SmartEnum.Generator.IntegrationTests;

public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
{
    public static readonly OrderStatus Pending = new("Pending", 1);
    public static readonly OrderStatus Approved = new("Approved", 2);

    private OrderStatus(string name, int value)
        : base(name, value)
    {
    }
}
