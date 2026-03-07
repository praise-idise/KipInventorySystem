using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.StockIssues.DTOs;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.StockIssues.Validators;

public class CreateStockIssueRequestValidator : AbstractValidator<CreateStockIssueRequest>
{
    public CreateStockIssueRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new StockIssueLineRequestValidator());

        RuleFor(x => x.ReferenceType)
            .Must(x => x is null || x == StockMovementReferenceType.SalesOrder)
            .WithMessage("Only SalesOrder is currently supported as stock issue reference type.");

        RuleFor(x => x.ReferenceId)
            .NotEmpty()
            .When(x => x.ReferenceType == StockMovementReferenceType.SalesOrder)
            .WithMessage("ReferenceId is required when reference type is SalesOrder.");
    }
}

public class StockIssueLineRequestValidator : AbstractValidator<StockIssueLineRequest>
{
    public StockIssueLineRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
