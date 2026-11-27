using Microsoft.CodeAnalysis;

namespace SmartEnum.Generators.Tests;

/// <summary>
/// Regression coverage for High-severity multi-partial generator findings.
/// Expectation: exactly one generated source per domain; no duplicate AddSource.
/// </summary>
public sealed class MultiPartialGeneratorRegressionTests
{
    [Fact]
    public void Second_partial_with_unrelated_interface_produces_exactly_one_generated_source()
    {
        (string FilePath, string Source)[] sources =
        [
            ("OrderStatus.Core.cs", """
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
                """),
            ("OrderStatus.Markers.cs", """
                using System;

                namespace Demo;

                public sealed partial class OrderStatus : IComparable
                {
                    public int CompareTo(object? obj) => 0;
                }
                """),
        ];

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Equal(0, result.Diagnostics.Count(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(0, compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Count(static d => d.Severity == DiagnosticSeverity.Error));

        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("Approved", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Inheritance_and_instance_fields_in_separate_files_discover_all_instances()
    {
        (string FilePath, string Source)[] sources =
        [
            ("PaymentStatus.Declaration.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class PaymentStatus : SmartEnum<PaymentStatus, int>
                {
                    private PaymentStatus(string name, int value)
                        : base(name, value)
                    {
                    }
                }
                """),
            ("PaymentStatus.Instances.cs", """
                namespace Demo;

                public sealed partial class PaymentStatus
                {
                    public static readonly PaymentStatus Open = new("Open", 1);
                    public static readonly PaymentStatus Closed = new("Closed", 2);
                    public static readonly PaymentStatus Pending = new("Pending", 3);
                }
                """),
        ];

        (GeneratorDriverRunResult result, Compilation compilation) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));
        Assert.Equal(0, compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Count(static d => d.Severity == DiagnosticSeverity.Error));

        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        Assert.Contains("Open", generated, StringComparison.Ordinal);
        Assert.Contains("Closed", generated, StringComparison.Ordinal);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Three_partials_across_separate_files_still_emit_single_source()
    {
        (string FilePath, string Source)[] sources =
        [
            ("ShipmentStatus.Declaration.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class ShipmentStatus : SmartEnum<ShipmentStatus, int>
                {
                    private ShipmentStatus(string name, int value)
                        : base(name, value)
                    {
                    }
                }
                """),
            ("ShipmentStatus.Instances.cs", """
                namespace Demo;

                public sealed partial class ShipmentStatus
                {
                    public static readonly ShipmentStatus Created = new("Created", 1);
                    public static readonly ShipmentStatus Shipped = new("Shipped", 2);
                }
                """),
            ("ShipmentStatus.Markers.cs", """
                using System;

                namespace Demo;

                public sealed partial class ShipmentStatus : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
                """),
        ];

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        Assert.Contains("Created", generated, StringComparison.Ordinal);
        Assert.Contains("Shipped", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispose", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_partial_generation_is_deterministic_across_runs()
    {
        (string FilePath, string Source)[] sources =
        [
            ("OrderStatus.Core.cs", """
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
                """),
            ("OrderStatus.Markers.cs", """
                using System;

                namespace Demo;

                public sealed partial class OrderStatus : IComparable
                {
                    public int CompareTo(object? obj) => 0;
                }
                """),
        ];

        (GeneratorDriverRunResult first, _) = GeneratorTestHelpers.RunGenerator(sources);
        (GeneratorDriverRunResult second, _) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(first));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(second));

        string? firstSource = GeneratorTestHelpers.GetGeneratedSource(first);
        string? secondSource = GeneratorTestHelpers.GetGeneratedSource(second);

        Assert.NotNull(firstSource);
        Assert.NotNull(secondSource);
        Assert.Equal(firstSource, secondSource);
    }

    [Fact]
    public Task Multi_partial_generated_source_snapshot()
    {
        (string FilePath, string Source)[] sources =
        [
            ("OrderStatus.Declaration.cs", """
                using SmartEnum;

                namespace Demo;

                public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
                {
                    private OrderStatus(string name, int value)
                        : base(name, value)
                    {
                    }
                }
                """),
            ("OrderStatus.Instances.cs", """
                namespace Demo;

                public sealed partial class OrderStatus
                {
                    public static readonly OrderStatus Pending = new("Pending", 1);
                    public static readonly OrderStatus Approved = new("Approved", 2);
                }
                """),
            ("OrderStatus.Markers.cs", """
                using System;

                namespace Demo;

                public sealed partial class OrderStatus : IComparable
                {
                    public int CompareTo(object? obj) => 0;
                }
                """),
        ];

        (GeneratorDriverRunResult result, _) = GeneratorTestHelpers.RunGenerator(sources);

        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(1, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        string? generated = GeneratorTestHelpers.GetGeneratedSource(result);
        Assert.NotNull(generated);
        return Verify(generated);
    }
}
