using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HierarchicalMvvm.Core
{
    public abstract partial class DeepObservableObject : ObservableObject, IObservableNode
    {
        protected bool _disposed = false;
        protected IObserver? _observer;
        
        protected readonly List<IObservableModel> _targetModels = new();

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

        protected virtual string ExtendPropertyName(string propertyName)
        {
            return $"{GetType().Name}.{propertyName}";
        }

        public virtual void PropagateChange(string propertyName, object? sender)
        {
            if (_observer is IParentObserver parent)
            {
                // Propaguj změnu nahoru s rozšířeným názvem
                parent.ProcessChange(ExtendPropertyName(propertyName), sender ?? this);
            }
            else
            {
                // Jsme na top úrovni (root) - vyvolej PropertyChanged event
                base.OnPropertyChanged(propertyName);
            }
        }

        public virtual void ProcessChange(string message, object? sender)
        {
            PropagateChange(message, sender ?? this);
        }

        public void RegisterNode(IObservableModel node)
        {
            if (!_targetModels.Contains(node))
            {
                _targetModels.Add(node);
                node.PropertyChanged += OnChildPropertyChanged;
            }
        }

        public void DetachNode(IObservableModel node)
        {
            if (_targetModels.Remove(node))
            {
                node.PropertyChanged -= OnChildPropertyChanged;
            }
        }

        protected void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropagateChange(e.PropertyName ?? string.Empty, sender);
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
            if (Observer != null)
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
                if (backingField is IObservableModel oldChild)
                {
                    DetachNode(oldChild);
                    oldChild.Observer = null;
                }

                // Nastavit novou hodnotu
                backingField = newValue;

                // Přihlásit nový objekt
                if (newValue is IObservableModel newChild)
                {
                    RegisterNode(newChild);
                    newChild.Observer = this;
                }

                OnPropertyChangedInternal(propertyName);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Clear event subscriptions
                if (disposing)
                {

                    if(_observer != null)
                    {
                        _observer.DetachNode(this);
                        _observer = null;
                    }
                    
                    foreach(var child in _targetModels)
                    {
                        DetachNode(child);
                        child.Dispose();
                    }

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