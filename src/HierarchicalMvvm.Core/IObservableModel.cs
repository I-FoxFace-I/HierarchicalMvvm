using System;
using System.ComponentModel;

namespace HierarchicalMvvm.Core
{

    public interface IObservableModel : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// The monitoring node that receives changes from this node.
        /// </summary>
        IObserver? Observer { get; set; }

        /// <summary>
        /// Propagates the change up the structure.
        /// </summary>
        /// <param name="propertyName">Name of the changed property.
        /// <param name="sender">Source of the change.
        void PropagateChange(string propertyName, object? sender);
    }
}