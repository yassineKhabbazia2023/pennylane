using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.Pagers;

public class PennylaneCompanyPager : PennylanePaged
{
    [JsonPropertyName("companies")]
    public List<PennylaneCompany> Companies { get; set; } = [];
}