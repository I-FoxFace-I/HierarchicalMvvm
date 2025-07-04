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