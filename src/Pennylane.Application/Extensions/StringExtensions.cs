using System.Globalization;
using System.Reflection;
using System.Web;

namespace Pennylane.Application.Extensions;

public static class StringExtensions
{
    public static IEnumerable<KeyValuePair<string, string>> ToKeyValuePairList<T>(this T param)
    {
        if (EqualityComparer<T>.Default.Equals(param, default))
        {
            yield break;
        }

        var properties = param?.GetType().GetProperties();
        if (properties == null)
        {
            yield break;
        }

        foreach (var property in properties)
        {
            var key = property.Name ?? string.Empty;
            var value = property.GetValue(param)?.ToString() ?? string.Empty;
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    public static string ToQueryParam<T>(this T param)
    {
        if (EqualityComparer<T>.Default.Equals(param, default))
        {
            return string.Empty;
        }

        var properties = param?.GetType().GetProperties();
        if (properties == null)
        {
            return string.Empty;
        }

        var queryParams = from p in properties
                          where p.GetValue(param, null) != null
                          let value = GetPropertyValue(p, param!)
                          select $"{HttpUtility.UrlEncode(p.Name.ToLowerInvariant())}={HttpUtility.UrlEncode(value)}";

        return string.Join("&", queryParams);
    }

    private static string GetPropertyValue(PropertyInfo property, object obj)
    {
        var value = property.GetValue(obj, null);

        // Check if the value is an IQueryable or IEnumerable
        if (value is IQueryable || value is System.Collections.IEnumerable && !(value is string))
        {
            return string.Join(",", ((System.Collections.IEnumerable)value).Cast<object>().Select(item => item.ToString()));
        }

        return value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Capitalizes each word in the string (first letter uppercase, rest lowercase).
    /// </summary>
    public static string ToCapitalize(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(value.ToLower(CultureInfo.GetCultureInfo("fr-FR")));
    }
}