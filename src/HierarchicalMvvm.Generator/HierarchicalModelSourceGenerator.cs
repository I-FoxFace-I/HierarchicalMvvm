using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace HierarchicalMvvm.Generator;
{
    [Generator]
    public class HierarchicalModelSourceGenerator : IIncrementalGenerator
{
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
                if (symbolInfo.Symbol is IMethodSymbol attributeConstructor &&
                    attributeConstructor.ContainingType.Name == "ModelWrapperAttribute")
                {
                    var targetType = GetTargetTypeFromAttribute(context, attribute);
                    if (targetType != null)
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

        foreach (var modelInfo in validModelInfos)
        {
            GenerateModel(modelInfo!, context);
        }
    }

    private static void GenerateModel(ModelGenerationInfo modelInfo, SourceProductionContext context)
    {
        var className = modelInfo.ClassDeclaration.Identifier.ValueText;
        var namespaceName = GetNamespace(modelInfo.ClassDeclaration);
        var targetType = modelInfo.TargetType;
        var targetTypeName = targetType.ToDisplayString();

        var properties = GetProperties(targetType);

        var sourceCode = GenerateModelClass(className, namespaceName, targetTypeName, properties);
        context.AddSource($"{className}.g.cs", sourceCode);
    }

    private static string GetNamespace(ClassDeclarationSyntax classDeclaration)
    {
        var namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDeclaration != null)
            return namespaceDeclaration.Name.ToString();

        var fileScopedNamespace = classDeclaration.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return fileScopedNamespace?.Name.ToString() ?? string.Empty;
    }

    private static ImmutableArray<PropertyInfo> GetProperties(INamedTypeSymbol targetType)
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
                    Type = member.Type.ToDisplayString(),
                    IsNullable = member.Type.CanBeReferencedByName && member.NullableAnnotation == NullableAnnotation.Annotated,
                    Kind = propertyKind,
                    CollectionElementType = collectionElementType
                });
            }
        }

        return properties.ToImmutable();
    }

    private static PropertyKind DeterminePropertyKind(ITypeSymbol type)
    {
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
        elementType = null;

        if (type is INamedTypeSymbol namedType)
        {
            // Direct generic collection
            if (namedType.TypeArguments.Length == 1 &&
                (namedType.Name is "IEnumerable" or "IList" or "List" or "ICollection" or "Collection"))
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }

            // Check implemented interfaces
            foreach (var @interface in namedType.AllInterfaces)
            {
                if (@interface.Name == "IEnumerable" && @interface.TypeArguments.Length == 1)
                {
                    elementType = @interface.TypeArguments[0];
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetCollectionElementType(ITypeSymbol type)
    {
        if (IsCollection(type, out var elementType))
        {
            return elementType?.ToDisplayString();
        }
        return null;
    }

    private static string GenerateModelClass(string className, string namespaceName, string targetTypeName, ImmutableArray<PropertyInfo> properties)
    {
        var sb = new StringBuilder();
        var hasHierarchicalObjects = properties.Any(p => p.Kind is PropertyKind.ModelObject or PropertyKind.ModelCollection);

        // Using statements
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Collections.Generic;");
        if (hasHierarchicalObjects)
        {
            sb.AppendLine("using HierarchicalMvvm.Core;");
            sb.AppendLine("using System.Linq;");
        }
        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }

        // Class declaration - choose base class based on whether we have hierarchical objects
        var baseClass = hasHierarchicalObjects ? "HierarchicalModelBase" : "INotifyPropertyChanged";
        var interfaces = hasHierarchicalObjects ? $"IModelWrapper<{targetTypeName}>" : $"INotifyPropertyChanged, IModelWrapper<{targetTypeName}>";
        
        sb.AppendLine($"    public partial class {className} : {baseClass}, {interfaces}");
        sb.AppendLine("    {");

        // Generate PropertyChanged event and OnPropertyChanged method only if not using HierarchicalModelBase
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
        sb.AppendLine($"        private {property.Type} {fieldName}{defaultValue};");
        sb.AppendLine();

        // Public property with notification
        sb.AppendLine($"        public {property.Type} {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine("            set");
        sb.AppendLine("            {");
        
        if (property.Type == "string")
        {
            sb.AppendLine($"                if ({fieldName} != value)");
        }
        else
        {
            sb.AppendLine($"                if (!EqualityComparer<{property.Type}>.Default.Equals({fieldName}, value))");
        }
        
        sb.AppendLine("                {");
        sb.AppendLine($"                    {fieldName} = value;");
        sb.AppendLine("                    OnPropertyChanged();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateModelObjectProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        var modelTypeName = GetModelTypeName(property.Type);

        sb.AppendLine($"        private {modelTypeName}? {fieldName};");
        sb.AppendLine();
        sb.AppendLine($"        public {modelTypeName}? {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }

    private static void GenerateModelCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var elementModelType = GetModelTypeName(property.CollectionElementType!);
        sb.AppendLine($"        public HierarchicalObservableCollection<{elementModelType}> {property.Name} {{ get; }}");
    }

    private static void GeneratePrimitiveCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        sb.AppendLine($"        public System.Collections.ObjectModel.ObservableCollection<{property.CollectionElementType}> {property.Name} {{ get; }}");
    }

    private static void GenerateConstructors(StringBuilder sb, string className, string targetTypeName, 
        ImmutableArray<PropertyInfo> properties, bool hasHierarchicalObjects)
    {
        var collections = properties.Where(p => p.Kind is PropertyKind.ModelCollection or PropertyKind.PrimitiveCollection).ToArray();

        // Constructor with source parameter
        sb.AppendLine($"        public {className}({targetTypeName} source)");
        sb.AppendLine("        {");

        // Initialize collections
        foreach (var collection in collections)
        {
            if (collection.Kind == PropertyKind.ModelCollection)
            {
                var elementModelType = GetModelTypeName(collection.CollectionElementType!);
                sb.AppendLine($"            {collection.Name} = new HierarchicalObservableCollection<{elementModelType}>(this);");
            }
            else
            {
                sb.AppendLine($"            {collection.Name} = new System.Collections.ObjectModel.ObservableCollection<{collection.CollectionElementType}>();");
            }
        }

        sb.AppendLine("            UpdateFrom(source);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Default constructor
        sb.AppendLine($"        public {className}()");
        sb.AppendLine("        {");

        foreach (var collection in collections)
        {
            if (collection.Kind == PropertyKind.ModelCollection)
            {
                var elementModelType = GetModelTypeName(collection.CollectionElementType!);
                sb.AppendLine($"            {collection.Name} = new HierarchicalObservableCollection<{elementModelType}>(this);");
            }
            else
            {
                sb.AppendLine($"            {collection.Name} = new System.Collections.ObjectModel.ObservableCollection<{collection.CollectionElementType}>();");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateToRecordMethod(StringBuilder sb, string targetTypeName, ImmutableArray<PropertyInfo> properties)
    {
        sb.AppendLine($"        public {targetTypeName} ToRecord()");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {targetTypeName}(");

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

            sb.AppendLine($"                {conversion}{comma}");
        }

        sb.AppendLine("            );");
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
                    sb.AppendLine($"                foreach (var item in source.{property.Name} ?? Enumerable.Empty<{property.CollectionElementType}>())");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    {property.Name}.Add(item.ToModel());");
                    sb.AppendLine("                }");
                    break;

                case PropertyKind.PrimitiveCollection:
                    sb.AppendLine($"                {property.Name}.Clear();");
                    sb.AppendLine($"                foreach (var item in source.{property.Name} ?? Enumerable.Empty<{property.CollectionElementType}>())");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    {property.Name}.Add(item);");
                    sb.AppendLine("                }");
                    break;
            }
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static string GetModelTypeName(string originalTypeName)
    {
        // Convert "Company" to "CompanyModel"
        var simpleName = originalTypeName.Split('.').Last();
        return $"{simpleName}Model";
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
}

public enum PropertyKind
{
    Primitive,
    ModelObject,
    ModelCollection,
    PrimitiveCollection
}

