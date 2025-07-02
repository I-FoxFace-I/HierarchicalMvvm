using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HierarchicalMvvm.Generator;

// Helper classes
public class ModelGenerationInfo
{
    public ClassDeclarationSyntax ClassDeclaration { get; set; } = null!;
    public INamedTypeSymbol TargetType { get; set; } = null!;
    public SemanticModel SemanticModel { get; set; } = null!;
}
