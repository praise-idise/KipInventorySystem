using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Customers.Validators;
using KipInventorySystem.Application.Services.Inventory.SalesOrders.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.SalesOrders.Validators;

public class CreateSalesOrderDraftRequestValidator : AbstractValidator<CreateSalesOrderDraftRequest>
{
    public CreateSalesOrderDraftRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue || x.Customer is not null)
            .WithMessage("Select an existing customer or provide a new customer.");

        RuleFor(x => x)
            .Must(x => !x.CustomerId.HasValue || x.Customer is null)
            .WithMessage("Provide either CustomerId or Customer, not both.");

        RuleFor(x => x.CustomerId).NotEmpty().When(x => x.CustomerId.HasValue);
        RuleFor(x => x.Customer!).SetValidator(new CreateCustomerRequestValidator()).When(x => x.Customer is not null);
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(line => line.ProductId).Distinct().Count() == lines.Count)
            .WithMessage("Duplicate products are not allowed in a sales order.");
        RuleForEach(x => x.Lines).SetValidator(new CreateSalesOrderLineRequestValidator());
    }
}

public class UpdateSalesOrderDraftRequestValidator : AbstractValidator<UpdateSalesOrderDraftRequest>
{
    public UpdateSalesOrderDraftRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CustomerId.HasValue || x.WarehouseId.HasValue || x.Notes is not null || x.Lines is not null)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.CustomerId).NotEmpty().When(x => x.CustomerId.HasValue);
        RuleFor(x => x.WarehouseId).NotEmpty().When(x => x.WarehouseId.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
        RuleFor(x => x.Lines).NotEmpty().When(x => x.Lines is not null);
        RuleForEach(x => x.Lines!)
            .SetValidator(new CreateSalesOrderLineRequestValidator())
            .When(x => x.Lines is not null);
    }
}

public class CreateSalesOrderLineRequestValidator : AbstractValidator<CreateSalesOrderLineRequest>
{
    public CreateSalesOrderLineRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityOrdered).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class FulfillSalesOrderRequestValidator : AbstractValidator<FulfillSalesOrderRequest>
{
    public FulfillSalesOrderRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new FulfillSalesOrderLineRequestValidator());
    }
}

public class FulfillSalesOrderLineRequestValidator : AbstractValidator<FulfillSalesOrderLineRequest>
{
    public FulfillSalesOrderLineRequestValidator()
    {
        RuleFor(x => x.SalesOrderLineId).NotEmpty();
        RuleFor(x => x.QuantityFulfilledNow).GreaterThan(0);
    }
}
