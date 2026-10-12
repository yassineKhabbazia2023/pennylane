// <copyright file="PnlRole.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Application.Models.Pennylane;

public class PnlRole
{
    public int AccountId { get; set; }

    public int ContactId { get; set; }

    public string Role { get; set; } = string.Empty;
}
