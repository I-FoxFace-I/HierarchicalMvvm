using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;
using System.Collections.Generic;

namespace HierarchicalMvvm.Demo.Models
{
    public record Department : IModelRecord<DepartmentModel>
    {
        public string Name { get; init; }
        public string Manager { get; init; }
        public List<Employee> Employees { get; init; }
    
        public DepartmentModel ToModel() => new(this);
    }
}