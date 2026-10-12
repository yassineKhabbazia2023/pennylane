using System.Text.Json.Serialization;
using System;

namespace Pennylane.Domain.ExternalApis.Models.FirmAPI;

public class PennylaneDmsFile
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class PennylaneDmsFilesResponse
{
    [JsonPropertyName("items")]
    public List<PennylaneDmsFile> Items { get; set; } = new();

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}
