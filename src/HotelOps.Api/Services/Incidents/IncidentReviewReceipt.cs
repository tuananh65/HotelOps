using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Incidents;
using Microsoft.AspNetCore.DataProtection;

namespace HotelOps.Api.Services.Incidents;

// Proof of a scoped review response, not mutation authority or a uniqueness guarantee.
internal static class IncidentReviewReceipt
{
    private sealed record Receipt(long User, long Session, long Branch, long? Room, long? Area, long? Asset, DateTimeOffset Expires);
    private static IDataProtector Protector(IDataProtectionProvider provider) => provider.CreateProtector("HotelOps.Incident.SimilarReview.v1");

    internal static string Issue(IDataProtectionProvider provider, TimeProvider time, long user, long session, IncidentQueryRequest input)
    {
        if (!input.SimilarOpenOnly || input.BranchId is not > 0 || user <= 0 || session <= 0)
            throw new InvalidOperationException("A scoped similar-incident review is required.");
        return Protector(provider).Protect(JsonSerializer.Serialize(new Receipt(user, session, input.BranchId.Value,
            input.RoomId, input.AreaId, input.AssetId, time.GetUtcNow().AddMinutes(10))));
    }

    internal static void Validate(IDataProtectionProvider provider, TimeProvider time, long user, long session, CreateIncidentRequest input)
    {
        Receipt? receipt = null;
        if (input.SimilarReviewToken is { Length: > 0 and <= 4096 })
        {
            try { receipt = JsonSerializer.Deserialize<Receipt>(Protector(provider).Unprotect(input.SimilarReviewToken)); }
            catch (Exception error) when (error is CryptographicException or JsonException or FormatException) { }
        }
        if (receipt == null || receipt.User != user || receipt.Session != session || receipt.Branch != input.BranchId
            || receipt.Room != input.RoomId || receipt.Area != input.AreaId || receipt.Asset != input.AssetId
            || receipt.Expires <= time.GetUtcNow())
            throw new ValidationException("Reload and review similar incidents for this location before creating a new incident.");
    }
}
