namespace HierarchicalMvvm.Core
{
    /// <summary>
    /// Interface pro původní POCO classes/records - umožňuje bidirectional převod
    /// </summary>
    /// <typeparam name="TModel">Typ Model třídy pro WPF binding</typeparam>
    public interface IModelRecord<TModel> where TModel : class
    {
        /// <summary>
        /// Převede POCO objekt na Model pro WPF binding
        /// </summary>
        TModel ToModel();
    }
}