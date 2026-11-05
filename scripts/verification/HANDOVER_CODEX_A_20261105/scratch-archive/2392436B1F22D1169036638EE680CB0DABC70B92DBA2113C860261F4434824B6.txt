using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;

namespace HotelOps.Api.Services.Authentication;

public interface IPasswordCredentialService
{
    string HashPassword(User user, string password);

    /// <summary>
    /// Returns SuccessRehashNeeded for a verified V3 credential requiring an upgrade.
    /// The caller must generate a new hash and persist it with credential concurrency
    /// checks in its transaction. This service never changes User or the database.
    /// </summary>
    PasswordVerificationResult VerifyPassword(User user, string? storedHash, string? password);
}
