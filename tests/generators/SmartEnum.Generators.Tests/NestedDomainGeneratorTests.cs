using Microsoft.CodeAnalysis;

namespace SmartEnum.Generators.Tests;

public sealed class NestedDomainGeneratorTests
{
    [Fact]
    public void Nested_domain_is_rejected_and_top_level_domain_still_generates()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }

            public sealed partial class Holder
            {
                public sealed partial class Status : SmartEnum<Status, int>
                {
                    public static readonly Status One = new("One", 1);

                    private Status(string name, int value) : base(name, value) { }
                }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SMARTENUM006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Descriptor.IsEnabledByDefault);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Descriptor.DefaultSeverity);
        Assert.Contains("Demo.Holder.Status", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(
            "Status",
            diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));

        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        string generated = GeneratorTestHelpers.GetGeneratedSource(result)!;
        Assert.Contains("class OrderStatus", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("class Status", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Holder", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_domain_with_multiple_partials_reports_one_diagnostic_and_no_source()
    {
        const string first = """
            using SmartEnum;

            namespace Demo;

            public partial class Outer
            {
                public partial class Status : SmartEnum<Status, int>
                {
                    private Status(string name, int value) : base(name, value) { }
                }
            }
            """;

        const string second = """
            using SmartEnum;

            namespace Demo;

            public partial class Outer
            {
                public partial class Status
                {
                    public static readonly Status One = new("One", 1);
                    public static readonly Status Two = new("Two", 2);
                }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(
            ("First.cs", first),
            ("Second.cs", second));

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SMARTENUM006", diagnostic.Id);
        Assert.Contains("Demo.Outer.Status", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(0, GeneratorTestHelpers.GetGeneratedSourceCount(result));
    }
}
