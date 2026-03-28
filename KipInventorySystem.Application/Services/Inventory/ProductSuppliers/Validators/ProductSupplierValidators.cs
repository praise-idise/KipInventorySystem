using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.ProductSuppliers.Validators;

public class CreateProductSupplierRequestValidator : AbstractValidator<CreateProductSupplierRequest>
{
    public CreateProductSupplierRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.UnitCost).GreaterThan(0);
    }
}

public class UpdateProductSupplierRequestValidator : AbstractValidator<UpdateProductSupplierRequest>
{
    public UpdateProductSupplierRequestValidator()
    {
        RuleFor(x => x.UnitCost).GreaterThan(0);
    }
}
