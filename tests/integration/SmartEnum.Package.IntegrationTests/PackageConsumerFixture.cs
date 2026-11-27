using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SmartEnum.Package.IntegrationTests;

public sealed class PackageConsumerFixture : IAsyncLifetime
{
    public const string PackageId = "SmartEnum";
    public const string PackageVersion = "1.0.0-preview.1";

    private bool _initialized;

    public string WorkspaceRoot { get; private set; } = string.Empty;
    public string FeedDirectory { get; private set; } = string.Empty;
    public string PackagesDirectory { get; private set; } = string.Empty;
    public string ConsumerDirectory { get; private set; } = string.Empty;
    public string ConsumerProjectPath { get; private set; } = string.Empty;
    public string PackagePath { get; private set; } = string.Empty;
    public string BuildStdout { get; private set; } = string.Empty;
    public string BuildStderr { get; private set; } = string.Empty;
    public string GeneratedSource { get; private set; } = string.Empty;
    public string AnalyzerPath { get; private set; } = string.Empty;
    public string AssetsFilePath { get; private set; } = string.Empty;
    public string ConsumerOutputDirectory { get; private set; } = string.Empty;
    public string ProbeStdout { get; private set; } = string.Empty;
    public string ProbeStderr { get; private set; } = string.Empty;
    public int ProbeExitCode { get; private set; }

    public ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return ValueTask.CompletedTask;
        }

        string repoRoot = LocateRepositoryRoot();
        WorkspaceRoot = Path.Combine(Path.GetTempPath(), "smartenum-r3-" + Guid.NewGuid().ToString("N"));
        FeedDirectory = Path.Combine(WorkspaceRoot, "feed");
        PackagesDirectory = Path.Combine(WorkspaceRoot, "packages");
        ConsumerDirectory = Path.Combine(WorkspaceRoot, "consumer");
        ConsumerProjectPath = Path.Combine(ConsumerDirectory, "Consumer.csproj");
        AssetsFilePath = Path.Combine(ConsumerDirectory, "obj", "project.assets.json");

        Directory.CreateDirectory(FeedDirectory);
        Directory.CreateDirectory(PackagesDirectory);
        Directory.CreateDirectory(ConsumerDirectory);

        File.Copy(
            Path.Combine(repoRoot, "global.json"),
            Path.Combine(WorkspaceRoot, "global.json"),
            overwrite: true);

        WriteNuGetConfig();
        WriteConsumerProject();
        PackShippingPackage(repoRoot);
        RestoreAndBuildConsumer();
        CaptureAnalyzerPath();
        CaptureGeneratedSource();
        RunProbe();

        _initialized = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(WorkspaceRoot) && Directory.Exists(WorkspaceRoot))
            {
                Directory.Delete(WorkspaceRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of the disposable temp workspace.
        }

        return ValueTask.CompletedTask;
    }

    private void WriteNuGetConfig()
    {
        string configPath = Path.Combine(WorkspaceRoot, "nuget.config");
        File.WriteAllText(
            configPath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="smartenum-local" value="./feed" />
              </packageSources>
            </configuration>
            """,
            Encoding.UTF8);
    }

    private void WriteConsumerProject()
    {
        File.WriteAllText(
            ConsumerProjectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="SmartEnum" Version="1.0.0-preview.1" />
              </ItemGroup>
            </Project>
            """,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(ConsumerDirectory, "OrderStatus.cs"),
            """
            using SmartEnum;

            namespace Consumer;

            public sealed partial class OrderStatus : SmartEnum<OrderStatus, int>
            {
                public static readonly OrderStatus Pending = new("Pending", 1);
                public static readonly OrderStatus Processing = new("Processing", 2);
                public static readonly OrderStatus Completed = new("Completed", 3);

                private OrderStatus(string name, int value)
                    : base(name, value)
                {
                }
            }
            """,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(ConsumerDirectory, "Program.cs"),
            """
            using Consumer;

            int failures = 0;

            void Fail(string message)
            {
                failures++;
                Console.Error.WriteLine("PROBE_FAIL: " + message);
            }

            try
            {
                if (OrderStatus.List.Count != 3)
                {
                    Fail($"List.Count expected 3, actual {OrderStatus.List.Count}");
                }

                if (!ReferenceEquals(OrderStatus.List[0], OrderStatus.Pending)
                    || !ReferenceEquals(OrderStatus.List[1], OrderStatus.Processing)
                    || !ReferenceEquals(OrderStatus.List[2], OrderStatus.Completed))
                {
                    Fail("List order does not match Pending, Processing, Completed");
                }

                if (!ReferenceEquals(OrderStatus.FromName("Pending"), OrderStatus.Pending))
                {
                    Fail("FromName(Pending) mismatch");
                }

                if (!ReferenceEquals(OrderStatus.FromValue(2), OrderStatus.Processing))
                {
                    Fail("FromValue(2) mismatch");
                }

                if (!OrderStatus.TryFromName("Completed", out OrderStatus? byName)
                    || !ReferenceEquals(byName, OrderStatus.Completed))
                {
                    Fail("TryFromName(Completed) mismatch");
                }

                if (OrderStatus.TryFromName("Missing", out OrderStatus? missingName) || missingName is not null)
                {
                    Fail("TryFromName(Missing) should miss");
                }

                if (!OrderStatus.TryFromValue(3, out OrderStatus? byValue)
                    || !ReferenceEquals(byValue, OrderStatus.Completed))
                {
                    Fail("TryFromValue(3) mismatch");
                }

                if (OrderStatus.TryFromValue(99, out OrderStatus? missingValue) || missingValue is not null)
                {
                    Fail("TryFromValue(99) should miss");
                }

                if (!OrderStatus.ContainsName("Processing") || OrderStatus.ContainsName("Missing"))
                {
                    Fail("ContainsName hit/miss mismatch");
                }

                if (!OrderStatus.ContainsValue(1) || OrderStatus.ContainsValue(99))
                {
                    Fail("ContainsValue hit/miss mismatch");
                }

                if (failures == 0)
                {
                    Console.WriteLine("PROBE_OK");
                }
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
            }

            return failures == 0 ? 0 : 1;
            """,
            Encoding.UTF8);
    }

    private void PackShippingPackage(string repoRoot)
    {
        string packagingProject = Path.Combine(repoRoot, "src", "SmartEnum.Packaging", "SmartEnum.Packaging.csproj");
        CommandResult pack = RunDotNet(
            workingDirectory: repoRoot,
            arguments: ["pack", packagingProject, "-c", "Release", "--nologo", "-o", FeedDirectory],
            nugetPackagesDirectory: null);

        if (pack.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Packaging failed."
                + Environment.NewLine
                + FormatCommandFailure("dotnet pack", pack));
        }

        PackagePath = Path.Combine(FeedDirectory, $"{PackageId}.{PackageVersion}.nupkg");
        if (!File.Exists(PackagePath))
        {
            string listing = string.Join(
                Environment.NewLine,
                Directory.Exists(FeedDirectory)
                    ? Directory.GetFiles(FeedDirectory).Select(Path.GetFileName)
                    : ["<feed missing>"]);
            throw new InvalidOperationException(
                $"Expected package '{PackageId}.{PackageVersion}.nupkg' was not produced."
                + Environment.NewLine
                + "Feed contents:"
                + Environment.NewLine
                + listing
                + Environment.NewLine
                + FormatCommandFailure("dotnet pack", pack));
        }
    }

    private void RestoreAndBuildConsumer()
    {
        CommandResult restore = RunDotNet(
            workingDirectory: ConsumerDirectory,
            arguments: ["restore", ConsumerProjectPath, "--nologo", "--configfile", Path.Combine(WorkspaceRoot, "nuget.config")],
            nugetPackagesDirectory: PackagesDirectory);
        if (restore.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Consumer restore failed."
                + Environment.NewLine
                + FormatCommandFailure("dotnet restore", restore));
        }

        CommandResult build = RunDotNet(
            workingDirectory: ConsumerDirectory,
            arguments:
            [
                "build",
                ConsumerProjectPath,
                "-c",
                "Release",
                "--nologo",
                "--no-restore",
                "-p:EmitCompilerGeneratedFiles=true",
            ],
            nugetPackagesDirectory: PackagesDirectory);
        BuildStdout = build.Stdout;
        BuildStderr = build.Stderr;
        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Consumer build failed."
                + Environment.NewLine
                + FormatCommandFailure("dotnet build", build));
        }

        if (BuildStdout.Contains("CS0311", StringComparison.Ordinal)
            || BuildStderr.Contains("CS0311", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Consumer build reported CS0311."
                + Environment.NewLine
                + FormatCommandFailure("dotnet build", build));
        }

        ConsumerOutputDirectory = Path.Combine(ConsumerDirectory, "bin", "Release", "net10.0");
        if (!Directory.Exists(ConsumerOutputDirectory))
        {
            throw new InvalidOperationException($"Consumer output directory was not created: {ConsumerOutputDirectory}");
        }
    }

    private void CaptureAnalyzerPath()
    {
        CommandResult result = RunDotNet(
            workingDirectory: ConsumerDirectory,
            arguments:
            [
                "msbuild",
                ConsumerProjectPath,
                "-t:ResolveLockFileAnalyzers",
                "-getItem:Analyzer",
                "-nologo",
            ],
            nugetPackagesDirectory: PackagesDirectory);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Failed to read Analyzer items."
                + Environment.NewLine
                + FormatCommandFailure("dotnet msbuild -getItem:Analyzer", result));
        }

        using JsonDocument document = JsonDocument.Parse(result.Stdout);
        if (!document.RootElement.TryGetProperty("Items", out JsonElement items)
            || !items.TryGetProperty("Analyzer", out JsonElement analyzers)
            || analyzers.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Analyzer item JSON was missing."
                + Environment.NewLine
                + result.Stdout);
        }

        string? analyzer = analyzers.EnumerateArray()
            .Select(static element => element.TryGetProperty("Identity", out JsonElement identity)
                ? identity.GetString()
                : null)
            .FirstOrDefault(static path =>
                path is not null
                && path.EndsWith("SmartEnum.Generators.dll", StringComparison.OrdinalIgnoreCase));

        if (analyzer is null)
        {
            throw new InvalidOperationException(
                "SmartEnum.Generators.dll analyzer was not found in MSBuild Analyzer items."
                + Environment.NewLine
                + result.Stdout);
        }

        AnalyzerPath = Path.GetFullPath(analyzer);
    }

    private void CaptureGeneratedSource()
    {
        string generatedRoot = Path.Combine(ConsumerDirectory, "obj", "generated");
        if (!Directory.Exists(generatedRoot))
        {
            string objRoot = Path.Combine(ConsumerDirectory, "obj");
            string listing = Directory.Exists(objRoot)
                ? string.Join(
                    Environment.NewLine,
                    Directory.GetFiles(objRoot, "*.cs", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(objRoot, path)))
                : "<obj missing>";
            throw new InvalidOperationException(
                $"Generated source directory was not created: {generatedRoot}"
                + Environment.NewLine
                + "obj *.cs files:"
                + Environment.NewLine
                + listing);
        }

        string[] files = Directory.GetFiles(generatedRoot, "*.cs", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"No generated .cs files were found under {generatedRoot}");
        }

        GeneratedSource = string.Join(
            Environment.NewLine + Environment.NewLine,
            files.Select(File.ReadAllText));
    }

    private void RunProbe()
    {
        CommandResult probe = RunDotNet(
            workingDirectory: ConsumerDirectory,
            arguments: ["run", "--project", ConsumerProjectPath, "-c", "Release", "--no-build", "--nologo"],
            nugetPackagesDirectory: PackagesDirectory);
        ProbeExitCode = probe.ExitCode;
        ProbeStdout = probe.Stdout;
        ProbeStderr = probe.Stderr;
    }

    private static string LocateRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "SmartEnum.slnx");
            string packaging = Path.Combine(directory, "src", "SmartEnum.Packaging", "SmartEnum.Packaging.csproj");
            if (File.Exists(candidate) && File.Exists(packaging))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate repository root by walking up from the test base directory.");
    }

    private static CommandResult RunDotNet(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? nugetPackagesDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (nugetPackagesDirectory is not null)
        {
            startInfo.Environment["NUGET_PACKAGES"] = nugetPackagesDirectory;
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string FormatCommandFailure(string step, CommandResult result)
        => $"{step} exited with code {result.ExitCode}."
            + Environment.NewLine
            + "STDOUT:"
            + Environment.NewLine
            + result.Stdout
            + Environment.NewLine
            + "STDERR:"
            + Environment.NewLine
            + result.Stderr;

    private sealed record CommandResult(int ExitCode, string Stdout, string Stderr);
}
