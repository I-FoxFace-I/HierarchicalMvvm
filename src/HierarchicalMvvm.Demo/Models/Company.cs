using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;
using System.Collections.Generic;

namespace HierarchicalMvvm.Demo.Models
{
    public record Company(
        string Name,
        string Address,
        List<Department> Departments
    ) : IModelRecord<CompanyModel>
    {
        public CompanyModel ToModel() => new(this);
    }
}