// <copyright file="PennylaneFirmTokenOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Configurations.Options;

public class PennylaneFirmTokenOptions : List<PennylaneFirmTokenItem>

{
}

public class PennylaneFirmTokenItem
{
    public string Id { get; set; } = string.Empty;
    public List<string> Tokens { get; set; } = new();
}
