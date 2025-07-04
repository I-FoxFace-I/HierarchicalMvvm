using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HierarchicalMvvm.Core
{
    public class NodeObservableCollection<T> : ObservableObject, IObservableModel, INotifyCollectionChanged, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        protected bool _disposed;
        protected IObserver? _observer;
        private readonly ObservableCollection<T> _items = new();

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _items.CollectionChanged += value;
            remove => _items.CollectionChanged -= value;
        }

        public NodeObservableCollection(IParentObserver? parent = null)
        {
            _observer = parent;
            _disposed = false;
            _items.CollectionChanged += OnCollectionChanged;
        }

        public NodeObservableCollection(IEnumerable<T> collection, IParentObserver? parent = null) : this(parent)
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

        public IObserver? Observer
        {
            get => _observer;
            set
            {
                if (_observer != value)
                {
                    _observer?.DetachNode(this);
                    _observer = value;
                    _observer?.RegisterNode(this);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void PropagateChange(string propertyName, object? sender)
        {

            if (_observer is IParentObserver parent)
            {
                // Propaguj změnu nahoru s rozšířeným názvem
                parent.ProcessChange($"{GetType().Name}.{propertyName}", sender ?? this);
            }
            else
            {
                // Jsme na top úrovni (root) - vyvolej PropertyChanged event
                base.OnPropertyChanged(propertyName);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_observer is IParentObserver parent)
            {
                parent.ProcessChange(e.PropertyName ?? string.Empty, this);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Clear event subscriptions
                if (!disposing)
                {
                    PropertyChanged = null;
                    _items.Clear();
                    _disposed = true;
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}