using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;

namespace HierarchicalMvvm.Demo.Models
{
    public record Employee(
        string FirstName,
        string LastName,
        decimal Salary,
        Person PersonalInfo
    ) : IModelRecord<EmployeeModel>
    {
        public EmployeeModel ToModel() => new(this);
    }
}