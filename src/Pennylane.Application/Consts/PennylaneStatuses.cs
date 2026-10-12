namespace Pennylane.Application.Consts;

/// <summary>
/// Defines status values used by Pennylane controller responses.
/// </summary>
public static class PennylaneStatuses
{
    /// <summary>
    /// Operation failed.
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// User already has access to the company.
    /// </summary>
    public const string AlreadyHasAccess = "already_has_access";

    /// <summary>
    /// Operation completed by creating a new resource.
    /// </summary>
    public const string Created = "created";

    /// <summary>
    /// Company was created on the default hub because the specified hub was not found.
    /// </summary>
    public const string CreatedOnDefault = "created_on_default";

    /// <summary>
    /// Existing Pennylane user was granted access to the company.
    /// </summary>
    public const string ExistingUserAccessGranted = "existing_user_access_granted";

    /// <summary>
    /// User was not found in the company.
    /// </summary>
    public const string UserNotFound = "user_not_found";

    /// <summary>
    /// Access was revoked.
    /// </summary>
    public const string Revoked = "revoked";

    /// <summary>
    /// Company already exists.
    /// </summary>
    public const string AlreadyExists = "already_exists";

    /// <summary>
    /// Company needs to be created later.
    /// </summary>
    public const string ToCreate = "to_create";

    /// <summary>
    /// Operation succeeded with partial user additions.
    /// </summary>
    public const string PartialSuccess = "partial_success";

    /// <summary>
    /// Company was created and the SaaS plan matches the requested plan.
    /// </summary>
    public const string Validated = "validated";

    /// <summary>
    /// Company exists in Pennylane but is not assigned to any firm; the client must request to be assigned to your firm.
    /// </summary>
    public const string RequiresFirmAssignment = "requires_firm_assignment";

    /// <summary>
    /// Company already exists in Pennylane and is attached to another firm; must request its transfer from the current firm.
    /// </summary>
    public const string RequiresFirmTransfer = "requires_firm_transfer";
}
