using System.ComponentModel;

namespace HierarchicalMvvm.Core
{
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
}