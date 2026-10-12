using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.Pagers;

public class PennylanePaged
{
    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }
}