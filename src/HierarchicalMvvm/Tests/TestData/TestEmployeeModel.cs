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