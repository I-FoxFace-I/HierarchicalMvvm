using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace HierarchicalMvvm.Core
{
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
}