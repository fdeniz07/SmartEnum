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
    private const string DomainModelStepName = "SmartEnum.DomainModel";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Each matching declaration is reduced to one equatable model in the transform.
        // Only the declaration whose own base clause leads to Core emits a model, so partials
        // do not need a global Collect(). Source output consumes that model alone.
        IncrementalValuesProvider<DomainModel> models = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsClassOrRecordDeclaration(node),
                transform: static (syntaxContext, cancellationToken) => CreateDomainModel(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithComparer(DomainModelComparer.Instance)
            .WithTrackingName(DomainModelStepName);

        context.RegisterSourceOutput(
            models,
            static (sourceContext, model) => Execute(sourceContext, model));
    }

    private static bool IsClassOrRecordDeclaration(SyntaxNode node)
        => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 }
            or RecordDeclarationSyntax { BaseList.Types.Count: > 0 };

    private static DomainModel? CreateDomainModel(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not TypeDeclarationSyntax declaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        if (!IsSelectedOwner(declaration, symbol, context.SemanticModel, cancellationToken))
        {
            return null;
        }

        return DomainModel.Create(declaration, symbol, context.SemanticModel.Compilation, cancellationToken);
    }

    private static bool IsSelectedOwner(
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol symbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (!IsOwnerDeclaration(declaration, symbol, semanticModel, cancellationToken))
        {
            return false;
        }

        TypeDeclarationSyntax? selected = null;
        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax candidate)
            {
                continue;
            }

            SemanticModel candidateModel = candidate.SyntaxTree == declaration.SyntaxTree
                ? semanticModel
                : semanticModel.Compilation.GetSemanticModel(candidate.SyntaxTree);
            if (!IsOwnerDeclaration(candidate, symbol, candidateModel, cancellationToken))
            {
                continue;
            }

            if (selected is null || CompareDeclarations(candidate, selected, semanticModel.Compilation) < 0)
            {
                selected = candidate;
            }
        }

        return selected is not null
            && selected.SyntaxTree == declaration.SyntaxTree
            && selected.SpanStart == declaration.SpanStart;
    }

    private static int CompareDeclarations(
        TypeDeclarationSyntax left,
        TypeDeclarationSyntax right,
        Compilation compilation)
    {
        int pathOrder = string.Compare(
            left.SyntaxTree.FilePath,
            right.SyntaxTree.FilePath,
            StringComparison.Ordinal);
        if (pathOrder != 0)
        {
            return pathOrder;
        }

        int spanOrder = left.SpanStart.CompareTo(right.SpanStart);
        if (spanOrder != 0)
        {
            return spanOrder;
        }

        return SyntaxTreeOrder(left.SyntaxTree, compilation)
            .CompareTo(SyntaxTreeOrder(right.SyntaxTree, compilation));
    }

    private static int SyntaxTreeOrder(SyntaxTree tree, Compilation compilation)
    {
        int index = 0;
        foreach (SyntaxTree candidate in compilation.SyntaxTrees)
        {
            if (candidate == tree)
            {
                return index;
            }

            index++;
        }

        return int.MaxValue;
    }

    private static bool IsOwnerDeclaration(
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol symbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (declaration.BaseList is null
            || ResolveCore(semanticModel.Compilation) is not INamedTypeSymbol core)
        {
            return false;
        }

        foreach (BaseTypeSyntax baseSyntax in declaration.BaseList.Types)
        {
            if (semanticModel.GetTypeInfo(baseSyntax.Type, cancellationToken).Type is not INamedTypeSymbol bound
                || bound.TypeKind == TypeKind.Interface)
            {
                continue;
            }

            if (ChainDeclaresCore(bound, symbol, core))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ChainDeclaresCore(
        INamedTypeSymbol start,
        INamedTypeSymbol domain,
        INamedTypeSymbol core)
    {
        for (INamedTypeSymbol? current = start; current is not null; current = current.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, core))
            {
                continue;
            }

            if (current.TypeArguments.Length == 2
                && SymbolEqualityComparer.Default.Equals(current.TypeArguments[0], domain))
            {
                return true;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? ResolveCore(Compilation compilation)
    {
        // A missing or ambiguous metadata name is not a Core match.
        // The assembly simple name is only a sanity check, not a cryptographic identity.
        if (compilation.GetTypeByMetadataName(CoreMetadataName) is not INamedTypeSymbol core
            || core.ContainingAssembly.Identity.Name != CoreAssemblyName)
        {
            return null;
        }

        return core;
    }

    private static void Execute(SourceProductionContext context, DomainModel model)
    {
        if (model.IsNested)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.NestedDomainUnsupported,
                model.TypeLocation.ToLocation(),
                model.DisplayName));
            return;
        }

        if (!model.IsPartial)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.DomainMustBePartial,
                model.TypeLocation.ToLocation(),
                model.TypeName));
            return;
        }

        ImmutableArray<FieldModel>.Builder validFields = ImmutableArray.CreateBuilder<FieldModel>();
        foreach (FieldModel field in model.Fields)
        {
            if (field.IsReadOnly)
            {
                validFields.Add(field);
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.InstanceFieldMustBeReadonly,
                field.Location.ToLocation(),
                field.FieldName,
                model.TypeName));
        }

        ImmutableArray<FieldModel> fields = validFields.ToImmutable();
        ReportUniquenessDiagnostics(context, model, fields);

        if (fields.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SmartEnumDiagnostics.EmptyDomain,
                model.TypeLocation.ToLocation(),
                model.TypeName));
        }

        context.AddSource(model.HintName, SourceText.From(GenerateSource(model, fields), Encoding.UTF8));
    }

    private static void ReportUniquenessDiagnostics(
        SourceProductionContext context,
        DomainModel model,
        ImmutableArray<FieldModel> fields)
    {
        Dictionary<string, FieldModel> names = new(StringComparer.Ordinal);
        Dictionary<object, FieldModel> values = new(EqualityComparer<object>.Default);

        foreach (FieldModel field in fields)
        {
            if (field.ConstantName is { } name)
            {
                if (names.TryGetValue(name, out FieldModel priorName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SmartEnumDiagnostics.DuplicateName,
                        field.Location.ToLocation(),
                        additionalLocations: [priorName.Location.ToLocation()],
                        messageArgs:
                        [
                            model.TypeName,
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
                if (values.TryGetValue(value, out FieldModel priorValue))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SmartEnumDiagnostics.DuplicateValue,
                        field.Location.ToLocation(),
                        additionalLocations: [priorValue.Location.ToLocation()],
                        messageArgs:
                        [
                            model.TypeName,
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

    private static string GenerateSource(DomainModel model, ImmutableArray<FieldModel> fields)
    {
        string fullyQualifiedDomain = model.FullyQualifiedTypeName;
        string interfaceName = "global::SmartEnum.Internal.ISmartEnumGenerated<" + fullyQualifiedDomain + ">";

        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            builder.Append("namespace ");
            builder.Append(model.NamespaceName);
            builder.AppendLine(";");
            builder.AppendLine();
        }

        builder.Append(model.AccessibilityKeyword);
        if (model.IsSealed)
        {
            builder.Append(" sealed");
        }

        builder.Append(" partial ");
        builder.Append(model.TypeKeyword);
        builder.Append(' ');
        builder.Append(model.TypeName);
        builder.Append(model.TypeParameterListText);
        builder.Append(" : ");
        builder.Append(interfaceName);
        builder.AppendLine();

        if (!string.IsNullOrEmpty(model.ConstraintClausesText))
        {
            builder.AppendLine(model.ConstraintClausesText);
        }

        // CS9203: returning a ReadOnlySpan collection expression may escape scope.
        // Back instances with a static readonly array and return a span over it.
        builder.AppendLine("{");
        builder.Append("    private static readonly ");
        builder.Append(fullyQualifiedDomain);
        builder.Append("[] _smartEnumInstances = ");

        if (fields.Length == 0)
        {
            builder.Append("global::System.Array.Empty<");
            builder.Append(fullyQualifiedDomain);
            builder.AppendLine(">();");
        }
        else
        {
            builder.Append('[');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(fields[i].FieldName);
            }

            builder.AppendLine("];");
        }

        builder.AppendLine();
        builder.Append("    static global::System.ReadOnlySpan<");
        builder.Append(fullyQualifiedDomain);
        builder.Append("> ");
        builder.Append(interfaceName);
        builder.AppendLine(".GetInstances()");
        builder.AppendLine("        => _smartEnumInstances;");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static bool TryGetConstructorConstants(
        IFieldSymbol field,
        Compilation compilation,
        CancellationToken cancellationToken,
        out string? constantName,
        out object? constantValue)
    {
        constantName = null;
        constantValue = null;

        foreach (SyntaxReference reference in field.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax
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
            if (AsObjectCreation(model.GetOperation(initializer, cancellationToken)) is not IObjectCreationOperation creation
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

            return true;
        }

        return false;
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

    private sealed class DomainModel
    {
        private DomainModel(
            string typeName,
            string displayName,
            string fullyQualifiedTypeName,
            string? namespaceName,
            string accessibilityKeyword,
            string typeKeyword,
            string typeParameterListText,
            string constraintClausesText,
            bool isPartial,
            bool isSealed,
            bool isNested,
            SourceAnchor typeLocation,
            ImmutableArray<FieldModel> fields)
        {
            TypeName = typeName;
            DisplayName = displayName;
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            NamespaceName = namespaceName;
            AccessibilityKeyword = accessibilityKeyword;
            TypeKeyword = typeKeyword;
            TypeParameterListText = typeParameterListText;
            ConstraintClausesText = constraintClausesText;
            IsPartial = isPartial;
            IsSealed = isSealed;
            IsNested = isNested;
            TypeLocation = typeLocation;
            Fields = fields;
            HintName = (string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName)
                .Replace('+', '.') + ".SmartEnum.g.cs";
        }

        public string TypeName { get; }
        public string DisplayName { get; }
        public string FullyQualifiedTypeName { get; }
        public string? NamespaceName { get; }
        public string AccessibilityKeyword { get; }
        public string TypeKeyword { get; }
        public string TypeParameterListText { get; }
        public string ConstraintClausesText { get; }
        public bool IsPartial { get; }
        public bool IsSealed { get; }
        public bool IsNested { get; }
        public SourceAnchor TypeLocation { get; }
        public ImmutableArray<FieldModel> Fields { get; }
        public string HintName { get; }

        public static DomainModel Create(
            TypeDeclarationSyntax ownerDeclaration,
            INamedTypeSymbol symbol,
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            ImmutableArray<TypeDeclarationSyntax> declarations = symbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<TypeDeclarationSyntax>()
                .ToImmutableArray();

            bool isPartial = declarations.Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
            string? namespaceName = symbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
                ? containingNamespace.ToDisplayString()
                : null;

            string typeParameters = string.Empty;
            string constraints = string.Empty;
            if (symbol.TypeParameters.Length > 0)
            {
                // Open generic domains are not supported. Nested domains are rejected
                // before source is emitted.
                typeParameters = "<" + string.Join(", ", symbol.TypeParameters.Select(static parameter => parameter.Name)) + ">";
                constraints = BuildConstraints(symbol);
            }

            ImmutableArray<FieldModel> fields = symbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(field => field.IsStatic
                    && !field.IsImplicitlyDeclared
                    && field.DeclaredAccessibility == Accessibility.Public
                    && SymbolEqualityComparer.Default.Equals(field.Type, symbol))
                .Select(field => FieldModel.Create(field, ownerDeclaration, compilation, cancellationToken))
                .OrderBy(static field => field.Location.Path, StringComparer.Ordinal)
                .ThenBy(static field => field.Location.Start)
                .ToImmutableArray();

            return new DomainModel(
                symbol.Name,
                symbol.ToDisplayString(),
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                namespaceName,
                GetAccessibilityKeyword(symbol),
                ownerDeclaration.Keyword.ValueText,
                typeParameters,
                constraints,
                isPartial,
                symbol.IsSealed,
                symbol.ContainingType is not null,
                SourceAnchor.From(ownerDeclaration.Identifier.GetLocation()),
                fields);
        }

        public override string ToString() => DisplayName;

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
            StringBuilder builder = new();
            foreach (ITypeParameterSymbol parameter in symbol.TypeParameters)
            {
                ImmutableArray<string>.Builder parts = ImmutableArray.CreateBuilder<string>();
                if (parameter.HasReferenceTypeConstraint)
                {
                    parts.Add("class");
                }

                if (parameter.HasValueTypeConstraint)
                {
                    parts.Add("struct");
                }

                if (parameter.HasNotNullConstraint)
                {
                    parts.Add("notnull");
                }

                if (parameter.HasUnmanagedTypeConstraint)
                {
                    parts.Add("unmanaged");
                }

                foreach (ITypeSymbol constraintType in parameter.ConstraintTypes)
                {
                    parts.Add(constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }

                if (parameter.HasConstructorConstraint)
                {
                    parts.Add("new()");
                }

                if (parts.Count == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("    where ");
                builder.Append(parameter.Name);
                builder.Append(" : ");
                builder.Append(string.Join(", ", parts));
            }

            return builder.ToString();
        }
    }

    private readonly struct FieldModel : IEquatable<FieldModel>
    {
        public FieldModel(
            string fieldName,
            bool isReadOnly,
            string? constantName,
            object? constantValue,
            SourceAnchor location)
        {
            FieldName = fieldName;
            IsReadOnly = isReadOnly;
            ConstantName = constantName;
            ConstantValue = constantValue;
            Location = location;
        }

        public string FieldName { get; }
        public bool IsReadOnly { get; }
        public string? ConstantName { get; }
        public object? ConstantValue { get; }
        public SourceAnchor Location { get; }

        public static FieldModel Create(
            IFieldSymbol field,
            TypeDeclarationSyntax ownerDeclaration,
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            Location location = field.Locations.FirstOrDefault() ?? ownerDeclaration.Identifier.GetLocation();
            TryGetConstructorConstants(
                field,
                compilation,
                cancellationToken,
                out string? constantName,
                out object? constantValue);
            return new FieldModel(
                field.Name,
                field.IsReadOnly,
                constantName,
                constantValue,
                SourceAnchor.From(location));
        }

        public bool Equals(FieldModel other)
            => FieldName == other.FieldName
                && IsReadOnly == other.IsReadOnly
                && ConstantName == other.ConstantName
                && Equals(ConstantValue, other.ConstantValue)
                && Location.Equals(other.Location);

        public override bool Equals(object? obj) => obj is FieldModel other && Equals(other);

        public override int GetHashCode()
        {
            int hash = FieldName.GetHashCode();
            hash = Combine(hash, IsReadOnly.GetHashCode());
            hash = Combine(hash, ConstantName?.GetHashCode() ?? 0);
            hash = Combine(hash, ConstantValue?.GetHashCode() ?? 0);
            hash = Combine(hash, Location.GetHashCode());
            return hash;
        }

        private static int Combine(int left, int right) => unchecked((left * 397) ^ right);
    }

    private readonly struct SourceAnchor : IEquatable<SourceAnchor>
    {
        public SourceAnchor(
            string path,
            int start,
            int length,
            int startLine,
            int startCharacter,
            int endLine,
            int endCharacter)
        {
            Path = path;
            Start = start;
            Length = length;
            StartLine = startLine;
            StartCharacter = startCharacter;
            EndLine = endLine;
            EndCharacter = endCharacter;
        }

        public string Path { get; }
        public int Start { get; }
        public int Length { get; }
        public int StartLine { get; }
        public int StartCharacter { get; }
        public int EndLine { get; }
        public int EndCharacter { get; }

        public static SourceAnchor From(Location location)
        {
            FileLinePositionSpan lineSpan = location.GetLineSpan();
            TextSpan textSpan = location.SourceSpan;
            return new SourceAnchor(
                lineSpan.Path,
                textSpan.Start,
                textSpan.Length,
                lineSpan.StartLinePosition.Line,
                lineSpan.StartLinePosition.Character,
                lineSpan.EndLinePosition.Line,
                lineSpan.EndLinePosition.Character);
        }

        public bool Equals(SourceAnchor other)
            => Path == other.Path
                && Start == other.Start
                && Length == other.Length
                && StartLine == other.StartLine
                && StartCharacter == other.StartCharacter
                && EndLine == other.EndLine
                && EndCharacter == other.EndCharacter;

        public override bool Equals(object? obj) => obj is SourceAnchor other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Path.GetHashCode();
            hash = Combine(hash, Start);
            hash = Combine(hash, Length);
            hash = Combine(hash, StartLine);
            hash = Combine(hash, StartCharacter);
            hash = Combine(hash, EndLine);
            hash = Combine(hash, EndCharacter);
            return hash;
        }

        private static int Combine(int left, int right) => unchecked((left * 397) ^ right);

        public Location ToLocation()
            => Location.Create(
                Path,
                new TextSpan(Start, Length),
                new LinePositionSpan(
                    new LinePosition(StartLine, StartCharacter),
                    new LinePosition(EndLine, EndCharacter)));
    }

    private sealed class DomainModelComparer : IEqualityComparer<DomainModel>
    {
        public static DomainModelComparer Instance { get; } = new();

        public bool Equals(DomainModel? x, DomainModel? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.TypeName == y.TypeName
                && x.DisplayName == y.DisplayName
                && x.FullyQualifiedTypeName == y.FullyQualifiedTypeName
                && x.NamespaceName == y.NamespaceName
                && x.AccessibilityKeyword == y.AccessibilityKeyword
                && x.TypeKeyword == y.TypeKeyword
                && x.TypeParameterListText == y.TypeParameterListText
                && x.ConstraintClausesText == y.ConstraintClausesText
                && x.IsPartial == y.IsPartial
                && x.IsSealed == y.IsSealed
                && x.IsNested == y.IsNested
                && x.TypeLocation.Equals(y.TypeLocation)
                && x.HintName == y.HintName
                && x.Fields.SequenceEqual(y.Fields);
        }

        public int GetHashCode(DomainModel obj)
        {
            int hash = obj.TypeName.GetHashCode();
            hash = Combine(hash, obj.DisplayName.GetHashCode());
            hash = Combine(hash, obj.FullyQualifiedTypeName.GetHashCode());
            hash = Combine(hash, obj.NamespaceName?.GetHashCode() ?? 0);
            hash = Combine(hash, obj.AccessibilityKeyword.GetHashCode());
            hash = Combine(hash, obj.TypeKeyword.GetHashCode());
            hash = Combine(hash, obj.TypeParameterListText.GetHashCode());
            hash = Combine(hash, obj.ConstraintClausesText.GetHashCode());
            hash = Combine(hash, obj.IsPartial.GetHashCode());
            hash = Combine(hash, obj.IsSealed.GetHashCode());
            hash = Combine(hash, obj.IsNested.GetHashCode());
            hash = Combine(hash, obj.TypeLocation.GetHashCode());
            hash = Combine(hash, obj.HintName.GetHashCode());
            hash = Combine(hash, obj.Fields.Length);
            foreach (FieldModel field in obj.Fields)
            {
                hash = Combine(hash, field.GetHashCode());
            }

            return hash;
        }

        private static int Combine(int left, int right) => unchecked((left * 397) ^ right);
    }
}
