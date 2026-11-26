using System.ComponentModel;

namespace SmartEnum.Internal;

/// <summary>
/// Technical contract implemented by the SmartEnum source generator for compile-time
/// instance discovery. Consumers should not implement this interface manually.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EditorBrowsableAttribute"/> with <see cref="EditorBrowsableState.Never"/>
/// only hides the type from most IDE IntelliSense lists. It does <strong>not</strong>
/// make the interface private or non-public. The interface remains part of the public API
/// surface of the Core assembly and can be referenced from any compilation that
/// references SmartEnum.
/// </para>
/// <para>
/// This public visibility is intentional for the prototype: static abstract interface
/// dispatch requires a visible contract that generic Core APIs can constrain against.
/// A future design may relocate or rename this surface, but accessibility cannot be
/// internal while remaining usable across assemblies.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The concrete Smart Enum domain type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISmartEnumGenerated<TSelf>
    where TSelf : ISmartEnumGenerated<TSelf>
{
    /// <summary>
    /// Returns the compile-time discovered public static readonly instances of
    /// <typeparamref name="TSelf"/> in source declaration order.
    /// </summary>
    static abstract ReadOnlySpan<TSelf> GetInstances();
}
