namespace HierarchicalMvvm.Attributes
{
    /// <summary>
    /// Označuje třídu, pro kterou má být vygenerován Model wrapper s WPF binding support
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ModelWrapperAttribute : System.Attribute
    {
        /// <summary>
        /// Typ původní POCO třídy, pro kterou se generuje Model
        /// </summary>
        public System.Type TargetType { get; }
        
        /// <summary>
        /// Vytvoří ModelWrapper attribute
        /// </summary>
        /// <param name="targetType">POCO typ pro který se má vygenerovat Model</param>
        public ModelWrapperAttribute(System.Type targetType)
        {
            TargetType = targetType;
        }
    }
}