// <copyright file="PennylaneFirm.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.FirmAPI
{
    public class PennylaneFirm
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("reg_no")]
        public string? RegNo { get; set; }
    }
}
