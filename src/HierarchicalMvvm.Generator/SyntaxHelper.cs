using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Reflection;


namespace HierarchicalMvvm.Generator;

public static class SyntaxHelper
{
    public static string GetNamespace(ClassDeclarationSyntax classDeclaration)
    {
        var namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

        if (namespaceDeclaration is not null)
            return namespaceDeclaration.Name.ToString();

        var fileScopedNamespace = classDeclaration.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();

        return fileScopedNamespace?.Name.ToString() ?? string.Empty;
    }


    public static INamedTypeSymbol? GetTargetRecordType(GeneratorSyntaxContext context, AttributeSyntax attribute)
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

    private static bool TryGetBaseRecordType(INamedTypeSymbol? type, out INamedTypeSymbol? baseRecordType)
    {
        baseRecordType = default;

        var baseType = type?.BaseType;

        if (baseType == null || baseType.Name == "Object")
            return false;

        var attr = baseType.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "ModelWrapperAttribute");

        if (attr is null)
            return false;

        if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is INamedTypeSymbol namedType)
        {
            baseRecordType = namedType;
            
            return true;
        }

        return false;
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
                    var targetType = GetTargetRecordType(context, attribute);

                    var generatedType = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                    
                    var isDerived = TryGetBaseRecordType(generatedType, out var baseWrapperType);

                    if (targetType is not null)
                    {
                        return new ModelGenerationInfo
                        {
                            IsDerived = isDerived,
                            IsAbstract = targetType.IsAbstract,
                            ClassDeclaration = classDeclaration,
                            GeneratedType = generatedType!,
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

}
