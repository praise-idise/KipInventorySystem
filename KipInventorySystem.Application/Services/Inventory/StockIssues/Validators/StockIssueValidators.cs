using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.StockIssues.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.StockIssues.Validators;

public class CreateStockIssueRequestValidator : AbstractValidator<CreateStockIssueRequest>
{
    public CreateStockIssueRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Notes)
            .NotEmpty()
            .When(x => x.Reason == Domain.Enums.StockIssueReason.Other)
            .WithMessage("Notes are required when stock issue reason is Other.");
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(line => line.ProductId).Distinct().Count() == lines.Count)
            .WithMessage("Duplicate products are not allowed in a stock issue request.");
        RuleForEach(x => x.Lines).SetValidator(new StockIssueLineRequestValidator());
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
