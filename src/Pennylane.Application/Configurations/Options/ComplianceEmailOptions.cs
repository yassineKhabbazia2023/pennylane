using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

public class ComplianceEmailOptions
{
    public const string Section = "ComplianceEmailSettings";

    [Required]
    public required string AcceptanceTemplateName { get; set; }

    [Required]
    public required string RefusalTemplateName { get; set; }

    [Required]
    public required string CustomerRelationRefusalTemplateName { get; set; }

    [Required]
    public required string SenderEmail { get; set; }

    [Required]
    public required string LoginPage { get; set; }

    public string? BccEmail { get; set; }

    public string ServiceBusTopic { get; set; } = "feedcenter";

    [Required]
    public required string CustomerRelationRefusalRecipientEmail { get; set; }
}
