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