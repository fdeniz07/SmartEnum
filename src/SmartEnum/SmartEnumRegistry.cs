using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using SmartEnum.Internal;

namespace SmartEnum;

/// <summary>
/// Validated, immutable catalog of Smart Enum instances for a closed domain type.
/// </summary>
/// <remarks>
/// Initialization is thread-safe and all-or-nothing: a Registry instance is published
/// only after empty, null, duplicate-name, and duplicate-value checks succeed.
/// <para>
/// <typeparamref name="TValue"/> should be immutable, with <c>Equals</c> and
/// <c>GetHashCode</c> kept consistent. SmartEnum does not deep-copy or freeze values.
/// </para>
/// <para>
/// Reading <see cref="Instance"/> from the domain static constructor body is only
/// defined after static field initializers have finished. Reading it while those
/// fields are still initializing is not supported and may throw
/// <see cref="SmartEnumValidationException"/> or <see cref="InvalidOperationException"/>.
/// See <c>docs/design/REGISTRY.md</c>.
/// </para>
/// </remarks>
/// <typeparam name="TEnum">Concrete Smart Enum domain type.</typeparam>
/// <typeparam name="TValue">Underlying non-nullable value type.</typeparam>
public sealed class SmartEnumRegistry<TEnum, TValue>
    where TEnum : SmartEnum<TEnum, TValue>, ISmartEnumGenerated<TEnum>
    where TValue : notnull
{
    private static readonly Lazy<SmartEnumRegistry<TEnum, TValue>> InstanceFactory =
        new(CreateValidated, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly TEnum[] _items;
    private readonly Dictionary<string, TEnum> _names;
    private readonly Dictionary<TValue, TEnum> _values;

    private SmartEnumRegistry(
        TEnum[] items,
        Dictionary<string, TEnum> names,
        Dictionary<TValue, TEnum> values)
    {
        _items = items;
        _names = names;
        _values = values;
        Items = new ReadOnlyCollection<TEnum>(items);
    }

    /// <summary>
    /// Gets the process-wide validated registry for <typeparamref name="TEnum"/>.
    /// </summary>
    /// <remarks>
    /// Internal implementation entry. Consumers must use
    /// <see cref="SmartEnum{TEnum, TValue}.Registry"/>.
    /// </remarks>
    /// <exception cref="SmartEnumValidationException">
    /// Thrown when validation fails. Failed initialization is cached by the lazy factory;
    /// a partially constructed registry is never published.
    /// </exception>
    internal static SmartEnumRegistry<TEnum, TValue> Instance => InstanceFactory.Value;

    /// <summary>
    /// Gets the validated instances in discovery order.
    /// </summary>
    public IReadOnlyList<TEnum> Items { get; }

    internal bool TryGetByName(string name, [NotNullWhen(true)] out TEnum? result)
    {
        if (_names.TryGetValue(name, out TEnum? found))
        {
            result = found;
            return true;
        }

        result = null;
        return false;
    }

    internal bool TryGetByValue(TValue value, [NotNullWhen(true)] out TEnum? result)
    {
        if (_values.TryGetValue(value, out TEnum? found))
        {
            result = found;
            return true;
        }

        result = null;
        return false;
    }

    private static SmartEnumRegistry<TEnum, TValue> CreateValidated()
    {
        ReadOnlySpan<TEnum> discovered = TEnum.GetInstances();

        if (discovered.Length == 0)
        {
            throw new SmartEnumValidationException(
                $"SmartEnum domain '{typeof(TEnum).FullName}' has no instances.");
        }

        TEnum[] buffer = new TEnum[discovered.Length];
        Dictionary<string, TEnum> names = new(discovered.Length, StringComparer.Ordinal);
        Dictionary<TValue, TEnum> values = new(discovered.Length, EqualityComparer<TValue>.Default);

        for (int i = 0; i < discovered.Length; i++)
        {
            TEnum? instance = discovered[i];
            if (instance is null)
            {
                throw new SmartEnumValidationException(
                    $"SmartEnum domain '{typeof(TEnum).FullName}' contains a null instance at index {i}.");
            }

            if (!names.TryAdd(instance.Name, instance))
            {
                throw new SmartEnumValidationException(
                    $"SmartEnum domain '{typeof(TEnum).FullName}' contains duplicate Name '{instance.Name}'.");
            }

            if (!values.TryAdd(instance.Value, instance))
            {
                throw new SmartEnumValidationException(
                    $"SmartEnum domain '{typeof(TEnum).FullName}' contains duplicate Value '{instance.Value}'.");
            }

            buffer[i] = instance;
        }

        // Publish only after full validation succeeded. The dictionaries built above are retained as-is.
        return new SmartEnumRegistry<TEnum, TValue>(buffer, names, values);
    }
}
