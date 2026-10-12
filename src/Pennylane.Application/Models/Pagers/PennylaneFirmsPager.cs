// <copyright file="PennylaneFirmsPager.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Domain.ExternalApis.Models.FirmAPI;
using System.Text.Json.Serialization;

namespace Pennylane.Application.Models.Pagers;

public class PennylaneFirmsPager : PennylanePaged
{
    [JsonPropertyName("firms")]
    public List<PennylaneFirm> Firms { get; set; } = [];
}
