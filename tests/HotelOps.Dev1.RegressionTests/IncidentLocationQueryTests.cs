using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentLocationQueryTests
{
    private static IQueryable<Incident> Rows() => new[]
    {
        new Incident { IncidentId = 1, BranchId = 3, RoomId = 8, Status = "OPEN" },
        new Incident { IncidentId = 2, BranchId = 3, AssetId = 9, Status = "IN_PROGRESS" },
        new Incident { IncidentId = 3, BranchId = 3, RoomId = 8, AssetId = 9, Status = "RESOLVED" },
        new Incident { IncidentId = 4, BranchId = 3, RoomId = 8, Status = "CLOSED" },
        new Incident { IncidentId = 5, BranchId = 3, AssetId = 9, Status = "CANCELLED" },
        new Incident { IncidentId = 6, BranchId = 4, RoomId = 8, Status = "OPEN" },
        new Incident { IncidentId = 7, BranchId = 3, RoomId = 10, Status = "OPEN" },
    }.AsQueryable();

    [Fact]
    public void SimilarMatchesAnyLocationWithoutExpandingAuthorizedPopulation()
    {
        var input = new IncidentQueryRequest { BranchId = 3, RoomId = 8, AssetId = 9, SimilarOpenOnly = true };
        Assert.Equal(new long[] { 1, 2, 3 }, IncidentLocationQuery.Apply(Rows(), input).Select(i => i.IncidentId));
        Assert.Equal(new long[] { 1, 3 }, IncidentLocationQuery.Apply(Rows().Where(i => i.IncidentId != 2), input).Select(i => i.IncidentId));
    }

    [Fact]
    public void OrdinaryLocationFiltersStillIntersect()
    {
        Assert.Equal(new long[] { 3 }, IncidentLocationQuery.Apply(Rows(), new() { RoomId = 8, AssetId = 9 }).Select(i => i.IncidentId));
    }

    [Fact]
    public void SimilarReviewRequiresValidBranchAndLocation()
    {
        foreach (var input in new IncidentQueryRequest[] {
            new() { SimilarOpenOnly = true }, new() { SimilarOpenOnly = true, BranchId = 3 },
            new() { SimilarOpenOnly = true, RoomId = 8 }, new() { SimilarOpenOnly = true, BranchId = 3, RoomId = -1 },
            new() { SimilarOpenOnly = true, BranchId = 3, RoomId = 8, Keyword = "hide other matches" },
            new() { SimilarOpenOnly = true, BranchId = 3, RoomId = 8, Status = "CLOSED" } })
            Assert.Throws<ValidationException>(() => IncidentLocationQuery.Apply(Rows(), input));
    }
}
