using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Incidents;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentReviewReceiptTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void ReviewIsBoundToCurrentActorSessionAndExactLocation()
    {
        var provider = new EphemeralDataProtectionProvider();
        var time = new Clock(new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero));
        var token = IncidentReviewReceipt.Issue(provider, time, 4, 5, new() { SimilarOpenOnly = true, BranchId = 3, RoomId = 8 });
        var input = new CreateIncidentRequest { BranchId = 3, RoomId = 8, SimilarReviewToken = token };
        IncidentReviewReceipt.Validate(provider, time, 4, 5, input);
        Assert.Throws<ValidationException>(() => IncidentReviewReceipt.Validate(provider, time, 6, 5, input));
        Assert.Throws<ValidationException>(() => IncidentReviewReceipt.Validate(provider, time, 4, 6, input));
        foreach (var changed in new CreateIncidentRequest[] {
            new() { BranchId = 9, RoomId = 8, SimilarReviewToken = token },
            new() { BranchId = 3, RoomId = 9, SimilarReviewToken = token },
            new() { BranchId = 3, RoomId = 8, AssetId = 1, SimilarReviewToken = token } })
            Assert.Throws<ValidationException>(() => IncidentReviewReceipt.Validate(provider, time, 4, 5, changed));
        time.Now = time.Now.AddMinutes(10);
        Assert.Throws<ValidationException>(() => IncidentReviewReceipt.Validate(provider, time, 4, 5, input));
    }

    [Fact]
    public void MissingTamperedOrForeignProtectedReceiptsFailClosed()
    {
        var provider = new EphemeralDataProtectionProvider();
        var foreign = new EphemeralDataProtectionProvider();
        var token = IncidentReviewReceipt.Issue(foreign, TimeProvider.System, 4, 5,
            new() { SimilarOpenOnly = true, BranchId = 3, RoomId = 8 });
        foreach (var value in new[] { null, "", "fabricated", token, new string('x', 4097) })
            Assert.Throws<ValidationException>(() => IncidentReviewReceipt.Validate(provider, TimeProvider.System, 4, 5,
                new() { BranchId = 3, RoomId = 8, SimilarReviewToken = value }));
    }
}
