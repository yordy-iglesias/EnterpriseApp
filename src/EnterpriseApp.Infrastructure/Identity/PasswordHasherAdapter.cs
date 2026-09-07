using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace EnterpriseApp.Infrastructure.Identity;

internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();
    private static readonly User _dummy = User.Create(
        Domain.ValueObjects.Email.From("dummy@dummy.com"), "x", "D", "U", null, null);

    public string Hash(string password) => _inner.HashPassword(_dummy, password);

    public Application.Common.Interfaces.PasswordVerificationResult Verify(string hash, string password)
    {
        var result = _inner.VerifyHashedPassword(_dummy, hash, password);
        return result switch
        {
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                => Application.Common.Interfaces.PasswordVerificationResult.Success,
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded
                => Application.Common.Interfaces.PasswordVerificationResult.SuccessRehashNeeded,
            _ => Application.Common.Interfaces.PasswordVerificationResult.Failed,
        };
    }
}
