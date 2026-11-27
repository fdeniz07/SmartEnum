# SmartEnum Registry — Behavior Notes (Milestone 1.3)

These notes document observed behavior. They do not change ADR-002 or ADR-003 decisions.

Turkish counterpart: [REGISTRY.tr.md](REGISTRY.tr.md).

## Official entry point

`SmartEnum<TEnum, TValue>.Registry` is the only public Registry entry point.

`SmartEnumRegistry<TEnum, TValue>` stays **public** because `Registry` returns it. A public property cannot return a less accessible type (CS0050).

`SmartEnumRegistry<TEnum, TValue>.Instance` is **internal**. It is the lazy factory used by `Registry`. External assemblies cannot call it.

## TValue contract

- `TValue` should preferably be immutable.
- `Equals` and `GetHashCode` must be consistent with each other.
- Equality results must remain stable after Registry initialization. The Registry does not snapshot a private copy of equality behavior beyond the values observed during validation.
- A mutable reference-type `TValue` can change after publication and would invalidate any future dictionary lookup (`FromValue`) even if validation succeeded.
- SmartEnum does **not** deep-copy or freeze `TValue`.

Runtime duplicate detection uses `EqualityComparer<TValue>.Default`. Compile-time duplicate detection uses Roslyn constant values only.

## Lookup indexes

Name and value dictionaries are created inside `CreateValidated` with `StringComparer.Ordinal` and `EqualityComparer<TValue>.Default`. When validation succeeds, those same dictionary instances are stored on the registry. Lookup does not rebuild or copy them, and they are not part of the public surface.

`SmartEnum<TEnum, TValue>.List` returns `Registry.Items`. It is that same read-only collection.

`FromName`, `FromValue`, `TryFromName`, `TryFromValue`, `ContainsName`, and `ContainsValue` read those indexes. They do not catch registry initialization failures. Null, empty, and whitespace names, and null values, are classified before the indexes are read. The accepted contract is ADR-005.

`List` and the lookup methods follow the same initialization rule as `Registry`, because a real lookup reads the registry. Calling them while the domain's own static fields are still initializing is not supported.

## Recursive Registry access

### Static constructor body

C# runs static field initializers before the static constructor body. Reading `Registry` from the static constructor body, after those initializers, is the scenario covered by integration tests. When field initialization has completed, Registry validation can succeed. This is **not** a general promise that every recursive access is supported.

### During field initialization

Reading `Registry` while static fields of the same domain are still initializing can observe an incomplete instance array (null entries or a not-yet-assigned backing array). Validation then fails with `SmartEnumValidationException`, or the runtime can throw `InvalidOperationException` if the `Lazy<T>` factory re-enters `Value` on the same thread.

That recursive case is **not supported**. Callers must not read `Registry` from instance constructors or field initializers that run as part of the domain’s own static initialization.

Failed `Lazy<T>` initialization is cached. A later access throws the same exception instance. A partially constructed `SmartEnumRegistry` is not published.

## Constructor argument binding (generator)

Uniqueness diagnostics bind object-creation arguments through the compiler's `IObjectCreationOperation`. Each `IArgumentOperation.Parameter` identifies the bound parameter, including named arguments in any order and a positional argument that follows a named argument. Constants are read from the argument operation, including an omitted optional parameter's explicit compile-time default. Parameters are recognized by the names `name` and `value`. The selected constructor does not have to be public. `tests/integration/SmartEnum.Generator.IntegrationTests/OrderStatus.cs` uses a private constructor with those parameter names.
