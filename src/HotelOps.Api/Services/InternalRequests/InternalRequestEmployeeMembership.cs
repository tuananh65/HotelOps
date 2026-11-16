using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;

namespace HotelOps.Api.Services.InternalRequests;

// Employment membership is independent of the explicit Department Head authority mapping.
internal static class InternalRequestEmployeeMembership
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => WorkdayMembershipStore.InstalledAsync(db, ct);

    internal static IQueryable<User> Members(HotelOpsDbContext db, long branch, long? department, DateTime now)
        => WorkdayMembershipStore.Employees(db, branch, department, now);
}
