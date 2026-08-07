namespace EnterpriseApp.Domain.Exceptions;

/// <summary>Thrown when a domain invariant is violated.</summary>
public sealed class DomainException(string message) : Exception(message);

/// <summary>Thrown when a requested resource does not exist.</summary>
public sealed class NotFoundException(string entity, object id)
    : Exception($"{entity} with id '{id}' was not found.");
