using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HierarchicalMvvm.Core
{
    public class NodeObservableCollection<T> : ObservableObject, IObservableChild, INotifyCollectionChanged, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        protected IObservableParent? _parent;
        private readonly ObservableCollection<T> _items = new();

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => _items.CollectionChanged += value;
            remove => _items.CollectionChanged -= value;
        }

        public NodeObservableCollection(IObservableParent? parent = null)
        {
            _parent = parent;
            _items.CollectionChanged += OnCollectionChanged;
        }

        public NodeObservableCollection(IEnumerable<T> collection, IObservableParent? parent = null) : this(parent)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        public void Add(T item)
        {
            _items.Add(item);
        }

        public bool Remove(T item) => _items.Remove(item);

        public void Clear()
        {
            _items.Clear();
        }

        public int Count => _items.Count;

        public T this[int index]
        {
            get => _items[index];
            set
            {
                var backingField = _items[index];

                if (!ReferenceEquals(backingField, value))
                {
                    // Nastavit novou hodnotu
                    _items[index] = value;

                    OnPropertyChangedInternal($"{typeof(T).Name}[{index}]");
                }
            }
        }

        public IEnumerable<T> Items => _items;

        public bool IsReadOnly => false;

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChangedInternal($"{typeof(T).Name}[]");
        }

        public bool Contains(T item) => _items.Contains(item);

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }


        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();


        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(T item) => _items.IndexOf(item);

        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            Remove(_items[index]);
        }

        public IObservableParent? Parent
        {
            get => _parent;
            set
            {
                if (_parent != value)
                {
                    _parent?.DetachChild(this);
                    _parent = value;
                    _parent?.RegisterChild(this);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void PropagateChange(string propertyName, object? sender)
        {
            if (Parent != null)
            {
                // Propaguj změnu nahoru s rozšířeným názvem
                Parent.ProcessChange($"{GetType().Name}.{propertyName}", sender ?? this);
            }
            else
            {
                // Jsme na top úrovni (root) - vyvolej PropertyChanged event
                base.OnPropertyChanged();
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (Parent != null)
            {
                Parent.ProcessChange(e.PropertyName ?? string.Empty, this);
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        protected void OnPropertyChangedInternal([CallerMemberName] string? propertyName = null)
        {
            PropagateChange(propertyName ?? string.Empty, this);
        }
    }

    public class DeepObservableCollection<T> : DeepObservableObject, INotifyCollectionChanged, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T> where T : IObservableChild
    {
        private readonly ObservableCollection<T> _items = new();

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => _items.CollectionChanged += value;
            remove => _items.CollectionChanged -= value;
        }

        public DeepObservableCollection(IObservableParent? parent = null)
        {
            _parent = parent;
            _items.CollectionChanged += OnCollectionChanged;
        }

        public DeepObservableCollection(IEnumerable<T> collection, IObservableParent? parent = null) : this(parent)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        public void Add(T item)
        {
            _items.Add(item);
            RegisterChild(item);
            item.Parent = this;
        }

        public bool Remove(T item)
        {
            if (_items.Remove(item))
            {
                DetachChild(item);
                item.Parent = null;

                return true;
            }

            return false;
        }

        public void Clear()
        {
            foreach (var item in _items)
            {
                DetachChild(item);
                item.Parent = null;
            }
            _items.Clear();
        }

        public int Count => _items.Count;

        public T this[int index]
        {
            get => _items[index];
            set
            {
                var backingField = _items[index];

                if (!ReferenceEquals(backingField, value))
                {
                    // Odhlásit starý objekt
                    if (backingField is IObservableChild oldChild)
                    {
                        DetachChild(oldChild);
                        oldChild.Parent = null;
                    }

                    // Nastavit novou hodnotu
                    _items[index] = value;

                    // Přihlásit nový objekt
                    if (value is IObservableChild newChild)
                    {
                        RegisterChild(newChild);
                        newChild.Parent = this;
                    }


                    OnPropertyChangedInternal($"{typeof(T).Name}[{index}]");
                }
            }
        }

        public IEnumerable<T> Items => _items;

        public bool IsReadOnly => false;

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (T item in e.OldItems)
                {
                    DetachChild(item);
                    item.Parent = null;
                }
            }

            if (e.NewItems != null)
            {
                foreach (T item in e.NewItems)
                {
                    RegisterChild(item);
                    item.Parent = this;
                }
            }

            OnPropertyChangedInternal($"{typeof(T).Name}[]");
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }


        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();


        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(T item) => _items.IndexOf(item);


        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
            RegisterChild(item);
            item.Parent = this;
        }

        public void RemoveAt(int index)
        {
            Remove(_items[index]);
        }

        protected override string ExtendPropertyName(string propertyName)
        {
            var splitNames = propertyName.Split('.');

            return $"{splitNames.First()}[].{string.Join('.', splitNames.Skip(1))}";
        }
    }

    public class ObservableItemCollection<T> : HierarchicalModelBase, INotifyCollectionChanged, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T> where T : IHierarchicalModel
    {
        private readonly ObservableCollection<T> _items = new();

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => _items.CollectionChanged += value;
            remove => _items.CollectionChanged -= value;
        }

        public ObservableItemCollection(IHierarchicalModel? parent = null)
        {
            _parent = parent;
            _items.CollectionChanged += OnCollectionChanged;
        }

        public ObservableItemCollection(IEnumerable<T> collection, IHierarchicalModel? parent = null) : this(parent)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        public void Add(T item)
        {
            _items.Add(item);
            RegisterChild(item);
            item.Parent = this;
        }

        public bool Remove(T item)
        {
            if (_items.Remove(item))
            {
                UnregisterChild(item);
                item.Parent = null;

                return true;
            }

            return false;
        }

        public void Clear()
        {
            foreach (var item in _items)
            {
                UnregisterChild(item);
                item.Parent = null;
            }
            _items.Clear();
        }

        public int Count => _items.Count;

        public T this[int index]
        {
            get => _items[index];
            set
            {
                var backingField = _items[index];

                if (!ReferenceEquals(backingField, value))
                {
                    // Odhlásit starý objekt
                    if (backingField is IHierarchicalModel oldChild)
                    {
                        UnregisterChild(oldChild);
                        oldChild.Parent = null;
                    }

                    // Nastavit novou hodnotu
                    _items[index] = value;

                    // Přihlásit nový objekt
                    if (value is IHierarchicalModel newChild)
                    {
                        RegisterChild(newChild);
                        newChild.Parent = this;
                    }


                    OnPropertyChangedInternal($"{typeof(T).Name}[{index}]");
                }
            }
        }

        public IEnumerable<T> Items => _items;

        public bool IsReadOnly => false;

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (T item in e.OldItems)
                {
                    UnregisterChild(item);
                    item.Parent = null;
                }
            }

            if (e.NewItems != null)
            {
                foreach (T item in e.NewItems)
                {
                    RegisterChild(item);
                    item.Parent = this;
                }
            }

            OnPropertyChangedInternal($"{typeof(T).Name}[]");
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }


        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
            RegisterChild(item);
            item.Parent = this;
        }

        public void RemoveAt(int index)
        {
            Remove(_items[index]);
        }

        public void CopyTo(Array array, int index)
        {
            if (array is T[] itemArray)
                _items.CopyTo(itemArray, index);
        }

        public int Add(object? value)
        {
            if (value is T item)
                Add(item);
            return 0;
        }

        public bool Contains(object? value)
        {
            if (value is T item)
                _items.Contains(item);
            return false;
        }

        public int IndexOf(object? value)
        {
            if (value is T item)
                return _items.IndexOf(item);
            return -1;
        }

        public void Insert(int index, object? value)
        {
            if (value is T item)
                Insert(index, item);
        }

        public void Remove(object? value)
        {
            if (value is T item)
                Remove(item);
        }
    }



    public abstract partial class DeepObservableObject : ObservableObject, IObservableNode
    {
        protected IObservableParent? _parent;

        protected readonly List<IObservableChild> _children = new();

        public IObservableParent? Parent
        {
            get => _parent;
            set
            {
                if (_parent != value)
                {
                    _parent?.DetachChild(this);
                    _parent = value;
                    _parent?.RegisterChild(this);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual string ExtendPropertyName(string propertyName)
        {
            return $"{GetType().Name}.{propertyName}";
        }

        public virtual void PropagateChange(string propertyName, object? sender)
        {
            if (Parent != null)
            {
                // Propaguj změnu nahoru s rozšířeným názvem
                Parent.ProcessChange(ExtendPropertyName(propertyName), sender ?? this);
            }
            else
            {
                // Jsme na top úrovni (root) - vyvolej PropertyChanged event
                base.OnPropertyChanged();
            }
        }

        public virtual void ProcessChange(string message, object? sender)
        {
            if (Parent != null)
            {
                ProcessChange(message, sender ?? this);
            }
            else
            {
                base.OnPropertyChanged();
            }
        }

        public void RegisterChild(IObservableChild child)
        {
            if (!_children.Contains(child))
            {
                _children.Add(child);
                child.PropertyChanged += OnChildPropertyChanged;
            }
        }

        public void DetachChild(IObservableChild child)
        {
            if (_children.Remove(child))
            {
                child.PropertyChanged -= OnChildPropertyChanged;
            }
        }

        protected void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropagateChange(e.PropertyName ?? string.Empty, sender);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (Parent != null)
            {
                Parent.ProcessChange(e.PropertyName ?? string.Empty, this);
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        protected void OnPropertyChangedInternal([CallerMemberName] string? propertyName = null)
        {
            if (Parent != null)
            {
                PropagateChange(propertyName ?? string.Empty, this);
            }
            else
            {
                base.OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Utility method pro smart property subscription
        /// </summary>
        protected void SetObjectProperty<T>(ref T backingField, T newValue, [CallerMemberName] string propertyName = "")
            where T : class?
        {
            if (!ReferenceEquals(backingField, newValue))
            {
                // Odhlásit starý objekt
                if (backingField is IObservableChild oldChild)
                {
                    DetachChild(oldChild);
                    oldChild.Parent = null;
                }

                // Nastavit novou hodnotu
                backingField = newValue;

                // Přihlásit nový objekt
                if (newValue is IObservableChild newChild)
                {
                    RegisterChild(newChild);
                    newChild.Parent = this;
                }

                OnPropertyChangedInternal(propertyName);
            }
        }
    }

    public abstract partial class HierarchicalModelBase : ObservableObject, IHierarchicalModel
    {
        protected readonly List<IHierarchicalModel> _children = new();

        protected IHierarchicalModel? _parent;

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
            if (Parent != null)
            {
                // Propaguj změnu nahoru s rozšířeným názvem
                Parent.PropagateChange($"{GetType().Name}.{propertyName}", sender ?? this);
            }
            else
            {
                // Jsme na top úrovni (root) - vyvolej PropertyChanged event
                base.OnPropertyChanged();
            }

            //Parent?.PropagateChange($"{GetType().Name}.{propertyName}", sender ?? this);
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

        protected void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropagateChange(e.PropertyName ?? string.Empty, sender);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            //base.OnPropertyChanged(e);
            //PropagateChange(e.PropertyName ?? string.Empty, this);



            // Propaguj změnu nahoru jen pokud máme parent
            // Pokud jsme root (Parent == null), už jsme na vrcholu
            if (Parent != null)
            {
                Parent.PropagateChange(e.PropertyName ?? string.Empty, this);
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        protected void OnPropertyChangedInternal([CallerMemberName] string? propertyName = null)
        {
            if (Parent != null)
            {
                PropagateChange(propertyName ?? string.Empty, this);
            }
            else
            {
                base.OnPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Utility method pro smart property subscription
        /// </summary>
        protected void SetObjectProperty<T>(ref T backingField, T newValue, [CallerMemberName] string propertyName = "")
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

                OnPropertyChangedInternal(propertyName);
            }
        }
    }
}