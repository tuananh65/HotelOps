using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace HotelOps.Api.Services.Authentication;

public static class SessionCredential
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        try { return WebEncoders.Base64UrlEncode(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static bool IsValid(string? credential) => credential is { Length: 43 }
        && credential.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && IsCanonical(credential);

    private static bool IsCanonical(string credential)
    {
        var bytes = WebEncoders.Base64UrlDecode(credential);
        try { return bytes.Length == 32 && WebEncoders.Base64UrlEncode(bytes) == credential; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static string Hash(string credential)
    {
        if (!IsValid(credential)) throw new ArgumentException("Invalid session credential format.");
        var bytes = WebEncoders.Base64UrlDecode(credential);
        try { return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
