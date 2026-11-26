using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SmartEnum.Generators.Tests;

public sealed class EqualityOperatorCompilationTests
{
    private const string Domains = """
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

        public sealed partial class PayStatus : SmartEnum<PayStatus, int>
        {
            public static readonly PayStatus Card = new("Card", 1);

            private PayStatus(string name, int value)
                : base(name, value)
            {
            }
        }
        """;

    [Fact]
    public void Valid_comparisons_compile_without_warnings()
    {
        const string consumer = """
            using SmartEnum;

            namespace Demo;

            public static class EqualityConsumer
            {
                public static void Run()
                {
                    OrderStatus left = OrderStatus.Pending;
                    OrderStatus? none = null;
                    OrderStatus? alsoNone = null;
                    SmartEnum<OrderStatus, int> asBase = OrderStatus.Pending;
                    SmartEnum<OrderStatus, int> asOther = OrderStatus.Approved;
                    SmartEnum<OrderStatus, int>? missing = null;

                    _ = left == OrderStatus.Pending;
                    _ = left == OrderStatus.Approved;
                    _ = left != OrderStatus.Approved;
                    _ = left == none;
                    _ = none == left;
                    _ = none == alsoNone;
                    _ = none != alsoNone;
                    _ = asBase == asOther;
                    _ = asBase == OrderStatus.Pending;
                    _ = asBase == missing;
                    _ = missing == asBase;
                }
            }
            """;

        Compilation compilation = Compile(consumer);
        Diagnostic[] problems = compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(problems);
    }

    [Fact]
    public void Cross_domain_operator_is_rejected()
    {
        const string consumer = """
            namespace Demo;

            public static class CrossDomainConsumer
            {
                public static void Run()
                {
                    _ = OrderStatus.Pending == PayStatus.Card;
                }
            }
            """;

        Compilation compilation = Compile(consumer);
        Diagnostic[] errors = compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Diagnostic cross = Assert.Single(errors);
        Assert.Equal("CS0019", cross.Id);
    }

    private static Compilation Compile(string consumer)
    {
        (_, Compilation output) = GeneratorTestHelpers.RunGenerator(("Domains.cs", Domains));
        SyntaxTree tree = CSharpSyntaxTree.ParseText(consumer, new CSharpParseOptions(LanguageVersion.CSharp13));
        return output.AddSyntaxTrees(tree);
    }
}
