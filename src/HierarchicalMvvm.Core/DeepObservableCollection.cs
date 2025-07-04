using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace HierarchicalMvvm.Core
{
    public class DeepObservableCollection<T> : DeepObservableObject, INotifyCollectionChanged, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T> where T : IObservableModel
    {
        private bool _disposed = false;
        private readonly ObservableCollection<T> _items = new();

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _items.CollectionChanged += value;
            remove => _items.CollectionChanged -= value;
        }

        public DeepObservableCollection(IParentObserver? parent = null)
        {
            _observer = parent;
            _items.CollectionChanged += OnCollectionChanged;
        }

        public DeepObservableCollection(IEnumerable<T> collection, IParentObserver? parent = null) : this(parent)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        public void Add(T item)
        {
            _items.Add(item);
            RegisterNode(item);
            item.Observer = this;
        }

        public bool Remove(T item)
        {
            if (_items.Remove(item))
            {
                DetachNode(item);
                item.Observer = null;

                return true;
            }

            return false;
        }

        public void Clear()
        {
            foreach (var item in _items)
            {
                DetachNode(item);
                item.Observer = null;
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
                    if (backingField is IObservableModel oldChild)
                    {
                        DetachNode(oldChild);
                        oldChild.Observer = null;
                    }

                    // Nastavit novou hodnotu
                    _items[index] = value;

                    // Přihlásit nový objekt
                    if (value is IObservableModel newChild)
                    {
                        RegisterNode(newChild);
                        newChild.Observer = this;
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
                    DetachNode(item);
                    item.Observer = null;
                }
            }

            if (e.NewItems != null)
            {
                foreach (T item in e.NewItems)
                {
                    RegisterNode(item);
                    item.Observer = this;
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
            RegisterNode(item);
            item.Observer = this;
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

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Clear event subscriptions
                if (!disposing)
                {
                    CollectionChanged -= OnCollectionChanged;
                    
                    foreach (var item in _items)
                    {
                        DetachNode(item);
                        item.Dispose();
                    }
                    
                    _items.Clear();

                    base.Dispose(disposing);

                    _disposed = true;
                }
            }
        }
    }
}