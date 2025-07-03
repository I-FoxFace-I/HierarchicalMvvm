//using CommunityToolkit.Mvvm.ComponentModel;
//using System.Collections.ObjectModel;
//using System.Collections.Specialized;
//using System.ComponentModel;

//// ===================================================================
//// 1. CORE INTERFACES
//// ===================================================================

///// <summary>
///// Interface pro původní POCO classes/records - umožňuje bidirectional převod
///// </summary>
///// <typeparam name="TModel">Typ Model třídy pro WPF binding</typeparam>
//public interface IModelRecord<TModel> where TModel : class
//{
//    /// <summary>
//    /// Převede POCO objekt na Model pro WPF binding
//    /// </summary>
//    TModel ToModel();
//}

///// <summary>
///// Interface pro Model třídy - umožňuje převod zpět na POCO
///// </summary>
///// <typeparam name="TRecord">Typ původní POCO třídy</typeparam>
//public interface IModelWrapper<TRecord> where TRecord : class
//{
//    /// <summary>
//    /// Převede Model zpět na POCO objekt
//    /// </summary>
//    TRecord ToRecord();
    
//    /// <summary>
//    /// Aktualizuje Model z POCO objektu
//    /// </summary>
//    void UpdateFrom(TRecord source);
//}

///// <summary>
///// Interface pro hierarchické modely s event propagation
///// </summary>
//public interface IHierarchicalModel : INotifyPropertyChanged
//{
//    /// <summary>
//    /// Parent model pro event propagation
//    /// </summary>
//    IHierarchicalModel? Parent { get; set; }
    
//    /// <summary>
//    /// Propaguje změnu nahoru hierarchií
//    /// </summary>
//    void PropagateChange(string propertyName, object? sender);
    
//    /// <summary>
//    /// Registruje child model pro event forwarding
//    /// </summary>
//    void RegisterChild(IHierarchicalModel child);
    
//    /// <summary>
//    /// Odregistruje child model
//    /// </summary>
//    void UnregisterChild(IHierarchicalModel child);
//}

//// ===================================================================
//// 2. EXAMPLE POCO CLASSES/RECORDS
//// ===================================================================

//// Parent POCO
//public record Company(
//    string Name,
//    string Address,
//    List<Department> Departments
//) : IModelRecord<CompanyModel>
//{
//    public CompanyModel ToModel() => new(this);
//}

//// Child POCO  
//public record Department(
//    string Name,
//    string Manager,
//    List<Employee> Employees
//) : IModelRecord<DepartmentModel>
//{
//    public DepartmentModel ToModel() => new(this);
//}

//// Leaf POCO
//public record Employee(
//    string FirstName,
//    string LastName,
//    decimal Salary
//) : IModelRecord<EmployeeModel>
//{
//    public EmployeeModel ToModel() => new(this);
//}

//// ===================================================================
//// 3. BASE CLASS PRO HIERARCHICKÉ MODELY
//// ===================================================================

//[ObservableObject]
//public abstract partial class HierarchicalModelBase : IHierarchicalModel
//{
//    private readonly List<IHierarchicalModel> _children = new();
//    private IHierarchicalModel? _parent;

//    public IHierarchicalModel? Parent
//    {
//        get => _parent;
//        set
//        {
//            if (_parent != value)
//            {
//                _parent?.UnregisterChild(this);
//                _parent = value;
//                _parent?.RegisterChild(this);
//            }
//        }
//    }

//    public virtual void PropagateChange(string propertyName, object? sender)
//    {
//        // Propaguj změnu nahoru
//        Parent?.PropagateChange($"{GetType().Name}.{propertyName}", sender ?? this);
//    }

//    public virtual void RegisterChild(IHierarchicalModel child)
//    {
//        if (!_children.Contains(child))
//        {
//            _children.Add(child);
//            child.PropertyChanged += OnChildPropertyChanged;
//        }
//    }

//    public virtual void UnregisterChild(IHierarchicalModel child)
//    {
//        if (_children.Remove(child))
//        {
//            child.PropertyChanged -= OnChildPropertyChanged;
//        }
//    }

//    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
//    {
//        PropagateChange(e.PropertyName ?? string.Empty, sender);
//    }

//    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
//    {
//        base.OnPropertyChanged(e);
//        PropagateChange(e.PropertyName ?? string.Empty, this);
//    }

//    // Utility method pro smart property subscription
//    protected void SetObjectProperty<T>(ref T backingField, T newValue, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
//        where T : class?
//    {
//        if (!ReferenceEquals(backingField, newValue))
//        {
//            // Odhlásit starý objekt
//            if (backingField is IHierarchicalModel oldChild)
//            {
//                UnregisterChild(oldChild);
//                oldChild.Parent = null;
//            }
            
//            // Nastavit novou hodnotu
//            backingField = newValue;
            
//            // Přihlásit nový objekt
//            if (newValue is IHierarchicalModel newChild)
//            {
//                RegisterChild(newChild);
//                newChild.Parent = this;
//            }
            
//            OnPropertyChanged(propertyName);
//        }
//    }
//}

//// ===================================================================
//// 4. OBSERVABLE COLLECTION S PARENT PROPAGATION
//// ===================================================================

//public class HierarchicalObservableCollection<T> : ObservableCollection<T> 
//    where T : IHierarchicalModel
//{
//    private readonly IHierarchicalModel _parent;

//    public HierarchicalObservableCollection(IHierarchicalModel parent)
//    {
//        _parent = parent;
//        CollectionChanged += OnCollectionChanged;
//    }

//    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
//    {
//        // Odregistrovat staré items
//        if (e.OldItems != null)
//        {
//            foreach (T item in e.OldItems)
//            {
//                _parent.UnregisterChild(item);
//                item.Parent = null;
//            }
//        }

//        // Registrovat nové items
//        if (e.NewItems != null)
//        {
//            foreach (T item in e.NewItems)
//            {
//                _parent.RegisterChild(item);
//                item.Parent = _parent;
//            }
//        }

//        // Propagovat změnu collection
//        _parent.PropagateChange($"Collection{e.Action}", this);
//    }
//}

//// ===================================================================
//// 5. VYGENEROVANÝ KÓD (opravená verze s hierarchickými typy)
//// ===================================================================

///*
//// Původní POCO:
//public record Department(string Name, string Manager, List<Employee> Employees)

//// Vygenerovaný DepartmentModel:
//*/

//using CommunityToolkit.Mvvm.ComponentModel;
//using System.Collections.ObjectModel;
//using System.ComponentModel;

//[ObservableObject]
//public partial class DepartmentModel : HierarchicalModelBase, IModelWrapper<Department>
//{
//    [ObservableProperty]
//    private string name = string.Empty;

//    [ObservableProperty]
//    private string manager = string.Empty;

//    // ✅ SPRÁVNĚ: Collection používá Model typy s event propagation
//    public HierarchicalObservableCollection<EmployeeModel> Employees { get; }

//    public DepartmentModel(Department source)
//    {
//        Employees = new HierarchicalObservableCollection<EmployeeModel>(this);
//        UpdateFrom(source);
//    }

//    public DepartmentModel()
//    {
//        Employees = new HierarchicalObservableCollection<EmployeeModel>(this);
//    }

//    public Department ToRecord()
//    {
//        return new Department(
//            Name,
//            Manager,
//            // ✅ SPRÁVNĚ: Převede EmployeeModel → Employee
//            Employees.Select(emp => emp.ToRecord()).ToList()
//        );
//    }

//    public void UpdateFrom(Department source)
//    {
//        if (source != null)
//        {
//            Name = source.Name ?? string.Empty;
//            Manager = source.Manager ?? string.Empty;
            
//            // ✅ SPRÁVNĚ: Převede Employee → EmployeeModel
//            Employees.Clear();
//            if (source.Employees != null)
//            {
//                foreach (var emp in source.Employees)
//                {
//                    Employees.Add(emp.ToModel());
//                }
//            }
//        }
//    }
//}

//// ===================================================================
//// Komplexnější příklad s vnořenými objekty:
//// ===================================================================

///*
//// POCO s vnořeným objektem:
//public record Company(string Name, Address HeadOffice, List<Department> Departments)
//public record Address(string Street, string City, string Country)

//// Vygenerované modely:
//*/

//[ObservableObject]
//public partial class CompanyModel : HierarchicalModelBase, IModelWrapper<Company>
//{
//    [ObservableProperty]
//    private string name = string.Empty;

//    // ✅ SPRÁVNĚ: Vnořený objekt používá Model typ
//    [ObservableProperty]
//    private AddressModel? headOffice;

//    // ✅ SPRÁVNĚ: Collection používá Model typ
//    public HierarchicalObservableCollection<DepartmentModel> Departments { get; }

//    partial void OnHeadOfficeChanged(AddressModel? value)
//    {
//        // Smart event subscription handled automatically by HierarchicalModelBase
//    }

//    public CompanyModel(Company source)
//    {
//        Departments = new HierarchicalObservableCollection<DepartmentModel>(this);
//        UpdateFrom(source);
//    }

//    public Company ToRecord()
//    {
//        return new Company(
//            Name,
//            HeadOffice?.ToRecord(),        // ✅ AddressModel → Address
//            Departments.Select(d => d.ToRecord()).ToList()  // ✅ DepartmentModel → Department
//        );
//    }

//    public void UpdateFrom(Company source)
//    {
//        if (source != null)
//        {
//            Name = source.Name ?? string.Empty;
//            HeadOffice = source.HeadOffice?.ToModel();  // ✅ Address → AddressModel
            
//            Departments.Clear();
//            if (source.Departments != null)
//            {
//                foreach (var dept in source.Departments)
//                {
//                    Departments.Add(dept.ToModel());  // ✅ Department → DepartmentModel
//                }
//            }
//        }
//    }
//}

//// ===================================================================
//// 6. USAGE EXAMPLE
//// ===================================================================

//public class MainViewModel : ObservableObject
//{
//    [ObservableProperty]
//    private CompanyModel? selectedCompany;

//    [ObservableProperty]
//    private string changeLog = string.Empty;

//    public MainViewModel()
//    {
//        LoadData();
//    }

//    private void LoadData()
//    {
//        // Create POCO data
//        var company = new Company(
//            "Acme Corp",
//            "123 Main St",
//            new List<Department>
//            {
//                new("IT", "John Doe", new List<Employee>
//                {
//                    new("Jane", "Smith", 75000),
//                    new("Bob", "Jones", 65000)
//                }),
//                new("HR", "Alice Brown", new List<Employee>
//                {
//                    new("Charlie", "Wilson", 55000)
//                })
//            }
//        );

//        // Convert to Model for binding
//        SelectedCompany = company.ToModel();
        
//        // Subscribe to change notifications
//        SelectedCompany.PropertyChanged += OnCompanyChanged;
//    }

//    private void OnCompanyChanged(object? sender, PropertyChangedEventArgs e)
//    {
//        ChangeLog += $"[{DateTime.Now:HH:mm:ss}] {e.PropertyName} changed in {sender?.GetType().Name}\n";
//    }

//    [RelayCommand]
//    private void AddEmployee()
//    {
//        if (SelectedCompany?.Departments.FirstOrDefault() is DepartmentModel dept)
//        {
//            var newEmployee = new Employee("New", "Employee", 50000);
//            dept.Employees.Add(newEmployee.ToModel());
//        }
//    }

//    [RelayCommand]
//    private void SaveCompany()
//    {
//        if (SelectedCompany != null)
//        {
//            var companyRecord = SelectedCompany.ToRecord();
//            // Save companyRecord to database
            
//            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Company saved!\n";
//        }
//    }
//}

//// ===================================================================
//// 7. ENHANCED SOURCE GENERATOR (ukázka rozšíření)
//// ===================================================================

///*
//[Generator]
//public class HierarchicalModelSourceGenerator : IIncrementalGenerator
//{
//    public void Initialize(IncrementalGeneratorInitializationContext context)
//    {
//        // Detect classes with [ModelWrapper] attribute
//        // Analyze target type properties
//        // Detect hierarchical relationships (IEnumerable<T>, nested objects)
//        // Generate appropriate Model classes with:
//        //   - HierarchicalModelBase inheritance for complex types
//        //   - HierarchicalObservableCollection for collections
//        //   - Smart property setters with event subscription
//        //   - ToRecord() and UpdateFrom() methods
//        //   - IModelWrapper<T> implementation
//    }
//}

//// Generated attribute usage:
//[ModelWrapper(typeof(Company))]
//public partial class CompanyModel { }

//[ModelWrapper(typeof(Department))]  
//public partial class DepartmentModel { }

//[ModelWrapper(typeof(Employee))]
//public partial class EmployeeModel { }
//*/