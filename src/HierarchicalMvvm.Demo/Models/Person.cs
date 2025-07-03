using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.ViewModels;

namespace HierarchicalMvvm.Demo.Models
{

    public record Person : IModelRecord<PersonModel>
    {
        public int Age { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
    
        public PersonModel ToModel() => new(this);
    }
}