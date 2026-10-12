// <copyright file="EmailRecapOptions.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

public class EmailRecapOptions
{
    public const string EmailRecapSettings = "EmailRecapSettings";

    public bool Enabled { get; set; } = true;

    [Range(0, 23)]
    public int ReferenceHour { get; set; } = 9;

    [Required]
    public string TemplateName { get; set; } = "Document deposit pennylane";

    [Range(1, 168)]
    public int LookbackHours { get; set; } = 24;

    [Required]
    [EmailAddress]
    public required string SenderEmail { get; set; }

    [Required]
    [Url]
    public required string LoginUrl { get; set; }

    [Range(1, 50)]
    public int MaxParallelEmailSends { get; set; } = 20;

    [Range(10, 500)]
    public int BatchSize { get; set; } = 100;

    [Required]
    public string GenerationCron { get; set; } = "0 8 * * *";

    [Required]
    public string SendCron { get; set; } = "0 9 * * *";

    [Required]
    public string ServiceBusTopic { get; set; } = "feedcenter";
}
