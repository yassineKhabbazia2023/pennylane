// <copyright file="PennylaneCompanyFirmPager.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.Pagers;

public class PennylaneCompanyFirmPager : PennylanePaged
{
    [JsonPropertyName("items")]
    public List<PennylaneCompanyFirm> Items { get; set; } = [];
}
