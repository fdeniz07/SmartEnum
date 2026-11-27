using SmartEnum;

namespace SmartEnum.Generator.IntegrationTests;

public sealed partial class PayStatus : SmartEnum<PayStatus, int>
{
    public static readonly PayStatus Card = new("Card", 1);

    private PayStatus(string name, int value)
        : base(name, value)
    {
    }
}
