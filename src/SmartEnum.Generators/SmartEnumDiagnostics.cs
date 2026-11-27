using Microsoft.CodeAnalysis;

namespace SmartEnum.Generators;

internal static class SmartEnumDiagnostics
{
    private const string Category = "SmartEnum.Generators";

    public static readonly DiagnosticDescriptor DomainMustBePartial = new(
        id: "SMARTENUM001",
        title: "SmartEnum domain type must be partial",
        messageFormat: "Type '{0}' inherits from SmartEnum<,> and must be declared partial so the source generator can emit instance discovery",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InstanceFieldMustBeReadonly = new(
        id: "SMARTENUM002",
        title: "SmartEnum instance field must be static readonly",
        messageFormat: "Field '{0}' on SmartEnum domain '{1}' must be 'static readonly' to participate in compile-time instance discovery",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateName = new(
        id: "SMARTENUM003",
        title: "Duplicate SmartEnum Name",
        messageFormat: "SmartEnum domain '{0}' declares duplicate Name '{1}' on fields '{2}' and '{3}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateValue = new(
        id: "SMARTENUM004",
        title: "Duplicate SmartEnum Value",
        messageFormat: "SmartEnum domain '{0}' declares duplicate Value '{1}' on fields '{2}' and '{3}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyDomain = new(
        id: "SMARTENUM005",
        title: "SmartEnum domain has no instances",
        messageFormat: "SmartEnum domain '{0}' has no valid public static readonly instances",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedDomainUnsupported = new(
        id: "SMARTENUM006",
        title: "Nested SmartEnum domain types are not supported",
        messageFormat: "SmartEnum domain '{0}' is nested inside another type and is not supported; declare the domain as a top-level type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
