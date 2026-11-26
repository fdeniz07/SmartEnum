using Microsoft.CodeAnalysis;

namespace SmartEnum.Generators.Tests;

public sealed class SmartEnumGeneratorTests
{
    private const string OrderStatusSource = """
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
        """;

    [Fact]
    public void Generates_interface_implementation_for_basic_domain()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(OrderStatusSource);

        Assert.Equal(0, result.Diagnostics.Count(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(0, compilation.GetDiagnostics(TestContext.Current.CancellationToken).Count(static d => d.Severity == DiagnosticSeverity.Error));

        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        Assert.Contains("ISmartEnumGenerated<global::Demo.OrderStatus>", generated, StringComparison.Ordinal);
        Assert.Contains("GetInstances()", generated, StringComparison.Ordinal);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("Approved", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generated_source_snapshot()
    {
        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(OrderStatusSource);
        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        await Verify(generated);
    }

    [Fact]
    public void Preserves_namespace()
    {
        const string source = """
            using SmartEnum;

            namespace Company.App.Domain;

            public sealed partial class PaymentStatus : SmartEnum<PaymentStatus, int>
            {
                public static readonly PaymentStatus Open = new("Open", 1);

                private PaymentStatus(string name, int value)
                    : base(name, value)
                {
                }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);
        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);

        Assert.NotNull(generated);
        Assert.Contains("namespace Company.App.Domain;", generated);
        Assert.Contains("ISmartEnumGenerated<global::Company.App.Domain.PaymentStatus>", generated);
    }

    [Fact]
    public void Reports_error_for_non_partial_domain()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value)
                    : base(name, value)
                {
                }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static d => d.Id == "SMARTENUM001");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(GeneratorTestHelpers.GetGeneratedSource(result));
    }

    [Fact]
    public void Reports_error_for_mutable_static_instance_field()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static OrderStatus Approved = new("Approved", 2);

                private OrderStatus(string name, int value)
                    : base(name, value)
                {
                }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(
            result.Diagnostics,
            static d => d.Id == "SMARTENUM002");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Approved", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_unrelated_static_fields()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static readonly string Label = "order";
                public static readonly int Code = 42;

                private OrderStatus(string name, int value)
                    : base(name, value)
                {
                }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Equal(0, result.Diagnostics.Count(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(0, compilation.GetDiagnostics(TestContext.Current.CancellationToken).Count(static d => d.Severity == DiagnosticSeverity.Error));

        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Label", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Code", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_compilation_implements_interface()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(OrderStatusSource);

        Assert.Equal(0, result.Diagnostics.Count(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(0, compilation.GetDiagnostics(TestContext.Current.CancellationToken).Count(static d => d.Severity == DiagnosticSeverity.Error));

        INamedTypeSymbol? orderStatus = compilation.GetTypeByMetadataName("Demo.OrderStatus");
        Assert.NotNull(orderStatus);

        INamedTypeSymbol? generatedInterface = compilation.GetTypeByMetadataName("SmartEnum.Internal.ISmartEnumGenerated`1");
        Assert.NotNull(generatedInterface);

        bool implements = orderStatus.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, generatedInterface)
            && i.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], orderStatus));

        Assert.True(implements);
    }
}
