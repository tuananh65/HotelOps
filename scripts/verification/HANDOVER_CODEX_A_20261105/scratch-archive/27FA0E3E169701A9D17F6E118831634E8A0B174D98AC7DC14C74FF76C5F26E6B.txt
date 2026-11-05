using System.Buffers.Binary;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Authentication;

public sealed class PasswordCredentialService : IPasswordCredentialService
{
    public const int BaselineIterationCount = 100_000;
    public const int MaximumIterationCount = 1_000_000;
    private const int MaximumStoredHashLength = 255;
    private readonly PasswordHasher<User> _hasher;

    public PasswordCredentialService(IOptions<PasswordHasherOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Value.CompatibilityMode != PasswordHasherCompatibilityMode.IdentityV3
            || options.Value.IterationCount < BaselineIterationCount
            || options.Value.IterationCount > MaximumIterationCount)
        {
            throw new ArgumentException("Identity V3 with 100,000 to 1,000,000 iterations is required.", nameof(options));
        }

        _hasher = new PasswordHasher<User>(options);
    }

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _hasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyPassword(User user, string? storedHash, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(password) || !IsSupportedV3Hash(storedHash))
        {
            return PasswordVerificationResult.Failed;
        }

        // Preserve SuccessRehashNeeded; verification alone must not mutate credentials.
        return _hasher.VerifyHashedPassword(user, storedHash!, password);
    }

    private static bool IsSupportedV3Hash(string? hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length > MaximumStoredHashLength)
        {
            return false;
        }

        Span<byte> payload = stackalloc byte[MaximumStoredHashLength];
        if (!Convert.TryFromBase64String(hash, payload, out var length)
            || length < 13 + 16 + 16 || payload[0] != 0x01)
        {
            return false;
        }

        // Validate the documented Identity V3 envelope only; all cryptography and
        // password comparison remain in ASP.NET Core PasswordHasher<User>.
        var prf = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(1, 4));
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(5, 4));
        var saltLength = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(9, 4));
        return prf <= 2 // Framework-defined HMAC SHA1/SHA256/SHA512 V3 formats.
            && iterations > 0 && iterations <= MaximumIterationCount
            && saltLength >= 16 && saltLength <= (uint)(length - 13 - 16);
    }
}
