using System.ComponentModel;

namespace HierarchicalMvvm.Core
{
    /// <summary>
    /// Interface pro hierarchické modely s event propagation
    /// </summary>
    public interface IHierarchicalModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Parent model pro event propagation
        /// </summary>
        IHierarchicalModel? Parent { get; set; }

        /// <summary>
        /// Propaguje změnu nahoru hierarchií
        /// </summary>
        void PropagateChange(string propertyName, object? sender);
        
        /// <summary>
        /// Registruje child model pro event forwarding
        /// </summary>
        void RegisterChild(IHierarchicalModel child);
        
        /// <summary>
        /// Odregistruje child model
        /// </summary>
        void UnregisterChild(IHierarchicalModel child);
    }

   

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

    public interface IObservableParent : INotifyPropertyChanged
    {
        /// <summary>
        /// Příjímá změny z child
        /// </summary>
        void ProcessChange(string propertyName, object? source);

        /// <summary>
        /// Registruje child model pro event forwarding
        /// </summary>
        void RegisterChild(IObservableChild node);

        /// <summary>
        /// Odregistruje child model
        /// </summary>
        void DetachChild(IObservableChild node);
    }

    public interface IObservableNode : IObservableParent, IObservableChild
    {

    }
}