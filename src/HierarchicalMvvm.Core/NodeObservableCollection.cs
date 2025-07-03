using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
}