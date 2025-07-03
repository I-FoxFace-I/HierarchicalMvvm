using System.ComponentModel;

namespace HierarchicalMvvm.Core
{
    public interface IObservableChild : INotifyPropertyChanged
    {
        /// <summary>
        /// Monitorující uzel, který přijímá změny z tohoto uzlu.
        /// </summary>
        IObservableParent? Parent { get; set; }

        /// <summary>
        /// Propaguje změnu výše ve struktuře.
        /// </summary>
        /// <param name="propertyName">Název změněné vlastnosti.</param>
        /// <param name="sender">Zdroj změny.</param>
        void PropagateChange(string propertyName, object? sender);
    }
}