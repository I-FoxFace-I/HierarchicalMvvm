namespace HierarchicalMvvm.Core
{
    /// <summary>
    /// Interface for the Model class - allows conversion back to POCO
    /// </summary>
    /// <typeparam name="TRecord">Type of the original POCO class</typeparam>
    public interface IModelWrapper<TRecord> where TRecord : class
    {
        /// <summary>
        /// Converts the Model back to a POCO object
        /// </summary>
        TRecord ToRecord();

        /// <summary>
        /// Updates the Model from a POCO object
        /// </summary>
        void UpdateFrom(TRecord source);
    }
}