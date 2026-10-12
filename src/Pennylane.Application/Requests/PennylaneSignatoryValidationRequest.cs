namespace Pennylane.Application.Requests;

public class PennylaneSignatoryValidationRequest
{
    public required IEnumerable<string> AccountNumbers { get; set; }

    public required bool Status { get; set; }
}
