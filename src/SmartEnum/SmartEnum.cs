using System.Diagnostics.CodeAnalysis;
using SmartEnum.Internal;

namespace SmartEnum;

/// <summary>
/// Minimal generic base type for Smart Enum domain types.
/// </summary>
/// <remarks>
/// Instance discovery is provided by the Source Generator via
/// <see cref="ISmartEnumGenerated{TSelf}"/>. Validated enumeration is exposed through
/// <see cref="Registry"/> (ADR-003).
/// </remarks>
/// <typeparam name="TEnum">The concrete Smart Enum domain type (CRTP).</typeparam>
/// <typeparam name="TValue">The underlying value type; must be non-nullable.</typeparam>
public abstract class SmartEnum<TEnum, TValue> : IEquatable<TEnum>
    where TEnum : SmartEnum<TEnum, TValue>, ISmartEnumGenerated<TEnum>
    where TValue : notnull
{
    /// <summary>
    /// Gets the canonical name of this Smart Enum instance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the underlying value of this Smart Enum instance.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// Gets the validated registry of all discovered instances for this domain.
    /// This is the only public Registry entry point.
    /// </summary>
    /// <exception cref="SmartEnumValidationException">
    /// Thrown when registry validation fails on first access.
    /// </exception>
    public static SmartEnumRegistry<TEnum, TValue> Registry => SmartEnumRegistry<TEnum, TValue>.Instance;

    /// <summary>
    /// Gets the validated instances in discovery order.
    /// </summary>
    /// <remarks>
    /// Returns the same collection instance as <see cref="Registry"/>.<see cref="SmartEnumRegistry{TEnum, TValue}.Items"/>.
    /// </remarks>
    /// <exception cref="SmartEnumValidationException">
    /// Thrown when registry validation fails on first access.
    /// </exception>
    public static IReadOnlyList<TEnum> List => Registry.Items;

    /// <summary>
    /// Returns the instance whose name equals <paramref name="name"/> using <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="name">Registered instance name.</param>
    /// <returns>The matching instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="name"/> is not registered.</exception>
    /// <exception cref="SmartEnumValidationException">Thrown when registry validation fails.</exception>
    public static TEnum FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must be a non-empty, non-whitespace string.", nameof(name));
        }

        if (!Registry.TryGetByName(name, out TEnum? result))
        {
            throw new KeyNotFoundException($"SmartEnum name '{name}' was not found in domain '{typeof(TEnum).FullName}'.");
        }

        return result;
    }

    /// <summary>
    /// Returns the instance whose value equals <paramref name="value"/> using <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="value">Registered underlying value.</param>
    /// <returns>The matching instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null at runtime.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="value"/> is not registered.</exception>
    /// <exception cref="SmartEnumValidationException">Thrown when registry validation fails.</exception>
    public static TEnum FromValue(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!Registry.TryGetByValue(value, out TEnum? result))
        {
            throw new KeyNotFoundException($"SmartEnum value '{value}' was not found in domain '{typeof(TEnum).FullName}'.");
        }

        return result;
    }

    /// <summary>
    /// Tries to return the instance whose name equals <paramref name="name"/> using <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="name">Name to look up. Null, empty, and whitespace return <see langword="false"/>.</param>
    /// <param name="result">The matching instance when this method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching instance exists.</returns>
    /// <remarks>Registry initialization failures are not caught.</remarks>
    /// <exception cref="SmartEnumValidationException">Thrown when registry validation fails during a real lookup.</exception>
    public static bool TryFromName(string? name, [NotNullWhen(true)] out TEnum? result)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            result = null;
            return false;
        }

        return Registry.TryGetByName(name, out result);
    }

    /// <summary>
    /// Tries to return the instance whose value equals <paramref name="value"/> using <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="value">Value to look up. Null returns <see langword="false"/>.</param>
    /// <param name="result">The matching instance when this method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching instance exists.</returns>
    /// <remarks>Registry initialization failures are not caught.</remarks>
    /// <exception cref="SmartEnumValidationException">Thrown when registry validation fails during a real lookup.</exception>
    public static bool TryFromValue(TValue? value, [NotNullWhen(true)] out TEnum? result)
    {
        if (value is not TValue concrete)
        {
            result = null;
            return false;
        }

        return Registry.TryGetByValue(concrete, out result);
    }

    /// <summary>
    /// Returns whether a registered instance has the given name using <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="name">Name to look up. Null, empty, and whitespace return <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when a matching instance exists.</returns>
    /// <remarks>Null, empty, and whitespace do not throw. Registry initialization failures are not caught.</remarks>
    /// <exception cref="SmartEnumValidationException">Thrown when registry validation fails during a real lookup.</exception>
    public static bool ContainsName(string? name) => TryFromName(name, out _);

    /// <summary>
    /// Returns whether a registered instance has the given value using <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="value">Value to look up. Null returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when a matching instance exists.</returns>
    /// <remarks>Null does not throw. Registry initialization failures are not caught.</remarks>
    /// <exception cref="SmartEnumValidationException">Thrown when registry validation fails during a real lookup.</exception>
    public static bool ContainsValue(TValue? value) => TryFromValue(value, out _);

    /// <summary>
    /// Returns whether this instance and <paramref name="other"/> belong to this domain and have equal values.
    /// </summary>
    /// <param name="other">Instance to compare. Null returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when both values compare equal with <see cref="EqualityComparer{TValue}.Default"/>.</returns>
    /// <remarks>Name, runtime type, and registry membership are not part of equality.</remarks>
    public bool Equals(TEnum? other)
        => other is not null && EqualityComparer<TValue>.Default.Equals(Value, other.Value);

    /// <summary>
    /// Returns whether <paramref name="obj"/> is a <typeparamref name="TEnum"/> with an equal value.
    /// </summary>
    /// <param name="obj">Object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is a <typeparamref name="TEnum"/> with an equal value.</returns>
    /// <remarks>This override is sealed. A derived type cannot replace the domain-and-value contract used by <c>==</c>.</remarks>
    public sealed override bool Equals(object? obj)
        => obj is TEnum other && Equals(other);

    /// <summary>
    /// Returns a hash code for the domain type and value.
    /// </summary>
    /// <returns>The result of <see cref="HashCode.Combine{T1, T2}(T1, T2)"/> for <c>typeof(TEnum)</c> and <see cref="Value"/>.</returns>
    /// <remarks>This override is sealed. This hash is not a durable identifier.</remarks>
    public sealed override int GetHashCode()
        => HashCode.Combine(typeof(TEnum), Value);

    /// <summary>
    /// Returns whether two instances are equal by domain and value.
    /// </summary>
    /// <param name="left">Left operand. Null is allowed.</param>
    /// <param name="right">Right operand. Null is allowed.</param>
    /// <returns><see langword="true"/> when both are null, or when <paramref name="left"/> equals <paramref name="right"/>.</returns>
    public static bool operator ==(
        SmartEnum<TEnum, TValue>? left,
        SmartEnum<TEnum, TValue>? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Returns whether two instances are not equal by domain and value.
    /// </summary>
    /// <param name="left">Left operand. Null is allowed.</param>
    /// <param name="right">Right operand. Null is allowed.</param>
    /// <returns>The logical inverse of <see cref="operator =="/>.</returns>
    public static bool operator !=(
        SmartEnum<TEnum, TValue>? left,
        SmartEnum<TEnum, TValue>? right)
        => !(left == right);

    /// <summary>
    /// Initializes a new Smart Enum instance.
    /// </summary>
    /// <param name="name">Non-null, non-whitespace instance name.</param>
    /// <param name="value">Non-null underlying value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    protected SmartEnum(string name, TValue value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must be a non-empty, non-whitespace string.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(value);

        Name = name;
        Value = value;
    }
}
