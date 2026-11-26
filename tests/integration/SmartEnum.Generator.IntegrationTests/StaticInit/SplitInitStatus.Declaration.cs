using SmartEnum;

namespace SmartEnum.Generator.IntegrationTests.StaticInit;

/// <summary>
/// Inheritance and construction live in this partial; instances live elsewhere.
/// </summary>
public sealed partial class SplitInitStatus : SmartEnum<SplitInitStatus, int>
{
    static SplitInitStatus()
    {
        // Explicit static constructor — forces type initializer semantics under test.
        ExplicitStaticConstructorRan = true;
    }

    private SplitInitStatus(string name, int value)
        : base(name, value)
    {
    }

    public static bool ExplicitStaticConstructorRan { get; private set; }
}
