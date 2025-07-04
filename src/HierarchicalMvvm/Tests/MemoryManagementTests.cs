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