using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SmartEnum.Generators.Tests;

public sealed class OwnerDeclarationGeneratorTests
{
    [Fact]
    public void Direct_smartenum_base_emits_one_source()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Contains("Pending", GeneratorTestHelpers.GetGeneratedSource(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Indirect_base_class_is_still_recognized()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public abstract class SmartEnumBase<TSelf> : SmartEnum<TSelf, int>
                where TSelf : SmartEnum<TSelf, int>
            {
            }

            public sealed partial class OrderStatus : SmartEnumBase<OrderStatus>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.DoesNotContain(
            result.Diagnostics,
            static diagnostic => diagnostic.Id is "SMARTENUM001"
                or "SMARTENUM002"
                or "SMARTENUM003"
                or "SMARTENUM004"
                or "SMARTENUM005"
                or "SMARTENUM006");
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        string generated = GeneratorTestHelpers.GetGeneratedSource(result)!;
        Assert.Contains("partial class OrderStatus", generated, StringComparison.Ordinal);
        Assert.Contains("ISmartEnumGenerated<global::Demo.OrderStatus>", generated, StringComparison.Ordinal);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("GetInstances()", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("class SmartEnumBase", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_smartenum_base_clauses_emit_one_deterministic_source()
    {
        (string FilePath, string Source)[] sources =
        [
            ("B.OrderStatus.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
                {
                    public static readonly OrderStatus Approved = new("Approved", 2);
                }
                """),
            ("A.OrderStatus.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
                {
                    public static readonly OrderStatus Pending = new("Pending", 1);

                    private OrderStatus(string name, int value) : base(name, value) { }
                }
                """),
        ];

        (GeneratorDriverRunResult first, _) = GeneratorTestHelpers.RunGenerator(sources);
        (GeneratorDriverRunResult second, _) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(first));
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(second));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(first));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(second));
        Assert.Equal(GeneratorTestHelpers.GetGeneratedSource(first), GeneratorTestHelpers.GetGeneratedSource(second));

        string generated = GeneratorTestHelpers.GetGeneratedSource(first)!;
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("Approved", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_path_and_span_on_separate_trees_emit_one_deterministic_source()
    {
        const string path = "OrderStatus.cs";
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;
        (string FilePath, string Source)[] sources = [(path, source), (path, source)];

        Compilation compilation = GeneratorTestHelpers.CreateCompilation([], sources);
        SyntaxTree firstTree = compilation.SyntaxTrees.ElementAt(0);
        SyntaxTree secondTree = compilation.SyntaxTrees.ElementAt(1);
        Assert.NotSame(firstTree, secondTree);
        Assert.Equal(path, firstTree.FilePath);
        Assert.Equal(path, secondTree.FilePath);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ClassDeclarationSyntax firstClass = firstTree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        ClassDeclarationSyntax secondClass = secondTree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Assert.Equal(firstClass.SpanStart, secondClass.SpanStart);
        Assert.NotNull(firstClass.BaseList);
        Assert.NotNull(secondClass.BaseList);

        (GeneratorDriverRunResult first, _) = GeneratorTestHelpers.RunGenerator(sources);
        (GeneratorDriverRunResult second, _) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(first));
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(second));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(first));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(second));
        Assert.Equal(GeneratorTestHelpers.GetGeneratedSource(first), GeneratorTestHelpers.GetGeneratedSource(second));
    }

    [Fact]
    public void SmartEnum_base_and_interface_on_the_same_declaration_emit_one_source()
    {
        const string source = """
            using System;
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>, IComparable<OrderStatus>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }

                public int CompareTo(OrderStatus? other) => 0;
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Contains("Pending", GeneratorTestHelpers.GetGeneratedSource(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_and_baseless_partials_do_not_create_extra_owners()
    {
        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(
            ("Own.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
                {
                    public static readonly OrderStatus Pending = new("Pending", 1);

                    private OrderStatus(string name, int value) : base(name, value) { }
                }
                """),
            ("Iface.cs", """
                using System;

                namespace Demo;

                public sealed partial class OrderStatus : IComparable<OrderStatus>
                {
                    public static readonly OrderStatus Approved = new("Approved", 2);

                    public int CompareTo(OrderStatus? other) => 0;
                }
                """),
            ("Fields.cs", """
                namespace Demo;

                public sealed partial class OrderStatus
                {
                    public static readonly OrderStatus Shipped = new("Shipped", 3);
                }
                """));

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        string generated = GeneratorTestHelpers.GetGeneratedSource(result)!;
        Assert.Contains("Shipped, Approved, Pending", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Using_alias_base_is_recognized()
    {
        const string source = """
            namespace Demo;

            using AliasBase = SmartEnum.SmartEnum<AliasStatus, int>;

            public sealed partial class AliasStatus : AliasBase
            {
                public static readonly AliasStatus One = new("One", 1);

                private AliasStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Contains("One", GeneratorTestHelpers.GetGeneratedSource(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Global_qualified_base_is_recognized()
    {
        const string source = """
            namespace Demo;

            public sealed partial class QualifiedStatus : global::SmartEnum.SmartEnum<QualifiedStatus, int>
            {
                public static readonly QualifiedStatus One = new("One", 1);

                private QualifiedStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Contains("One", GeneratorTestHelpers.GetGeneratedSource(result), StringComparison.Ordinal);
    }
}
