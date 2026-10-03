using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lunaria.Proto.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TrackedStateAnalyzer : DiagnosticAnalyzer
{
    public const string MissingTrackingId = "LUNTRACK001";
    public const string MutableValueId = "LUNTRACK002";
    private static readonly DiagnosticDescriptor Missing = new(MissingTrackingId,
        "Declare how mutable state is tracked", "Mutable member '{0}' must use a [Tracked] property or be explicitly [Untracked]",
        "Persistence", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Mutable = new(MutableValueId,
        "Unsupported mutable tracked value", "Tracked member '{0}' contains mutable type '{1}'; use tracked state or an immutable value",
        "Persistence", DiagnosticSeverity.Error, true);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Missing, Mutable);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Field, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var member = context.Symbol;
        if (member.IsImplicitlyDeclared || member.IsStatic || member.Name.StartsWith("__tracked")
            || !TrackedObject(member.ContainingType) || Has(member, "UntrackedAttribute")) return;
        if (member is IPropertySymbol property)
        {
            if (Has(member, "TrackedAttribute"))
            {
                if (Unsupported(property.Type, ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default)) is {} bad)
                    context.ReportDiagnostic(Diagnostic.Create(Mutable, member.Locations[0], member.Name, bad.ToDisplayString()));
            }
            else if (property.SetMethod is { IsInitOnly: false })
                context.ReportDiagnostic(Diagnostic.Create(Missing, member.Locations[0], member.Name));
        }
        else if (member is IFieldSymbol field && (!field.IsReadOnly || MutableCollection(field.Type)))
            context.ReportDiagnostic(Diagnostic.Create(Missing, member.Locations[0], member.Name));
    }

    private static bool Has(ISymbol member, string name) => member.GetAttributes().Any(a =>
        a.AttributeClass?.ToDisplayString() == "Lunaria.Common.Tracking." + name);
    private static bool Tracked(ITypeSymbol? type) => type is not null && type.AllInterfaces.Any(i =>
        i.ToDisplayString() == "Lunaria.Common.Tracking.ITrackedState");
    private static bool TrackedObject(INamedTypeSymbol? type)
    {
        for (var current = type?.BaseType; current is not null; current = current.BaseType)
            if (current.ToDisplayString() == "Lunaria.Common.Tracking.TrackedObject") return true;
        return false;
    }
    private static bool MutableCollection(ITypeSymbol type) => type is IArrayTypeSymbol
        || type is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
            && new[] { "List", "Dictionary", "SortedDictionary", "HashSet", "SortedSet", "Queue", "Stack", "ICollection", "IDictionary", "IList", "ISet" }.Contains(named.Name);

    private static ITypeSymbol? Unsupported(ITypeSymbol type, ImmutableHashSet<ITypeSymbol> visited)
    {
        if (visited.Contains(type)) return null;
        visited = visited.Add(type);
        if (MutableCollection(type)) return type;
        if (type is not INamedTypeSymbol named) return null;
        if (Tracked(type))
        {
            // A tracked collection can still contain untracked mutable values inside records.
            foreach (var argument in named.TypeArguments)
                if (Unsupported(argument, visited) is {} bad) return bad;
            return null;
        }
        if (named.TypeKind == TypeKind.Delegate) return named;
        foreach (var argument in named.TypeArguments)
            if (Unsupported(argument, visited) is {} bad) return bad;
        if (named.SpecialType != SpecialType.None || named.ContainingNamespace.ToDisplayString().StartsWith("System")) return null;
        foreach (var field in named.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsImplicitlyDeclared && !f.IsStatic))
            if (!field.IsReadOnly || Unsupported(field.Type, visited) is not null) return named;
        foreach (var property in named.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic))
        {
            if (property.SetMethod is { IsInitOnly: false }) return named;
            if (Unsupported(property.Type, visited) is {} bad) return bad;
        }
        return null;
    }
}
