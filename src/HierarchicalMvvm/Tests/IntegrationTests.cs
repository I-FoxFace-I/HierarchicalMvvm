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