//using HierarchicalMvvm.Tests.TestData;
//using Xunit;
//using FluentAssertions;
//using System.ComponentModel;

//namespace HierarchicalMvvm.Tests
//{
//    public class EventPropagationTests
//    {
//        [Fact]
//        public void SimplePropertyChange_ShouldFirePropertyChangedEvent()
//        {
//            // Arrange
//            var person = new SimplePerson("John", "Doe", 30);
//            var model = person.ToModel();
            
//            string? changedPropertyName = null;
//            model.PropertyChanged += (_, e) => changedPropertyName = e.PropertyName;

//            // Act
//            model.FirstName = "Jane";

//            // Assert
//            changedPropertyName.Should().Be("FirstName");
//        }

//        [Fact]
//        public void HierarchicalPropertyChange_ShouldPropagateToRoot()
//        {
//            // Arrange
//            var company = new TestCompany("Acme", new List<TestDepartment>
//            {
//                new("IT", new List<TestEmployee>
//                {
//                    new("John", new SimplePerson("John", "Doe", 30), new List<string>())
//                })
//            });
//            var companyModel = company.ToModel();
            
//            var propertyChanges = new List<string>();
//            companyModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName ?? "");

//            // Act
//            companyModel.Departments[0].Employees[0].Name = "Jane";

//            // Assert
//            propertyChanges.Should().Contain("TestDepartmentModel.TestEmployeeModel.Name");
//        }

//        [Fact]
//        public void CollectionAdd_ShouldPropagateToRoot()
//        {
//            // Arrange
//            var company = new TestCompany("Acme", new List<TestDepartment>
//            {
//                new("IT", new List<TestEmployee>())
//            });
//            var companyModel = company.ToModel();
            
//            var propertyChanges = new List<string>();
//            companyModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName ?? "");

//            // Act
//            var newEmployee = new TestEmployee("New", new SimplePerson("New", "Employee", 25), new List<string>());
//            companyModel.Departments[0].Employees.Add(newEmployee.ToModel());

//            // Assert
//            propertyChanges.Should().Contain("TestDepartmentModel.Employees.Add");
//            propertyChanges.Should().Contain("TestDepartmentModel.Employees");
//        }

//        [Fact]
//        public void PrimitiveCollectionAdd_ShouldPropagateToRoot()
//        {
//            // Arrange
//            var company = new TestCompany("Acme", new List<TestDepartment>
//            {
//                new("IT", new List<TestEmployee>
//                {
//                    new("John", new SimplePerson("John", "Doe", 30), new List<string>())
//                })
//            });
//            var companyModel = company.ToModel();
            
//            var propertyChanges = new List<string>();
//            companyModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName ?? "");

//            // Act
//            companyModel.Departments[0].Employees[0].Skills.Add("New Skill");

//            // Assert
//            propertyChanges.Should().Contain("TestDepartmentModel.TestEmployeeModel.Skills.Add");
//            propertyChanges.Should().Contain("TestDepartmentModel.TestEmployeeModel.Skills");
//        }

//        [Fact]
//        public void ParentChildRelationship_ShouldBeSetCorrectly()
//        {
//            // Arrange
//            var company = new TestCompany("Acme", new List<TestDepartment>
//            {
//                new("IT", new List<TestEmployee>
//                {
//                    new("John", new SimplePerson("John", "Doe", 30), new List<string>())
//                })
//            });

//            // Act
//            var companyModel = company.ToModel();
//            var departmentModel = companyModel.Departments[0];
//            var employeeModel = departmentModel.Employees[0];

//            // Assert
//            departmentModel.Parent.Should().Be(companyModel);
//            employeeModel.Parent.Should().Be(departmentModel);
//        }
//    }
//}