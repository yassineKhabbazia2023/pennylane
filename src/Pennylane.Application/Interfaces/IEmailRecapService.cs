// <copyright file="IEmailRecapService.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using Pennylane.Application.Models.EmailRecap;

namespace Pennylane.Application.Interfaces;

public interface IEmailRecapService
{
    Task<EmailRecapGenerationResult> GenerateDailyRecapsAsync();
    Task<EmailRecapSendResult> SendPendingRecapsAsync();
}
