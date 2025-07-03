using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections;
using System.Data;
using System.Numerics;
using System.Reflection;

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

                    if (targetType is not null)
                    {
                        return new ModelGenerationInfo
                        {
                            ClassDeclaration = classDeclaration,
                            TargetType = targetType,
                            SemanticModel = context.SemanticModel
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

        var namespaceMapping = BuildNamespaceMapping(validModelInfos!);

        foreach (var modelInfo in validModelInfos)
        {
            GenerateModel(modelInfo!, namespaceMapping, context);
        }
    }

    private static string GetNamespace(ClassDeclarationSyntax classDeclaration)
    {
        var namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

        if (namespaceDeclaration is not null)
            return namespaceDeclaration.Name.ToString();

        var fileScopedNamespace = classDeclaration.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();

        return fileScopedNamespace?.Name.ToString() ?? string.Empty;
    }

    private static Dictionary<string, string> BuildNamespaceMapping(ModelGenerationInfo[] modelInfos)
    {
        var mapping = new Dictionary<string, string>();

        foreach (var modelInfo in modelInfos)
        {
            var modelClassName = modelInfo.ClassDeclaration.Identifier.ValueText;

            var modelNamespace = GetNamespace(modelInfo.ClassDeclaration);

            var targetTypeName = modelInfo.TargetType.ToDisplayString();

            // Map: "Person" -> "HierarchicalMvvm.Demo.ViewModels.PersonModel"


            var targetTypeSimpleName = modelInfo.TargetType.Name;

            var fullModelTypeName = string.IsNullOrEmpty(modelNamespace)
                ? modelClassName
                : $"{modelNamespace}.{modelClassName}";

            mapping[targetTypeSimpleName] = fullModelTypeName;
        }

        return mapping;
    }

    private static void GenerateModel(ModelGenerationInfo modelInfo, Dictionary<string, string> namespaceMapping, SourceProductionContext context)
    {
        var className = modelInfo.ClassDeclaration.Identifier.ValueText;
        var namespaceName = GetNamespace(modelInfo.ClassDeclaration);
        var targetType = modelInfo.TargetType;
        var targetTypeName = targetType.ToDisplayString();

        var properties = GetProperties(targetType, namespaceMapping);

        var sourceCode = GenerateModelClass(className, namespaceName, targetTypeName, properties, namespaceMapping);

        context.AddSource($"{className}.g.cs", sourceCode);
    }

    private static ImmutableArray<PropertyInfo> GetProperties(INamedTypeSymbol targetType, Dictionary<string, string> namespaceMapping)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();

        foreach (var member in targetType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.Kind == SymbolKind.Property &&
                member.DeclaredAccessibility == Accessibility.Public &&
                !member.IsStatic &&
                member.GetMethod != null)
            {
                var propertyKind = DeterminePropertyKind(member.Type);
                var collectionElementType = GetCollectionElementType(member.Type);

                properties.Add(new PropertyInfo
                {
                    Name = member.Name,
                    Kind = propertyKind,
                    Type = member.Type.ToDisplayString(),
                    IsNullable = member.Type.CanBeReferencedByName && member.NullableAnnotation == NullableAnnotation.Annotated,
                    FullModelTypeName = GetFullModelTypeName(member.Type, namespaceMapping, propertyKind, collectionElementType?.ToDisplayString()),
                    CollectionElementType = propertyKind switch
                    {
                        PropertyKind.PrimitiveCollection => collectionElementType?.ToDisplayString(),
                        PropertyKind.ModelCollection => GetFullModelTypeName(member.Type, namespaceMapping, propertyKind, collectionElementType?.ToDisplayString()),
                        _ => default
                    }
                });
            }
        }

        return properties.ToImmutable();
    }

    private static string? GetFullModelTypeName(ITypeSymbol type, Dictionary<string, string> namespaceMapping, PropertyKind kind, string? collectionElementType)
    {
        switch (kind)
        {
            case PropertyKind.ModelObject:
                if (type is INamedTypeSymbol namedType)
                {
                    return namespaceMapping.TryGetValue(namedType.Name, out var fullName) ? fullName.Split('.').Last() : $"{namedType.Name}Model";
                }
                break;

            case PropertyKind.ModelCollection:
                if (!string.IsNullOrEmpty(collectionElementType))
                {
                    var elementTypeName = collectionElementType!.Split('.').Last();

                    return namespaceMapping.TryGetValue(elementTypeName, out var fullName) ? fullName.Split('.').Last() : $"{elementTypeName}Model";
                }
                break;
        }

        return null;
    }

    private static PropertyKind DeterminePropertyKind(ITypeSymbol type)
    {
        if (PrimitiveTypes.Contains(type.ToDisplayString()))
            return PropertyKind.Primitive;

        // Check if it's a collection
        if (IsCollection(type, out var elementType))
        {
            if (elementType != null && ImplementsIModelRecord(elementType))
            {
                return PropertyKind.ModelCollection;
            }
            return PropertyKind.PrimitiveCollection;
        }

        // Check if it's a single model object
        if (ImplementsIModelRecord(type))
        {
            return PropertyKind.ModelObject;
        }

        // Default to primitive
        return PropertyKind.Primitive;
    }

    private static bool ImplementsIModelRecord(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType) return false;

        // Check if type implements IModelRecord<T>
        return namedType.AllInterfaces.Any(i =>
            i.Name == "IModelRecord" &&
            i.TypeArguments.Length == 1);
    }

    private static bool IsCollection(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        elementType = default;

        if (type is INamedTypeSymbol namedType)
        {
            if (PrimitiveTypes.Contains(namedType.ToDisplayString()))
                return false;

            // Direct generic collection
            if (namedType.TypeArguments.Length == 1 && CollectionTypes.Contains(namedType.Name))
            {
                elementType = namedType.TypeArguments.First();

                return true;
            }

            // Check implemented interfaces
            if (namedType.AllInterfaces.FirstOrDefault(i => i.Name == "IEnumerable" && i.TypeArguments.Length == 1) is INamedTypeSymbol interfaceType)
            {
                elementType = interfaceType.TypeArguments.First();

                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        if (IsCollection(type, out var elementType))
        {
            return elementType;
        }
        return null;
    }

    private static string GenerateModelClass(string className, string namespaceName, string targetTypeName, ImmutableArray<PropertyInfo> properties, Dictionary<string, string> namespaceMapping)
    {
        var sb = new StringBuilder();
        var hasHierarchicalObjects = properties.Any(p => p.Kind is PropertyKind.ModelObject or PropertyKind.ModelCollection);

        // Using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.ObjectModel;");
        sb.AppendLine("using HierarchicalMvvm.Core;");


        if (hasHierarchicalObjects)
        {
            sb.AppendLine("using System.Linq;");
        }

        // Add using statements for model namespaces
        var modelNamespaces = properties
            .Where(p => !string.IsNullOrEmpty(p.FullModelTypeName))
            .Select(p => GetNamespaceFromFullTypeName(p.FullModelTypeName!))
            .Where(ns => !string.IsNullOrEmpty(ns) && ns != namespaceName)
            .Distinct()
            .OrderBy(ns => ns);

        foreach (var ns in modelNamespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }

        // Class declaration - choose base class based on whether we have hierarchical objects
        var baseClass = "DeepObservableObject";
        var interfaces = hasHierarchicalObjects ? $"IModelWrapper<{targetTypeName}>" : $"INotifyPropertyChanged, IModelWrapper<{targetTypeName}>";

        sb.AppendLine($"    public partial class {className} : {baseClass}, {interfaces}");


        sb.AppendLine("    {");

        // Generate PropertyChanged event and OnPropertyChanged method only if not using DeepObservableObject
        if (!hasHierarchicalObjects)
        {
            sb.AppendLine("        public event PropertyChangedEventHandler? PropertyChanged;");
            sb.AppendLine();
            sb.AppendLine("        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Generate properties based on their kind
        GenerateProperties(sb, properties);

        // Generate constructors
        GenerateConstructors(sb, className, targetTypeName, properties, hasHierarchicalObjects);

        // Generate ToRecord method
        GenerateToRecordMethod(sb, targetTypeName, properties);

        // Generate UpdateFrom method
        GenerateUpdateFromMethod(sb, targetTypeName, properties);

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string? GetNamespaceFromFullTypeName(string fullTypeName)
    {
        var lastDotIndex = fullTypeName.LastIndexOf('.');
        return lastDotIndex > 0 ? fullTypeName.Substring(0, lastDotIndex) : null;
    }

    private static void GenerateProperties(StringBuilder sb, ImmutableArray<PropertyInfo> properties)
    {
        foreach (var property in properties)
        {
            switch (property.Kind)
            {
                case PropertyKind.Primitive:
                    GeneratePrimitiveProperty(sb, property);
                    break;

                case PropertyKind.ModelObject:
                    GenerateModelObjectProperty(sb, property);
                    break;

                case PropertyKind.ModelCollection:
                    GenerateModelCollectionProperty(sb, property);
                    break;

                case PropertyKind.PrimitiveCollection:
                    GeneratePrimitiveCollectionProperty(sb, property);
                    break;
            }
            sb.AppendLine();
        }
    }

    private static void GeneratePrimitiveProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        var defaultValue = property.Type == "string" ? " = string.Empty" : "";

        // Backing field

        if (property.IsNullable)
        {
            sb.AppendLine($"        private {property.Type}? {fieldName};");
        }
        else
        {
            sb.AppendLine($"        private {property.Type} {fieldName}{defaultValue};");
        }


        sb.AppendLine();

        // Public property with notification
        if (property.IsNullable)
        {
            sb.AppendLine($"        public {property.Type}? {property.Name}");
        }
        else
        {
            sb.AppendLine($"        public {property.Type} {property.Name}");
        }
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine("            set");
        sb.AppendLine("            {");
        sb.AppendLine($"                if ({fieldName} != value)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {fieldName} = value;");
        sb.AppendLine($"                    OnPropertyChangedInternal(nameof({property.Name}));");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateModelObjectProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        var modelTypeName = GetSimpleTypeName(property.FullModelTypeName ?? $"{property.Type}Model");

        if (property.IsNullable)
        {
            sb.AppendLine($"        private {modelTypeName}? {fieldName};");
            sb.AppendLine();
            sb.AppendLine($"        public {modelTypeName}? {property.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {fieldName};");
            sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        private {modelTypeName} {fieldName} = new {modelTypeName}{{ }};");
            sb.AppendLine();
            sb.AppendLine($"        public {modelTypeName} {property.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {fieldName};");
            sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
            sb.AppendLine("        }");
        }


    }

    private static void GenerateModelCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        var elementModelType = GetSimpleTypeName(property.FullModelTypeName ?? $"{property.CollectionElementType}Model");
        
        sb.AppendLine($"        private DeepObservableCollection<{elementModelType.Split('.').Last()}> {fieldName};");
        
        sb.AppendLine($"        public DeepObservableCollection<{elementModelType.Split('.').Last()}> {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }

    private static string GetSimpleTypeName(string fullTypeName)
    {
        return fullTypeName.Split('.').Last();
    }

    private static void GeneratePrimitiveCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        sb.AppendLine($"        private NodeObservableCollection<{property.CollectionElementType}> {fieldName};");

        sb.AppendLine($"        public NodeObservableCollection<{property.CollectionElementType}> {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }

    private static void GenerateConstructors(StringBuilder sb, string className, string targetTypeName,
        ImmutableArray<PropertyInfo> properties, bool hasHierarchicalObjects)
    {

        var collections = properties.Where(p => p.Kind is PropertyKind.ModelCollection or PropertyKind.PrimitiveCollection).ToArray();

        // Constructor with source parameter
        sb.AppendLine($"        public {className}({targetTypeName} source)");
        sb.AppendLine("        {");

        foreach (var property in properties)
        {
            if (property.Kind == PropertyKind.Primitive)
                sb.AppendLine($"            {property.Name} = source.{property.Name};");
            else if (property.Kind == PropertyKind.ModelObject)
                sb.AppendLine($"            {property.Name} = source.{property.Name}?.ToModel();");
            else if (property.Kind == PropertyKind.PrimitiveCollection)
                sb.AppendLine($"            {property.Name} = new NodeObservableCollection<{property.CollectionElementType}>(source.{property.Name}, this);");
            else if (property.Kind == PropertyKind.ModelCollection)
                sb.AppendLine($"            {property.Name} = new DeepObservableCollection<{property.CollectionElementType}>(source.{property.Name}.Select(x => x.ToModel()), this);");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Default constructor
        sb.AppendLine($"        public {className}()");
        sb.AppendLine("        {");

        foreach (var collection in collections)
        {
            if (collection.Kind == PropertyKind.ModelCollection)
                sb.AppendLine($"            {collection.Name} = new DeepObservableCollection<{collection.CollectionElementType}>(this);");
            else
                sb.AppendLine($"            {collection.Name} = new NodeObservableCollection<{collection.CollectionElementType}>(this);");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateToRecordMethod(StringBuilder sb, string targetTypeName, ImmutableArray<PropertyInfo> properties)
    {
        sb.AppendLine($"        public {targetTypeName} ToRecord()");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {targetTypeName}");
        sb.AppendLine("            {");

        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var comma = i < properties.Length - 1 ? "," : "";

            var conversion = property.Kind switch
            {
                PropertyKind.Primitive => property.Name,
                PropertyKind.ModelObject => $"{property.Name}?.ToRecord()",
                PropertyKind.ModelCollection => $"{property.Name}.Select(x => x.ToRecord()).ToList()",
                PropertyKind.PrimitiveCollection => $"{property.Name}.ToList()",
                _ => property.Name
            };

            sb.AppendLine($"                {property.Name} = {conversion}{comma}");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateUpdateFromMethod(StringBuilder sb, string targetTypeName, ImmutableArray<PropertyInfo> properties)
    {
        sb.AppendLine($"        public void UpdateFrom({targetTypeName} source)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (source != null)");
        sb.AppendLine("            {");

        foreach (var property in properties)
        {
            switch (property.Kind)
            {
                case PropertyKind.Primitive:
                    if (property.Type == "string")
                    {
                        sb.AppendLine($"                {property.Name} = source.{property.Name} ?? string.Empty;");
                    }
                    else
                    {
                        sb.AppendLine($"                {property.Name} = source.{property.Name};");
                    }
                    break;

                case PropertyKind.ModelObject:
                    sb.AppendLine($"                {property.Name} = source.{property.Name}?.ToModel();");
                    break;

                case PropertyKind.ModelCollection:
                    sb.AppendLine($"                {property.Name}.Clear();");
                    sb.AppendLine($"                foreach (var item in source.{property.Name})");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    {property.Name}.Add(item.ToModel());");
                    sb.AppendLine("                }");
                    break;

                case PropertyKind.PrimitiveCollection:
                    sb.AppendLine($"                {property.Name}.Clear();");
                    sb.AppendLine($"                foreach (var item in source.{property.Name})");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    {property.Name}.Add(item);");
                    sb.AppendLine("                }");
                    break;
            }
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

}

// Helper classes
public class ModelGenerationInfo
{
    public ClassDeclarationSyntax ClassDeclaration { get; set; } = null!;
    public INamedTypeSymbol TargetType { get; set; } = null!;
    public SemanticModel SemanticModel { get; set; } = null!;
}

public class PropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public PropertyKind Kind { get; set; }
    public string? CollectionElementType { get; set; }
    public string? FullModelTypeName { get; set; }
}

public enum PropertyKind
{
    Primitive,
    ModelObject,
    ModelCollection,
    PrimitiveCollection
}