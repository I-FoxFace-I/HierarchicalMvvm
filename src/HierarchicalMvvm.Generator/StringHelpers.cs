using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Globalization;
using System;


namespace HierarchicalMvvm.Generator;

public static class StringHelpers
{
    public static bool IsSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;

        if (str.StartsWith("_") || str.EndsWith("_") || str.Contains("__"))
            return false;

        foreach (char c in str)
        {
            if (!char.IsLower(c) && !char.IsNumber(c) && c != '_')
                return false;
        }

        return true;
    }
    public static bool IsTitleCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;

        if (!char.IsUpper(str[0]))
            return false;

        for (int i = 1; i < str.Length; i++)
        {
            if (!char.IsLetter(str[i]) && !char.IsDigit(str[i]))
                return false;
        }

        return true;
    }
    public static bool IsCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;

        if (!char.IsLower(str[0]))
            return false;

        for (int i = 1; i < str.Length; i++)
        {
            if (!char.IsLetterOrDigit(str[i]))
                return false;
        }

        return true;
    }
    private static string SnakeCaseToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        var titleCaseName = SnakeCaseToTitleCase(str);

        return char.ToLower(titleCaseName[0]) + titleCaseName.Substring(1);
    }
    private static string SnakeCaseToTitleCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return str.Split('_').Where(segment => !string.IsNullOrEmpty(segment))
                              .Select(segment => char.ToUpper(segment[0]) + segment.Substring(1))
                              .Aggregate(string.Empty, (current, segment) => current + segment);
    }
    private static string CamelCaseToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        var builder = new StringBuilder();

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                    builder.Append('_');

                builder.Append(char.ToLower(c));
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
    private static string CamelCaseToTitleCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return ToTileCase(str);
    }
    private static string TitleCaseToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        var sb = new StringBuilder();

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');

                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
    private static string TitleCaseToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return string.Concat(str[0].ToString().ToLower(), str.Substring(1));
    }
    public static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        str = str.Replace('-', '_');
        str = str.Replace('/', '_');

        if (IsSnakeCase(str))
            return str;
        else if (IsCamelCase(str))
            return CamelCaseToSnakeCase(str);
        else if (IsTitleCase(str))
            return TitleCaseToSnakeCase(str);

        return TitleCaseToSnakeCase(str);
    }
    public static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        str = str.Replace('-', '_');

        if (IsSnakeCase(str))
            return SnakeCaseToCamelCase(str);
        else if (IsCamelCase(str))
            return str;
        else if (IsTitleCase(str))
            return TitleCaseToCamelCase(str);

        return SnakeCaseToCamelCase(str);
    }
    public static string ToTileCase(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }
        str = str.Replace('-', '_');

        if (IsSnakeCase(str))
            return SnakeCaseToTitleCase(str);
        else if (IsCamelCase(str))
            return CamelCaseToTitleCase(str);
        else if (IsTitleCase(str))
            return str;
        else
            return SnakeCaseToTitleCase(str);
    }
}
