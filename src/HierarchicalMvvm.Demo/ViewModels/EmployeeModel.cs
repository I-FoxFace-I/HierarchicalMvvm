using HierarchicalMvvm.Attributes;
using HierarchicalMvvm.Demo.Models;

namespace HierarchicalMvvm.Demo.ViewModels
{
    [ModelWrapper(typeof(Employee))]
    public partial class EmployeeModel { }

}

