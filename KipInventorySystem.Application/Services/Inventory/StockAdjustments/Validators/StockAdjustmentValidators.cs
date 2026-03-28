using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.StockAdjustments.Validators;

public class CreateStockAdjustmentDraftRequestValidator : AbstractValidator<CreateStockAdjustmentDraftRequest>
{
    public CreateStockAdjustmentDraftRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new CreateStockAdjustmentLineRequestValidator());
    }
}

public class CreateStockAdjustmentLineRequestValidator : AbstractValidator<CreateStockAdjustmentLineRequest>
{
    public CreateStockAdjustmentLineRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityAfter).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost)
            .GreaterThan(0)
            .When(x => x.UnitCost.HasValue);
    }
}
