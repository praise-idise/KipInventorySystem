using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Products.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductDTO>
{
    private const string Code3Pattern = @"^[A-Za-z0-9]{3}$";

    public CreateProductValidator()
    {
        RuleFor(x => x.CategoryCode)
            .NotEmpty()
            .Matches(Code3Pattern)
            .WithMessage("CategoryCode must be exactly 3 letters or numbers.");
        RuleFor(x => x.BrandCode)
            .NotEmpty()
            .Matches(Code3Pattern)
            .WithMessage("BrandCode must be exactly 3 letters or numbers.");
        RuleForEach(x => x.VariantAttributes)
            .SetValidator(new CreateProductVariantAttributeValidator());
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DefaultSupplierId).NotEmpty();
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0);
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductDTO>
{
    private const string Code3Pattern = @"^[A-Za-z0-9]{3}$";
    
    public UpdateProductValidator()
    {
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.CategoryCode)
            .Matches(Code3Pattern)
            .WithMessage("CategoryCode must be exactly 3 letters or numbers.")
            .When(x => x.CategoryCode is not null);
        RuleFor(x => x.BrandCode)
            .Matches(Code3Pattern)
            .WithMessage("BrandCode must be exactly 3 letters or numbers.")
            .When(x => x.BrandCode is not null);
        RuleForEach(x => x.VariantAttributes!)
            .SetValidator(new UpdateProductVariantAttributeValidator())
            .When(x => x.VariantAttributes is not null);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(20).When(x => x.UnitOfMeasure is not null);
        RuleFor(x => x.DefaultSupplierId).NotEmpty().When(x => x.DefaultSupplierId.HasValue);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0).When(x => x.ReorderThreshold.HasValue);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0).When(x => x.ReorderQuantity.HasValue);
    }

    private static bool HasAtLeastOneField(UpdateProductDTO request)
    {
        return request.CategoryCode is not null ||
               request.BrandCode is not null ||
               request.VariantAttributes is not null ||
               request.Name is not null ||
               request.Description is not null ||
               request.UnitOfMeasure is not null ||
               request.ReorderThreshold.HasValue ||
               request.ReorderQuantity.HasValue ||
               request.DefaultSupplierId.HasValue ||
               request.IsActive.HasValue;
    }
}

public class CreateProductVariantAttributeValidator : AbstractValidator<CreateProductVariantAttributeDTO>
{
    private const string AttributeCodePattern = @"^[A-Za-z0-9]{1,30}$";

    public CreateProductVariantAttributeValidator()
    {
        ApplyVariantRules();
    }

    private void ApplyVariantRules()
    {
        RuleFor(x => x.AttributeName).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AttributeCode)
            .NotEmpty()
            .Matches(AttributeCodePattern)
            .WithMessage("AttributeCode must contain only letters or numbers and be 1-30 characters long.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateProductVariantAttributeValidator : AbstractValidator<ProductVariantAttributeDTO>
{
    private const string AttributeCodePattern = @"^[A-Za-z0-9]{1,30}$";

    public UpdateProductVariantAttributeValidator()
    {
        RuleFor(x => x.AttributeName).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AttributeCode)
            .NotEmpty()
            .Matches(AttributeCodePattern)
            .WithMessage("AttributeCode must contain only letters or numbers and be 1-30 characters long.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
