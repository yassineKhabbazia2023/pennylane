// <copyright file="EmailRecapStatus.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace Pennylane.Domain.Enums;

public enum EmailRecapBatchStatus
{
    Generating = 0,
    Ready = 1,
    Sending = 2,
    Completed = 3,
    Failed = 4
}

public enum EmailRecapItemStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
