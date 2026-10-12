using System.ComponentModel.DataAnnotations;

namespace Pennylane.Application.Configurations.Options;

public class PennylaneServiceOptions
{
    public const string PennylaneService = "PennylaneService";

    [Required(AllowEmptyStrings = false)]
    public required string ApiUrl { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string ApiFirmUrl { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string AuthenticationMode { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string CollaboratorAuthenticationMode { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string DefaultRole { get; set; }

    public bool EnableEventHandler { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string SupportedSaasPlans { get; set; }

    [Required(AllowEmptyStrings =false, ErrorMessage = "Token is required.")]
    public required string Token { get; set; }

    [Range(1, 365, ErrorMessage = "Days must be between 1 and 365.")]
    public int LastSyncDateRetentionDays { get; set; } = 7;

    public bool EnableUpdatePennylaneUsersEmails { get; set; }

    public List<PennylanePlansRoleAssignmentOptions> PlansRoleAssignments { get; set; } = new List<PennylanePlansRoleAssignmentOptions>();

    /// <summary>
    /// If set, this role will be used for all clients regardless of the company's SaaS plan.
    /// This overrides the PlansRoleAssignments and DefaultRole settings.
    /// </summary>
    public string? OverrideClientRole { get; set; }

    /// <summary>
    /// Feature flag to control the user identification method for grant access operations.
    /// If absent or false (default): Uses user_email for identification (new mechanism).
    /// If true: Uses user_id for identification (legacy mechanism).
    /// </summary>
    public bool UseUserIdForGrantAccess { get; set; }

    /// <summary>
    /// Number of retries when the company users endpoint returns 404 "Company not found in your scope".
    /// Happens right after company creation: Pennylane propagates the firm scope asynchronously.
    /// </summary>
    [Range(0, 10)]
    public int CompanyUsersRetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds between two retries on the company users endpoint (exponential backoff).
    /// </summary>
    [Range(0, 60000)]
    public int CompanyUsersRetryDelayInMilliseconds { get; set; } = 500;
}