using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;
using System.Linq;
using HierarchicalMvvm.Generator;
using System.Collections.Generic;

namespace HierarchicalMvvm.Tests
{
    public class PropertyAnalyzerTests
    {
        [Fact]
        public void GetProperties_ShouldReturnAllPublicProperties()
        {
            // Arrange
            var source = @"
public record Person(string Name, int Age, bool IsActive)
{
    public string FullName => $""{Name} ({Age})"";
    private string PrivateProperty => ""private"";
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
            var personType = semanticModel.Compilation.GetTypeByMetadataName("Person");

            // Act
            var properties = PropertyAnalyzer.GetProperties(personType!, new Dictionary<string, string>());

            // Assert
            properties.Should().HaveCount(4);
            properties.Should().Contain(p => p.Name == "Name");
            properties.Should().Contain(p => p.Name == "Age");
            properties.Should().Contain(p => p.Name == "IsActive");
        }

        [Fact]
        public void GetProperties_ShouldHandleInheritance()
        {
            // Arrange
            var source = @"
public record BasePerson(string Name)
{
}

public record Employee(string Name, string Department) : BasePerson(Name)
{
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
            var employeeType = semanticModel.Compilation.GetTypeByMetadataName("Employee");

            // Act
            var properties = PropertyAnalyzer.GetProperties(employeeType!, new Dictionary<string, string>());

            // Assert
            properties.Should().HaveCount(2);
            properties.Should().Contain(p => p.Name == "Name");
            properties.Should().Contain(p => p.Name == "Department");
        }
    }
} 