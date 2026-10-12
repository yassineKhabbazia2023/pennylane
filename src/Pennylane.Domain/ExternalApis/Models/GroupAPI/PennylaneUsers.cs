using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.GroupAPI;

public class PennylaneUsers
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = default!;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = default!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = default!;

    [JsonPropertyName("blocked_at")]
    public object BlockedAt { get; set; } = default!;

    [JsonPropertyName("role")]
    public string Role { get; set; } = default!;

    [JsonPropertyName("sub_role")]
    public string? SubRole { get; set; }

    [JsonPropertyName("authentication_method")]
    public string? AuthenticationMethod { get; set; }
}