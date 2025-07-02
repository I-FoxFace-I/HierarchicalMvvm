namespace HierarchicalMvvm.Core
{
    /// <summary>
    /// Interface pro Model třídy - umožňuje převod zpět na POCO
    /// </summary>
    /// <typeparam name="TRecord">Typ původní POCO třídy</typeparam>
    public interface IModelWrapper<TRecord> where TRecord : class
    {
        /// <summary>
        /// Převede Model zpět na POCO objekt
        /// </summary>
        TRecord ToRecord();
        
        /// <summary>
        /// Aktualizuje Model z POCO objektu
        /// </summary>
        void UpdateFrom(TRecord source);
    }
}