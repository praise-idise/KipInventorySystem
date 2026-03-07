using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.TransferRequests.Validators;

public class CreateTransferRequestDraftRequestValidator : AbstractValidator<CreateTransferRequestDraftRequest>
{
    public CreateTransferRequestDraftRequestValidator()
    {
        RuleFor(x => x.SourceWarehouseId).NotEmpty();
        RuleFor(x => x.DestinationWarehouseId).NotEmpty();
        RuleFor(x => x).Must(x => x.SourceWarehouseId != x.DestinationWarehouseId)
            .WithMessage("Source and destination warehouses must be different.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new CreateTransferRequestLineRequestValidator());
    }
}

public class CreateTransferRequestLineRequestValidator : AbstractValidator<CreateTransferRequestLineRequest>
{
    public CreateTransferRequestLineRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityRequested).GreaterThan(0);
    }
}
