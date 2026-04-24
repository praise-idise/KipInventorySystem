using FluentValidation;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;

namespace KipInventorySystem.Application.Services.Inventory.Products.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductDTO>
{
    private const string CodePattern = "^[A-Za-z0-9]{3}$";

    public CreateProductValidator()
    {
        RuleFor(x => x.CategoryCode)
            .NotEmpty()
            .Matches(CodePattern)
            .WithMessage("CategoryCode must be a 3-character alphanumeric code.");
        RuleFor(x => x.Brand)
            .NotEmpty()
            .MaximumLength(80);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.UnitOfMeasure)
               .IsInEnum()
               .WithMessage("UnitOfMeasure must be a valid unit of measure.");
        RuleFor(x => x.Color).MaximumLength(30);
        RuleFor(x => x.Storage).MaximumLength(30);
        RuleFor(x => x.Size)
               .IsInEnum()
               .WithMessage("Size must be one of: S, M, L, XL, TwoXL, ThreeXL.");
        RuleFor(x => x.Dosage).MaximumLength(30);
        RuleFor(x => x.Grade).MaximumLength(30);
        RuleFor(x => x.Finish).MaximumLength(30);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0);
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductDTO>
{
    private const string CodePattern = "^[A-Za-z0-9]{3}$";

    public UpdateProductValidator()
    {
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        RuleFor(x => x.CategoryCode)
            .Matches(CodePattern)
            .When(x => x.CategoryCode is not null)
            .WithMessage("CategoryCode must be a 3-character alphanumeric code.");
        RuleFor(x => x.Brand)
            .MaximumLength(80)
            .When(x => x.Brand is not null);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.UnitOfMeasure)
              .IsInEnum()
              .WithMessage("UnitOfMeasure must be a valid unit of measure.")
              .When(x => x.UnitOfMeasure.HasValue);
        RuleFor(x => x.Color).MaximumLength(30).When(x => x.Color is not null);
        RuleFor(x => x.Storage).MaximumLength(30).When(x => x.Storage is not null);
        RuleFor(x => x.Size)
              .IsInEnum()
              .WithMessage("Size must be one of: S, M, L, XL, TwoXL, ThreeXL.")
              .When(x => x.Size.HasValue);
        RuleFor(x => x.Dosage).MaximumLength(30).When(x => x.Dosage is not null);
        RuleFor(x => x.Grade).MaximumLength(30).When(x => x.Grade is not null);
        RuleFor(x => x.Finish).MaximumLength(30).When(x => x.Finish is not null);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0).When(x => x.ReorderThreshold.HasValue);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0).When(x => x.ReorderQuantity.HasValue);
    }

    private static bool HasAtLeastOneField(UpdateProductDTO request)
    {
        return request.CategoryCode is not null ||
            request.Brand is not null ||
            request.Name is not null ||
            request.Description is not null ||
            request.UnitOfMeasure is not null ||
            request.Color is not null ||
            request.Storage is not null ||
            request.Size is not null ||
            request.Dosage is not null ||
            request.Grade is not null ||
            request.Finish is not null ||
            request.ReorderThreshold.HasValue ||
            request.ReorderQuantity.HasValue ||
            request.IsActive.HasValue;
    }
}

