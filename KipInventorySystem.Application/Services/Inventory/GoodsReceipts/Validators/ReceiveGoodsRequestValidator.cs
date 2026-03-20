using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.GoodsReceipts.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.GoodsReceipts.Validators;

public class ReceiveGoodsRequestValidator : AbstractValidator<ReceiveGoodsRequest>
{
    public ReceiveGoodsRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(line => line.PurchaseOrderLineId).Distinct().Count() == lines.Count)
            .WithMessage("Duplicate purchase order lines are not allowed in a goods receipt.");
        RuleForEach(x => x.Lines).SetValidator(new ReceiveGoodsLineRequestValidator());
    }
}

public class ReceiveGoodsLineRequestValidator : AbstractValidator<ReceiveGoodsLineRequest>
{
    public ReceiveGoodsLineRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderLineId).NotEmpty();
        RuleFor(x => x.QuantityReceivedNow).GreaterThan(0);
    }
}
