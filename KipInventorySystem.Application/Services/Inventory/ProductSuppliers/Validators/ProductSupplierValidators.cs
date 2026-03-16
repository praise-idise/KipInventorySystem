using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.ProductSuppliers.Validators;

public class CreateProductSupplierRequestValidator : AbstractValidator<CreateProductSupplierRequest>
{
    public CreateProductSupplierRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
    }
}

public class UpdateProductSupplierRequestValidator : AbstractValidator<UpdateProductSupplierRequest>
{
    public UpdateProductSupplierRequestValidator()
    {
    }
}
