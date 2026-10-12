namespace Pennylane.Application.Models.Signatory;

public class SignatoryValidationHistory
{
    public required string AccountNumber { get; set; }

    public required bool Status { get; set; }

    public required int TermsId { get; set; }

    public required DateTime CreationDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public required bool CanAccess { get; set; }
}
