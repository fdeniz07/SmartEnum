# SmartEnum — Usage

This page shows the implemented Core API and the Source Generator contract. It matches `tests/integration/SmartEnum.Generator.IntegrationTests/OrderStatus.cs` and `PayStatus.cs`.

There is no published NuGet package yet. Build against the projects in this repository.

## Build requirements

`global.json` selects SDK `10.0.404` (`rollForward`: `latestFeature`, `allowPrerelease`: `false`). The repo language version is C# 14.

A consumer project needs two references:

- `src/SmartEnum/SmartEnum.csproj`, the Core assembly. It has no runtime package dependencies.
- `src/SmartEnum.Generators/SmartEnum.Generators.csproj` as an analyzer, with `ReferenceOutputAssembly="false"`.

Core does not reference the generator. The generator does not reference Core. Discovery is emitted into the consumer compilation.

From the repository root:

```powershell
dotnet test SmartEnum.slnx -p:TreatWarningsAsErrors=true
```

## Domain declaration

The domain type is `partial`. The constructor that the static fields call takes `(string name, int value)` and passes both to the base. The generator reads parameters named `name` and `value`. That constructor may be private, as it is here.

```csharp
using SmartEnum;

namespace Demo;

public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
{
    public static readonly OrderStatus Pending = new("Pending", 1);
    public static readonly OrderStatus Approved = new("Approved", 2);

    private OrderStatus(string name, int value)
        : base(name, value)
    {
    }
}
```

`SmartEnum<OrderStatus, int>` requires `OrderStatus` to implement `ISmartEnumGenerated<OrderStatus>`. The generator adds that implementation and a static array of `Pending` and `Approved`. Do not write `GetInstances()` by hand.

`TValue` is `notnull`. `Name` and `Value` are get-only.

## Registry and list

```csharp
IReadOnlyList<OrderStatus> list = OrderStatus.List;
SmartEnumRegistry<OrderStatus, int> registry = OrderStatus.Registry;

// List and Registry.Items are the same collection.
bool sameCatalog = ReferenceEquals(list, registry.Items);
```

The first successful read validates the discovered instances and publishes one registry for the domain. A failed validation throws `SmartEnumValidationException` and later reads throw that same exception instance. An empty set, a null entry, a duplicate name, or a duplicate value fails validation.

## Lookup

```csharp
OrderStatus pending = OrderStatus.FromName("Pending");
OrderStatus alsoPending = OrderStatus.FromValue(1);

bool found = OrderStatus.TryFromName("Pending", out OrderStatus? match);
bool missing = OrderStatus.TryFromName("Missing", out OrderStatus? none);
bool hasApproved = OrderStatus.ContainsName("Approved");
bool hasValue = OrderStatus.ContainsValue(2);
```

Name comparison is ordinal. `"pending"` and `"PENDING"` do not match `"Pending"`. Leading or trailing spaces are not trimmed.

`FromName(null)` throws `ArgumentNullException`. `FromName("")` and a whitespace name throw `ArgumentException`. A non-empty name that is not registered throws `KeyNotFoundException`.

`TryFromName` returns `false` for null, empty, whitespace, and a missing name. `ContainsName` uses the same rule. Neither method throws for those inputs.

`FromValue` throws `ArgumentNullException` when the runtime value is null, and `KeyNotFoundException` when the value is not registered. `TryFromValue(null)` and `ContainsValue(null)` return `false`. On `OrderStatus` the value type is `int`, so the null value calls belong on a reference-type value such as `string`.

These methods do not catch `SmartEnumValidationException`. A real lookup still surfaces a failed registry.

## Equality

Equality is the domain type plus `Value`, compared with `EqualityComparer<TValue>.Default`. `Name` is not part of equality. A runtime subtype with the same value is equal. An instance that was never placed in the registry is equal to the registered instance with the same value.

```csharp
OrderStatus pending = OrderStatus.Pending;
OrderStatus approved = OrderStatus.Approved;

bool same = pending == OrderStatus.Pending;
bool different = pending == approved;
bool notSame = pending != approved;

OrderStatus? none = null;
OrderStatus? alsoNone = null;
bool pendingIsNull = pending == none;
bool bothNull = none == alsoNone;
```

`==` and `!=` are declared on `SmartEnum<TEnum, TValue>`. Both operands may be null. `none == alsoNone` is `true`. `pending == none` is `false`. `!=` is the opposite of `==`.

`GetHashCode()` combines `typeof(OrderStatus)` and `Value`. Equal instances have equal hash codes, so `HashSet<OrderStatus>` and `Dictionary<OrderStatus, T>` treat the same value as one key.

A second domain with the same numeric value is not the same type:

```csharp
public sealed partial class PayStatus : SmartEnum<PayStatus, int>
{
    public static readonly PayStatus Card = new("Card", 1);

    private PayStatus(string name, int value)
        : base(name, value)
    {
    }
}
```

```csharp
bool sameObject = OrderStatus.Pending.Equals((object)PayStatus.Card);
```

`sameObject` is `false`. `OrderStatus.Pending == PayStatus.Card` does not compile (CS0019). That expression is intentionally absent from the samples above.

## Where this is tested

- `tests/integration/SmartEnum.Generator.IntegrationTests/OrderStatus.cs`
- `tests/integration/SmartEnum.Generator.IntegrationTests/PayStatus.cs`
- `tests/integration/SmartEnum.Generator.IntegrationTests/EqualityIntegrationTests.cs`
- `tests/unit/SmartEnum.Tests/SmartEnumLookupTests.cs`
- `tests/unit/SmartEnum.Tests/SmartEnumEqualityTests.cs`
