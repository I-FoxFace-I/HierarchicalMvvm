using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;
using System.Linq;
using HierarchicalMvvm.Generator;

namespace HierarchicalMvvm.Tests
{
    public class TypeHelperTests
    {
        [Fact]
        public void IsCollectionType_ShouldDetectCollections()
        {
            // Arrange
            var source = @"
using System.Collections.Generic;

public class TestClass
{
    public List<string> StringList { get; set; }
    public string StringProperty { get; set; }
    public int IntProperty { get; set; }
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
            var testClass = semanticModel.Compilation.GetTypeByMetadataName("TestClass");
            var properties = testClass!.GetMembers().OfType<IPropertySymbol>();

            // Act & Assert
            var stringListProperty = properties.First(p => p.Name == "StringList");
            TypeHelper.IsCollectionType(stringListProperty.Type).Should().BeTrue();

            var stringProperty = properties.First(p => p.Name == "StringProperty");
            TypeHelper.IsCollectionType(stringProperty.Type).Should().BeFalse();

            var intProperty = properties.First(p => p.Name == "IntProperty");
            TypeHelper.IsCollectionType(intProperty.Type).Should().BeFalse();
        }

        [Fact]
        public void TryGetElementType_ShouldReturnElementType()
        {
            // Arrange
            var source = @"
using System.Collections.Generic;

public class TestClass
{
    public List<string> StringList { get; set; }
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
            var testClass = semanticModel.Compilation.GetTypeByMetadataName("TestClass");
            var stringListProperty = testClass!.GetMembers().OfType<IPropertySymbol>().First();

            // Act
            var success = TypeHelper.TryGetElementType(stringListProperty.Type, out var elementType);

            // Assert
            success.Should().BeTrue();
            elementType.Should().NotBeNull();
            elementType!.Name.Should().Be("String");
        }
    }
} 