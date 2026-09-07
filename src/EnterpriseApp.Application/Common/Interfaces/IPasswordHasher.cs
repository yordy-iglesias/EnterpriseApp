namespace EnterpriseApp.Application.Common.Interfaces;

public enum PasswordVerificationResult { Failed, Success, SuccessRehashNeeded }

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string hash, string password);
}
