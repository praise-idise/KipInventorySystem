using KipInventorySystem.Application.Services.Auth.DTOs;
using FluentValidation;

namespace KipInventorySystem.Application.Validators.Auth;

public class RegisterDTOValidator : AbstractValidator<RegisterDTO>
{
    public RegisterDTOValidator()
    {
        // Add validation rules here when migrating from DataAnnotations
        // Example:
        // RuleFor(x => x.Email)
        //     .NotEmpty().WithMessage("Email is required")
        //     .EmailAddress().WithMessage("Invalid email format");
    }
}
