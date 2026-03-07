using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Products.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    private const string Code3Pattern = @"^[A-Za-z0-9]{3}$";
    private const string VariantPattern = @"^[A-Za-z0-9]{2,10}$";

    public CreateProductRequestValidator()
    {
        RuleFor(x => x.CategoryCode)
            .NotEmpty()
            .Matches(Code3Pattern)
            .WithMessage("CategoryCode must be exactly 3 letters or numbers.");
        RuleFor(x => x.BrandCode)
            .NotEmpty()
            .Matches(Code3Pattern)
            .WithMessage("BrandCode must be exactly 3 letters or numbers.");
        RuleFor(x => x.VariantCode)
            .NotEmpty()
            .Matches(VariantPattern)
            .WithMessage("VariantCode must be 2-10 letters or numbers.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0);
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(20).When(x => x.UnitOfMeasure is not null);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0).When(x => x.ReorderThreshold.HasValue);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0).When(x => x.ReorderQuantity.HasValue);
    }

    private static bool HasAtLeastOneField(UpdateProductRequest request)
    {
        return request.Name is not null ||
               request.Description is not null ||
               request.UnitOfMeasure is not null ||
               request.ReorderThreshold.HasValue ||
               request.ReorderQuantity.HasValue ||
               request.DefaultSupplierId.HasValue ||
               request.IsActive.HasValue;
    }
}
