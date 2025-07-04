//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.Testing;
//using Xunit;
//using FluentAssertions;
//using System.Linq;
//using System;
//using HierarchicalMvvm.Generator;

//namespace HierarchicalMvvm.Tests
//{
//    public class SourceGeneratorTests
//    {
//        [Fact]
//        public void SourceGenerator_ShouldGenerateSimpleModel()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;

//namespace TestNamespace
//{
//    public record Person : IModelRecord<PersonModel>
//    {
//        public string Name { get; init; }
//        public int Age { get; init; }
//        public PersonModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(Person))]
//    public partial class PersonModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(1);
//            var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
            
//            generatedSource.Should().Contain("public partial class PersonModel : DeepObservableObject, IModelWrapper<Person>");
//            generatedSource.Should().Contain("private string _name = string.Empty;");
//            generatedSource.Should().Contain("private int _age;");
//            generatedSource.Should().Contain("public string Name");
//            generatedSource.Should().Contain("public int Age");
//            generatedSource.Should().Contain("public Person ToRecord()");
//            generatedSource.Should().Contain("public void UpdateFrom(Person data)");
//        }

//        [Fact]
//        public void SourceGenerator_ShouldGenerateHierarchicalModel()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;
//using System.Collections.Generic;

//namespace TestNamespace
//{
//    public record Department : IModelRecord<DepartmentModel>
//    {
//        public string Name { get; init; }
//        public List<Employee> Employees { get; init; }
//        public DepartmentModel ToModel() => new(this);
//    }

//    public record Employee : IModelRecord<EmployeeModel>
//    {
//        public string Name { get; init; }
//        public EmployeeModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(Department))]
//    public partial class DepartmentModel { }

//    [ModelWrapper(typeof(Employee))]
//    public partial class EmployeeModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(2);
            
//            var departmentSource = result.Results[0].GeneratedSources.First(s => s.HintName.Contains("DepartmentModel")).SourceText.ToString();
//            departmentSource.Should().Contain("public partial class DepartmentModel : DeepObservableObject, IModelWrapper<Department>");
//            departmentSource.Should().Contain("public DeepObservableCollection<Employee> Employees");
//        }

//        [Fact]
//        public void SourceGenerator_ShouldHandleNullableProperties()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;

//namespace TestNamespace
//{
//    public record Person : IModelRecord<PersonModel>
//    {
//        public string? Name { get; init; }
//        public int? Age { get; init; }
//        public PersonModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(Person))]
//    public partial class PersonModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(1);
//            var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
            
//            generatedSource.Should().Contain("private string? _name;");
//            generatedSource.Should().Contain("private int? _age;");
//            generatedSource.Should().Contain("public string? Name");
//            generatedSource.Should().Contain("public int? Age");
//        }

//        [Fact]
//        public void SourceGenerator_ShouldHandleCollections()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;
//using System.Collections.Generic;

//namespace TestNamespace
//{
//    public record Company : IModelRecord<CompanyModel>
//    {
//        public string Name { get; init; }
//        public List<string> Tags { get; init; }
//        public CompanyModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(Company))]
//    public partial class CompanyModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(2);
//            var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
            
//            generatedSource.Should().Contain("public NodeObservableCollection<string> Tags");
//            generatedSource.Should().Contain("Tags = new NodeObservableCollection<string>(source.Tags, this);");
//        }

//        [Fact]
//        public void SourceGenerator_ShouldHandleAbstractClasses()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;

//namespace TestNamespace
//{
//    public abstract record BasePerson : IModelRecord<BasePersonModel>
//    {
//        public string Name { get; init; }
//        public BasePersonModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(BasePerson))]
//    public abstract partial class BasePersonModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(1);
//            var generatedSource = result.Results[0].GeneratedSources[0].SourceText.ToString();
            
//            generatedSource.Should().Contain("public abstract partial class BasePersonModel");
//            generatedSource.Should().Contain("public abstract BasePerson ToRecord();");
//            generatedSource.Should().Contain("public abstract void UpdateFrom(BasePerson data);");
//        }

//        [Fact]
//        public void SourceGenerator_ShouldHandleInheritance()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;

//namespace TestNamespace
//{
//    public record Person : IModelRecord<PersonModel>
//    {
//        public string Name { get; init; }
//        public PersonModel ToModel() => new(this);
//    }

//    public record Employee : Person, IModelRecord<EmployeeModel>
//    {
//        public string Department { get; init; }
//        public EmployeeModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(Person))]
//    public partial class PersonModel { }

//    [ModelWrapper(typeof(Employee))]
//    public partial class EmployeeModel : PersonModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(3);
            
//            var employeeSource = result.Results[0].GeneratedSources.First(s => s.HintName.Contains("EmployeeModel")).SourceText.ToString();
//            employeeSource.Should().Contain("public partial class EmployeeModel : PersonModel");
//            employeeSource.Should().Contain("public string Department");
//        }

//        [Fact]
//        public void SourceGenerator_ShouldGenerateCorrectUsingStatements()
//        {
//            // Arrange
//            var source = @"
//using HierarchicalMvvm.Core;
//using HierarchicalMvvm.Attributes;
//using System.Collections.Generic;

//namespace TestNamespace
//{
//    public record Department : IModelRecord<DepartmentModel>
//    {
//        public string Name { get; init; }
//        public List<Employee> Employees { get; init; }
//        public DepartmentModel ToModel() => new(this);
//    }

//    public record Employee : IModelRecord<EmployeeModel>
//    {
//        public string Name { get; init; }
//        public EmployeeModel ToModel() => new(this);
//    }

//    [ModelWrapper(typeof(Department))]
//    public partial class DepartmentModel { }

//    [ModelWrapper(typeof(Employee))]
//    public partial class EmployeeModel { }
//}";

//            // Act
//            var result = RunGenerator(source);

//            // Assert
//            result.Diagnostics.Should().BeEmpty();
//            result.Results[0].GeneratedSources.Should().HaveCount(2);
            
//            var departmentSource = result.Results[0].GeneratedSources.First(s => s.HintName.Contains("DepartmentModel")).SourceText.ToString();
//            departmentSource.Should().Contain("using System;");
//            departmentSource.Should().Contain("using System.Linq;");
//            departmentSource.Should().Contain("using System.ComponentModel;");
//            departmentSource.Should().Contain("using System.Collections.Generic;");
//            departmentSource.Should().Contain("using System.Runtime.CompilerServices;");
//            departmentSource.Should().Contain("using System.Collections.ObjectModel;");
//            departmentSource.Should().Contain("using HierarchicalMvvm.Core;");
//        }

        
//        private GeneratorDriverRunResult RunGenerator(string source)
//        {
//            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            
//            var netstandardPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory() + "netstandard.dll";
//            var references = new[]
//            {
//                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
//                MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
//                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
//                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
//                MetadataReference.CreateFromFile(typeof(HierarchicalMvvm.Core.DeepObservableObject).Assembly.Location),
//                MetadataReference.CreateFromFile(typeof(HierarchicalMvvm.Core.IModelWrapper<>).Assembly.Location),
//                MetadataReference.CreateFromFile(typeof(HierarchicalMvvm.Attributes.ModelWrapperAttribute).Assembly.Location),
//                MetadataReference.CreateFromFile(netstandardPath)
//            };

//            var compilation = CSharpCompilation.Create(
//                "TestAssembly",
//                new[] { syntaxTree },
//                references,
//                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

//            // Zkontroluj, jestli kompilace proběhla bez chyb
//            var diagnostics = compilation.GetDiagnostics();
//            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
//            {
//                throw new InvalidOperationException($"Compilation failed: {string.Join("\n", diagnostics)}");
//            }

//            var driver = CSharpGeneratorDriver.Create(new HierarchicalModelSourceGenerator());

//            return driver.RunGenerators(compilation).GetRunResult();
//        }
//    }
//}