using FluentValidation;
using FintechBackend.DTOs; // Adjust to your actual namespace

namespace FintechBackend.Validators
{
    public class UpdateTransactionDtoValidator : AbstractValidator<UpdateTransactionDto>
    {
        public UpdateTransactionDtoValidator()
        {
            RuleFor(x => x.AccountHolder)
                .NotEmpty().WithMessage("Account holder name is required.")
                .MaximumLength(100).WithMessage("Account holder name cannot exceed 100 characters.");

            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Amount must be greater than zero.");

            RuleFor(x => x.TransactionType)
                .NotEmpty().WithMessage("Transaction type is required.")
                .Must(type => type == "Deposit" || type == "Withdrawal" || type == "Transfer")
                .WithMessage("Transaction type must be 'Deposit', 'Withdrawal', or 'Transfer'.");
        }
    }
}