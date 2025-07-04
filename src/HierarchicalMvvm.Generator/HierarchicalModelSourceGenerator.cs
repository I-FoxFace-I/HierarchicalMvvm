using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections;
using System.Data;
using System.Numerics;
using System.Reflection;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Text.RegularExpressions;
using System;


namespace HierarchicalMvvm.Generator;

[Generator]
public class HierarchicalModelSourceGenerator : IIncrementalGenerator
{
    private static HashSet<string> PrimitiveTypes = new HashSet<string>
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "nint", "nuint",
        "long", "ulong", "short", "ushort", "string", "DateTime", "DateTimeOffset", "Guid", "Index",
        "TimeSpan", "Version", "Type", "Uri", "TimeOnly", "DateOnly", "Half", "BigInteger", "Complex",
        "Range", "Rune"
    };

    private static HashSet<string> CollectionTypes = new HashSet<string>
    {
        "IEnumerable", "ICollection", "IReadOnlyCollection", "IList", "IReadOnlyList",
        "IImmutableSet", "ISet", "IReadOnlySet", "IImmutableSet", "ILookup", "IDictionary",
        "IReadOnlyDictionary", "List", "ReadOnlyCollection", "ReadOnlyList", "ImmutableList",
        "HashSet", "ReadOnlySet", "ImmutableHashSet", "GroupTable", "Dictionary", "ReadOnlyDictionary",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        var compilationAndTypes = context.CompilationProvider
            .Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndTypes, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        // Pouze třídy s attributy, které by mohly obsahovat ModelWrapperAttribute
        if (node is not ClassDeclarationSyntax { AttributeLists.Count: > 0 } classDeclaration)
            return false;

        // Rychlá kontrola jestli obsahuje "ModelWrapper" - bez semantic modelu
        return classDeclaration.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr => attr.Name.ToString().Contains("ModelWrapper"));
    }

    private static INamedTypeSymbol? TryGetWrappedTypeFromBase(INamedTypeSymbol? type)
    {
        var baseType = type?.BaseType;

        if (baseType == null || baseType.Name == "Object")
            return null;

        var attr = baseType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "ModelWrapperAttribute");

        if (attr != null &&
            attr.ConstructorArguments.Length == 1 &&
            attr.ConstructorArguments[0].Value is INamedTypeSymbol baseWrappedType)
        {
            return baseWrappedType;
        }

        return null;
    }

    private static ModelGenerationInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);

                if (symbolInfo.Symbol is IMethodSymbol attributeConstructor && attributeConstructor.ContainingType.Name == "ModelWrapperAttribute")
                {
                    var targetType = GetTargetTypeFromAttribute(context, attribute);

                    var generatedType = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                    var baseWrapperType = TryGetWrappedTypeFromBase(generatedType);

                    if (targetType is not null)
                    {
                        return new ModelGenerationInfo
                        {
                            IsDerived = baseWrapperType != null,
                            IsAbstract = targetType.IsAbstract,
                            ClassDeclaration = classDeclaration,
                            TargetWrapperType = targetType,
                            SemanticModel = context.SemanticModel,
                            BaseType = generatedType?.BaseType,
                            BaseWrapperType = baseWrapperType,
                            GeneratedType = generatedType!
                        };
                    }
                }
            }
        }

        return null;
    }

    private static INamedTypeSymbol? GetTargetTypeFromAttribute(GeneratorSyntaxContext context, AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            var argument = attribute.ArgumentList.Arguments[0];

            if (argument.Expression is TypeOfExpressionSyntax typeOfExpression)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(typeOfExpression.Type);
                return typeInfo.Type as INamedTypeSymbol;
            }
        }
        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ModelGenerationInfo?> modelInfos, SourceProductionContext context)
    {
        if (modelInfos.IsDefaultOrEmpty) return;

        var validModelInfos = modelInfos.Where(m => m is not null).ToArray();
        if (validModelInfos.Length == 0) return;

        DiagnosticHelper.LogInfo(context, $"Processing {validModelInfos.Length} model(s)");

        try
        {
            // Topologický sort podle závislostí
            var sortedModels = TopologicalSorter.SortByDependencies(validModelInfos!);
            
            if (TopologicalSorter.HasCyclicDependencies(validModelInfos!))
            {
                DiagnosticHelper.LogWarning(context, "Cyclic dependencies detected, using original order");
                sortedModels = validModelInfos!.ToList();
            }

            var namespaceMapping = BuildNamespaceMapping(sortedModels);

            foreach (var modelInfo in sortedModels)
            {
                try
                {
                    var properties = PropertyAnalyzer.GetProperties(modelInfo.TargetWrapperType, namespaceMapping);
                    var generator = new CodeGenerator(modelInfo, properties, namespaceMapping, context);
                    var sourceCode = generator.Generate();
                    
                    var className = modelInfo.ClassDeclaration.Identifier.ValueText;
                    context.AddSource($"{className}.g.cs", sourceCode);
                    
                    DiagnosticHelper.LogInfo(context, $"Successfully generated {className}");
                }
                catch (Exception ex)
                {
                    var className = modelInfo.ClassDeclaration.Identifier.ValueText;
                    DiagnosticHelper.LogError(context, $"Failed to generate {className}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticHelper.LogError(context, $"Error during generation: {ex.Message}");
        }
    }

    private static Dictionary<string, string> BuildNamespaceMapping(List<ModelGenerationInfo> modelInfos)
    {
        var mapping = new Dictionary<string, string>();

        foreach (var modelInfo in modelInfos)
        {
            var modelClassName = modelInfo.ClassDeclaration.Identifier.ValueText;
            var modelNamespace = SyntaxHelper.GetNamespace(modelInfo.ClassDeclaration);
            var targetTypeSimpleName = modelInfo.TargetWrapperType.Name;

            var fullModelTypeName = string.IsNullOrEmpty(modelNamespace)
                ? modelClassName
                : $"{modelNamespace}.{modelClassName}";

            mapping[targetTypeSimpleName] = fullModelTypeName;
        }

        return mapping;
    }
}

// Helper classes
/*
public class ModelGenerationInfo
{
    public bool IsDerived { get; set; }
    public bool IsAbstract { get; set; }
    public INamedTypeSymbol? BaseType { get; set; }
    public INamedTypeSymbol? BaseWrapperType { get; set; }
    public INamedTypeSymbol GeneratedType { get; set; } = null!;
    public INamedTypeSymbol TargetWrapperType { get; set; } = null!;
    public SemanticModel SemanticModel { get; set; } = null!;
    public ClassDeclarationSyntax ClassDeclaration { get; set; } = null!;
}


public class TypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
}

public class PropertyInfo
{
    public bool IsReadOnly { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? CollectionElementType { get; set; }
    public string? FullModelTypeName { get; set; }

    public PropertyKind Kind { get; set; }
    public INamedTypeSymbol Type { get; set; } = null!;
    public INamedTypeSymbol? EllementType { get; set; }
}

public enum PropertyKind
{
    Primitive,
    Collection,
    ModelObject,
    ModelCollection,
}*/