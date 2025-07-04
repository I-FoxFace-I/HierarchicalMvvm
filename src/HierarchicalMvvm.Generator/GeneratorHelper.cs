using System.Linq;
using System.Text;


namespace HierarchicalMvvm.Generator;

public static class GeneratorHelper
{
    
    private static void GeneratePrimitiveProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        var defaultValue = property.TypeName == "string" ? " = string.Empty" : "";

        // Backing field

        if (property.IsNullable)
        {
            sb.AppendLine($"        private {property.TypeName}? {fieldName};");
        }
        else
        {
            sb.AppendLine($"        private {property.TypeName} {fieldName}{defaultValue};");
        }


        sb.AppendLine();

        // Public property with notification
        if (property.IsNullable)
        {
            sb.AppendLine($"        public {property.TypeName}? {property.Name}");
        }
        else
        {
            sb.AppendLine($"        public {property.TypeName} {property.Name}");
        }
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine("            set");
        sb.AppendLine("            {");
        sb.AppendLine($"                if ({fieldName} != value)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    {fieldName} = value;");
        sb.AppendLine($"                    OnPropertyChangedInternal(nameof({property.Name}));");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }
    private static void GenerateModelObjectProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        var modelTypeName = property.FullModelTypeName?.Split('.').Last();

        if (property.IsNullable)
        {
            sb.AppendLine($"        private {modelTypeName}? {fieldName};");
            sb.AppendLine();
            sb.AppendLine($"        public {modelTypeName}? {property.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {fieldName};");
            sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        private {modelTypeName} {fieldName} = new {modelTypeName}{{ }};");
            sb.AppendLine();
            sb.AppendLine($"        public {modelTypeName} {property.Name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {fieldName};");
            sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
            sb.AppendLine("        }");
        }


    }
    private static void GenerateModelCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = StringHelpers.ToCamelCase(property.Name);
        var elementModelType = property.FullModelTypeName?.Split('.').Last();

        sb.AppendLine($"        private DeepObservableCollection<{elementModelType.Split('.').Last()}> {fieldName};");

        sb.AppendLine($"        public DeepObservableCollection<{elementModelType.Split('.').Last()}> {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }
    private static void GeneratePrimitiveCollectionProperty(StringBuilder sb, PropertyInfo property)
    {
        var fieldName = $"_{char.ToLower(property.Name[0])}{property.Name.Substring(1)}";
        sb.AppendLine($"        private NodeObservableCollection<{property.CollectionElementType}> {fieldName};");

        sb.AppendLine($"        public NodeObservableCollection<{property.CollectionElementType}> {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {fieldName};");
        sb.AppendLine($"            set => SetObjectProperty(ref {fieldName}, value);");
        sb.AppendLine("        }");
    }
}
