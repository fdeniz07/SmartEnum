using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SmartEnum.Generators;

namespace SmartEnum.Generators.Tests;

internal static class GeneratorTestHelpers
{
    public static (GeneratorDriverRunResult Result, Compilation OutputCompilation) RunGenerator(string source)
        => RunGenerator(("Test.cs", source));

    public static (GeneratorDriverRunResult Result, Compilation OutputCompilation) RunGenerator(
        params (string FilePath, string Source)[] sources)
    {
        if (sources is null || sources.Length == 0)
        {
            throw new ArgumentException("At least one source file is required.", nameof(sources));
        }

        CSharpParseOptions parseOptions = new(LanguageVersion.CSharp13);
        var syntaxTrees = new SyntaxTree[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            syntaxTrees[i] = CSharpSyntaxTree.ParseText(
                sources[i].Source,
                parseOptions,
                path: sources[i].FilePath);
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: syntaxTrees,
            references: CreateMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new SmartEnumGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out _);
        return (driver.GetRunResult(), outputCompilation);
    }

    public static int GetGeneratedSourceCount(GeneratorDriverRunResult result)
    {
        if (result.Results.Length == 0)
        {
            return 0;
        }

        return result.Results[0].GeneratedSources.Length;
    }

    public static string? GetGeneratedSource(GeneratorDriverRunResult result)
    {
        if (result.Results.Length == 0 || result.Results[0].GeneratedSources.Length == 0)
        {
            return null;
        }

        return string.Join(
            "\n\n",
            result.Results[0].GeneratedSources.Select(static s => s.SourceText.ToString().Replace("\r\n", "\n")));
    }

    public static Exception? GetGeneratorException(GeneratorDriverRunResult result)
    {
        if (result.Results.Length == 0)
        {
            return null;
        }

        return result.Results[0].Exception;
    }

    public static ImmutableArray<Diagnostic> GeneratorDiagnostics(GeneratorDriverRunResult result)
        => result.Diagnostics;

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>();
        builder.Add(MetadataReference.CreateFromFile(typeof(SmartEnum.SmartEnum<,>).Assembly.Location));

        string? tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrWhiteSpace(tpa))
        {
            foreach (string path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }
        else
        {
            builder.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            builder.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            builder.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        }

        return builder.ToImmutable();
    }
}
