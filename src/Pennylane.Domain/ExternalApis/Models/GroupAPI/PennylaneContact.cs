using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.GroupAPI;

public class PennylaneContact
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; set; } = default!;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = default!;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = default!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = default!;

    [JsonPropertyName("skip_welcome_email")]
    public bool SkipWelcomeEmail { get; set; } = default!;

    [JsonPropertyName("authentication_method")]
    public string AuthenticationMethod { get; set; } = default!;
}