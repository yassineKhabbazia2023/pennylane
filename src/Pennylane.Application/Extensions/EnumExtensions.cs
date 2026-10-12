using System.ComponentModel;
using System.Reflection;

namespace Pennylane.Application.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        Type? type = value?.GetType();

        if (type == null) return string.Empty;

        FieldInfo? field = type.GetField(value!.ToString());

        if (field == null) return string.Empty;

        DescriptionAttribute? attribute = field!.GetCustomAttribute<DescriptionAttribute>();

        return attribute == null ? value.ToString() : attribute.Description;
    }
}