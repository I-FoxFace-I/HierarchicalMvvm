using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HierarchicalMvvm.Generator;

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
    public INamedTypeSymbol? ElementType { get; set; }
    
    /// <summary>
    /// Původní IPropertySymbol pro přístup k metadata
    /// </summary>
    public IPropertySymbol Symbol { get; set; } = null!;
}

public enum PropertyKind
{
    Primitive,
    Collection,
    ModelObject,
    ModelCollection,
} 