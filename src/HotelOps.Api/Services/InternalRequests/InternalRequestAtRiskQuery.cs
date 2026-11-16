using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public static class InternalRequestAtRiskQuery
{
    // Query component only: caller supplies current authorized scope and owns the read transaction.
    public static IQueryable<InternalRequest> Apply(HotelOpsDbContext db,IQueryable<InternalRequest> authorized,DateTime now,bool atRisk)
    {
        var ids=Ids(db,now);
        return authorized.Where(r=>ids.Contains(r.InternalRequestId)==atRisk);
    }
    // Only IDs for intersection with the caller's already authorized query. No standalone read API.
    // Microsecond boundary differences plus the remaining 100ns digit preserve datetime2 precision
    // across its full range; decimal multiplication avoids bigint overflow at the 80% comparison.
    private static IQueryable<long> Ids(HotelOpsDbContext db,DateTime now)=>db.Database.SqlQuery<long>($"""
        SELECT internal_request_id AS Value FROM dbo.internal_requests
        WHERE due_at>created_at AND {now}<=due_at AND status NOT IN('CLOSED','REJECTED','CANCELLED')
         AND (CAST(DATEDIFF_BIG(MICROSECOND,created_at,{now}) AS decimal(28,0))*10
              +(DATEPART(NANOSECOND,{now})%1000-DATEPART(NANOSECOND,created_at)%1000)/100)*5
          >= (CAST(DATEDIFF_BIG(MICROSECOND,created_at,due_at) AS decimal(28,0))*10
              +(DATEPART(NANOSECOND,due_at)%1000-DATEPART(NANOSECOND,created_at)%1000)/100)*4
        """);
}
