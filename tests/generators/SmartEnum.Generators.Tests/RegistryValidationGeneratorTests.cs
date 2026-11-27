using Microsoft.CodeAnalysis;

namespace SmartEnum.Generators.Tests;

public sealed class RegistryValidationGeneratorTests
{
    [Fact]
    public void Reports_SMARTENUM003_for_duplicate_constant_name()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static readonly OrderStatus AlsoPending = new("Pending", 2);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "SMARTENUM003");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Pending", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.NotEmpty(diagnostic.AdditionalLocations);
    }

    [Fact]
    public void Name_comparison_is_ordinal_case_sensitive()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static readonly OrderStatus pending = new("pending", 2);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "SMARTENUM003");
        Assert.DoesNotContain(result.Diagnostics, static d => d.Id is "SMARTENUM004" or "SMARTENUM005");
    }

    [Fact]
    public void Reports_SMARTENUM004_for_duplicate_constant_value()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static readonly OrderStatus Approved = new("Approved", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "SMARTENUM004");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.NotEmpty(diagnostic.AdditionalLocations);
    }

    [Theory]
    [InlineData("string", "\"A\"", "\"B\"")]
    [InlineData("long", "1L", "2L")]
    [InlineData("bool", "true", "false")]
    public void Supports_constant_TValue_types_without_false_duplicates(
        string valueType,
        string firstValue,
        string secondValue)
    {
        string source = $$"""
            using SmartEnum;

            namespace Demo;

            public sealed partial class SampleStatus : SmartEnum<SampleStatus, {{valueType}}>
            {
                public static readonly SampleStatus First = new("First", {{firstValue}});
                public static readonly SampleStatus Second = new("Second", {{secondValue}});

                private SampleStatus(string name, {{valueType}} value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id is "SMARTENUM003" or "SMARTENUM004" or "SMARTENUM005");
        Assert.Equal(0, compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Count(static d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Skips_compile_time_name_uniqueness_for_runtime_computed_name()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Left = new(Compute(), 1);
                public static readonly OrderStatus Right = new(Compute(), 2);

                private OrderStatus(string name, int value) : base(name, value) { }

                private static string Compute() => "Same";
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "SMARTENUM003");
    }

    [Fact]
    public void Skips_compile_time_value_uniqueness_for_runtime_computed_value()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Left = new("Left", Compute());
                public static readonly OrderStatus Right = new("Right", Compute());

                private OrderStatus(string name, int value) : base(name, value) { }

                private static int Compute() => 7;
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "SMARTENUM004");
    }

    [Fact]
    public void Reports_SMARTENUM005_for_empty_domain()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class EmptyStatus : SmartEnum<EmptyStatus, int>
            {
                private EmptyStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "SMARTENUM005");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Valid_domain_emits_no_registry_validation_diagnostics()
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

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(
            result.Diagnostics,
            static d => d.Id is "SMARTENUM003" or "SMARTENUM004" or "SMARTENUM005");
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
    }

    [Fact]
    public void Reports_multiple_name_conflicts_across_three_fields()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus A = new("Same", 1);
                public static readonly OrderStatus B = new("Same", 2);
                public static readonly OrderStatus C = new("Same", 3);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Equal(2, result.Diagnostics.Count(static d => d.Id == "SMARTENUM003"));
    }

    [Fact]
    public void Detects_duplicate_constant_name_across_multi_partial_files()
    {
        (string FilePath, string Source)[] sources =
        [
            ("Status.Declaration.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class SplitStatus : SmartEnum<SplitStatus, int>
                {
                    private SplitStatus(string name, int value) : base(name, value) { }
                }
                """),
            ("Status.Instances.A.cs", """
                namespace Demo;

                public sealed partial class SplitStatus
                {
                    public static readonly SplitStatus Open = new("Open", 1);
                }
                """),
            ("Status.Instances.B.cs", """
                namespace Demo;

                public sealed partial class SplitStatus
                {
                    public static readonly SplitStatus AlsoOpen = new("Open", 2);
                }
                """),
        ];

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Contains(result.Diagnostics, static d => d.Id == "SMARTENUM003");
    }

    [Theory]
    [InlineData("new(\"Pending\", 1)", "new(\"Approved\", 2)")]
    [InlineData("new(name: \"Pending\", value: 1)", "new(name: \"Approved\", value: 2)")]
    [InlineData("new(value: 1, name: \"Pending\")", "new(value: 2, name: \"Approved\")")]
    public void Named_and_positional_arguments_do_not_false_positive(string first, string second)
    {
        string source = $$"""
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = {{first}};
                public static readonly OrderStatus Approved = {{second}};

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id is "SMARTENUM003" or "SMARTENUM004");
    }

    [Fact]
    public void Named_arguments_still_detect_duplicate_name_when_value_is_first()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new(value: 1, name: "Pending");
                public static readonly OrderStatus AlsoPending = new(value: 2, name: "Pending");

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.Contains(result.Diagnostics, static d => d.Id == "SMARTENUM003");
        Assert.DoesNotContain(result.Diagnostics, static d => d.Id == "SMARTENUM004");
    }

    [Fact]
    public void Overload_selection_uses_the_bound_constructor_parameters()
    {
        const string source = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new(name: "Pending", value: 1);
                public static readonly OrderStatus Approved = new(2, "Approved");

                private OrderStatus(string name, int value) : base(name, value) { }

                private OrderStatus(int value, string name) : base(name, value) { }
            }
            """;

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, static d => d.Id is "SMARTENUM003" or "SMARTENUM004" or "SMARTENUM005");
    }
}
