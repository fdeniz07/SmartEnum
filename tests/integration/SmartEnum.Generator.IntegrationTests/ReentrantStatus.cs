using SmartEnum;

namespace SmartEnum.Generator.IntegrationTests;

/// <summary>
/// Static constructor reads Registry after field initializers.
/// Observed behavior is recorded by the type itself so tests can document it.
/// </summary>
public sealed partial class ReentrantStatus : SmartEnum<ReentrantStatus, int>
{
    public static readonly ReentrantStatus One = new(name: "One", value: 1);
    public static readonly ReentrantStatus Two = new(value: 2, name: "Two");

    public static Exception? StaticCtorException { get; private set; }
    public static int StaticCtorItemCount { get; private set; }

    static ReentrantStatus()
    {
        try
        {
            StaticCtorItemCount = Registry.Items.Count;
        }
        catch (Exception ex)
        {
            StaticCtorException = ex;
        }
    }

    private ReentrantStatus(string name, int value)
        : base(name, value)
    {
    }
}
