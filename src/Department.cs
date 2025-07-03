using System.ComponentModel;

// 1. CORE INTERFACES

/// <summary>
/// Interface pro původní POCO classes/records - umožňuje bidirectional převod
/// </summary>
/// <typeparam name="TModel">Typ Model třídy pro WPF binding</typeparam>
public interface IModelRecord<TModel> where TModel : class
{
    /// <summary>
    /// Převede POCO objekt na Model pro WPF binding
    /// </summary>
    TModel ToModel();
}

/// <summary>
/// Interface pro Model třídy - umožňuje převod zpět na POCO
/// </summary>
/// <typeparam name="TRecord">Typ původní POCO třídy</typeparam>
public interface IModelWrapper<TRecord> where TRecord : class
{
    /// <summary>
    /// Převede Model zpět na POCO objekt
    /// </summary>
    TRecord ToRecord();
    
    /// <summary>
    /// Aktualizuje Model z POCO objektu
    /// </summary>
    void UpdateFrom(TRecord source);
}

/// <summary>
/// Interface pro hierarchické modely s event propagation
/// </summary>
public interface IHierarchicalModel : INotifyPropertyChanged
{
    /// <summary>
    /// Parent model pro event propagation
    /// </summary>
    IHierarchicalModel? Parent { get; set; }
    
    /// <summary>
    /// Propaguje změnu nahoru hierarchií
    /// </summary>
    void PropagateChange(string propertyName, object? sender);
    
    /// <summary>
    /// Registruje child model pro event forwarding
    /// </summary>
    void RegisterChild(IHierarchicalModel child);
    
    /// <summary>
    /// Odregistruje child model
    /// </summary>
    void UnregisterChild(IHierarchicalModel child);
}

// 2. EXAMPLE POCO CLASSES/RECORDS

// Parent POCO
public record Company(
    string Name,
    string Address,
    List<Department> Departments
) : IModelRecord<CompanyModel>
{
    public CompanyModel ToModel() => new(this);
}

// Child POCO  
public record Department(
    string Name,
    string Manager,
    List<Employee> Employees
) : IModelRecord<DepartmentModel>
{
    public DepartmentModel ToModel() => new(this);
}

// Leaf POCO
public record Employee(
    string FirstName,
    string LastName,
    decimal Salary
) : IModelRecord<EmployeeModel>
{
    public EmployeeModel ToModel() => new(this);
}

// 3. BASE CLASS PRO HIERARCHICKÉ MODELY

[ObservableObject]
public abstract partial class HierarchicalModelBase : IHierarchicalModel
{
    private readonly List<IHierarchicalModel> _children = new();
    private IHierarchicalModel? _parent;

    public IHierarchicalModel? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value)
            {
                _parent?.UnregisterChild(this);
                _parent = value;
                _parent?.RegisterChild(this);
            }
        }
    }

    public virtual void PropagateChange(string propertyName, object? sender)
    {
        // Propaguj změnu nahoru
        Parent?.PropagateChange($"{GetType().Name}.{propertyName}", sender ?? this);
    }

    public virtual void RegisterChild(IHierarchicalModel child)
    {
        if (!_children.Contains(child))
        {
            _children.Add(child);
            child.PropertyChanged += OnChildPropertyChanged;
        }
    }

    public virtual void UnregisterChild(IHierarchicalModel child)
    {
        if (_children.Remove(child))
        {
            child.PropertyChanged -= OnChildPropertyChanged;
        }
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropagateChange(e.PropertyName ?? string.Empty, sender);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        PropagateChange(e.PropertyName ?? string.Empty, this);
    }

    // Utility method pro smart property subscription
    protected void SetObjectProperty<T>(ref T backingField, T newValue, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        where T : class?
    {
        if (!ReferenceEquals(backingField, newValue))
        {
            // Odhlásit starý objekt
            if (backingField is IHierarchicalModel oldChild)
            {
                UnregisterChild(oldChild);
                oldChild.Parent = null;
            }
            
            // Nastavit novou hodnotu
            backingField = newValue;
            
            // Přihlásit nový objekt
            if (newValue is IHierarchicalModel newChild)
            {
                RegisterChild(newChild);
                newChild.Parent = this;
            }
            
            OnPropertyChanged(propertyName);
        }
    }
}

// 4. OBSERVABLE COLLECTION S PARENT PROPAGATION

public class HierarchicalObservableCollection<T> : ObservableCollection<T> 
    where T : IHierarchicalModel
{
    private readonly IHierarchicalModel _parent;

    public HierarchicalObservableCollection(IHierarchicalModel parent)
    {
        _parent = parent;
        CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Odregistrovat staré items
        if (e.OldItems != null)
        {
            foreach (T item in e.OldItems)
            {
                _parent.UnregisterChild(item);
                item.Parent = null;
            }
        }

        // Registrovat nové items
        if (e.NewItems != null)
        {
            foreach (T item in e.NewItems)
            {
                _parent.RegisterChild(item);
                item.Parent = _parent;
            }
        }

        // Propagovat změnu collection
        _parent.PropagateChange($"Collection{e.Action}", this);
    }
}

// 5. VYGENEROVANÝ KÓD (opravená verze s hierarchickými typy)