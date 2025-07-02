using HierarchicalMvvm.Attributes;
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

    [ModelWrapper(typeof(Company))]
    public partial class CompanyModel { }

    [ModelWrapper(typeof(Department))]
    public partial class DepartmentModel { }

    [ModelWrapper(typeof(Employee))]
    public partial class EmployeeModel { }

}