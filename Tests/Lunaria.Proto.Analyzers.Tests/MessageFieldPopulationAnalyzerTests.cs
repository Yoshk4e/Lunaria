using System.Collections.Immutable;
using Lunaria.Proto.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Lunaria.Proto.Analyzers.Tests;

public sealed class MessageFieldPopulationAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var references = trusted
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Google.Protobuf.IMessage).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Msg.SCCaseCluePut).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new MessageFieldPopulationAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(d => d.Id == MessageFieldPopulationAnalyzer.DiagnosticId)
            .ToImmutableArray();
    }

    [Fact]
    public async Task MissingScalarField_WarnsAndNamesMissingFields()
    {
        const string source = """
            using Msg;
            class C
            {
                object M() => new SCCaseCluePut { Result = 0 };
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Single(diagnostics);
        var message = diagnostics[0].GetMessage();
        Assert.Contains("SCCaseCluePut", message);
        Assert.Contains("ClueId", message);
        Assert.Contains("CaseId", message);
        Assert.Contains("FinishedPhase", message);
    }

    [Fact]
    public async Task FullyPopulated_NoWarning()
    {
        const string source = """
            using Msg;
            class C
            {
                object M() => new SCCaseCluePut
                {
                    Result = 0,
                    ClueId = 1,
                    CaseId = 2,
                    FinishedPhase = 3,
                };
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task PostConstructionAssignment_CountsAsPopulated()
    {
        const string source = """
            using Msg;
            class C
            {
                object M()
                {
                    var m = new SCCaseCluePut { Result = 0 };
                    m.ClueId = 1;
                    m.CaseId = 2;
                    m.FinishedPhase = 3;
                    return m;
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task RepeatedCollectionInitializer_CountsAsPopulated()
    {
        const string source = """
            using Msg;
            class C
            {
                object M() => new SCCaseReceiveNtf
                {
                    CaseId = 1,
                    ClueIds = { 2UL },
                    EvidenceIds = { 3UL },
                };
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingRepeatedField_Warns()
    {
        const string source = """
            using Msg;
            class C
            {
                object M() => new SCCaseReceiveNtf { CaseId = 1 };
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Single(diagnostics);
        Assert.Contains("ClueIds", diagnostics[0].GetMessage());
        Assert.Contains("EvidenceIds", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task OneofAlternative_CountsAsPopulated()
    {
        const string source = """
            using Msg;
            class C
            {
                object M() => new PlayerAttr { AttrType = 1, ValueInt32 = 5 };
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingOneof_Warns()
    {
        const string source = """
            using Msg;
            class C
            {
                object M() => new PlayerAttr { AttrType = 1 };
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Single(diagnostics);
        Assert.Contains("oneof", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task CopyConstructor_NoWarning()
    {
        const string source = """
            using Msg;
            class C
            {
                object M(SCCaseCluePut other) => new SCCaseCluePut(other);
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NonProtoType_NoWarning()
    {
        const string source = """
            using System.Text;
            class C
            {
                object M() => new StringBuilder();
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }
}
