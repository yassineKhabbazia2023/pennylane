using System.Text.Json;

namespace Pennylane.Application.Extensions;

public static class SerializerExtensions
{
    public static readonly JsonSerializerOptions s_writeOptions = new JsonSerializerOptions()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions s_readOptions = new JsonSerializerOptions()
    {
        AllowTrailingCommas = true
    };

    public static string Serialize<T>(this T value)
    {
        return JsonSerializer.Serialize(value, s_writeOptions);
    }

    public static T Deserialize<T>(this string json) where T : new()
    {
        var data = JsonSerializer.Deserialize<T>(json, s_readOptions);

        return data ?? new T();
    }
}