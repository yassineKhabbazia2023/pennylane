using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.Pagers;

public class PennylaneUsersPager : PennylanePaged
{
    [JsonPropertyName("users")]
    public List<PennylaneUsers> Users { get; set; } = [];
}