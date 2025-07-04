using System;
using System.ComponentModel;

namespace HierarchicalMvvm.Core
{
    public interface IObserver : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Registers child model for event forwarding
        /// </summary>

        void RegisterNode(IObservableModel node);

        /// <summary>
        /// Unregisters child model
        /// </summary>
        void DetachNode(IObservableModel node);
    }
}