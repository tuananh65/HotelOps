using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentNotificationQueryTests
{
    [Fact]
    public void RetainedIntentScanAndSourceLookupTranslateToSql()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var cutoff = new DateTime(2026, 11, 1); var now = cutoff.AddDays(1);
        var scan = IncidentReportedNotificationDelivery.Events(db)
            .Where(e => e.Id > 7 && e.At >= cutoff && e.At <= now).OrderBy(e => e.Id).Select(e => e.Id).Take(100).ToQueryString();
        Assert.Contains("CREATED", scan);
        Assert.Contains("reported_by", scan);
        Assert.Contains("reported_at", scan);
        Assert.Contains("ORDER BY", scan);
        var lookup = IncidentReportedNotificationDelivery.Events(db).Where(e => e.Id == 9).ToQueryString();
        Assert.Contains("event_id", lookup);
    }
}
