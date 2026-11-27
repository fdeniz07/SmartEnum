using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SmartEnum.Generators.Tests;

public sealed class CoreSymbolIdentityGeneratorTests
{
    private const string CoreMetadataName = "SmartEnum.SmartEnum`2";

    [Fact]
    public void Real_core_symbol_is_recognized()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class SampleStatus : SmartEnum<SampleStatus, int>
            {
                public static readonly SampleStatus One = new("One", 1);

                private SampleStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        AssertNoCompilerErrors(compilation);
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        AssertRealCoreIdentity(compilation, "SampleStatus");
    }

    [Fact]
    public void Source_type_with_the_core_metadata_name_resolves_to_source_and_is_not_accepted()
    {
        const string source = """
            namespace SmartEnum
            {
                public abstract class SmartEnum<TEnum, TValue>
                    where TEnum : SmartEnum<TEnum, TValue>
                    where TValue : notnull
                {
                    protected SmartEnum(string name, TValue value) { }
                }
            }

            namespace Demo
            {
                public sealed partial class OrderStatus : SmartEnum.SmartEnum<OrderStatus, int>
                {
                    public static readonly OrderStatus Pending = new("Pending", 1);

                    private OrderStatus(string name, int value) : base(name, value) { }
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(0, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        INamedTypeSymbol resolved = compilation.GetTypeByMetadataName(CoreMetadataName)!;
        INamedTypeSymbol sourceDefinition = RequireSourceDefinition(compilation, "SmartEnum");
        INamedTypeSymbol realCore = RequireReferencedCoreDefinition(compilation);
        INamedTypeSymbol domain = RequireDeclaredType(compilation, "OrderStatus");
        INamedTypeSymbol boundBase = domain.BaseType!;
        Assert.Equal("SmartEnum`2", sourceDefinition.MetadataName);
        Assert.Equal("SmartEnum", sourceDefinition.ContainingNamespace.ToDisplayString());
        Assert.True(SymbolEqualityComparer.Default.Equals(resolved, sourceDefinition));
        Assert.NotEqual("SmartEnum", resolved.ContainingAssembly.Identity.Name);
        Assert.False(SymbolEqualityComparer.Default.Equals(resolved, realCore));
        Assert.True(SymbolEqualityComparer.Default.Equals(boundBase.OriginalDefinition, sourceDefinition));
        Assert.False(SymbolEqualityComparer.Default.Equals(boundBase.OriginalDefinition, realCore));
    }

    [Fact]
    public void Two_metadata_assemblies_with_the_core_metadata_name_are_ambiguous_and_emit_nothing()
    {
        MetadataReference lookalike = CreateLookalikeReference("global", "Fake");
        Assert.Contains("global", lookalike.Properties.Aliases);
        Assert.Contains("Fake", lookalike.Properties.Aliases);

        const string source = """
            extern alias Fake;

            namespace Demo;

            public sealed partial class OrderStatus : Fake::SmartEnum.SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(
            [lookalike],
            ("AmbiguousMetadata.cs", source));

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        AssertNoCompilerErrors(compilation);
        Assert.Null(compilation.GetTypeByMetadataName(CoreMetadataName));
        Assert.Equal(2, CountMetadataDefinitions(compilation));
        Assert.Equal(0, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        INamedTypeSymbol domain = RequireDeclaredType(compilation, "OrderStatus");
        Assert.NotNull(domain.BaseType);
        INamedTypeSymbol boundBase = domain.BaseType!;
        INamedTypeSymbol lookalikeDefinition = RequireAssemblyDefinition(compilation, "SmartEnumLookalike");
        INamedTypeSymbol realCore = RequireReferencedCoreDefinition(compilation);
        Assert.True(SymbolEqualityComparer.Default.Equals(boundBase.OriginalDefinition, lookalikeDefinition));
        Assert.False(SymbolEqualityComparer.Default.Equals(boundBase.OriginalDefinition, realCore));
    }

    [Fact]
    public void Same_named_type_from_another_assembly_is_not_recognized()
    {
        MetadataReference lookalike = CreateLookalikeReference();
        const string source = """
            extern alias Fake;

            namespace Demo;

            public sealed partial class OrderStatus : Fake::SmartEnum.SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(
            [lookalike],
            ("LookalikeDomain.cs", source));

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        AssertNoCompilerErrors(compilation);
        Assert.Equal(0, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        INamedTypeSymbol resolved = compilation.GetTypeByMetadataName(CoreMetadataName)!;
        INamedTypeSymbol realCore = RequireReferencedCoreDefinition(compilation);
        INamedTypeSymbol domain = RequireDeclaredType(compilation, "OrderStatus");
        INamedTypeSymbol boundBase = domain.BaseType!;
        Assert.Equal("SmartEnum", resolved.ContainingAssembly.Identity.Name);
        Assert.True(SymbolEqualityComparer.Default.Equals(resolved, realCore));
        Assert.Equal("SmartEnumLookalike", boundBase.ContainingAssembly.Identity.Name);
        Assert.False(SymbolEqualityComparer.Default.Equals(boundBase.OriginalDefinition, realCore));
    }

    [Fact]
    public void Unrelated_generic_base_is_not_recognized()
    {
        const string source = """
            namespace Demo;

            public abstract class Holder<TEnum, TValue>
                where TValue : notnull
            {
                protected Holder(string name, TValue value) { }
            }

            public sealed partial class OrderStatus : Holder<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        AssertNoCompilerErrors(compilation);
        Assert.Equal(0, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        INamedTypeSymbol domain = RequireDeclaredType(compilation, "OrderStatus");
        INamedTypeSymbol core = compilation.GetTypeByMetadataName(CoreMetadataName)!;
        Assert.Equal("SmartEnum", core.ContainingAssembly.Identity.Name);
        Assert.False(SymbolEqualityComparer.Default.Equals(domain.BaseType!.OriginalDefinition, core));
    }

    [Fact]
    public void Existing_order_status_domain_still_uses_the_real_core_symbol()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static readonly OrderStatus Approved = new("Approved", 2);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        AssertNoCompilerErrors(compilation);
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        string generated = GeneratorTestHelpers.GetGeneratedSource(result)!;
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("Approved", generated, StringComparison.Ordinal);
        AssertRealCoreIdentity(compilation, "OrderStatus");
    }

    private static void AssertRealCoreIdentity(Compilation compilation, string domainName)
    {
        INamedTypeSymbol domain = RequireDeclaredType(compilation, domainName);
        INamedTypeSymbol core = compilation.GetTypeByMetadataName(CoreMetadataName)!;
        Assert.Equal("SmartEnum", core.ContainingAssembly.Identity.Name);
        Assert.True(SymbolEqualityComparer.Default.Equals(domain.BaseType!.OriginalDefinition, core));
        Assert.Equal(2, domain.BaseType.TypeArguments.Length);
        Assert.True(SymbolEqualityComparer.Default.Equals(domain.BaseType.TypeArguments[0], domain));
    }

    private static INamedTypeSymbol RequireSourceDefinition(Compilation compilation, string name)
    {
        ISymbol symbol = Assert.Single(
            compilation.GetSymbolsWithName(name, SymbolFilter.Type, TestContext.Current.CancellationToken),
            static candidate => candidate is INamedTypeSymbol { DeclaringSyntaxReferences.Length: > 0 });
        return (INamedTypeSymbol)symbol;
    }

    private static INamedTypeSymbol RequireDeclaredType(Compilation compilation, string name)
    {
        INamedTypeSymbol symbol = Assert.Single(
            compilation.GetSymbolsWithName(name, SymbolFilter.Type, TestContext.Current.CancellationToken)
                .OfType<INamedTypeSymbol>());
        return symbol;
    }

    private static INamedTypeSymbol RequireReferencedCoreDefinition(Compilation compilation)
    {
        INamedTypeSymbol? match = null;
        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            INamedTypeSymbol? type = assembly.GetTypeByMetadataName(CoreMetadataName);
            if (type?.ContainingAssembly.Identity.Name != "SmartEnum")
            {
                continue;
            }

            Assert.Null(match);
            match = type;
        }

        Assert.NotNull(match);
        return match;
    }

    private static INamedTypeSymbol RequireAssemblyDefinition(Compilation compilation, string assemblyName)
    {
        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly
                || assembly.Identity.Name != assemblyName)
            {
                continue;
            }

            INamedTypeSymbol? type = assembly.GetTypeByMetadataName(CoreMetadataName);
            Assert.NotNull(type);
            return type;
        }

        Assert.Fail($"Metadata type '{CoreMetadataName}' was not found in assembly '{assemblyName}'.");
        return null!;
    }

    private static int CountMetadataDefinitions(Compilation compilation)
    {
        int count = 0;
        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            if (assembly.GetTypeByMetadataName(CoreMetadataName) is not null)
            {
                count++;
            }
        }

        return count;
    }

    private static MetadataReference CreateLookalikeReference(params string[] aliases)
    {
        const string source = """
            namespace SmartEnum;

            public abstract class SmartEnum<TEnum, TValue>
                where TEnum : SmartEnum<TEnum, TValue>
                where TValue : notnull
            {
                protected SmartEnum(string name, TValue value) { }
            }
            """;

        var compilation = CSharpCompilation.Create(
            assemblyName: "SmartEnumLookalike",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp13))],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var image = new MemoryStream();
        var emit = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        MetadataReference reference = MetadataReference.CreateFromImage(image.ToArray());
        return reference.WithAliases(aliases.Length == 0 ? ["Fake"] : aliases);
    }

    private static void AssertNoCompilerErrors(Compilation compilation)
    {
        Diagnostic[] errors = compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
    }
}
