using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.PurchaseOrders.Validators;

public class CreatePurchaseOrderDraftRequestValidator : AbstractValidator<CreatePurchaseOrderDraftRequest>
{
    public CreatePurchaseOrderDraftRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new CreatePurchaseOrderLineRequestValidator());
    }
}

public class UpdatePurchaseOrderDraftRequestValidator : AbstractValidator<UpdatePurchaseOrderDraftRequest>
{
    public UpdatePurchaseOrderDraftRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.SupplierId).NotEmpty().When(x => x.SupplierId.HasValue);
        RuleFor(x => x.WarehouseId).NotEmpty().When(x => x.WarehouseId.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
        RuleFor(x => x.Lines).NotEmpty().When(x => x.Lines is not null);
        RuleForEach(x => x.Lines!)
            .SetValidator(new CreatePurchaseOrderLineRequestValidator())
            .When(x => x.Lines is not null);
    }

    private static bool HasAtLeastOneField(UpdatePurchaseOrderDraftRequest request)
    {
        return request.SupplierId.HasValue ||
               request.WarehouseId.HasValue ||
               request.ExpectedArrivalDate.HasValue ||
               request.Notes is not null ||
               request.Lines is not null;
    }
}

public class CreatePurchaseOrderLineRequestValidator : AbstractValidator<CreatePurchaseOrderLineRequest>
{
    public CreatePurchaseOrderLineRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityOrdered).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThan(0);
    }
}
