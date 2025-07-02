using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;
using System.Collections.Generic;

namespace HierarchicalMvvm.Demo.Models
{
    public record Department(
        string Name,
        string Manager,
        List<Employee> Employees
    ) : IModelRecord<DepartmentModel>
    {
        public DepartmentModel ToModel() => new(this);
    }
}