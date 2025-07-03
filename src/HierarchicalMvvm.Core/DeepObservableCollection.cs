using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace HierarchicalMvvm.Core
{
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
}