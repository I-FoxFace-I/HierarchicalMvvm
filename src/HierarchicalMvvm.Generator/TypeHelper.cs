using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;


namespace HierarchicalMvvm.Generator;

public class TypeHelper
{
    private static HashSet<string> _primitiveTypes = new HashSet<string>
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "nint", "nuint",
        "long", "ulong", "short", "ushort", "string", "DateTime", "DateTimeOffset", "Guid", "Index",
        "TimeSpan", "Version", "Type", "Uri", "TimeOnly", "DateOnly", "Half", "BigInteger", "Complex",
        "Range", "Rune"
    };

    private static HashSet<string> _collectionTypes = new HashSet<string>
    {
        "IEnumerable", "ICollection", "IReadOnlyCollection", "IList", "IReadOnlyList",
        "IImmutableSet", "ISet", "IReadOnlySet", "IImmutableSet", "ILookup", "IDictionary",
        "IReadOnlyDictionary", "List", "ReadOnlyCollection", "ReadOnlyList", "ImmutableList",
        "HashSet", "ReadOnlySet", "ImmutableHashSet", "GroupTable", "Dictionary", "ReadOnlyDictionary",
    };

    public static string StringType => "string";
    public static string EnumerableInterface => "IEnumerable";
    public static string DeepObjectType => "DeepObservableObject";
    public static string DeepCollectionType => "DeepObservableCollection";
    public static IImmutableSet<string> PrimitiveTypes = _primitiveTypes.ToImmutableHashSet();
    public static IImmutableSet<string> CollectionTypes = _collectionTypes.ToImmutableHashSet();

    public static string GetTypeName(ITypeSymbol type)
    {
        if(type is INamedTypeSymbol namedType)
        {
            if (namedType.Name.EndsWith("?"))
                return namedType.Name.Substring(0, namedType.Name.Length - 1);

            return namedType.Name;
        }

        return string.Empty;
    }
    public static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            var typeName = GetTypeName(namedType);

            if (_primitiveTypes.Contains(typeName))
                return false;

            if (_collectionTypes.Contains(typeName) && namedType.TypeArguments.Length == 1)
                return true;

            if (namedType.BaseType is INamedTypeSymbol baseType)
            {
                if (_collectionTypes.Contains(GetTypeName(baseType)) && namedType.TypeArguments.Length == 1)
                    return true;
            }

            if (namedType.AllInterfaces.Any(i => i.Name == "IEnumerable" && i.TypeArguments.Length == 1))
                return true;
        }

        return false;
    }
    public static bool TryGetElementType(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        elementType = default;

        if (type is INamedTypeSymbol namedType)
        {
            var typeName = GetTypeName(namedType);

            if (_primitiveTypes.Contains(typeName))
                return false;

            // Direct generic collection
            if (namedType.TypeArguments.Length == 1 && _collectionTypes.Contains(namedType.Name))
            {
                elementType = namedType.TypeArguments.First();

                return true;
            }

            // Check implemented interfaces
            if (namedType.AllInterfaces.FirstOrDefault(i => i.Name == "IEnumerable" && i.TypeArguments.Length == 1) is INamedTypeSymbol interfaceType)
            {
                elementType = interfaceType.TypeArguments.First();

                return true;
            }
        }

        return false;
    }
}
