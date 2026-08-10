using FluentValidation;
using FintechBackend.DTOs;
using System.Linq;

namespace FintechBackend.Validators;

public class CreateTransactionDtoValidator : AbstractValidator<CreateTransactionDto>
{
    public CreateTransactionDtoValidator()
    {
        RuleFor(x => x.AccountHolder)
            .NotEmpty().WithMessage("Account holder name is required.")
            .MinimumLength(5).WithMessage("Account holder name must be at least 4 characters.")
            .MaximumLength(100).WithMessage("Account holder name must not exceed 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Transaction amount must be greater than zero.");

        RuleFor(x => x.TransactionType)
            .NotEmpty().WithMessage("Transaction type is required.")
            .Must(BeAValidTransactionType).WithMessage("Transaction type must be 'Deposit', 'Withdrawal', or 'Transfer'.");
    }

    // Helper method to enforce exact transaction types
    private bool BeAValidTransactionType(string type)
    {
        string[] validTypes = { "Deposit", "Withdrawal", "Transfer" };
        return validTypes.Contains(type);
    }
}