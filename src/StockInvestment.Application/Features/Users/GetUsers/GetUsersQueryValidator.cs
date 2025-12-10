using FluentValidation;

namespace StockInvestment.Application.Features.Users.GetUsers;

public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Page size must not exceed 100");

        RuleFor(x => x.Role)
            .Must(role => string.IsNullOrEmpty(role) || 
                         role == "Admin" || 
                         role == "Investor" || 
                         role == "Analyst")
            .WithMessage("Role must be Admin, Investor, or Analyst");
    }
}

