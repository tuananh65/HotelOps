using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Only invoked inside RecoveryAuthProbe's exact-identity disposable fixture.
internal static class RecoveryReplenishmentNotice
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long root, long branch, long actor,
        List<object> checks, CancellationToken ct)
    {
        const string kind = "REPLENISHMENT_CREATED";
        var type = typeof(HotelOpsDbContext).Assembly.GetType("HotelOps.Api.Services.MaterialRequests.MaterialApprovalNotificationDelivery", true)!;
        var delivery = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [db, Options.Create(new NotificationCenterOptions())], null)!;
        var method = type.GetMethod("DeliverReplenishmentCreatedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task Deliver() => await (Task)method.Invoke(delivery, [root, ct])!;
        await Deliver();
        if (await db.Notifications.AnyAsync(n => n.SourceType == kind && n.SourceId == root, ct))
            throw new InvalidOperationException("Storekeeper received Operations Manager creation notification.");
        var manager = await db.Roles.AsNoTracking().SingleAsync(r => r.RoleCode == "OPERATIONS_MANAGER" && r.IsActive, ct);
        var assignment = new UserRole { UserId = actor, RoleId = manager.RoleId, BranchId = branch,
            IsActive = true, AssignedAt = DateTime.UtcNow };
        db.UserRoles.Add(assignment); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await Deliver();
        await Deliver();
        var notice = await db.Notifications.AsNoTracking().SingleAsync(n => n.SourceType == kind && n.SourceId == root, ct);
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.Length != 1 || receipts[0].UserId != actor || receipts[0].DeliveryStatus != "DELIVERED")
            throw new InvalidOperationException("Creation recipient or delivery deduplication incorrect.");
        using var opened = await http.GetAsync($"/api/notifications/{notice.NotificationId}/source", ct);
        if (opened.StatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException($"Eligible creation notification source rejected: {opened.StatusCode}.");
        var body = await opened.Content.ReadAsStringAsync(ct);
        if (!body.Contains($"/operations/replenishments/{root}", StringComparison.Ordinal))
            throw new InvalidOperationException("Creation notification target incorrect.");
        await db.UserRoles.Where(r => r.UserRoleId == assignment.UserRoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        using var denied = await http.GetAsync($"/api/notifications/{notice.NotificationId}/source", ct);
        if (denied.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Revoked manager retained notification access: {denied.StatusCode}.");
        checks.Add(new { replenishmentCreatedNotice = "PASS", storekeeperDenied = true,
            managerDeliveredOnce = true, httpsSource = 200, revokedManagerSource = 404 });
    }
}
