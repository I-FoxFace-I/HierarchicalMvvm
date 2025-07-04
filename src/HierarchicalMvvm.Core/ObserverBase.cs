using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace HierarchicalMvvm.Core
{
    public abstract partial class ObserverBase : IObserver
    {
        protected bool _disposed = false;
        protected readonly List<IObservableModel> _targetModels = new();
        
        public event PropertyChangedEventHandler? PropertyChanged;

        protected ObserverBase()
        {
        
        }

        public void RegisterNode(IObservableModel child)
        {
            if (!_targetModels.Contains(child))
            {
                _targetModels.Add(child);
                child.PropertyChanged += OnTargetNodeChanged;
            }
        }

        public void DetachNode(IObservableModel child)
        {
            if (_targetModels.Remove(child))
            {
                child.PropertyChanged -= OnTargetNodeChanged;
            }
        }

        protected abstract void OnTargetNodeChanged(object? sender, PropertyChangedEventArgs e);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Clear event subscriptions
                if (disposing)
                {
                    foreach (var child in _targetModels)
                    {
                        DetachNode(child);
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