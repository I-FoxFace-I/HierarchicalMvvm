using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HierarchicalMvvm.Core
{
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
}