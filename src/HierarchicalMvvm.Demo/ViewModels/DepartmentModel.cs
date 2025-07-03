using HierarchicalMvvm.Attributes;
using HierarchicalMvvm.Demo.Models;

namespace HierarchicalMvvm.Demo.ViewModels
{
    [ModelWrapper(typeof(Department))]
    public partial class DepartmentModel { }

}

