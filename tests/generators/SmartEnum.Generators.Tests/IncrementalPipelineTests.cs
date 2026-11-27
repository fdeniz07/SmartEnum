using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SmartEnum.Generators.Tests;

public sealed class IncrementalPipelineTests
{
    [Fact]
    public void Initial_run_processes_two_independent_domains()
    {
        (GeneratorDriver driver, _) = GeneratorTestHelpers.RunTracked(
            ("OrderStatus.cs", OrderStatus("Pending")),
            ("ProductStatus.cs", ProductStatus()));

        GeneratorDriverRunResult result = driver.GetRunResult();
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Assert.Equal(2, GeneratorTestHelpers.GetGeneratedSourceCount(result));

        Dictionary<string, IncrementalStepRunReason> models = DomainReasons(driver);
        Assert.Equal(IncrementalStepRunReason.New, models["Demo.OrderStatus"]);
        Assert.Equal(IncrementalStepRunReason.New, models["Demo.ProductStatus"]);
        Dictionary<string, IncrementalStepRunReason> sources = SourceReasonsByDomain(driver);
        Assert.Equal(IncrementalStepRunReason.New, sources["Demo.OrderStatus"]);
        Assert.Equal(IncrementalStepRunReason.New, sources["Demo.ProductStatus"]);

        string generated = GeneratorTestHelpers.GetGeneratedSource(result)!;
        Assert.Contains("class OrderStatus", generated, StringComparison.Ordinal);
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("class ProductStatus", generated, StringComparison.Ordinal);
        Assert.Contains("Active", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Changing_one_domain_leaves_the_other_cached()
    {
        (GeneratorDriver driver, Compilation compilation) = GeneratorTestHelpers.RunTracked(
            ("OrderStatus.cs", OrderStatus("Pending")),
            ("ProductStatus.cs", ProductStatus()));

        compilation = Replace(compilation, "OrderStatus.cs", OrderStatus("Pending", "Cancelled"));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        GeneratorDriverRunResult result = driver.GetRunResult();
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(result));
        Dictionary<string, IncrementalStepRunReason> models = DomainReasons(driver);
        Assert.Equal(IncrementalStepRunReason.Modified, models["Demo.OrderStatus"]);
        Assert.Equal(IncrementalStepRunReason.Unchanged, models["Demo.ProductStatus"]);

        Dictionary<string, IncrementalStepRunReason> sources = SourceReasonsByDomain(driver);
        Assert.Equal(IncrementalStepRunReason.Modified, sources["Demo.OrderStatus"]);
        Assert.Equal(IncrementalStepRunReason.Cached, sources["Demo.ProductStatus"]);

        string generated = GeneratorTestHelpers.GetGeneratedSource(result)!;
        Assert.Contains("Pending", generated, StringComparison.Ordinal);
        Assert.Contains("Cancelled", generated, StringComparison.Ordinal);
        Assert.Contains("Active", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Changing_a_non_owner_partial_updates_only_that_domain()
    {
        const string owner = """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);

                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;
        const string partial = """
            namespace Demo;

            public sealed partial class OrderStatus
            {
                public static readonly OrderStatus Approved = new("Approved", 2);
            }
            """;
        const string partialWithShipped = """
            namespace Demo;

            public sealed partial class OrderStatus
            {
                public static readonly OrderStatus Approved = new("Approved", 2);
                public static readonly OrderStatus Shipped = new("Shipped", 3);
            }
            """;

        (GeneratorDriver driver, Compilation compilation) = GeneratorTestHelpers.RunTracked(
            ("A.OrderStatus.cs", owner),
            ("B.OrderStatus.cs", partial),
            ("ProductStatus.cs", ProductStatus()));

        GeneratorDriverRunResult first = driver.GetRunResult();
        string firstSource = GeneratorTestHelpers.GetGeneratedSource(first)!;
        Assert.Contains("Pending", firstSource, StringComparison.Ordinal);
        Assert.Contains("Approved", firstSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Shipped", firstSource, StringComparison.Ordinal);

        compilation = Replace(compilation, "B.OrderStatus.cs", partialWithShipped);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        GeneratorDriverRunResult second = driver.GetRunResult();
        Assert.Null(GeneratorTestHelpers.GetGeneratorException(second));
        Assert.Equal(2, GeneratorTestHelpers.GetGeneratedSourceCount(second));

        Dictionary<string, IncrementalStepRunReason> models = DomainReasons(driver);
        Assert.Equal(IncrementalStepRunReason.Modified, models["Demo.OrderStatus"]);
        Assert.Equal(IncrementalStepRunReason.Unchanged, models["Demo.ProductStatus"]);
        Dictionary<string, IncrementalStepRunReason> sources = SourceReasonsByDomain(driver);
        Assert.Equal(IncrementalStepRunReason.Modified, sources["Demo.OrderStatus"]);
        Assert.Equal(IncrementalStepRunReason.Cached, sources["Demo.ProductStatus"]);

        string generated = GeneratorTestHelpers.GetGeneratedSource(second)!;
        Assert.Contains("Pending, Approved, Shipped", generated, StringComparison.Ordinal);
        Assert.Contains("Active", generated, StringComparison.Ordinal);
    }

    private static Dictionary<string, IncrementalStepRunReason> DomainReasons(GeneratorDriver driver)
    {
        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        Assert.True(result.TrackedSteps.TryGetValue("SmartEnum.DomainModel", out ImmutableArray<IncrementalGeneratorRunStep> steps));

        var reasons = new Dictionary<string, IncrementalStepRunReason>(StringComparer.Ordinal);
        foreach (IncrementalGeneratorRunStep step in steps)
        {
            foreach ((object value, IncrementalStepRunReason reason) in step.Outputs)
            {
                if (reason == IncrementalStepRunReason.Removed)
                {
                    continue;
                }

                reasons[value.ToString()!] = reason;
            }
        }

        return reasons;
    }

    private static Dictionary<string, IncrementalStepRunReason> SourceReasonsByDomain(GeneratorDriver driver)
    {
        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        Assert.True(result.TrackedSteps.TryGetValue("SourceOutput", out ImmutableArray<IncrementalGeneratorRunStep> steps));

        var reasons = new Dictionary<string, IncrementalStepRunReason>(StringComparer.Ordinal);
        foreach (IncrementalGeneratorRunStep step in steps)
        {
            Assert.Equal(step.Inputs.Length, step.Outputs.Length);
            for (int i = 0; i < step.Outputs.Length; i++)
            {
                (_, IncrementalStepRunReason reason) = step.Outputs[i];
                if (reason == IncrementalStepRunReason.Removed)
                {
                    continue;
                }

                (IncrementalGeneratorRunStep inputStep, int outputIndex) = step.Inputs[i];
                (object model, _) = inputStep.Outputs[outputIndex];
                reasons[model.ToString()!] = reason;
            }
        }

        return reasons;
    }

    private static Compilation Replace(Compilation compilation, string path, string source)
    {
        SyntaxTree tree = compilation.SyntaxTrees.Single(candidate => candidate.FilePath == path);
        return compilation.ReplaceSyntaxTree(tree, tree.WithChangedText(SourceText.From(source, Encoding.UTF8)));
    }

    private static string OrderStatus(params string[] names)
    {
        StringBuilder fields = new();
        for (int i = 0; i < names.Length; i++)
        {
            fields.Append("        public static readonly OrderStatus ");
            fields.Append(names[i]);
            fields.Append(" = new(\"");
            fields.Append(names[i]);
            fields.Append("\", ");
            fields.Append(i + 1);
            fields.AppendLine(");");
        }

        return """
            using SmartEnum;

            namespace Demo;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
            """ + fields + """
                private OrderStatus(string name, int value) : base(name, value) { }
            }
            """;
    }

    private static string ProductStatus()
        => """
            using SmartEnum;

            namespace Demo;

            public sealed partial class ProductStatus : SmartEnum<ProductStatus, int>
            {
                public static readonly ProductStatus Active = new("Active", 1);

                private ProductStatus(string name, int value) : base(name, value) { }
            }
            """;
}
