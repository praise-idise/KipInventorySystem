using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Suppliers.Validators;

public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.ContactPerson).MaximumLength(120);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
    }
}

public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40).When(x => x.Phone is not null);
        RuleFor(x => x.ContactPerson).MaximumLength(120).When(x => x.ContactPerson is not null);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0).When(x => x.LeadTimeDays.HasValue);
    }

    private static bool HasAtLeastOneField(UpdateSupplierRequest request)
    {
        return request.Name is not null ||
               request.Email is not null ||
               request.Phone is not null ||
               request.ContactPerson is not null ||
               request.LeadTimeDays.HasValue ||
               request.IsActive.HasValue;
    }
}
