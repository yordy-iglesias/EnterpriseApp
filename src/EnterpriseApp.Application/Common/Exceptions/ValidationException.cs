using FluentValidation.Results;

namespace EnterpriseApp.Application.Common.Exceptions;

/// <summary>
/// Thrown by <see cref="Behaviors.ValidationBehavior{TRequest,TResponse}"/> when
/// one or more FluentValidation rules fail.  The API layer maps this to HTTP 400.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures occurred.") =>
        Errors = new Dictionary<string, string[]>();

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    /// <summary>Key = property name, Value = array of error messages.</summary>
    public IDictionary<string, string[]> Errors { get; }
}
