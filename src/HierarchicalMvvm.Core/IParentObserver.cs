using System;
using System.ComponentModel;

namespace HierarchicalMvvm.Core
{

    public interface IParentObserver : IObserver
    {
        /// <summary>
        /// Receives changes from child
        /// </summary>
        void ProcessChange(string propertyName, object? source);

    }
}