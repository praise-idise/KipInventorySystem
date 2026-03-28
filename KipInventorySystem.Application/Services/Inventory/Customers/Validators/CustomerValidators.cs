using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Customers.Validators;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(40).When(x => x.Phone is not null);
    }
}

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Name is not null || x.Email is not null || x.Phone is not null)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Email).MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(40).When(x => x.Phone is not null);
    }
}
