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
}