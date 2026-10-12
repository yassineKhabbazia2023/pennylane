using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.GroupAPI;

public class PennylaneResult<T>
{
    [JsonPropertyName("id")]
    public T Id { get; set; } = default!;
}