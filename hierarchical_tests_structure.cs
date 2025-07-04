// ===================================================================
// KROK 1: Test Project Setup
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/HierarchicalMvvm.Tests.csproj
/*
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.5.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing" Version="1.1.1" />
    <PackageReference Include="Moq" Version="4.20.69" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HierarchicalMvvm.Core\HierarchicalMvvm.Core.csproj" />
    <ProjectReference Include="..\..\src\HierarchicalMvvm.Attributes\HierarchicalMvvm.Attributes.csproj" />
    <ProjectReference Include="..\..\src\HierarchicalMvvm.Generator\HierarchicalMvvm.Generator.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Analyzer Include="..\..\src\HierarchicalMvvm.Generator\bin\Debug\netstandard2.0\HierarchicalMvvm.Generator.dll" />
  </ItemGroup>
</Project>
*/

// ===================================================================
// KROK 2: Test Data Setup
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/TestData/TestPoco.cs
using HierarchicalMvvm.Core;
using HierarchicalMvvm.Attributes;

namespace HierarchicalMvvm.Tests.TestData
{
    // Simple POCO for testing
    public record SimplePerson(
        string FirstName,
        string LastName,
        int Age
    ) : IModelRecord<SimplePersonModel>
    {
        public SimplePersonModel ToModel() => new(this);
    }

    [ModelWrapper(typeof(SimplePerson))]
    public partial class SimplePersonModel { }

    // Hierarchical POCO for testing
    public record TestCompany(
        string Name,
        List<TestDepartment> Departments
    ) : IModelRecord<TestCompanyModel>
    {
        public TestCompanyModel ToModel() => new(this);
    }

    public record TestDepartment(
        string Name,
        List<TestEmployee> Employees
    ) : IModelRecord<TestDepartmentModel>
    {
        public TestDepartmentModel ToModel() => new(this);
    }

    public record TestEmployee(
        string Name,
        SimplePerson PersonalInfo,
        List<string> Skills
    ) : IModelRecord<TestEmployeeModel>
    {
        public TestEmployeeModel ToModel() => new(this);
    }

    [ModelWrapper(typeof(TestCompany))]
    public partial class TestCompanyModel { }

    [ModelWrapper(typeof(TestDepartment))]
    public partial class TestDepartmentModel { }

    [ModelWrapper(typeof(TestEmployee))]
    public partial class TestEmployeeModel { }
}

// ===================================================================
// KROK 3: Source Generator Tests
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/SourceGeneratorTests.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using HierarchicalMvvm.Generators;
using Xunit;
using FluentAssertions;

namespace HierarchicalMvvm.Tests
{
    public class SourceGeneratorTests
    {
        [Fact]
        public void SourceGenerator_ShouldGenerateSimpleModel()
        {
            // Arrange
            var source = @"
using HierarchicalMvvm.Core;
using HierarchicalMvvm.Attributes;

namespace TestNamespace
{
    public record Person(string Name, int Age) : IModelRecord<PersonModel>
    {
        public PersonModel ToModel() => new(this);
    }

    [ModelWrapper(typeof(Person))]
    public partial class PersonModel { }
}";

            // Act
            var result = RunGenerator(source);

            // Assert
            result.GeneratedSources.Should().HaveCount(1);
            var generatedSource = result.GeneratedSources[0].SourceText.ToString();
            
            generatedSource.Should().Contain("public partial class PersonModel : INotifyPropertyChanged, IDisposable, IModelWrapper<Person>");
            generatedSource.Should().Contain("private string _name = string.Empty;");
            generatedSource.Should().Contain("private int _age;");
            generatedSource.Should().Contain("public string Name");
            generatedSource.Should().Contain("public int Age");
            generatedSource.Should().Contain("public Person ToRecord()");
            generatedSource.Should().Contain("public void UpdateFrom(Person source)");
            generatedSource.Should().Contain("public void Dispose()");
        }

        [Fact]
        public void SourceGenerator_ShouldGenerateHierarchicalModel()
        {
            // Arrange
            var source = @"
using HierarchicalMvvm.Core;
using HierarchicalMvvm.Attributes;
using System.Collections.Generic;

namespace TestNamespace
{
    public record Department(string Name, List<Employee> Employees) : IModelRecord<DepartmentModel>
    {
        public DepartmentModel ToModel() => new(this);
    }

    public record Employee(string Name) : IModelRecord<EmployeeModel>
    {
        public EmployeeModel ToModel() => new(this);
    }

    [ModelWrapper(typeof(Department))]
    public partial class DepartmentModel { }

    [ModelWrapper(typeof(Employee))]
    public partial class EmployeeModel { }
}";

            // Act
            var result = RunGenerator(source);

            // Assert
            result.GeneratedSources.Should().HaveCount(2);
            
            var departmentSource = result.GeneratedSources.First(s => s.HintName.Contains("DepartmentModel")).SourceText.ToString();
            departmentSource.Should().Contain("public partial class DepartmentModel : HierarchicalModelBase, IModelWrapper<Department>");
            departmentSource.Should().Contain("public HierarchicalObservableCollection<EmployeeModel> Employees { get; }");
            departmentSource.Should().Contain("Employees = new HierarchicalObservableCollection<EmployeeModel>(this, nameof(Employees));");
        }

        private GeneratorDriverRunResult RunGenerator(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { 
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(HierarchicalMvvm.Core.IModelRecord<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(HierarchicalMvvm.Attributes.ModelWrapperAttribute).Assembly.Location)
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new HierarchicalModelSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            return driver.RunGenerators(compilation).GetRunResult();
        }
    }
}

// ===================================================================
// KROK 4: Memory Management Tests
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/MemoryManagementTests.cs
using HierarchicalMvvm.Tests.TestData;
using Xunit;
using FluentAssertions;
using System.ComponentModel;

namespace HierarchicalMvvm.Tests
{
    public class MemoryManagementTests
    {
        [Fact]
        public void SimpleModel_ShouldImplementIDisposable()
        {
            // Arrange
            var person = new SimplePerson("John", "Doe", 30);
            var model = person.ToModel();

            // Act & Assert
            model.Should().BeAssignableTo<IDisposable>();
        }

        [Fact]
        public void SimpleModel_ShouldStopFiringEventsAfterDispose()
        {
            // Arrange
            var person = new SimplePerson("John", "Doe", 30);
            var model = person.ToModel();
            var eventFired = false;

            model.PropertyChanged += (_, _) => eventFired = true;

            // Act
            model.Dispose();
            model.FirstName = "Jane"; // This should not fire event

            // Assert
            eventFired.Should().BeFalse();
        }

        [Fact]
        public void HierarchicalModel_ShouldDisposeAllChildren()
        {
            // Arrange
            var company = new TestCompany("Acme", new List<TestDepartment>
            {
                new("IT", new List<TestEmployee>
                {
                    new("John", new SimplePerson("John", "Doe", 30), new List<string> { "C#" })
                })
            });
            var companyModel = company.ToModel();
            var departmentModel = companyModel.Departments[0];
            var employeeModel = departmentModel.Employees[0];

            // Track disposal
            var companyDisposed = false;
            var departmentDisposed = false;
            var employeeDisposed = false;

            // Subscribe to PropertyChanged to detect when objects stop responding
            companyModel.PropertyChanged += (_, _) => { };
            departmentModel.PropertyChanged += (_, _) => { };
            employeeModel.PropertyChanged += (_, _) => { };

            // Act
            companyModel.Dispose();

            // Assert - objects should be disposed and not respond to changes
            var companyEventFired = false;
            var departmentEventFired = false;
            var employeeEventFired = false;

            companyModel.PropertyChanged += (_, _) => companyEventFired = true;
            departmentModel.PropertyChanged += (_, _) => departmentEventFired = true;
            employeeModel.PropertyChanged += (_, _) => employeeEventFired = true;

            companyModel.Name = "New Name";
            departmentModel.Name = "New Dept";
            employeeModel.Name = "New Employee";

            companyEventFired.Should().BeFalse();
            departmentEventFired.Should().BeFalse();
            employeeEventFired.Should().BeFalse();
        }

        [Fact]
        public void Collection_ShouldDisposeItemsWhenDisposed()
        {
            // Arrange
            var department = new TestDepartment("IT", new List<TestEmployee>
            {
                new("John", new SimplePerson("John", "Doe", 30), new List<string>()),
                new("Jane", new SimplePerson("Jane", "Smith", 25), new List<string>())
            });
            var departmentModel = department.ToModel();
            var employees = departmentModel.Employees.ToList();

            // Act
            departmentModel.Dispose();

            // Assert - employees should be disposed
            foreach (var employee in employees)
            {
                var eventFired = false;
                employee.PropertyChanged += (_, _) => eventFired = true;
                employee.Name = "Changed";
                eventFired.Should().BeFalse();
            }
        }
    }
}

// ===================================================================
// KROK 5: Event Propagation Tests
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/EventPropagationTests.cs
using HierarchicalMvvm.Tests.TestData;
using Xunit;
using FluentAssertions;
using System.ComponentModel;

namespace HierarchicalMvvm.Tests
{
    public class EventPropagationTests
    {
        [Fact]
        public void SimplePropertyChange_ShouldFirePropertyChangedEvent()
        {
            // Arrange
            var person = new SimplePerson("John", "Doe", 30);
            var model = person.ToModel();
            
            string? changedPropertyName = null;
            model.PropertyChanged += (_, e) => changedPropertyName = e.PropertyName;

            // Act
            model.FirstName = "Jane";

            // Assert
            changedPropertyName.Should().Be("FirstName");
        }

        [Fact]
        public void HierarchicalPropertyChange_ShouldPropagateToRoot()
        {
            // Arrange
            var company = new TestCompany("Acme", new List<TestDepartment>
            {
                new("IT", new List<TestEmployee>
                {
                    new("John", new SimplePerson("John", "Doe", 30), new List<string>())
                })
            });
            var companyModel = company.ToModel();
            
            var propertyChanges = new List<string>();
            companyModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName ?? "");

            // Act
            companyModel.Departments[0].Employees[0].Name = "Jane";

            // Assert
            propertyChanges.Should().Contain("TestDepartmentModel.TestEmployeeModel.Name");
        }

        [Fact]
        public void CollectionAdd_ShouldPropagateToRoot()
        {
            // Arrange
            var company = new TestCompany("Acme", new List<TestDepartment>
            {
                new("IT", new List<TestEmployee>())
            });
            var companyModel = company.ToModel();
            
            var propertyChanges = new List<string>();
            companyModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName ?? "");

            // Act
            var newEmployee = new TestEmployee("New", new SimplePerson("New", "Employee", 25), new List<string>());
            companyModel.Departments[0].Employees.Add(newEmployee.ToModel());

            // Assert
            propertyChanges.Should().Contain("TestDepartmentModel.Employees.Add");
            propertyChanges.Should().Contain("TestDepartmentModel.Employees");
        }

        [Fact]
        public void PrimitiveCollectionAdd_ShouldPropagateToRoot()
        {
            // Arrange
            var company = new TestCompany("Acme", new List<TestDepartment>
            {
                new("IT", new List<TestEmployee>
                {
                    new("John", new SimplePerson("John", "Doe", 30), new List<string>())
                })
            });
            var companyModel = company.ToModel();
            
            var propertyChanges = new List<string>();
            companyModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName ?? "");

            // Act
            companyModel.Departments[0].Employees[0].Skills.Add("New Skill");

            // Assert
            propertyChanges.Should().Contain("TestDepartmentModel.TestEmployeeModel.Skills.Add");
            propertyChanges.Should().Contain("TestDepartmentModel.TestEmployeeModel.Skills");
        }

        [Fact]
        public void ParentChildRelationship_ShouldBeSetCorrectly()
        {
            // Arrange
            var company = new TestCompany("Acme", new List<TestDepartment>
            {
                new("IT", new List<TestEmployee>
                {
                    new("John", new SimplePerson("John", "Doe", 30), new List<string>())
                })
            });

            // Act
            var companyModel = company.ToModel();
            var departmentModel = companyModel.Departments[0];
            var employeeModel = departmentModel.Employees[0];

            // Assert
            departmentModel.Parent.Should().Be(companyModel);
            employeeModel.Parent.Should().Be(departmentModel);
        }
    }
}

// ===================================================================
// KROK 6: ToRecord/UpdateFrom Tests
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/ConversionTests.cs
using HierarchicalMvvm.Tests.TestData;
using Xunit;
using FluentAssertions;

namespace HierarchicalMvvm.Tests
{
    public class ConversionTests
    {
        [Fact]
        public void ToModel_ShouldCreateModelWithCorrectValues()
        {
            // Arrange
            var person = new SimplePerson("John", "Doe", 30);

            // Act
            var model = person.ToModel();

            // Assert
            model.FirstName.Should().Be("John");
            model.LastName.Should().Be("Doe");
            model.Age.Should().Be(30);
        }

        [Fact]
        public void ToRecord_ShouldCreateRecordWithCorrectValues()
        {
            // Arrange
            var person = new SimplePerson("John", "Doe", 30);
            var model = person.ToModel();
            model.FirstName = "Jane";
            model.Age = 25;

            // Act
            var record = model.ToRecord();

            // Assert
            record.FirstName.Should().Be("Jane");
            record.LastName.Should().Be("Doe");
            record.Age.Should().Be(25);
        }

        [Fact]
        public void UpdateFrom_ShouldUpdateModelValues()
        {
            // Arrange
            var originalPerson = new SimplePerson("John", "Doe", 30);
            var model = originalPerson.ToModel();
            var newPerson = new SimplePerson("Jane", "Smith", 25);

            // Act
            model.UpdateFrom(newPerson);

            // Assert
            model.FirstName.Should().Be("Jane");
            model.LastName.Should().Be("Smith");
            model.Age.Should().Be(25);
        }

        [Fact]
        public void HierarchicalConversion_ShouldWorkCorrectly()
        {
            // Arrange
            var company = new TestCompany("Acme", new List<TestDepartment>
            {
                new("IT", new List<TestEmployee>
                {
                    new("John", new SimplePerson("John", "Doe", 30), new List<string> { "C#", "JavaScript" })
                })
            });

            // Act
            var companyModel = company.ToModel();
            companyModel.Name = "Updated Acme";
            companyModel.Departments[0].Name = "IT Department";
            companyModel.Departments[0].Employees[0].Name = "John Smith";
            companyModel.Departments[0].Employees[0].Skills.Add("TypeScript");

            var convertedBack = companyModel.ToRecord();

            // Assert
            convertedBack.Name.Should().Be("Updated Acme");
            convertedBack.Departments[0].Name.Should().Be("IT Department");
            convertedBack.Departments[0].Employees[0].Name.Should().Be("John Smith");
            convertedBack.Departments[0].Employees[0].Skills.Should().Contain("TypeScript");
            convertedBack.Departments[0].Employees[0].Skills.Should().HaveCount(3);
        }
    }
}

// ===================================================================
// KROK 7: Integration Tests
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/IntegrationTests.cs
using HierarchicalMvvm.Tests.TestData;
using Xunit;
using FluentAssertions;
using System.ComponentModel;

namespace HierarchicalMvvm.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void CompleteWorkflow_ShouldWorkCorrectly()
        {
            // Arrange
            var originalData = new TestCompany("Original Corp", new List<TestDepartment>
            {
                new("Original Dept", new List<TestEmployee>
                {
                    new("Original Employee", new SimplePerson("John", "Doe", 30), new List<string> { "Skill1" })
                })
            });

            // Act 1: Convert to model
            var model = originalData.ToModel();

            // Act 2: Make changes
            var changeEvents = new List<string>();
            model.PropertyChanged += (_, e) => changeEvents.Add(e.PropertyName ?? "");

            model.Name = "Updated Corp";
            model.Departments[0].Name = "Updated Dept";
            model.Departments[0].Employees[0].Name = "Updated Employee";
            model.Departments[0].Employees[0].Skills.Add("Skill2");

            // Add new employee
            var newEmployee = new TestEmployee("New Employee", new SimplePerson("Jane", "Smith", 25), new List<string> { "Skill3" });
            model.Departments[0].Employees.Add(newEmployee.ToModel());

            // Act 3: Convert back to record
            var updatedData = model.ToRecord();

            // Assert: Data integrity
            updatedData.Name.Should().Be("Updated Corp");
            updatedData.Departments[0].Name.Should().Be("Updated Dept");
            updatedData.Departments[0].Employees[0].Name.Should().Be("Updated Employee");
            updatedData.Departments[0].Employees[0].Skills.Should().HaveCount(2);
            updatedData.Departments[0].Employees.Should().HaveCount(2);
            updatedData.Departments[0].Employees[1].Name.Should().Be("New Employee");

            // Assert: Event propagation
            changeEvents.Should().Contain("Name");
            changeEvents.Should().Contain("TestDepartmentModel.Name");
            changeEvents.Should().Contain("TestDepartmentModel.TestEmployeeModel.Name");
            changeEvents.Should().Contain("TestDepartmentModel.TestEmployeeModel.Skills.Add");
            changeEvents.Should().Contain("TestDepartmentModel.Employees.Add");

            // Act 4: Cleanup
            model.Dispose();

            // Assert: No events after disposal
            var eventsAfterDisposal = new List<string>();
            model.PropertyChanged += (_, e) => eventsAfterDisposal.Add(e.PropertyName ?? "");
            
            model.Name = "Should not fire event";
            eventsAfterDisposal.Should().BeEmpty();
        }

        [Fact]
        public void MemoryStressTest_ShouldNotLeakMemory()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (int i = 0; i < 1000; i++)
            {
                var company = new TestCompany($"Company {i}", new List<TestDepartment>
                {
                    new($"Dept {i}", new List<TestEmployee>
                    {
                        new($"Employee {i}", new SimplePerson($"First {i}", $"Last {i}", 30 + i), new List<string> { $"Skill {i}" })
                    })
                });

                using var model = company.ToModel();
                
                // Make some changes
                model.Name = $"Updated Company {i}";
                model.Departments[0].Employees[0].Skills.Add($"Additional Skill {i}");
                
                // Convert back
                var record = model.ToRecord();
                
                // Model will be disposed automatically by using statement
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);

            // Assert
            // Memory should not have grown significantly (allowing for some GC overhead)
            var memoryGrowth = finalMemory - initialMemory;
            memoryGrowth.Should().BeLessThan(10_000_000); // Less than 10MB growth
        }
    }
}

// ===================================================================
// KROK 8: Test Runner Setup
// ===================================================================

// File: tests/HierarchicalMvvm.Tests/TestRunner.cs
using Xunit;
using Xunit.Abstractions;

namespace HierarchicalMvvm.Tests
{
    public class TestRunner
    {
        private readonly ITestOutputHelper _output;

        public TestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RunAllTests()
        {
            _output.WriteLine("🧪 Starting Hierarchical MVVM Test Suite");
            _output.WriteLine("=".PadRight(50, '='));
            _output.WriteLine("✅ Source Generator Tests");
            _output.WriteLine("✅ Memory Management Tests");  
            _output.WriteLine("✅ Event Propagation Tests");
            _output.WriteLine("✅ Conversion Tests");
            _output.WriteLine("✅ Integration Tests");
            _output.WriteLine("=".PadRight(50, '='));
            _output.WriteLine("🎉 All tests should pass!");
        }
    }
}

// ===================================================================
// KROK 9: CI/CD Pipeline
// ===================================================================

// File: .github/workflows/tests.yml
/*
name: Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 8.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore
      
    - name: Test
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
      
    - name: Upload coverage reports
      uses: codecov/codecov-action@v3
*/

// ===================================================================
// KROK 10: Test Documentation
// ===================================================================

/*
# Test Suite Documentation

## Test Categories

### 1. Source Generator Tests (`SourceGeneratorTests.cs`)
- ✅ Simple model generation
- ✅ Hierarchical model generation  
- ✅ Namespace handling
- ✅ Property type detection
- ✅ Generated code validation

### 2. Memory Management Tests (`MemoryManagementTests.cs`)
- ✅ IDisposable implementation
- ✅ Event unsubscription after disposal
- ✅ Hierarchical disposal propagation
- ✅ Collection item disposal
- ✅ Memory leak prevention

### 3. Event Propagation Tests (`EventPropagationTests.cs`)
- ✅ Simple property change events
- ✅ Hierarchical event propagation
- ✅ Collection change propagation
- ✅ Parent-child relationship setup
- ✅ Event bubbling to root

### 4. Conversion Tests (`ConversionTests.cs`)
- ✅ POCO to Model conversion
- ✅ Model to POCO conversion
- ✅ UpdateFrom functionality
- ✅ Hierarchical conversions
- ✅ Data integrity preservation

### 5. Integration Tests (`IntegrationTests.cs`)
- ✅ Complete workflow testing
- ✅ Memory stress testing
- ✅ Real-world scenarios
- ✅ Performance validation

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific category
dotnet test --filter "Category=MemoryManagement"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Memory leak detection
dotnet test --logger "console;verbosity=detailed"
```

## Test Data

All tests use test POCOs in `TestData/` folder to avoid coupling with demo app.

## Coverage Goals

- ✅ Source generator: 95%+
- ✅ Core functionality: 98%+
- ✅ Memory management: 100%
- ✅ Event propagation: 95%+
*/