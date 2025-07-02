using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace HierarchicalMvvm.Generator;

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
        // Pouze tøídy s attributy, které by mohly obsahovat ModelWrapperAttribute
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
                properties.Add(new PropertyInfo
                {
                    Name = member.Name,
                    Type = member.Type.ToDisplayString(),
                    IsNullable = member.Type.CanBeReferencedByName && member.NullableAnnotation == NullableAnnotation.Annotated
                });
            }
        }

        return properties.ToImmutable();
    }

    private static string GenerateModelClass(string className, string namespaceName, string targetTypeName, ImmutableArray<PropertyInfo> properties)
    {
        var sb = new StringBuilder();

        // Using statements
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using HierarchicalMvvm.Core;");
        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }

        // Class declaration - implementuje INotifyPropertyChanged pøímo
        sb.AppendLine($"    public partial class {className} : INotifyPropertyChanged, IModelWrapper<{targetTypeName}>");
        sb.AppendLine("    {");

        // PropertyChanged event
        sb.AppendLine("        public event PropertyChangedEventHandler? PropertyChanged;");
        sb.AppendLine();

        // OnPropertyChanged method
        sb.AppendLine("        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate backing fields and properties
        foreach (var property in properties)
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
            sb.AppendLine();
        }

        // Constructor with source parameter
        sb.AppendLine($"        public {className}({targetTypeName} source)");
        sb.AppendLine("        {");
        sb.AppendLine("            UpdateFrom(source);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Default constructor
        sb.AppendLine($"        public {className}() {{ }}");
        sb.AppendLine();

        // ToRecord method
        sb.AppendLine($"        public {targetTypeName} ToRecord()");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {targetTypeName}(");

        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var comma = i < properties.Length - 1 ? "," : "";
            sb.AppendLine($"                {property.Name}{comma}");
        }

        sb.AppendLine("            );");
        sb.AppendLine("        }");
        sb.AppendLine();

        // UpdateFrom method
        sb.AppendLine($"        public void UpdateFrom({targetTypeName} source)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (source != null)");
        sb.AppendLine("            {");

        foreach (var property in properties)
        {
            if (property.Type == "string")
            {
                sb.AppendLine($"                {property.Name} = source.{property.Name} ?? string.Empty;");
            }
            else
            {
                sb.AppendLine($"                {property.Name} = source.{property.Name};");
            }
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}
