using FluentValidation;

namespace EnterpriseApp.Application.Features.TodoItems.Queries.GetPagedTodos;

public sealed class GetPagedTodosQueryValidator : AbstractValidator<GetPagedTodosQuery>
{
    public GetPagedTodosQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be >= 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}
