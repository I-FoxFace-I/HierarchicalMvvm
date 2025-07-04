using HierarchicalMvvm.Attributes;
using HierarchicalMvvm.Core;
using HierarchicalMvvm.Demo.Models;

namespace HierarchicalMvvm.Demo.ViewModels
{
    [ModelWrapper(typeof(Person))]
    public partial class PersonModel
    {
        // Generátor automaticky vytvoří kompletní implementaci!
        // - INotifyPropertyChanged
        // - Backing fields (_firstName, _lastName, _age, _email)
        // - Public properties (FirstName, LastName, Age, Email)
        // - Konstruktory
        // - ToRecord() a UpdateFrom() metody
    }

    public partial class PersonBaseModel : DeepObservableObject
    {

    }

    [ModelWrapper(typeof(LegalPerson))]
    public partial class LegalPersonModel : PersonBaseModel { }

}

