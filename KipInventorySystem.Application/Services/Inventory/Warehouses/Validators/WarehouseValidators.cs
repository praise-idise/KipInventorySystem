using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Warehouses.Validators;

public class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    private const string StatePattern = @"^[A-Za-z][A-Za-z\s\-]{1,99}$";

    public CreateWarehouseRequestValidator()
    {
        RuleFor(x => x.State)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(StatePattern)
            .WithMessage("State must contain only letters, spaces, or '-'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Location).MaximumLength(250);
        RuleFor(x => x.CapacityUnits).GreaterThanOrEqualTo(0);
    }
}

public class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.Location).MaximumLength(250).When(x => x.Location is not null);
        RuleFor(x => x.CapacityUnits).GreaterThanOrEqualTo(0).When(x => x.CapacityUnits.HasValue);
    }

    private static bool HasAtLeastOneField(UpdateWarehouseRequest request)
    {
        return request.Name is not null ||
               request.Location is not null ||
               request.CapacityUnits.HasValue ||
               request.IsActive.HasValue;
    }
}
