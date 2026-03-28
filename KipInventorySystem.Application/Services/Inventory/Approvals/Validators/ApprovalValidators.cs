using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Approvals.Validators;

public class ApprovalDecisionRequestValidator : AbstractValidator<ApprovalDecisionRequest>
{
    public ApprovalDecisionRequestValidator()
    {
        RuleFor(x => x.Comment)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
