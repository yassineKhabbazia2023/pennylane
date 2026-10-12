using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.FirmAPI;

/// <summary>
/// Body of PUT /companies/{id} setting the accounting contact slots; null slots are left untouched.
/// </summary>
public class UpdateCompanyAccountingContactsRequest
{
    [JsonPropertyName("accounting_manager_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountingManagerId { get; set; }

    [JsonPropertyName("accounting_supervisor_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountingSupervisorId { get; set; }

    [JsonPropertyName("accountant_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountantId { get; set; }
}
