namespace Pennylane.Application.Models.Signatory;

public class SignatoryDetails
{
    public required Terms Terms { get; set; }

    public required IEnumerable<SignatoryAccount> Accounts { get; set; }
}
