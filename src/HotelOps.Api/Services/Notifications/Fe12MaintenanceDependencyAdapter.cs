using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

public sealed record Fe12MaintenanceFacts(long WorkId,long BranchId,long? AssigneeId,long? DepartmentId,
    string Status,bool AssigneeEligible,string Fingerprint,string CycleContract="MISSING_ACCEPTED_MAINTENANCE_CYCLE_SOURCE",
    bool WorkTimeBindingAccepted=false,bool NotificationStagingAccepted=false);

// Selected integration base has no mapped MaintenanceWorkCycle or accepted cycle source.
// Executable persisted facts only; never invent a cycle from work ID or timestamp.
public sealed class Fe12MaintenanceDependencyAdapter(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions)
{
    private (Guid Tx,long Work,long Branch)? lease;
    private async Task Authorize(long branch,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(branch<=0||!current.TryGetCurrentUser(out var a)||a.UserId<=0||a.SessionId<=0||a.IsRestricted||
            !(await permissions.EvaluateBranchAsync("MAINTENANCE_VIEW",branch,ct)).IsGranted)throw new DependencyScopeException();
    }
    public async Task<Fe12MaintenanceFacts?> ReadFactsAsync(long work,long branch,CancellationToken ct)
    {
        await Authorize(branch,ct);
        if(work<=0)throw new WorkTimeContractException("TIME_STATE_CONFLICT");
        var w=await Dev2DependencyQueries.Maintenance(db.MaintenanceWorks.AsNoTracking(),branch)
            .Where(w=>w.MaintenanceWorkId==work).Select(w=>new{w.MaintenanceWorkId,w.AssigneeId,w.DepartmentId,w.Status,
                w.IncidentId,w.ResolvedAt,w.ReviewedAt,w.ActualStartAt,w.PlannedStartAt,w.PlannedEndAt}).SingleOrDefaultAsync(ct);
        if(w is null)return null;
        var eligible=w.AssigneeId is not null&&await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==w.AssigneeId
            &&u.DeletedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"
            &&db.UserBranchAccesses.Any(a=>a.UserId==u.UserId&&a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=DateTime.UtcNow),ct);
        await Authorize(branch,ct);
        return new(w.MaintenanceWorkId,branch,w.AssigneeId,w.DepartmentId,w.Status,eligible,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{branch,source=w,eligible}))));
    }
    public async Task<Fe12MaintenanceFacts?> ReadInCallerTransactionAsync(long work,long branch,CancellationToken ct)
    {
        await Authorize(branch,ct);
        if(db.Database.CurrentTransaction is not {} tx||tx.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Owner User/time/source locks and Serializable caller transaction required.");
        // Retained SERIALIZABLE reads protect the observed facts. Owner must prelock all Users,
        // then Incident -> MaintenanceWork and reread, preserving accepted ReturnWork protocol.
        var value=await ReadFactsAsync(work,branch,ct);lease=(tx.TransactionId,work,branch);return value;
    }
    public async Task<Fe12MaintenanceFacts> RevalidateInCallerTransactionAsync(Fe12MaintenanceFacts expected,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(db.Database.CurrentTransaction is not {} tx||lease!=(tx.TransactionId,expected.WorkId,expected.BranchId))
            throw new InvalidOperationException("Matching FE12 source transaction lease required.");
        var fresh=await ReadFactsAsync(expected.WorkId,expected.BranchId,ct);
        if(fresh is null||fresh.Fingerprint!=expected.Fingerprint)throw new DependencySnapshotConflictException();
        return fresh;
    }
    public async Task<Fe12MaintenanceFacts?> ReadAsync(long work,long branch,long expectedCycle,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(work<=0||branch<=0||expectedCycle<=0)throw new WorkTimeContractException("TIME_STATE_CONFLICT");
        await Authorize(branch,ct);
        throw new WorkTimeContractException("MAINTENANCE_CYCLE_CONTRACT_REQUIRED");
    }
}
