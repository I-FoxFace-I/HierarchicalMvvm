using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;

namespace HierarchicalMvvm.Demo.Models
{
    public record Person(
        string FirstName,
        string LastName,
        int Age,
        string Email
    ) : IModelRecord<PersonModel>
    {
        public PersonModel ToModel() => new(this);
    }
}