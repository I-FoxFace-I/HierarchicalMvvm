using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;
using System.Linq;
using HierarchicalMvvm.Generator;

namespace HierarchicalMvvm.Tests
{
    public class TopologicalSorterTests
    {
        [Fact]
        public void SortByDependencies_ShouldSortCorrectly()
        {
            // Arrange
            var models = CreateTestModels();

            // Act
            var sorted = TopologicalSorter.SortByDependencies(models);

            // Assert
            sorted.Should().HaveCount(3);
            // Employee by měl být před Department (protože Department závisí na Employee)
            var employeeIndex = sorted.FindIndex(m => m.TargetWrapperType.Name == "Employee");
            var departmentIndex = sorted.FindIndex(m => m.TargetWrapperType.Name == "Department");
            employeeIndex.Should().BeLessThan(departmentIndex);
        }

        [Fact]
        public void HasCyclicDependencies_ShouldDetectCycles()
        {
            // Arrange
            var models = CreateTestModels();

            // Act
            var hasCycles = TopologicalSorter.HasCyclicDependencies(models);

            // Assert
            hasCycles.Should().BeFalse(); // Náš test nemá cykly
        }

        private HierarchicalMvvm.Generator.ModelGenerationInfo[] CreateTestModels()
        {
            // Vytvoříme jednoduché test modely
            var source = @"
public record Employee(string Name) : IModelRecord<EmployeeModel>
{
    public EmployeeModel ToModel() => new(this);
}

public record Department(string Name, List<Employee> Employees) : IModelRecord<DepartmentModel>
{
    public DepartmentModel ToModel() => new(this);
}

public record Company(string Name, List<Department> Departments) : IModelRecord<CompanyModel>
{
    public CompanyModel ToModel() => new(this);
}";

            var compilation = CSharpCompilation.Create("Test")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source));
            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());

            var employeeType = semanticModel.Compilation.GetTypeByMetadataName("Employee");
            var departmentType = semanticModel.Compilation.GetTypeByMetadataName("Department");
            var companyType = semanticModel.Compilation.GetTypeByMetadataName("Company");

            return new[]
            {
                new ModelGenerationInfo
                {
                    TargetWrapperType = employeeType!,
                    ClassDeclaration = SyntaxFactory.ClassDeclaration("EmployeeModel")
                },
                new ModelGenerationInfo
                {
                    TargetWrapperType = departmentType!,
                    ClassDeclaration = SyntaxFactory.ClassDeclaration("DepartmentModel")
                },
                new ModelGenerationInfo
                {
                    TargetWrapperType = companyType!,
                    ClassDeclaration = SyntaxFactory.ClassDeclaration("CompanyModel")
                }
            };
        }
    }
} 