using System.Reflection;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class StaffIssueDocumentQueryTests
{
    [Theory]
    [InlineData(1L, "")]
    [InlineData(42L, "Towel")]
    public void SupplyDocumentsAggregateOnSqlServerWithoutOpeningAConnection(long branchId, string search)
    {
        using var db = new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>()
            .UseSqlServer("Server=localhost;Database=translation_only;Integrated Security=true;TrustServerCertificate=true").Options);
        var service = new DocumentService(db, null!, null!, null!, null!, TimeProvider.System, null!);
        var method = typeof(DocumentService).GetMethod("StaffIssueGroups", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var query = (IQueryable)method.Invoke(service, [branchId, search])!;
        var sql = query.ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("posted_at", sql);
        Assert.Contains("branch_id", sql);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
