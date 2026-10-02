using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Lunaria.Proto.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MessageFieldPopulationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "LUNPROTO001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Explicitly populate protobuf message fields",
        "Protobuf message '{0}' does not explicitly populate: {1}",
        "Protocol",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Handle every protobuf field explicitly, including intentional defaults. Populate one alternative for each oneof group.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start => {
            var messageInterface = start.Compilation.GetTypeByMetadataName("Google.Protobuf.IMessage");
            if (messageInterface is null) return;

            var schemas = new ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<FieldGroup>>(SymbolEqualityComparer.Default);
            start.RegisterOperationAction(operationContext => {
                var creation = (IObjectCreationOperation)operationContext.Operation;
                if (creation.Type is not INamedTypeSymbol type
                    || !type.AllInterfaces.Contains(messageInterface, SymbolEqualityComparer.Default)) return;

                // The generated copy constructor carries the existing message's fields.
                if (creation.Arguments.Length == 1
                    && SymbolEqualityComparer.Default.Equals(creation.Arguments[0].Parameter?.Type, type)) return;

                var fields = schemas.GetOrAdd(type, ReadFields);
                if (fields.IsEmpty) return;

                var populated = new HashSet<string>(StringComparer.Ordinal);
                if (creation.Initializer is {} initializer)
                {
                    foreach (var entry in initializer.Initializers)
                    {
                        switch (entry)
                        {
                            case ISimpleAssignmentOperation assignment
                                when assignment.Target is IPropertyReferenceOperation property:
                                populated.Add(property.Property.Name);
                                break;
                            case IMemberInitializerOperation member:
                                if (member.InitializedMember is IPropertyReferenceOperation memberProperty)
                                    populated.Add(memberProperty.Property.Name);
                                else if (member.InitializedMember is ISymbol memberSymbol)
                                    populated.Add(memberSymbol.Name);
                                break;
                        }
                    }
                }

                if (AssignedLocal(creation) is {} local)
                    CollectLocalWrites(creation, local, populated, fields, operationContext.CancellationToken);

                var missing = fields.Where(field => !field.Names.Any(populated.Contains)).Select(field => field.Display).ToArray();
                if (missing.Length > 0)
                    operationContext.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), type.Name, string.Join(", ", missing)));
            }, OperationKind.ObjectCreation);
        });
    }

    private static ImmutableArray<FieldGroup> ReadFields(INamedTypeSymbol type)
    {
        const string suffix = "FieldNumber";
        var fields = type.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.IsConst && field.Type.SpecialType == SpecialType.System_Int32
                && field.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Select(field => (Name: field.Name.Substring(0, field.Name.Length - suffix.Length), Number: (int)field.ConstantValue!))
            .Where(field => type.GetMembers(field.Name).OfType<IPropertySymbol>().Any(property => !property.IsStatic))
            .OrderBy(field => field.Number).ToArray();

        var oneofs = new Dictionary<int, FieldGroup>();
        foreach (var oneof in type.GetTypeMembers().Where(nested => nested.TypeKind == TypeKind.Enum
                     && nested.Name.EndsWith("OneofCase", StringComparison.Ordinal)))
        {
            var numbers = new HashSet<int>(oneof.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.HasConstantValue && field.ConstantValue is int number && number != 0)
                .Select(field => (int)field.ConstantValue!));
            var names = fields.Where(field => numbers.Contains(field.Number)).Select(field => field.Name).ToArray();
            if (names.Length == 0) continue;
            var groupName = oneof.Name.Substring(0, oneof.Name.Length - "OneofCase".Length);
            var clearMethods = new string[names.Length + 1];
            clearMethods[0] = "Clear" + groupName;
            for (var i = 0; i < names.Length; i++) clearMethods[i + 1] = "Clear" + names[i];
            var group = new FieldGroup(groupName + " (oneof: " + string.Join(" / ", names) + ")", names, clearMethods);
            foreach (var number in numbers) oneofs[number] = group;
        }

        var result = ImmutableArray.CreateBuilder<FieldGroup>();
        var seen = new HashSet<FieldGroup>();
        foreach (var field in fields)
        {
            var group = oneofs.TryGetValue(field.Number, out var oneof) ? oneof
                : new FieldGroup(field.Name, [field.Name], ["Clear" + field.Name]);
            if (seen.Add(group)) result.Add(group);
        }
        return result.ToImmutable();
    }

    private static ILocalSymbol? AssignedLocal(IObjectCreationOperation creation)
    {
        var value = SkipParentConversions(creation);
        return value.Parent switch {
            IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
            ISimpleAssignmentOperation { Target: ILocalReferenceOperation reference } assignment when assignment.Value == value => reference.Local,
            _ => null
        };
    }

    private static void CollectLocalWrites(IObjectCreationOperation creation, ILocalSymbol local, HashSet<string> populated,
        ImmutableArray<FieldGroup> fields, CancellationToken cancellationToken)
    {
        IOperation scope = creation;
        while (scope.Parent is {} parent && parent is not IAnonymousFunctionOperation and not ILocalFunctionOperation)
            scope = parent;

        var operations = Walk(scope).Where(operation => operation.Syntax.SpanStart >= creation.Syntax.Span.End).ToArray();
        var cutoff = int.MaxValue;
        foreach (var reference in operations.OfType<ILocalReferenceOperation>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SymbolEqualityComparer.Default.Equals(reference.Local, local)) continue;
            var value = SkipParentConversions(reference);
            if (value.Parent is IPropertyReferenceOperation property && property.Instance == value) continue;
            if (value.Parent is IInvocationOperation invocation && invocation.Instance == value
                && fields.Any(field => field.HandlesClear(invocation.TargetMethod.Name))) continue;

            // Do not credit writes after return, reassignment, copying, or passing the message to another method.
            cutoff = Math.Min(cutoff, reference.Syntax.SpanStart);
        }

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Syntax.SpanStart >= cutoff) continue;
            if (operation is IAssignmentOperation assignment && DirectProperty(assignment.Target, local) is {} assigned)
                populated.Add(assigned.Name);
            else if (operation is IInvocationOperation invocation)
            {
                if (invocation.TargetMethod.Name is "Add" or "AddRange" or "Clear"
                    && invocation.Instance is IPropertyReferenceOperation collection
                    && IsProtobufCollection(collection.Property.Type)
                    && DirectProperty(collection, local) is {} property)
                    populated.Add(property.Name);
                else if (IsLocal(invocation.Instance, local))
                {
                    foreach (var field in fields.Where(field => field.HandlesClear(invocation.TargetMethod.Name)))
                        populated.UnionWith(field.Names);
                }
            }
        }
    }

    private static IPropertySymbol? DirectProperty(IOperation target, ILocalSymbol local)
    {
        if (target is not IPropertyReferenceOperation property) return null;
        if (property.Property.IsIndexer && property.Instance is IPropertyReferenceOperation collection
            && IsProtobufCollection(collection.Property.Type)) property = collection;
        return IsLocal(property.Instance, local) ? property.Property : null;
    }

    private static bool IsProtobufCollection(ITypeSymbol? type) => type is INamedTypeSymbol named
        && named.ContainingNamespace.ToDisplayString() == "Google.Protobuf.Collections"
        && named.Name is "RepeatedField" or "MapField";

    private static bool IsLocal(IOperation? operation, ILocalSymbol local)
    {
        while (operation is IConversionOperation conversion) operation = conversion.Operand;
        return operation is ILocalReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Local, local);
    }

    private static IOperation SkipParentConversions(IOperation operation)
    {
        while (operation.Parent is IConversionOperation or IParenthesizedOperation) operation = operation.Parent;
        return operation;
    }

    private static IEnumerable<IOperation> Walk(IOperation root)
    {
        var pending = new Stack<IOperation>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            yield return operation;
            if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            foreach (var child in operation.ChildOperations) pending.Push(child);
        }
    }

    private sealed class FieldGroup(string display, string[] names, string[] clearMethods)
    {
        public string Display { get; } = display;
        public string[] Names { get; } = names;
        public string[] ClearMethods { get; } = clearMethods;

        public bool HandlesClear(string methodName)
        {
            foreach (var clear in ClearMethods)
            {
                if (string.Equals(clear, methodName, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
