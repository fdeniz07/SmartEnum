using Microsoft.CodeAnalysis;

namespace SmartEnum.Generators.Tests;

public sealed class ConstructorBindingGeneratorTests
{
    [Fact]
    public void Named_argument_followed_by_positional_argument_binds_the_positional_value()
    {
        (GeneratorDriverRunResult names, Compilation nameCompilation) = Compile(
            """
            public static readonly OrderStatus Pending = new(name: "Same", 1);
            public static readonly OrderStatus Approved = new(name: "Same", 2);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertBound(nameCompilation, names, "SMARTENUM003");

        (GeneratorDriverRunResult values, Compilation valueCompilation) = Compile(
            """
            public static readonly OrderStatus Pending = new(name: "Pending", 1);
            public static readonly OrderStatus Approved = new(name: "Approved", 1);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertBound(valueCompilation, values, "SMARTENUM004");
    }

    [Fact]
    public void Positional_argument_followed_by_named_argument_binds_both_parameters()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus Pending = new("Same", value: 1);
            public static readonly OrderStatus Approved = new("Same", value: 2);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertBound(compilation, result, "SMARTENUM003");
    }

    [Fact]
    public void Named_arguments_in_reversed_order_bind_by_parameter_name()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus Pending = new(value: 1, name: "Pending");
            public static readonly OrderStatus Approved = new(value: 1, name: "Approved");

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertBound(compilation, result, "SMARTENUM004");
    }

    [Fact]
    public void Explicit_object_creation_uses_the_bound_constructor_arguments()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus Pending = new OrderStatus(name: "Same", 1);
            public static readonly OrderStatus Approved = new OrderStatus(name: "Same", 2);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertBound(compilation, result, "SMARTENUM003");
    }

    [Fact]
    public void Target_typed_new_uses_the_bound_constructor_arguments()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus Pending = new(name: "Pending", 1);
            public static readonly OrderStatus Approved = new(name: "Approved", 2);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertNoCompilerErrors(compilation);
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id is "SMARTENUM003" or "SMARTENUM004");
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
    }

    [Fact]
    public void Selected_constructor_overload_determines_which_parameters_are_read()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus FromValue = new(name: "FromValue", 1);
            public static readonly OrderStatus AlsoOne = new(name: "AlsoOne", 1);
            public static readonly OrderStatus FromLabel = new(name: "FromLabel", "same");
            public static readonly OrderStatus AlsoLabel = new(label: "same", name: "AlsoLabel");

            private OrderStatus(string name, int value) : base(name, value) { }

            private OrderStatus(string name, string label) : base(name, label.Length) { }
            """);

        AssertNoCompilerErrors(compilation);
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SMARTENUM004", diagnostic.Id);
        Assert.Contains("'FromValue'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'AlsoOne'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Omitted_optional_parameter_uses_its_explicit_constant_default()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus First = new();
            public static readonly OrderStatus Second = new();

            private OrderStatus(string name = "Default", int value = 1) : base(name, value) { }
            """);

        AssertNoCompilerErrors(compilation);
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "SMARTENUM003");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "SMARTENUM004");
    }

    [Fact]
    public void Runtime_computed_arguments_stay_outside_compile_time_duplicate_diagnostics()
    {
        (GeneratorDriverRunResult result, Compilation compilation) = Compile(
            """
            public static readonly OrderStatus First = new(ComputeName(), 1);
            public static readonly OrderStatus Second = new(ComputeName(), 2);
            public static readonly OrderStatus Third = new("Third", ComputeValue());
            public static readonly OrderStatus Fourth = new("Fourth", ComputeValue());

            private static string ComputeName() => "Pending";
            private static int ComputeValue() => 1;

            private OrderStatus(string name, int value) : base(name, value) { }
            """);

        AssertNoCompilerErrors(compilation);
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id is "SMARTENUM003" or "SMARTENUM004");
    }

    [Fact]
    public void Duplicate_constant_names_and_values_are_still_detected()
    {
        (GeneratorDriverRunResult names, Compilation nameCompilation) = Compile(
            """
            public static readonly OrderStatus Pending = new("Same", 1);
            public static readonly OrderStatus Approved = new("Same", 2);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);
        AssertBound(nameCompilation, names, "SMARTENUM003");

        (GeneratorDriverRunResult values, Compilation valueCompilation) = Compile(
            """
            public static readonly OrderStatus Pending = new("Pending", 1);
            public static readonly OrderStatus Approved = new("Approved", 1);

            private OrderStatus(string name, int value) : base(name, value) { }
            """);
        AssertBound(valueCompilation, values, "SMARTENUM004");
    }

    private static (GeneratorDriverRunResult Result, Compilation Output) Compile(string body)
    {
        string source = $$"""
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
            {{body}}
            }
            """;

        return GeneratorTestHelpers.RunGenerator(source);
    }

    private static void AssertBound(Compilation compilation, GeneratorDriverRunResult result, string diagnosticId)
    {
        AssertNoCompilerErrors(compilation);
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        string other = diagnosticId == "SMARTENUM003" ? "SMARTENUM004" : "SMARTENUM003";
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == other);
    }

    private static void AssertNoCompilerErrors(Compilation compilation)
    {
        Diagnostic[] errors = compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && !diagnostic.Id.StartsWith("SMARTENUM", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(errors);
    }
}
