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
