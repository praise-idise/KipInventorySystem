using KipInventorySystem.Application.Services.Auth.DTOs;
using FluentValidation;

namespace KipInventorySystem.Application.Services.Auth.Validator;

public class RegisterValidator : AbstractValidator<RegisterDTO>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required");

        RuleFor(x => x.PhoneNumber)
             .Matches(@"^\+[1-9]\d{7,14}$")
             .WithMessage("Phone number must be in international format (e.g. +2348012345678");


    }
}

public class ResendVerificationValidator : AbstractValidator<ResendVerificationDTO>
{
    public ResendVerificationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

    }
}