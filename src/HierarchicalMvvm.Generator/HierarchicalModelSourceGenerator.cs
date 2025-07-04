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

            var targetTypeName = modelInfo.TargetWrapperType.ToDisplayString().Replace("?", "");

            // Map: "Person" -> "HierarchicalMvvm.Demo.ViewModels.PersonModel"


            var targetTypeSimpleName = modelInfo.TargetWrapperType.Name;

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
        var targetType = modelInfo.TargetWrapperType;
        var targetTypeName = targetType.ToDisplayString().Replace("?", "");

        var properties = GetProperties(targetType, namespaceMapping);

        var sourceCode = GenerateModelClass(modelInfo, properties, namespaceMapping);

        context.AddSource($"{className}.g.cs", sourceCode);
    }

    private static ImmutableArray<PropertyInfo> GetProperties(INamedTypeSymbol targetType, Dictionary<string, string> namespaceMapping)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();

        INamedTypeSymbol? basetype = targetType.BaseType;

        if (basetype != null)
        {
            properties.AddRange(GetProperties(basetype, namespaceMapping));
        }

        foreach (var member in targetType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.Kind == SymbolKind.Property &&
                member.DeclaredAccessibility == Accessibility.Public &&
                !member.IsStatic &&
                member.GetMethod != null)
            {

                var propertyKind = DeterminePropertyKind(member.Type);
                var collectionElementType = GetCollectionElementType(member.Type);

                if (properties.Count > 0 && properties.Any(p => p.Name == member.Name))
                {
                    continue;
                }

                properties.Add(new PropertyInfo
                {
                    Name = member.Name,
                    Kind = propertyKind,
                    IsReadOnly = member.IsReadOnly,
                    TypeName = member.Type.ToDisplayString().Replace("?", ""),
                    IsNullable = member.Type.CanBeReferencedByName && member.NullableAnnotation == NullableAnnotation.Annotated,
                    FullModelTypeName = GetFullModelTypeName(member.Type, namespaceMapping, propertyKind, collectionElementType?.ToDisplayString()),
                    CollectionElementType = propertyKind switch
                    {
                        PropertyKind.Collection => collectionElementType?.ToDisplayString(),
                        PropertyKind.ModelCollection => GetFullModelTypeName(member.Type, namespaceMapping, propertyKind, collectionElementType?.ToDisplayString()),
                        _ => default
                    },
                    ElementType = collectionElementType as INamedTypeSymbol,
                    Type = member.Type as INamedTypeSymbol ?? throw new InvalidOperationException($"Cannot convert {member.Type} to INamedTypeSymbol"),
                    
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
        if (PrimitiveTypes.Contains(type.ToDisplayString().Replace("?", "")))
            return PropertyKind.Primitive;

        // Check if it's a collection
        if (IsCollection(type, out var elementType))
        {
            if (elementType != null && ImplementsIModelRecord(elementType))
            {
                return PropertyKind.ModelCollection;
            }
            return PropertyKind.Collection;
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
            if (PrimitiveTypes.Contains(namedType.ToDisplayString().Replace("?", "")))
                return false;

            // Direct generic collection
            if (namedType.TypeArguments.Length == 1 && CollectionTypes.Contains(namedType.Name))
            {
                elementType = namedType.TypeArguments.First();

                return true;
            }

            // Check implemented interfaces
            //if (namedType.AllInterfaces.FirstOrDefault(i => i.Name == "IEnumerable" && i.TypeArguments.Length == 1) is INamedTypeSymbol interfaceType)
            //{
            //    elementType = interfaceType.TypeArguments.First();

            //    return true;
            //}
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

    private static string GenerateModelClass(ModelGenerationInfo modelInfo, ImmutableArray<PropertyInfo> properties, Dictionary<string, string> namespaceMapping)
    {
        var namespaceName = GetNamespace(modelInfo.ClassDeclaration);
        var className = modelInfo.ClassDeclaration.Identifier.ValueText;
        var targetType = modelInfo.TargetWrapperType;
        var baseType = modelInfo.BaseType;
        var targetTypeName = targetType.ToDisplayString();

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
        var baseTypeName = baseType?.ToDisplayString();

        if (baseTypeName is null || baseTypeName == "object")
            baseTypeName = "DeepObservableObject";

        var interfaces = new List<string> { baseTypeName };

        if (modelInfo.BaseWrapperType is null)
        {
            interfaces.Add($"IModelWrapper<{targetTypeName}>");
        }

        if (modelInfo.IsAbstract)
        {
            sb.AppendLine($"    public abstract partial class {className} : {string.Join(", ", interfaces)}");
        }
        else
        {
            sb.AppendLine($"    public partial class {className} : {string.Join(", ", interfaces)}");
        }




        sb.AppendLine("    {");

        if (!modelInfo.IsAbstract)
        {
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
        }


        GenerateToRecordMethod(sb, modelInfo, properties);

        // Generate ToRecord method


        // Generate UpdateFrom method
        GenerateUpdateFromMethod(sb, modelInfo, properties);

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

                case PropertyKind.Collection:
                    GeneratePrimitiveCollectionProperty(sb, property);
                    break;
            }
            sb.AppendLine();
        }
    }
    private static void GeneratePrimitiveProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        
        // Backing field

        if (property.IsNullable)
        {
            sb.AppendLine($"        private {property.TypeName}? {fieldName};");
        }
        else
        {
            var defaultValue = string.Empty;

            if (property.TypeName == "string")
                defaultValue = " = string.Empty";
            else if(!PrimitiveTypes.Contains(property.TypeName))
                defaultValue = " = new ()";
            
            sb.AppendLine($"        private {property.TypeName} {fieldName}{defaultValue};");
        }


        sb.AppendLine();

        // Public property with notification
        if (property.IsNullable)
        {
            sb.AppendLine($"        public {property.TypeName}? {property.Name}");
        }
        else
        {
            sb.AppendLine($"        public {property.TypeName} {property.Name}");
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
        var modelTypeName = property.FullModelTypeName?.Split('.').Last();
        
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";

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
    private static string GetSimpleTypeName(string fullTypeName)
    {
        return fullTypeName.Split('.').Last();
    }
    


    private static void GenerateConstructors(StringBuilder sb, string className, string targetTypeName,
        ImmutableArray<PropertyInfo> properties, bool hasHierarchicalObjects)
    {

        var collections = properties.Where(p => p.Kind is PropertyKind.ModelCollection or PropertyKind.Collection).ToArray();

        // Constructor with source parameter
        sb.AppendLine($"        public {className}({targetTypeName} source)");
        sb.AppendLine("        {");

        foreach (var property in properties)
        {
            if (property.Kind == PropertyKind.Primitive)
                sb.AppendLine($"            {property.Name} = source.{property.Name};");
            else if (property.Kind == PropertyKind.ModelObject)
                sb.AppendLine($"            {property.Name} = source.{property.Name}?.ToModel();");
            else if (property.Kind == PropertyKind.Collection)
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

    private static string GetObjectConversion(PropertyInfo property)
    {
        if (property.IsNullable)
            return $"{property.Name}?.ToRecord()";

        return $"{property.Name}.ToRecord()";
    }
    private static string GetCollectionConversion(PropertyInfo property)
    {
        if (property.Type.Constructors.FirstOrDefault(c => c.Parameters.Length == 1) is IMethodSymbol ctor)
        {
            if(ctor.Parameters.First().Type.ToDisplayString().StartsWith("IEnumerable"))
            {
                return $"new {property.TypeName}({property.Name}.Select(x => x.ToRecord()))";
            }
        }

        if(property.Type.AllInterfaces.Any(i => i.Name.StartsWith("ISet")))
        {
            return $"{property.Name}.Select(x => x.ToRecord()).ToHashSet()";
        }

        return $"{property.Name}.Select(x => x.ToRecord()).ToList()";
    }

    private static void GenerateToRecordMethod(StringBuilder sb, ModelGenerationInfo modelInfo, ImmutableArray<PropertyInfo> properties)
    {
        if (modelInfo.IsAbstract)
        {
            sb.AppendLine($"        public abstract {modelInfo.TargetWrapperType.ToDisplayString()} ToRecord();");

            return;
        }
        else if (modelInfo.BaseWrapperType is not null)
        {
            sb.AppendLine($"        public override {modelInfo.BaseWrapperType?.ToDisplayString()} ToRecord()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {modelInfo.TargetWrapperType.ToDisplayString()}");
        }
        else
        {
            sb.AppendLine($"        public {modelInfo.TargetWrapperType.ToDisplayString()} ToRecord()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {modelInfo.TargetWrapperType.ToDisplayString()}");
        }

        sb.AppendLine("            {");

        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];

            if (property.IsReadOnly)
                continue;

            var comma = i < properties.Length - 1 ? "," : "";

            var conversion = property.Kind switch
            {
                PropertyKind.Primitive => property.Name,
                PropertyKind.ModelObject => GetObjectConversion(property), //property.IsNullable ? $"{property.Name}?.ToRecord()" : $"{property.Name}.ToRecord()",
                PropertyKind.ModelCollection => GetCollectionConversion(property), //$"{property.Name}.Select(x => x.ToRecord()).ToList()",
                PropertyKind.Collection => GetCollectionConversion(property), //$"{property.Name}.ToList()",
                _ => property.Name
            };

            sb.AppendLine($"                {property.Name} = {conversion}{comma}");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
    private static void GenerateUpdateFromMethod(StringBuilder sb, ModelGenerationInfo modelInfo, ImmutableArray<PropertyInfo> properties)
    {
        if (modelInfo.IsAbstract)
        {
            sb.AppendLine($"        public abstract void UpdateFrom({modelInfo.TargetWrapperType.ToDisplayString()} data);");

            return;
        }
        else if (modelInfo.BaseWrapperType is not null)
        {
            sb.AppendLine($"        public override void UpdateFrom({modelInfo.BaseWrapperType?.ToDisplayString()} data)");
        }
        else
        {
            sb.AppendLine($"        public void UpdateFrom({modelInfo.TargetWrapperType.ToDisplayString()} data)");
        }
        sb.AppendLine("        {");
        sb.AppendLine($"            if (data is {modelInfo.TargetWrapperType.ToDisplayString()} source)");
        sb.AppendLine("            {");

        foreach (var property in properties)
        {
            switch (property.Kind)
            {
                case PropertyKind.Primitive:
                    if (property.TypeName == "string")
                    {
                        if (property.IsNullable)
                            sb.AppendLine($"                {property.Name} = source.{property.Name};");
                        else
                            sb.AppendLine($"                {property.Name} = source.{property.Name} ?? string.Empty;");
                    }
                    else
                    {
                        sb.AppendLine($"                {property.Name} = source.{property.Name};");
                    }
                    break;

                case PropertyKind.ModelObject:
                    if(property.IsNullable)
                        sb.AppendLine($"                {property.Name} = source.{property.Name}?.ToModel();");
                    else
                        sb.AppendLine($"                {property.Name} = source.{property.Name}.ToModel();");
                    break;

                case PropertyKind.ModelCollection:
                    sb.AppendLine($"                {property.Name}.Clear();");
                    sb.AppendLine($"                foreach (var item in source.{property.Name})");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    {property.Name}.Add(item.ToModel());");
                    sb.AppendLine("                }");
                    break;

                case PropertyKind.Collection:
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