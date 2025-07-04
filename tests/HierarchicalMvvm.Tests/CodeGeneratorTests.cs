using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using FluentAssertions;
using System.Linq;
using HierarchicalMvvm.Generator;
using System.Collections.Immutable;
using System.Collections.Generic;

namespace HierarchicalMvvm.Tests
{
    public class CodeGeneratorTests
    {
        [Fact]
        public void CodeGenerator_Constructor_ShouldAcceptRequiredParameters()
        {
            // Arrange
            var modelInfo = CreateTestModelInfo();
            var properties = ImmutableArray<PropertyInfo>.Empty;
            var namespaceMapping = new Dictionary<string, string>();

            // Act & Assert
            var generator = new CodeGenerator(modelInfo, properties, namespaceMapping);
            generator.Should().NotBeNull();
        }

        [Fact]
        public void CodeGenerator_Generate_ShouldReturnValidCode()
        {
            // Arrange
            var modelInfo = CreateTestModelInfo();
            var properties = CreateTestProperties();
            var namespaceMapping = new Dictionary<string, string>();

            var generator = new CodeGenerator(modelInfo, properties, namespaceMapping);

            // Act
            var generatedCode = generator.Generate();

            // Assert
            generatedCode.Should().NotBeNullOrEmpty();
            generatedCode.Should().Contain("public partial class TestModel");
            generatedCode.Should().Contain("using System;");
            generatedCode.Should().Contain("using HierarchicalMvvm.Core;");
        }

        [Fact]
        public void CodeGenerator_Generate_ShouldHandleAbstractClasses()
        {
            // Arrange
            var modelInfo = CreateTestModelInfo();
            modelInfo.IsAbstract = true;
            var properties = ImmutableArray<PropertyInfo>.Empty;
            var namespaceMapping = new Dictionary<string, string>();

            var generator = new CodeGenerator(modelInfo, properties, namespaceMapping);

            // Act
            var generatedCode = generator.Generate();

            // Assert
            generatedCode.Should().Contain("public abstract partial class TestModel");
            generatedCode.Should().Contain("public abstract TestRecord ToRecord();");
            generatedCode.Should().Contain("public abstract void UpdateFrom(TestRecord data);");
        }

        [Fact]
        public void CodeGenerator_Generate_ShouldIncludeToRecordMethod()
        {
            // Arrange
            var modelInfo = CreateTestModelInfo();
            var properties = CreateTestProperties();
            var namespaceMapping = new Dictionary<string, string>();

            var generator = new CodeGenerator(modelInfo, properties, namespaceMapping);

            // Act
            var generatedCode = generator.Generate();

            // Assert
            generatedCode.Should().Contain("public TestRecord ToRecord()");
            generatedCode.Should().Contain("return new TestRecord");
        }

        [Fact]
        public void CodeGenerator_Generate_ShouldIncludeUpdateFromMethod()
        {
            // Arrange
            var modelInfo = CreateTestModelInfo();
            var properties = CreateTestProperties();
            var namespaceMapping = new Dictionary<string, string>();

            var generator = new CodeGenerator(modelInfo, properties, namespaceMapping);

            // Act
            var generatedCode = generator.Generate();

            // Assert
            generatedCode.Should().Contain("public void UpdateFrom(TestRecord data)");
            generatedCode.Should().Contain("Name = source.Name");
        }

        private ModelGenerationInfo CreateTestModelInfo()
        {
            var source = @"
public record TestRecord(string Name, int Age) : IModelRecord<TestModel>
{
    public TestModel ToModel() => new(this);
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
            var testRecordType = semanticModel.Compilation.GetTypeByMetadataName("TestRecord");

            return new ModelGenerationInfo
            {
                TargetWrapperType = testRecordType!,
                ClassDeclaration = SyntaxFactory.ClassDeclaration("TestModel"),
                IsAbstract = false,
                IsDerived = false,
                GeneratedType = testRecordType!,
                SemanticModel = semanticModel
            };
        }

        private ImmutableArray<PropertyInfo> CreateTestProperties()
        {
            var source = @"
public record TestRecord(string Name, int Age) : IModelRecord<TestModel>
{
    public TestModel ToModel() => new(this);
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
            var testRecordType = semanticModel.Compilation.GetTypeByMetadataName("TestRecord");
            var properties = testRecordType!.GetMembers().OfType<IPropertySymbol>();

            var propertyList = new List<PropertyInfo>();
            foreach (var prop in properties)
            {
                propertyList.Add(new PropertyInfo
                {
                    Name = prop.Name,
                    TypeName = prop.Type.ToDisplayString(),
                    Type = prop.Type as INamedTypeSymbol ?? throw new InvalidOperationException(),
                    Kind = PropertyKind.Primitive,
                    IsNullable = prop.Type.NullableAnnotation == NullableAnnotation.Annotated,
                    Symbol = prop
                });
            }

            return propertyList.ToImmutableArray();
        }
    }
} 