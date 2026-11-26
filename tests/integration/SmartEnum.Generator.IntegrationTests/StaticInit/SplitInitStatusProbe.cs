using SmartEnum.Internal;

namespace SmartEnum.Generator.IntegrationTests.StaticInit;

/// <summary>
/// Captures generated instances from another type's static constructor without first
/// touching the named instance fields, to stress type-initializer ordering.
/// </summary>
internal static class SplitInitStatusProbe
{
    public static readonly SplitInitStatus?[] CapturedFromForeignStaticConstructor;

    static SplitInitStatusProbe()
    {
        ReadOnlySpan<SplitInitStatus> instances = Discover<SplitInitStatus>();
        CapturedFromForeignStaticConstructor = new SplitInitStatus?[instances.Length];
        for (int i = 0; i < instances.Length; i++)
        {
            CapturedFromForeignStaticConstructor[i] = instances[i];
        }
    }

    private static ReadOnlySpan<TEnum> Discover<TEnum>()
        where TEnum : class, ISmartEnumGenerated<TEnum>
        => TEnum.GetInstances();
}
