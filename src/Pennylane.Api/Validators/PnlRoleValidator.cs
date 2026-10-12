using FluentValidation;
using Pennylane.Application.Consts;
using Pennylane.Application.Models.Pennylane;

namespace Pennylane.WebApi.Validators;

public class PnlRoleValidator : AbstractValidator<PnlRole>
{
    public PnlRoleValidator()
    {
        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .WithMessage("AccountId must be greater than 0.");

        RuleFor(x => x.ContactId)
            .GreaterThan(0)
            .WithMessage("ContactId must be greater than 0.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(PennylaneRoles.IsValidRoleCode)
            .WithMessage(x => $"The role '{x.Role}' is not valid. Valid roles are: {PennylaneRoles.GetValidRoleCodesAsString()}.");
    }
}
