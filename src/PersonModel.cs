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