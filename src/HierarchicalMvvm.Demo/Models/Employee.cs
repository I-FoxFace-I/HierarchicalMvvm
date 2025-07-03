using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;

namespace HierarchicalMvvm.Demo.Models
{
    public record Employee
        : IModelRecord<EmployeeModel>
    {
        
        public string FirstName {get; init;} = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public decimal Salary {get; init;}
        public Person? PersonalInfo { get; init; }
        public EmployeeModel ToModel() => new(this);
    }
    
}