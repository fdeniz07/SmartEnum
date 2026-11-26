namespace SmartEnum.Generator.IntegrationTests.StaticInit;

/// <summary>
/// Instance fields are intentionally declared in a different partial file than the base list.
/// </summary>
public sealed partial class SplitInitStatus
{
    public static readonly SplitInitStatus Pending = new("Pending", 1);
    public static readonly SplitInitStatus Approved = new("Approved", 2);
    public static readonly SplitInitStatus Rejected = new("Rejected", 3);
}
