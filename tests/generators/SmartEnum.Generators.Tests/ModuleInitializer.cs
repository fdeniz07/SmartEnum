using System.Runtime.CompilerServices;

namespace SmartEnum.Generators.Tests;

internal static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Verify.XunitV3 works without additional source-generator plugins when
        // asserting raw generated source strings.
        VerifierSettings.DisableRequireUniquePrefix();
    }
}
