using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace SmartEnum.Generators;

/// <summary>
/// Incremental source generator that emits <c>ISmartEnumGenerated&lt;T&gt;</c>
/// implementations for SmartEnum domain types.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SmartEnumGenerator : IIncrementalGenerator
{
    private const string CoreAssemblyName = "SmartEnum";
    private const string CoreMetadataName = "SmartEnum.SmartEnum`2";
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Multiple partial declarations of the same type can each match the syntax
        // predicate (any non-empty base list). Deduplicate by Roslyn symbol identity
        // so each domain is emitted exactly once while GetMembers() still sees fields
        // declared in every partial.
        IncrementalValuesProvider<INamedTypeSymbol> domainSymbols = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsClassOrRecordDeclaration(node),
                transform: static (ctx, _) => GetDomainTypeSymbol(ctx))
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!);

        IncrementalValuesProvider<DomainCandidate> candidates = domainSymbols
            .Collect()
            .SelectMany(static (symbols, _) => DeduplicateDomainSymbols(symbols));

        context.RegisterSourceOutput(
            candidates.Collect().Combine(context.CompilationProvider),
            static (spc, pair) =>
            {
                ImmutableArray<DomainCandidate> domains = pair.Left;
                Compilation compilation = pair.Right;
                foreach (DomainCandidate candidate in domains)
                {
                    Execute(spc, candidate, compilation);
                }
            });
    }

    private static bool IsClassOrRecordDeclaration(SyntaxNode node)
        => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 }
            or RecordDeclarationSyntax { BaseList.Types.Count: > 0 };

    private static INamedTypeSymbol? GetDomainTypeSymbol(GeneratorSyntaxContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        return IsSmartEnumDomain(typeSymbol, context.SemanticModel.Compilation, out _) ? typeSymbol : null;
    }

    private static ImmutableArray<DomainCandidate> DeduplicateDomainSymbols(
        ImmutableArray<INamedTypeSymbol> symbols)
    {
        if (symbols.IsDefaultOrEmpty)
        {
            return [];
        }

        var distinct = symbols
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .OrderBy(static s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToList();

        ImmutableArray<DomainCandidate>.Builder builder =
            ImmutableArray.CreateBuilder<DomainCandidate>(distinct.Count);

        foreach (INamedTypeSymbol symbol in distinct)
        {
            builder.Add(DomainCandidate.Create(symbol));
        }

        return builder.ToImmutable();
    }

    private static bool IsSmartEnumDomain(
        INamedTypeSymbol typeSymbol,
        Compilation compilation,
        out INamedTypeSymbol? smartEnumBase)
    {
        smartEnumBase = null;

        // A missing or ambiguous metadata name is not a Core match.
        // The assembly simple name is only a sanity check, not a cryptographic identity.
        if (compilation.GetTypeByMetadataName(CoreMetadataName) is not INamedTypeSymbol coreDefinition
            || coreDefinition.ContainingAssembly.Identity.Name != CoreAssemblyName)
        {
            return false;
        }

        for (INamedTypeSymbol? current = typeSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, coreDefinition))
            {
                continue;
            }

            if (current.TypeArguments.Length == 2
                && SymbolEqualityComparer.Default.Equals(current.TypeArguments[0], typeSymbol))
            {
                smartEnumBase = current;
                return true;
            }
        }

        return false;
    }

    private static void Execute(
        SourceProductionContext context,
        DomainCandidate candidate,
        Compilation compilation)
    {
        if (candidate.TypeSymbol.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.NestedDomainUnsupported,
                candidate.TypeLocation,
                candidate.TypeSymbol.ToDisplayString()));
            return;
        }

        if (!candidate.IsPartial)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.DomainMustBePartial,
                candidate.TypeLocation,
                candidate.TypeName));
            return;
        }

        ImmutableArray<InstanceField>.Builder validFields = ImmutableArray.CreateBuilder<InstanceField>();

        // Order fields by source location so multi-file partials stay deterministic.
        IEnumerable<IFieldSymbol> orderedFields = candidate.TypeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .OrderBy(static f => f.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0);

        foreach (IFieldSymbol field in orderedFields)
        {
            if (!field.IsStatic || field.IsImplicitlyDeclared || field.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (!IsAssignableToDomain(field.Type, candidate.TypeSymbol))
            {
                continue;
            }

            if (!field.IsReadOnly)
            {
                Location location = field.Locations.FirstOrDefault() ?? candidate.TypeLocation;
                context.ReportDiagnostic(Diagnostic.Create(
                    SmartEnumDiagnostics.InstanceFieldMustBeReadonly,
                    location,
                    field.Name,
                    candidate.TypeName));
                continue;
            }

            validFields.Add(InstanceField.Create(field, compilation));
        }

        ImmutableArray<InstanceField> fields = validFields.ToImmutable();
        ReportUniquenessDiagnostics(context, candidate, fields);

        if (fields.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.EmptyDomain,
                candidate.TypeLocation,
                candidate.TypeName));
        }

        string source = GenerateSource(candidate, fields);
        string hintName = CreateHintName(candidate);
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static void ReportUniquenessDiagnostics(
        SourceProductionContext context,
        DomainCandidate candidate,
        ImmutableArray<InstanceField> fields)
    {
        Dictionary<string, InstanceField> names = new(StringComparer.Ordinal);
        Dictionary<object, InstanceField> values = new(EqualityComparer<object>.Default);

        foreach (InstanceField field in fields)
        {
            if (field.ConstantName is { } name)
            {
                if (names.TryGetValue(name, out InstanceField priorName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SmartEnumDiagnostics.DuplicateName,
                        field.Location,
                        additionalLocations: [priorName.Location],
                        messageArgs:
                        [
                            candidate.TypeName,
                            name,
                            priorName.FieldName,
                            field.FieldName,
                        ]));
                }
                else
                {
                    names.Add(name, field);
                }
            }

            if (field.ConstantValue is { } value)
            {
                if (values.TryGetValue(value, out InstanceField priorValue))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SmartEnumDiagnostics.DuplicateValue,
                        field.Location,
                        additionalLocations: [priorValue.Location],
                        messageArgs:
                        [
                            candidate.TypeName,
                            value,
                            priorValue.FieldName,
                            field.FieldName,
                        ]));
                }
                else
                {
                    values.Add(value, field);
                }
            }
        }
    }

    private static bool IsAssignableToDomain(ITypeSymbol fieldType, INamedTypeSymbol domainType)
    {
        // Public static readonly fields whose type is the domain type (exact match).
        return SymbolEqualityComparer.Default.Equals(fieldType, domainType);
    }

    private static string CreateHintName(DomainCandidate candidate)
    {
        string name = string.IsNullOrEmpty(candidate.NamespaceName)
            ? candidate.TypeName
            : candidate.NamespaceName + "." + candidate.TypeName;

        return name.Replace('+', '.') + ".SmartEnum.g.cs";
    }

    private static string GenerateSource(DomainCandidate candidate, ImmutableArray<InstanceField> fields)
    {
        string fullyQualifiedDomain = candidate.FullyQualifiedTypeName;
        string interfaceName = "global::SmartEnum.Internal.ISmartEnumGenerated<" + fullyQualifiedDomain + ">";

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(candidate.NamespaceName))
        {
            sb.Append("namespace ");
            sb.Append(candidate.NamespaceName);
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(candidate.AccessibilityKeyword);
        if (candidate.IsSealed)
        {
            sb.Append(" sealed");
        }

        sb.Append(" partial ");
        sb.Append(candidate.TypeKeyword);
        sb.Append(' ');
        sb.Append(candidate.TypeName);
        sb.Append(candidate.TypeParameterListText);
        sb.Append(" : ");
        sb.Append(interfaceName);
        sb.AppendLine();

        if (!string.IsNullOrEmpty(candidate.ConstraintClausesText))
        {
            sb.AppendLine(candidate.ConstraintClausesText);
        }

        // CS9203: returning a ReadOnlySpan collection expression may escape scope.
        // Back instances with a static readonly array and return a span over it.
        sb.AppendLine("{");
        sb.Append("    private static readonly ");
        sb.Append(fullyQualifiedDomain);
        sb.Append("[] _smartEnumInstances = ");

        if (fields.Length == 0)
        {
            sb.Append("global::System.Array.Empty<");
            sb.Append(fullyQualifiedDomain);
            sb.AppendLine(">();");
        }
        else
        {
            sb.Append('[');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(fields[i].FieldName);
            }

            sb.AppendLine("];");
        }

        sb.AppendLine();
        sb.Append("    static global::System.ReadOnlySpan<");
        sb.Append(fullyQualifiedDomain);
        sb.Append("> ");
        sb.Append(interfaceName);
        sb.AppendLine(".GetInstances()");
        sb.AppendLine("        => _smartEnumInstances;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private sealed class DomainCandidate
    {
        private DomainCandidate(
            INamedTypeSymbol typeSymbol,
            string typeName,
            string fullyQualifiedTypeName,
            string? namespaceName,
            string accessibilityKeyword,
            string typeKeyword,
            string typeParameterListText,
            string constraintClausesText,
            bool isPartial,
            bool isSealed,
            Location typeLocation)
        {
            TypeSymbol = typeSymbol;
            TypeName = typeName;
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            NamespaceName = namespaceName;
            AccessibilityKeyword = accessibilityKeyword;
            TypeKeyword = typeKeyword;
            TypeParameterListText = typeParameterListText;
            ConstraintClausesText = constraintClausesText;
            IsPartial = isPartial;
            IsSealed = isSealed;
            TypeLocation = typeLocation;
        }

        public INamedTypeSymbol TypeSymbol { get; }
        public string TypeName { get; }
        public string FullyQualifiedTypeName { get; }
        public string? NamespaceName { get; }
        public string AccessibilityKeyword { get; }
        public string TypeKeyword { get; }
        public string TypeParameterListText { get; }
        public string ConstraintClausesText { get; }
        public bool IsPartial { get; }
        public bool IsSealed { get; }
        public Location TypeLocation { get; }

        public static DomainCandidate Create(INamedTypeSymbol symbol)
        {
            var declarations = symbol.DeclaringSyntaxReferences
                .Select(static reference => reference.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .OrderBy(static d => d.SyntaxTree.FilePath, StringComparer.Ordinal)
                .ThenBy(static d => d.SpanStart)
                .ToImmutableArray();

            TypeDeclarationSyntax primary = declarations.Length > 0
                ? declarations[0]
                : throw new InvalidOperationException(
                    $"SmartEnum domain '{symbol.Name}' has no declaring type syntax.");

            // Partial is a type-level fact across all declarations, not a single syntax node.
            bool isPartial = declarations.Any(static d => d.Modifiers.Any(SyntaxKind.PartialKeyword));
            bool isSealed = symbol.IsSealed;
            string accessibility = GetAccessibilityKeyword(symbol);
            string typeKeyword = primary.Keyword.ValueText;
            string typeName = symbol.Name;
            string fullyQualified = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string? ns = symbol.ContainingNamespace is { IsGlobalNamespace: false } n
                ? n.ToDisplayString()
                : null;

            string typeParameters = string.Empty;
            string constraints = string.Empty;

            if (symbol.TypeParameters.Length > 0)
            {
                // Open generic domains are not supported. Nested domains are rejected
                // before source is emitted.
                typeParameters = "<" + string.Join(", ", symbol.TypeParameters.Select(static tp => tp.Name)) + ">";
                constraints = BuildConstraints(symbol);
            }

            Location location = primary.Identifier.GetLocation();

            return new DomainCandidate(
                symbol,
                typeName,
                fullyQualified,
                ns,
                accessibility,
                typeKeyword,
                typeParameters,
                constraints,
                isPartial,
                isSealed,
                location);
        }

        private static string GetAccessibilityKeyword(INamedTypeSymbol symbol)
            => symbol.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.Private => "private",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "internal",
            };

        private static string BuildConstraints(INamedTypeSymbol symbol)
        {
            StringBuilder sb = new();
            foreach (ITypeParameterSymbol tp in symbol.TypeParameters)
            {
                ImmutableArray<string>.Builder parts = ImmutableArray.CreateBuilder<string>();
                if (tp.HasReferenceTypeConstraint)
                {
                    parts.Add("class");
                }

                if (tp.HasValueTypeConstraint)
                {
                    parts.Add("struct");
                }

                if (tp.HasNotNullConstraint)
                {
                    parts.Add("notnull");
                }

                if (tp.HasUnmanagedTypeConstraint)
                {
                    parts.Add("unmanaged");
                }

                foreach (ITypeSymbol constraintType in tp.ConstraintTypes)
                {
                    parts.Add(constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }

                if (tp.HasConstructorConstraint)
                {
                    parts.Add("new()");
                }

                if (parts.Count == 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append("    where ");
                sb.Append(tp.Name);
                sb.Append(" : ");
                sb.Append(string.Join(", ", parts));
            }

            return sb.ToString();
        }
    }

    private readonly struct InstanceField
    {
        private InstanceField(
            string fieldName,
            Location location,
            string? constantName,
            object? constantValue)
        {
            FieldName = fieldName;
            Location = location;
            ConstantName = constantName;
            ConstantValue = constantValue;
        }

        public string FieldName { get; }
        public Location Location { get; }
        public string? ConstantName { get; }
        public object? ConstantValue { get; }

        public static InstanceField Create(IFieldSymbol field, Compilation compilation)
        {
            Location location = field.Locations.FirstOrDefault() ?? Location.None;
            TryGetConstructorConstants(field, compilation, out string? constantName, out object? constantValue);
            return new InstanceField(field.Name, location, constantName, constantValue);
        }

        private static void TryGetConstructorConstants(
            IFieldSymbol field,
            Compilation compilation,
            out string? constantName,
            out object? constantValue)
        {
            constantName = null;
            constantValue = null;

            foreach (SyntaxReference reference in field.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not VariableDeclaratorSyntax
                    {
                        Initializer.Value: var initializer
                    })
                {
                    continue;
                }

                if (initializer is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax))
                {
                    continue;
                }

                SemanticModel model = compilation.GetSemanticModel(initializer.SyntaxTree);
                if (AsObjectCreation(model.GetOperation(initializer)) is not IObjectCreationOperation creation
                    || creation.Constructor is not { } constructor)
                {
                    continue;
                }

                // The operation's constructor is the overload the compiler selected.
                // Each argument's Parameter is the parameter that argument was bound to.
                HashSet<IParameterSymbol> bound = new(SymbolEqualityComparer.Default);
                foreach (IArgumentOperation argument in creation.Arguments)
                {
                    IParameterSymbol? parameter = argument.Parameter;
                    if (parameter is null)
                    {
                        continue;
                    }

                    bound.Add(parameter);
                    if (TryGetArgumentConstant(argument, out object? constant))
                    {
                        AssignConstant(parameter, constant, ref constantName, ref constantValue);
                    }
                }

                foreach (IParameterSymbol parameter in constructor.Parameters)
                {
                    if (bound.Contains(parameter) || !parameter.HasExplicitDefaultValue)
                    {
                        continue;
                    }

                    AssignConstant(parameter, parameter.ExplicitDefaultValue, ref constantName, ref constantValue);
                }

                return;
            }
        }

        private static IObjectCreationOperation? AsObjectCreation(IOperation? operation)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            return operation as IObjectCreationOperation;
        }

        private static bool TryGetArgumentConstant(IArgumentOperation argument, out object? constant)
        {
            Optional<object?> folded = argument.Value.ConstantValue;
            if (folded.HasValue)
            {
                constant = folded.Value;
                return true;
            }

            if (argument.ArgumentKind == ArgumentKind.DefaultValue
                && argument.Parameter?.HasExplicitDefaultValue == true)
            {
                constant = argument.Parameter.ExplicitDefaultValue;
                return true;
            }

            constant = null;
            return false;
        }

        private static void AssignConstant(
            IParameterSymbol parameter,
            object? constant,
            ref string? constantName,
            ref object? constantValue)
        {
            if (parameter.Name == "name" && constant is string name)
            {
                constantName = name;
            }
            else if (parameter.Name == "value" && constant is not null)
            {
                constantValue = constant;
            }
        }
    }
}
