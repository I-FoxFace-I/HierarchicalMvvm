using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;
using System.Collections.Generic;

namespace HierarchicalMvvm.Demo.Models
{
    public record Company : IModelRecord<CompanyModel>
    {
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public List<Department> Departments { get; init; } = new List<Department>();
        public CompanyModel ToModel() => new(this);


    }
}