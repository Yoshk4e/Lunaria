using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Lunaria.Proto.Analyzers.Tests;

public sealed class TrackedStateAnalyzerTests
{
    private const string Infrastructure = """
        using System;
        using System.Collections.Generic;
        using Lunaria.Common.Tracking;
        namespace Lunaria.Common.Tracking {
            public interface ITrackedState { }
            public abstract class TrackedObject : ITrackedState { }
            public sealed class TrackedAttribute : Attribute { }
            public sealed class UntrackedAttribute : Attribute { }
            public class TrackedDictionary<K,V> : ITrackedState { }
        }
        """;

    [Fact]
    public async Task OrdinaryMutablePropertyCannotSilentlyBypassTracking()
    {
        var diagnostics = await Analyze("class State : TrackedObject { public int Count { get; set; } }");
        Assert.Equal(TrackedStateAnalyzer.MissingTrackingId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task SessionOnlyMutableStateIsExplicitlyAllowed()
    {
        var diagnostics = await Analyze("class State : TrackedObject { [Untracked] public int Count { get; set; } }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MutableEntryInsideReadOnlyRecordCollectionIsRejected()
    {
        var diagnostics = await Analyze("""
            class Entry { public int Count { get; set; } }
            record Snapshot(IReadOnlyList<Entry> Entries);
            class State : TrackedObject {
                [Tracked] public TrackedDictionary<int, Snapshot> Values { get; }
            }
            """);
        Assert.Equal(TrackedStateAnalyzer.MutableValueId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task ImmutableRecordsAndTrackedChildrenAreAllowed()
    {
        var diagnostics = await Analyze("""
            record Snapshot(int Count, IReadOnlyList<int> Values);
            class Entry : TrackedObject { [Tracked] public int Count { get; set; } }
            class State : TrackedObject {
                [Tracked] public TrackedDictionary<int, Snapshot> Records { get; }
                [Tracked] public TrackedDictionary<int, Entry> Children { get; }
            }
            """);
        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("TrackingAnalysis",
            [CSharpSyntaxTree.ParseText(Infrastructure + source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        return await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TrackedStateAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }
}
