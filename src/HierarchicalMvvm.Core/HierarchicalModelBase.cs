using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HierarchicalMvvm.Core
{
    public abstract partial class HierarchicalModelBase : ObservableObject, IHierarchicalModel
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
                
                OnPropertyChanged(propertyName);
            }
        }
    }
}