using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.OpeningBalances.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.OpeningBalances.Validators;

public class CreateOpeningBalanceRequestValidator : AbstractValidator<CreateOpeningBalanceRequest>
{
    public CreateOpeningBalanceRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(line => line.ProductId).Distinct().Count() == lines.Count)
            .WithMessage("Duplicate products are not allowed in opening balance lines.");
        RuleForEach(x => x.Lines).SetValidator(new CreateOpeningBalanceLineRequestValidator());
    }
}

public class CreateOpeningBalanceLineRequestValidator : AbstractValidator<CreateOpeningBalanceLineRequest>
{
    public CreateOpeningBalanceLineRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThan(0);
    }
}
