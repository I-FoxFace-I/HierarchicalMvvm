using HierarchicalMvvm.Attributes;
using HierarchicalMvvm.Demo.Models;

namespace HierarchicalMvvm.Demo.ViewModels
{
    [ModelWrapper(typeof(Person))]
    public partial class PersonModel
    {
        // Generátor automaticky vytvoří implementaci!
    }
}