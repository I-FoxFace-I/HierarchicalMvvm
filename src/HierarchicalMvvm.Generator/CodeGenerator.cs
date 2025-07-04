using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace HierarchicalMvvm.Generator;

public class CodeGenerator
{
    private readonly ModelGenerationInfo _modelInfo;
    private readonly ImmutableArray<PropertyInfo> _properties;
    private readonly Dictionary<string, string> _namespaceMapping;
    private readonly SourceProductionContext _context;

    public CodeGenerator(ModelGenerationInfo modelInfo, ImmutableArray<PropertyInfo> properties, 
        Dictionary<string, string> namespaceMapping, SourceProductionContext context)
    {
        _modelInfo = modelInfo;
        _properties = properties;
        _namespaceMapping = namespaceMapping;
        _context = context;
    }

    public string Generate()
    {
        var className = _modelInfo.ClassDeclaration.Identifier.ValueText;
        DiagnosticHelper.LogInfo(_context, $"Generating code for {className}");
        
        try
        {
            var namespaceName = SyntaxHelper.GetNamespace(_modelInfo.ClassDeclaration);
            var targetTypeName = _modelInfo.TargetWrapperType.ToDisplayString();

            var sb = new StringBuilder();
            var hasHierarchicalObjects = HasHierarchicalObjects();

            // Using statements
            GenerateUsingStatements(sb, namespaceName, hasHierarchicalObjects);

            // Namespace
            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            // Class declaration
            GenerateClassDeclaration(sb, className, targetTypeName, hasHierarchicalObjects);

            // Class body
            sb.AppendLine("    {");

            if (!_modelInfo.IsAbstract)
            {
                // PropertyChanged event a OnPropertyChanged metoda (pouze pokud nepoužíváme DeepObservableObject)
                if (!hasHierarchicalObjects)
                {
                    GeneratePropertyChangedInfrastructure(sb);
                }

                // Properties
                GenerateProperties(sb);

                // Constructors
                GenerateConstructors(sb, className, targetTypeName, hasHierarchicalObjects);
            }

            // ToRecord method
            GenerateToRecordMethod(sb);

            // UpdateFrom method
            GenerateUpdateFromMethod(sb);

            // Close class
            sb.AppendLine("    }");

            // Close namespace
            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine("}");
            }

            DiagnosticHelper.LogInfo(_context, $"Successfully generated {className}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            DiagnosticHelper.LogError(_context, $"Error generating {className}: {ex.Message}");
            throw;
        }
    }

    private void GenerateUsingStatements(StringBuilder sb, string namespaceName, bool hasHierarchicalObjects)
    {
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Collections.ObjectModel;");
        sb.AppendLine("using HierarchicalMvvm.Core;");

        if (hasHierarchicalObjects)
        {
            sb.AppendLine("using System.Linq;");
        }

        // Přidej using pro model namespaces
        var modelNamespaces = _properties
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
    }

    private void GenerateClassDeclaration(StringBuilder sb, string className, string targetTypeName, bool hasHierarchicalObjects)
    {
        var baseTypeName = _modelInfo.BaseType?.ToDisplayString();
        if (baseTypeName is null || baseTypeName == "object")
            baseTypeName = "DeepObservableObject";

        var interfaces = new List<string> { baseTypeName };

        if (_modelInfo.BaseWrapperType is null)
        {
            interfaces.Add($"IModelWrapper<{targetTypeName}>");
        }

        var modifiers = _modelInfo.IsAbstract ? "public abstract partial" : "public partial";
        sb.AppendLine($"    {modifiers} class {className} : {string.Join(", ", interfaces)}");
    }

    private void GeneratePropertyChangedInfrastructure(StringBuilder sb)
    {
        sb.AppendLine("        public event PropertyChangedEventHandler? PropertyChanged;");
        sb.AppendLine();
        sb.AppendLine("        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)");
        sb.AppendLine("        {");
        sb.AppendLine($"            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private void GenerateProperties(StringBuilder sb)
    {
        foreach (var property in _properties)
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

    private void GeneratePrimitiveProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = StringHelpers.ToCamelCase($"_{property.Name}");
        var defaultValue = GetDefaultValue(property);

        // Backing field
        if (property.IsNullable)
        {
            sb.AppendLine($"        private {property.TypeName}? {fieldName};");
        }
        else
        {
            sb.AppendLine($"        private {property.TypeName} {fieldName}{defaultValue};");
        }

        sb.AppendLine();

        // Public property
        var propertyType = property.IsNullable ? $"{property.TypeName}?" : property.TypeName;
        sb.AppendLine($"        public {propertyType} {property.Name}");
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

    private void GenerateModelObjectProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = StringHelpers.ToCamelCase($"_{property.Name}");
        var modelTypeName = property.FullModelTypeName?.Split('.').Last();

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
            sb.AppendLine($"        private {modelTypeName} {fieldName} = new {modelTypeName}();");
            sb.AppendLine();
            sb.AppendLine($"        public {modelTypeName} {property.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {fieldName};");
            sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
            sb.AppendLine("        }");
        }
    }

    private void GenerateModelCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = StringHelpers.ToCamelCase($"_{property.Name}");
        var elementModelType = GetSimpleTypeName(property.FullModelTypeName ?? $"{property.CollectionElementType}Model");

        sb.AppendLine($"        private DeepObservableCollection<{elementModelType}> {fieldName};");
        sb.AppendLine();
        sb.AppendLine($"        public DeepObservableCollection<{elementModelType}> {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }

    private void GeneratePrimitiveCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = StringHelpers.ToCamelCase($"_{property.Name}");
        sb.AppendLine($"        private NodeObservableCollection<{property.CollectionElementType}> {fieldName};");
        sb.AppendLine();
        sb.AppendLine($"        public NodeObservableCollection<{property.CollectionElementType}> {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }

    private void GenerateConstructors(StringBuilder sb, string className, string targetTypeName, bool hasHierarchicalObjects)
    {
        var collections = _properties.Where(p => p.Kind is PropertyKind.ModelCollection or PropertyKind.Collection).ToArray();

        // Constructor s source parametrem
        sb.AppendLine($"        public {className}({targetTypeName} source)");
        sb.AppendLine("        {");

        foreach (var property in _properties)
        {
            switch (property.Kind)
            {
                case PropertyKind.Primitive:
                    sb.AppendLine($"            {property.Name} = source.{property.Name};");
                    break;
                case PropertyKind.ModelObject:
                    sb.AppendLine($"            {property.Name} = source.{property.Name}?.ToModel();");
                    break;
                case PropertyKind.Collection:
                    sb.AppendLine($"            {property.Name} = new NodeObservableCollection<{property.CollectionElementType}>(source.{property.Name}, this);");
                    break;
                case PropertyKind.ModelCollection:
                    sb.AppendLine($"            {property.Name} = new DeepObservableCollection<{property.CollectionElementType}>(source.{property.Name}.Select(x => x.ToModel()), this);");
                    break;
            }
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

    private void GenerateToRecordMethod(StringBuilder sb)
    {
        if (_modelInfo.IsAbstract)
        {
            sb.AppendLine($"        public abstract {_modelInfo.TargetWrapperType.ToDisplayString()} ToRecord();");
            sb.AppendLine();
            return;
        }

        var methodSignature = _modelInfo.BaseWrapperType is not null 
            ? $"        public override {_modelInfo.BaseWrapperType.ToDisplayString()} ToRecord()"
            : $"        public {_modelInfo.TargetWrapperType.ToDisplayString()} ToRecord()";

        sb.AppendLine(methodSignature);
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {_modelInfo.TargetWrapperType.ToDisplayString()}");
        sb.AppendLine("            {");

        var writableProperties = _properties.Where(p => !p.IsReadOnly).ToArray();
        for (int i = 0; i < writableProperties.Length; i++)
        {
            var property = writableProperties[i];
            var comma = i < writableProperties.Length - 1 ? "," : "";
            var conversion = GetToRecordConversion(property);
            sb.AppendLine($"                {property.Name} = {conversion}{comma}");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private void GenerateUpdateFromMethod(StringBuilder sb)
    {
        var methodSignature = _modelInfo.IsAbstract
            ? $"        public abstract void UpdateFrom({_modelInfo.TargetWrapperType.ToDisplayString()} data);"
            : _modelInfo.BaseWrapperType is not null
                ? $"        public override void UpdateFrom({_modelInfo.BaseWrapperType.ToDisplayString()} data)"
                : $"        public void UpdateFrom({_modelInfo.TargetWrapperType.ToDisplayString()} data)";

        sb.AppendLine(methodSignature);

        if (_modelInfo.IsAbstract)
        {
            sb.AppendLine();
            return;
        }

        sb.AppendLine("        {");
        sb.AppendLine($"            if (data is {_modelInfo.TargetWrapperType.ToDisplayString()} source)");
        sb.AppendLine("            {");

        foreach (var property in _properties)
        {
            GenerateUpdateFromPropertyLogic(sb, property);
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private void GenerateUpdateFromPropertyLogic(StringBuilder sb, PropertyInfo property)
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
                if (property.IsNullable)
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

    private bool HasHierarchicalObjects()
    {
        return _properties.Any(p => p.Kind is PropertyKind.ModelObject or PropertyKind.ModelCollection);
    }

    private string GetDefaultValue(PropertyInfo property)
    {
        if (property.TypeName == "string")
            return " = string.Empty";
        else if (!TypeHelper.PrimitiveTypes.Contains(property.TypeName))
            return " = new()";
        return string.Empty;
    }

    private string GetToRecordConversion(PropertyInfo property)
    {
        return property.Kind switch
        {
            PropertyKind.Primitive => property.Name,
            PropertyKind.ModelObject => property.IsNullable ? $"{property.Name}?.ToRecord()" : $"{property.Name}.ToRecord()",
            PropertyKind.ModelCollection => GetCollectionConversion(property),
            PropertyKind.Collection => GetCollectionConversion(property),
            _ => property.Name
        };
    }

    private string GetCollectionConversion(PropertyInfo property)
    {
        if (property.Type.Constructors.FirstOrDefault(c => c.Parameters.Length == 1) is IMethodSymbol ctor)
        {
            if (ctor.Parameters.First().Type.ToDisplayString().StartsWith("IEnumerable"))
            {
                return $"new {property.TypeName}({property.Name}.Select(x => x.ToRecord()))";
            }
        }

        if (property.Type.AllInterfaces.Any(i => i.Name.StartsWith("ISet")))
        {
            return $"{property.Name}.Select(x => x.ToRecord()).ToHashSet()";
        }

        return $"{property.Name}.Select(x => x.ToRecord()).ToList()";
    }

    private string GetSimpleTypeName(string fullTypeName)
    {
        return fullTypeName.Split('.').Last();
    }

    private string? GetNamespaceFromFullTypeName(string fullTypeName)
    {
        var lastDotIndex = fullTypeName.LastIndexOf('.');
        return lastDotIndex > 0 ? fullTypeName.Substring(0, lastDotIndex) : null;
    }
} 