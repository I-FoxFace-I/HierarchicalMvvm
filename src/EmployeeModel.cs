using System.ComponentModel;

[ObservableObject]
public partial class DepartmentModel : HierarchicalModelBase, IModelWrapper<Department>
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string manager = string.Empty;

    // ✅ SPRÁVNĚ: Collection používá Model typy s event propagation
    public HierarchicalObservableCollection<EmployeeModel> Employees { get; }

    public DepartmentModel(Department source)
    {
        Employees = new HierarchicalObservableCollection<EmployeeModel>(this);
        UpdateFrom(source);
    }

    public DepartmentModel()
    {
        Employees = new HierarchicalObservableCollection<EmployeeModel>(this);
    }

    public Department ToRecord()
    {
        return new Department(
            Name,
            Manager,
            // ✅ SPRÁVNĚ: Převede EmployeeModel → Employee
            Employees.Select(emp => emp.ToRecord()).ToList()
        );
    }

    public void UpdateFrom(Department source)
    {
        if (source != null)
        {
            Name = source.Name ?? string.Empty;
            Manager = source.Manager ?? string.Empty;
            
            // ✅ SPRÁVNĚ: Převede Employee → EmployeeModel
            Employees.Clear();
            if (source.Employees != null)
            {
                foreach (var emp in source.Employees)
                {
                    Employees.Add(emp.ToModel());
                }
            }
        }
    }
}

// Komplexnější příklad s vnořenými objekty:


[ObservableObject]
public partial class CompanyModel : HierarchicalModelBase, IModelWrapper<Company>
{
    [ObservableProperty]
    private string name = string.Empty;

    // ✅ SPRÁVNĚ: Vnořený objekt používá Model typ
    [ObservableProperty]
    private AddressModel? headOffice;

    // ✅ SPRÁVNĚ: Collection používá Model typ
    public HierarchicalObservableCollection<DepartmentModel> Departments { get; }

    partial void OnHeadOfficeChanged(AddressModel? value)
    {
        // Smart event subscription handled automatically by HierarchicalModelBase
    }

    public CompanyModel(Company source)
    {
        Departments = new HierarchicalObservableCollection<DepartmentModel>(this);
        UpdateFrom(source);
    }

    public Company ToRecord()
    {
        return new Company(
            Name,
            HeadOffice?.ToRecord(),        // ✅ AddressModel → Address
            Departments.Select(d => d.ToRecord()).ToList()  // ✅ DepartmentModel → Department
        );
    }

    public void UpdateFrom(Company source)
    {
        if (source != null)
        {
            Name = source.Name ?? string.Empty;
            HeadOffice = source.HeadOffice?.ToModel();  // ✅ Address → AddressModel
            
            Departments.Clear();
            if (source.Departments != null)
            {
                foreach (var dept in source.Departments)
                {
                    Departments.Add(dept.ToModel());  // ✅ Department → DepartmentModel
                }
            }
        }
    }
}

// 6. USAGE EXAMPLE

public class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private CompanyModel? selectedCompany;

    [ObservableProperty]
    private string changeLog = string.Empty;

    public MainViewModel()
    {
        LoadData();
    }

    private void LoadData()
    {
        // Create POCO data
        var company = new Company(
            "Acme Corp",
            "123 Main St",
            new List<Department>
            {
                new("IT", "John Doe", new List<Employee>
                {
                    new("Jane", "Smith", 75000),
                    new("Bob", "Jones", 65000)
                }),
                new("HR", "Alice Brown", new List<Employee>
                {
                    new("Charlie", "Wilson", 55000)
                })
            }
        );

        // Convert to Model for binding
        SelectedCompany = company.ToModel();
        
        // Subscribe to change notifications
        SelectedCompany.PropertyChanged += OnCompanyChanged;
    }

    private void OnCompanyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ChangeLog += $"[{DateTime.Now:HH:mm:ss}] {e.PropertyName} changed in {sender?.GetType().Name}\n";
    }

    [RelayCommand]
    private void AddEmployee()
    {
        if (SelectedCompany?.Departments.FirstOrDefault() is DepartmentModel dept)
        {
            var newEmployee = new Employee("New", "Employee", 50000);
            dept.Employees.Add(newEmployee.ToModel());
        }
    }

    [RelayCommand]
    private void SaveCompany()
    {
        if (SelectedCompany != null)
        {
            var companyRecord = SelectedCompany.ToRecord();
            // Save companyRecord to database
            
            ChangeLog += $"[{DateTime.Now:HH:mm:ss}] Company saved!\n";
        }
    }
}

// 7. ENHANCED SOURCE GENERATOR (ukázka rozšíření)