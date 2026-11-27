# ADR-005 — SmartEnum Lookup and Equality API

| Field | Value |
| --- | --- |
| Status | **Accepted** |
| Date | 2026-11-26 |
| Amendment | 2026-11-26 — operator parameters corrected for CS0563 |
| Scope | SmartEnum Core |
| Related | ADR-002, ADR-003, ADR-004 |
| Turkish | [ADR-005-lookup-and-equality.tr.md](ADR-005-lookup-and-equality.tr.md) |

This document defines the accepted Lookup and Equality contract. The Lookup and Equality API has been implemented. This ADR now documents the accepted contract of the implemented API.

[ADR-004](ADR-004-registry-public-access.md) exists and is **Accepted**. It documents the public Registry access decision: `SmartEnum<TEnum, TValue>.Registry` is the only public entry point, and `SmartEnumRegistry<TEnum, TValue>.Instance` is internal.

## 1. Context and problem

SmartEnum already supports instance discovery through the mandatory Source Generator and runtime Registry validation.

The Registry guarantees:

- At least one valid instance.
- No null instances.
- Unique `Name` values.
- Unique `Value` values.
- An immutable public instance collection.
- Thread-safe, one-time initialization.

Callers still cannot look up an instance by name or value, and SmartEnum instances do not yet follow a standard domain equality contract.

## 2. Decision

SmartEnum will provide:

- `List`
- `FromName`
- `FromValue`
- `TryFromName`
- `TryFromValue`
- `ContainsName`
- `ContainsValue`

And:

- `IEquatable<TEnum>`
- `Equals(TEnum?)`
- `Equals(object?)`
- `GetHashCode()`
- `operator ==`
- `operator !=`

The mandatory Source Generator and Registry architecture will not change.

## 3. Lookup API

### 3.1 List

All registered instances are exposed as a read-only collection.

```csharp
public static IReadOnlyList<TEnum> List { get; }
```

The collection:

- Preserves the Registry's deterministic order.
- Must not be mutable by callers.
- Must reuse the existing Registry collection.
- Must not allocate a new collection on each access.

### 3.2 FromName

```csharp
public static TEnum FromName(string name);
```

| Condition | Result |
| --- | --- |
| Name exists | Matching instance |
| Name not found | `KeyNotFoundException` |
| Name is null | `ArgumentNullException` |
| Name is empty or whitespace | `ArgumentException` |

Comparison uses `StringComparer.Ordinal` (case-sensitive).

### 3.3 FromValue

```csharp
public static TEnum FromValue(TValue value);
```

| Condition | Result |
| --- | --- |
| Value exists | Matching instance |
| Value not found | `KeyNotFoundException` |
| Value is null at runtime | `ArgumentNullException` |

Comparison uses `EqualityComparer<TValue>.Default`.

### 3.4 TryFromName

```csharp
public static bool TryFromName(string? name, out TEnum? result);
```

- Found: `true` and the matching instance.
- Not found: `false` and `null`.
- Null, empty, or whitespace input: `false` and `null`.
- Ordinary lookup misses do not throw.

Registry initialization or validation failures must not be swallowed by Try methods.

### 3.5 TryFromValue

```csharp
public static bool TryFromValue(TValue? value, out TEnum? result);
```

- Found: `true` and the matching instance.
- Not found: `false` and `null`.
- Null input: `false` and `null`.
- Ordinary lookup misses do not throw.

## 4. Additional lookup APIs

```csharp
public static bool ContainsName(string? name);
public static bool ContainsValue(TValue? value);
```

`ContainsName`:

- `ContainsName(null)` returns `false`.
- `ContainsName("")` returns `false`.
- `ContainsName("   ")` returns `false`.
- These inputs do not throw.
- A matching instance returns `true`.
- A name that is not registered returns `false`.
- Ordinary lookup misses do not throw.

`ContainsValue`:

- A matching instance returns `true`.
- A value that is not registered returns `false`.
- A null input returns `false`.
- Ordinary lookup misses do not throw.

`FromName` still throws `ArgumentNullException` for null and `ArgumentException` for empty or whitespace. `TryFromName` still returns `false` and `null` for null, empty, or whitespace. Those rules are unchanged.

`Find` methods are deferred. `TryFromName` and `TryFromValue` already provide non-throwing lookup.

This version will not add case-insensitive overloads, predicate search, default-value overloads, or LINQ extensions.

## 5. Name comparison

Name comparison stays:

```csharp
StringComparer.Ordinal
```

- `Pending` matches `Pending`.
- `Pending` does not match `pending`.
- `PENDING` does not match `Pending`.

There is no automatic case normalization and no culture-dependent comparison.

## 6. Value comparison

Value comparison uses:

```csharp
EqualityComparer<TValue>.Default
```

The implementation must honor the equality behavior of the supplied `TValue`.

`TValue` must keep stable equality and hash behavior after Registry initialization. Immutable `TValue` is strongly recommended.

## 7. Equality semantics

Two SmartEnum instances are equal only when both are true:

1. They belong to the same SmartEnum domain type (`TEnum`).
2. Their values compare equal with `EqualityComparer<TValue>.Default`.

`Name` is not part of equality.

Runtime concrete type is not part of equality.

Registry membership is not part of equality.

Within one domain, two instances with different names are equal when their values are equal.

Instances from different SmartEnum domains are not equal even when their values are equal. That runtime result is observed through `Equals(object?)`, which returns `false`.

Members:

```csharp
IEquatable<TEnum>
bool Equals(TEnum? other)
override bool Equals(object? obj)
override int GetHashCode()
public static bool operator ==(SmartEnum<TEnum, TValue>? left, SmartEnum<TEnum, TValue>? right)
public static bool operator !=(SmartEnum<TEnum, TValue>? left, SmartEnum<TEnum, TValue>? right)
```

`Equals(object?)` is an override of `object.Equals(object?)`. It is not a hiding method.

`GetHashCode()` is an override of `object.GetHashCode()`. It is not a hiding method.

Operator signatures are exactly:

```csharp
public static bool operator ==(
    SmartEnum<TEnum, TValue>? left,
    SmartEnum<TEnum, TValue>? right);

public static bool operator !=(
    SmartEnum<TEnum, TValue>? left,
    SmartEnum<TEnum, TValue>? right);
```

The parameters are nullable. A non-nullable parameter list is not the contract, because `== null` must compile in a nullable-enabled consumer.

### Amendment — operator parameter type

This is a technical clarification of the accepted contract. Status stays **Accepted**.

The first accepted text used `operator ==(TEnum? left, TEnum? right)` and the matching `!=` signature. That declaration does not compile. The compiler reports **CS0563**: a binary operator must have at least one parameter whose type is the containing type.

`==` and `!=` are declared on `SmartEnum<TEnum, TValue>`. That class is the containing type. `TEnum` is a type parameter. The constraint `TEnum : SmartEnum<TEnum, TValue>` does not make `TEnum` the containing type, so `TEnum?` is not a legal operator parameter here.

`SmartEnum<TEnum, TValue>?` is the valid parameter type. Equality semantics are unchanged.

### Operator resolution

- A comparison of two concrete domain instances, such as `OrderStatus == OrderStatus`, binds to this inherited base operator. The concrete type converts to `SmartEnum<TEnum, TValue>`.
- Nullable comparisons compile when nullable reference types are enabled. `instance == null`, `null == instance`, and `null == null` bind to the same operator.
- A variable whose static type is `SmartEnum<TEnum, TValue>` uses the same operator.
- Two different closed SmartEnum domains have no conversion between them. `==` and `!=` are not supported for that pair.
- That comparison produces a compile-time operator resolution error (CS0019 in the reviewed consumer scenario).
- This is a compile-time API limitation, not a runtime equality result from `==`.
- Cross-domain runtime inequality is observed through `Equals(object?)`, which returns `false`.

Null behavior:

- `null == null` is `true`.
- `null == instance` is `false`.
- `instance == null` is `false`.
- `!=` is the logical inverse of `==`.
- Equality must be symmetric and consistent.

## 8. GetHashCode

```csharp
HashCode.Combine(typeof(TEnum), Value)
```

The hash represents the same identity as equality: domain type + value.

`GetHashCode` is not a durable identifier and must not be stored or transmitted as a domain identity.

## 9. Registry indexes and performance

The Registry will hold:

- An immutable instance collection.
- A private name dictionary.
- A private value dictionary.

Both dictionaries are built during Registry initialization together with the existing validation. Lookup must not rebuild them.

Expected average complexity:

- `FromName`: O(1)
- `FromValue`: O(1)
- `TryFromName`: O(1)
- `TryFromValue`: O(1)
- `ContainsName`: O(1)
- `ContainsValue`: O(1)

Hash collisions are resolved by dictionary equality. Equal hashes alone do not mean equal values.

## 10. Public API and access

`SmartEnum<TEnum, TValue>.Registry` remains the official public Registry entry point.

Per ADR-004:

`SmartEnumRegistry<TEnum, TValue>.Instance` remains internal.

Lookup APIs are exposed as inherited static members of the SmartEnum domain. Registry implementation details and dictionaries stay encapsulated.

The existing public Registry type and `Items` property keep backward compatibility.

## 11. Alternatives considered

| Alternative | Outcome |
| --- | --- |
| Case-insensitive name lookup | Rejected for this version — extra behavior and ambiguity without a concrete requirement |
| `Find` methods | Deferred — Try methods already provide non-throwing lookup |
| Equality by value only | Rejected — different domains with the same value would compare equal |
| Equality by runtime concrete type | Rejected — inconsistent between an abstract SmartEnum root and a concrete derived instance |
| Rebuild dictionaries on every lookup | Rejected — extra allocation and cost |

## 12. Consequences

### Positive

- Predictable lookup.
- Efficient name and value search.
- Consistent equality between domain instances.
- A clear split between throwing and non-throwing APIs.
- The Registry remains the single source of truth.
- No reflection or runtime discovery.

### Trade-offs

- `TValue` must keep stable equality and hash behavior.
- The new public methods are a long-term API commitment.
- `GetHashCode` is not stable across processes.
- This version has no case-insensitive lookup.

## 13. Security and performance

- No reflection-based lookup.
- No global mutable Registry.
- No mutable dictionaries exposed.
- Registry initialization failures are not swallowed.
- Dictionary keys must keep stable equality and hash behavior.
- Lookup must not allocate a new dictionary or collection.

## 14. Test strategy

### Lookup

- Existing name and value.
- Missing name and value.
- Null name and value.
- Empty and whitespace name.
- Case sensitivity.
- Try success and failure.
- Contains success and failure.
- `ContainsName(null)`, `ContainsName("")`, and `ContainsName("   ")` return `false` and do not throw.
- Runtime Registry initialization failure is not swallowed.

### Equality

- Same instance.
- Different instances in the same domain with the same value.
- Different values in the same domain.
- Same value in different domains, observed with `Equals(object?)` returning `false`.
- `==` and `!=` between two different domain types do not compile (CS0019 in the reviewed consumer scenario). This is a compile-time limitation, not a runtime `false` from `==`.
- Same value and different name.
- `null == null` is `true`.
- `null == instance` and `instance == null` are `false`.
- `!=` is the logical inverse of `==`.
- Operator parameters are `SmartEnum<TEnum, TValue>?`. Concrete domain comparisons bind to the inherited operator. Generic base references use the same operator. Null comparisons compile under nullable reference types.
- Operator consistency with `Equals`.
- Equal instances have equal hash codes.
- Abstract root and concrete derived instances.

### Registry

- `List` is immutable.
- Dictionaries stay encapsulated.
- Different domains in the same assembly have independent indexes.
- Hash collision behavior.
- Lookup does not rebuild dictionaries.

## 15. References

- ADR-002 — Source Generator architecture
- ADR-003 — SmartEnum Registry validation and uniqueness
- ADR-004 — Registry public access
- Microsoft documentation: `EqualityComparer<T>.Default`
- Microsoft documentation: `Dictionary<TKey, TValue>`
- Microsoft documentation: `HashCode.Combine`
