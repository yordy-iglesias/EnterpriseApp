namespace EnterpriseApp.Application.Features.Identity.Users.DTOs;

public sealed record UserSummaryDto(
    Guid           Id,
    string         Email,
    string         FullName,
    bool           IsActive,
    bool           EmailConfirmed,
    DateTimeOffset CreatedAt);

public sealed record UserDetailDto(
    Guid                  Id,
    string                Email,
    string                FirstName,
    string                LastName,
    string                FullName,
    bool                  IsActive,
    bool                  EmailConfirmed,
    Guid?                 TenantId,
    DateTimeOffset?       LastLoginAt,
    DateTimeOffset        CreatedAt,
    IReadOnlyList<string> Roles);

public sealed record PagedUsersDto(
    IReadOnlyList<UserSummaryDto> Data,
    int TotalCount,
    int PageNumber,
    int PageSize);
