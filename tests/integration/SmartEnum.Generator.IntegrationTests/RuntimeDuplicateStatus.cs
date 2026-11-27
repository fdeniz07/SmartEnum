using SmartEnum;

namespace SmartEnum.Generator.IntegrationTests;

/// <summary>
/// Names are not constant, so the generator must not emit SMARTENUM003.
/// Registry validation must still reject the duplicate at runtime.
/// </summary>
public sealed partial class RuntimeDuplicateStatus : SmartEnum<RuntimeDuplicateStatus, int>
{
    public static readonly RuntimeDuplicateStatus Left = new(SameName(), 1);
    public static readonly RuntimeDuplicateStatus Right = new(SameName(), 2);

    private RuntimeDuplicateStatus(string name, int value)
        : base(name, value)
    {
    }

    private static string SameName() => "Same";
}
