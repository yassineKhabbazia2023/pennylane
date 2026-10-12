// <copyright file="PennylaneCompanyFirm.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Pennylane.Domain.ExternalApis.Models.FirmAPI
{
    public class PennylaneCompanyFirm
    {
        public int Id { get; set; }
        [JsonPropertyName("client_code")]
        public string ClientCode { get; set; } = string.Empty;
    }
}
