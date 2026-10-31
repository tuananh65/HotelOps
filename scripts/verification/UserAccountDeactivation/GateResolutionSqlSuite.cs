using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

internal static class GateResolutionSqlSuite
{
    internal static async Task<IReadOnlyList<string>> RunAsync(string connection, Action<bool, string> report, CancellationToken ct)
    {
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var cases = new List<string>();
        void Check(bool ok, string label) { report(ok, label); cases.Add(label); }
        var assembly = typeof(UserAccountDeactivationService).Assembly;
        async Task<IReadOnlyList<string>> Read(string typeName, string method, long id)
        {
            var type = assembly.GetType("HotelOps.Api.Services.Users." + typeName, true)!;
            return await (Task<IReadOnlyList<string>>)type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Activator.CreateInstance(type, db), [id, ct])!;
        }
        Task<IReadOnlyList<string>> States(long id) => Read("Dev2ResponsibilityStateQueries", "ReadUnresolvedAsync", id);
        Task<IReadOnlyList<string>> Blockers(long id) => Read("DeactivationResponsibilityQueries", "ReadApprovedDev2BlockersAsync", id);
        var department = await db.Departments.AsNoTracking().Where(d => d.IsActive && d.Branch.IsActive).FirstAsync(ct);
        var now = DateTime.UtcNow; var prefix = "T54_" + Guid.NewGuid().ToString("N")[..10];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        User Person(string suffix) => new() { EmployeeCode = prefix + suffix, Username = prefix + suffix,
            Email = prefix + suffix + "@example.invalid", FullName = "Owned gate fixture", AccountStatus = "ACTIVE",
            EmploymentStatus = "ACTIVE", CreatedAt = now };
        var target = Person("t"); var actor = Person("a");
        db.AddRange(target, actor); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await new UserEligibilityLock(db).LockAsync([target.UserId, actor.UserId], ct);
        Check((await States(target.UserId)).Count == 0, "DEV2 state gate is clear only after actual SQL reads of an empty modeled target");

        var work = new MaintenanceWork { WorkNo = prefix, Title = prefix, CreatedBy = actor.UserId,
            AssigneeId = target.UserId, Status = "PENDING", CreatedAt = now };
        db.Add(work); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        foreach (var state in new[] { "PENDING", "IN_PROGRESS", "ON_HOLD", "COMPLETED", "CLOSED", "CANCELLED" })
        {
            await db.MaintenanceWorks.Where(x => x.MaintenanceWorkId == work.MaintenanceWorkId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, state).SetProperty(x => x.ResolvedAt, state == "COMPLETED" || state == "CLOSED" ? (DateTime?)now : null)
                .SetProperty(x => x.ReviewedAt, state == "CLOSED" ? (DateTime?)now : null), ct);
            Check((await States(target.UserId)).Count == 0
                && (await Blockers(target.UserId)).Contains("MAINTENANCE_ASSIGNEE") == (state is "PENDING" or "IN_PROGRESS" or "ON_HOLD"),
                "Maintenance exact current/history state: " + state);
        }
        await db.MaintenanceWorks.Where(x => x.MaintenanceWorkId == work.MaintenanceWorkId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, "COMPLETED").SetProperty(x => x.ResolvedAt, (DateTime?)null), ct);
        Check((await States(target.UserId)).Contains("DEV2_MAINTENANCE_COMPLETION_MARKER"), "incomplete Maintenance completion evidence is unresolved");
        await db.MaintenanceWorks.Where(x => x.MaintenanceWorkId == work.MaintenanceWorkId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ResolvedAt, now), ct);
        var incident = new Incident { IncidentNo = prefix, BranchId = department.BranchId, IncidentTitle = prefix,
            Description = prefix, ReportedBy = actor.UserId, DiscoveredAt = now, ReportedAt = now, Status = "OPEN" };
        db.Add(incident); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.MaintenanceWorks.Where(x => x.MaintenanceWorkId == work.MaintenanceWorkId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IncidentId, incident.IncidentId), ct);
        Check((await States(target.UserId)).Contains("DEV2_MAINTENANCE_INCIDENT_STATE"), "corrective Work/Incident state mismatch is unresolved");
        await db.Incidents.Where(x => x.IncidentId == incident.IncidentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "RESOLVED")
            .SetProperty(x => x.ResolvedAt, now), ct);
        Check((await States(target.UserId)).Count == 0, "synchronized completed corrective work is historical");

        var warehouse = new Warehouse { WarehouseCode = prefix, WarehouseName = prefix, BranchId = department.BranchId,
            DepartmentId = department.DepartmentId, ManagerUserId = target.UserId, IsActive = true };
        db.Add(warehouse); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Check((await Blockers(target.UserId)).Contains("WAREHOUSE_MANAGER"), "active Warehouse manager blocks");
        await db.Warehouses.Where(x => x.WarehouseId == warehouse.WarehouseId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        Check(!(await Blockers(target.UserId)).Contains("WAREHOUSE_MANAGER"), "inactive Warehouse manager reference is historical");

        var type = new AssetType { TypeCode = prefix, TypeName = prefix, IsActive = true };
        db.Add(type); await db.SaveChangesAsync(ct);
        var asset = new Asset { AssetCode = prefix, AssetName = prefix, AssetTypeId = type.AssetTypeId,
            BranchId = department.BranchId, ConditionStatus = "GOOD", LifecycleStatus = "IN_USE", CustodianUserId = target.UserId };
        db.Add(asset); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        foreach (var state in new[] { "IN_USE", "UNDER_REPAIR", "RETIRED", "in_use" })
        {
            await db.Assets.Where(x => x.AssetId == asset.AssetId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LifecycleStatus, state), ct);
            if (state == "in_use") Check((await States(target.UserId)).Contains("DEV2_ASSET_LIFECYCLE"), "noncanonical Asset vocabulary fails closed despite SQL CI matching");
            else Check((await States(target.UserId)).Count == 0
                && (await Blockers(target.UserId)).Contains("ASSET_CUSTODIAN") == (state != "RETIRED"), "Asset exact custody/history: " + state);
        }
        await db.Assets.Where(x => x.AssetId == asset.AssetId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LifecycleStatus, "IN_USE")
            .SetProperty(x => x.CustodianUserId, actor.UserId), ct);
        var handover = new AssetHandover { HandoverNo = prefix, AssetId = asset.AssetId, ToUserId = target.UserId,
            FromUserId = actor.UserId, CreatedBy = actor.UserId, CreatedAt = now, HandoverStatus = "PENDING" };
        db.Add(handover); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        foreach (var state in new[] { "PENDING", "ACCEPTED", "REJECTED", "pending" })
        {
            await db.AssetHandovers.Where(x => x.HandoverId == handover.HandoverId).ExecuteUpdateAsync(s => s.SetProperty(x => x.HandoverStatus, state)
                .SetProperty(x => x.AcceptedAt, state == "ACCEPTED" ? (DateTime?)now : null), ct);
            if (state == "pending") Check((await States(target.UserId)).Contains("DEV2_HANDOVER_STATUS"), "unknown Handover state cannot produce clear responsibility");
            else Check((await States(target.UserId)).Count == 0
                && (await Blockers(target.UserId)).Contains("ASSET_RECEIVER") == (state == "PENDING"), "Handover current/history: " + state);
        }
        await db.AssetHandovers.Where(x => x.HandoverId == handover.HandoverId).ExecuteUpdateAsync(s => s.SetProperty(x => x.HandoverStatus, "PENDING")
            .SetProperty(x => x.AcceptedAt, now), ct);
        Check((await States(target.UserId)).Contains("DEV2_HANDOVER_ACK_MARKER"), "PENDING handover with acceptance marker is inconsistent");
        await db.AssetHandovers.Where(x => x.HandoverId == handover.HandoverId).ExecuteUpdateAsync(s => s.SetProperty(x => x.HandoverStatus, "REJECTED")
            .SetProperty(x => x.AcceptedAt, (DateTime?)null), ct);

        var request = new MaterialRequest { RequestNo = prefix, BranchId = department.BranchId, DepartmentId = department.DepartmentId,
            RequesterId = target.UserId, Purpose = prefix, Status = "DRAFT", CreatedAt = now };
        db.Add(request); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Check((await States(target.UserId)).Count == 0 && (await Blockers(target.UserId)).Contains("MATERIAL_REQUEST_REQUESTER"), "MR DRAFT requester blocks without inventing an approval cycle");
        await db.MaterialRequests.Where(x => x.RequestId == request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "SUBMITTED"), ct);
        Check((await States(target.UserId)).Contains("DEV2_MATERIAL_REQUEST_STATUS_OR_LEGACY"), "legacy MR state is contract-required rather than silently historical");
        await db.MaterialRequests.Where(x => x.RequestId == request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequesterId, actor.UserId)
            .SetProperty(x => x.Status, "PENDING_APPROVAL"), ct);
        var cycle = new MaterialRequestApprovalCycle { RequestId = request.RequestId, RevisionNo = 1, SubmittedBy = actor.UserId,
            AssignedApproverId = target.UserId, Status = "PENDING", SubmittedAt = now };
        db.Add(cycle); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Check((await States(target.UserId)).Count == 0 && (await Blockers(target.UserId)).Contains("MATERIAL_REQUEST_APPROVER"), "current latest PENDING approval cycle blocks");
        await db.MaterialRequests.Where(x => x.RequestId == request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "APPROVED"), ct);
        Check((await States(target.UserId)).Contains("DEV2_MATERIAL_REQUEST_CURRENT_CYCLE"), "MR parent/cycle inconsistency fails closed");
        var newer = new MaterialRequestApprovalCycle { RequestId = request.RequestId, RevisionNo = 2, SubmittedBy = target.UserId,
            AssignedApproverId = actor.UserId, Status = "APPROVED", SubmittedAt = now, ResolvedAt = now };
        db.Add(newer); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        Check((await States(target.UserId)).Count == 0 && !(await Blockers(target.UserId)).Contains("MATERIAL_REQUEST_APPROVER"),
            "newer resolved revision supersedes older PENDING target approver without a false blocker");
        await db.MaterialRequestApprovalCycles.Where(x => x.ApprovalCycleId == newer.ApprovalCycleId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignedApproverId, target.UserId).SetProperty(x => x.SubmittedBy, actor.UserId)
                .SetProperty(x => x.ResolvedAt, (DateTime?)null), ct);
        Check((await States(target.UserId)).Contains("DEV2_MATERIAL_APPROVAL_MARKER"), "latest resolved cycle without resolution marker is unresolved");
        await db.MaterialRequests.Where(x => x.RequestId == request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequesterId, target.UserId)
            .SetProperty(x => x.Status, "NEEDS_INFORMATION"), ct);
        await db.MaterialRequestApprovalCycles.Where(x => x.ApprovalCycleId == newer.ApprovalCycleId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AssignedApproverId, actor.UserId).SetProperty(x => x.SubmittedBy, target.UserId)
                .SetProperty(x => x.Status, "NEEDS_INFORMATION").SetProperty(x => x.ResolvedAt, now), ct);
        Check((await States(target.UserId)).Count == 0 && (await Blockers(target.UserId)).Contains("MATERIAL_REQUEST_REQUESTER"),
            "current NEEDS_INFORMATION cycle returns requester responsibility, not historical approver responsibility");
        await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
        Check(!await db.Users.AsNoTracking().AnyAsync(x => x.UserId == target.UserId, ct), "all gate fixtures roll back, including users, responsibilities and reference types");
        return cases;
    }
}
