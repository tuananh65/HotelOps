using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    public async Task<object> Maintenance(long branchId,CancellationToken ct)
    {
        await access.Require("DASHBOARD_VIEW",branchId,ct:ct);
        await access.Require("MAINTENANCE_VIEW",branchId,ct:ct);
        var now=clock.GetUtcNow().UtcDateTime;
        var tasks=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&w.Kind=="MAINTENANCE");
        var open=tasks.Where(w=>w.Status!="CLOSED"&&w.Status!="CANCELLED");
        var unassigned=open.Where(w=>!db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssignmentStatus=="ACTIVE"));
        var incidents=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&w.Kind=="INCIDENT"&&!WorkRules.Terminal.Contains(w.Status));
        var pm=db.WorkTemplates.AsNoTracking().Where(t=>t.BranchId==branchId&&t.Kind=="MAINTENANCE"&&t.IsActive);
        var canIncident=await access.Allows("INCIDENT_VIEW",branchId,ct:ct);var canPm=await access.Allows("PM_MANAGE",branchId,ct:ct);
        return new{branchId,metrics=new{
            open=await open.CountAsync(ct),unassigned=await unassigned.CountAsync(ct),inProgress=await tasks.CountAsync(w=>w.Status=="IN_PROGRESS",ct),
            pendingReview=await tasks.CountAsync(w=>w.Status=="COMPLETED",ct),closed=await tasks.CountAsync(w=>w.Status=="CLOSED",ct),overdue=await open.CountAsync(w=>w.DueAt<now,ct),
            incidents=canIncident?(int?)await incidents.CountAsync(ct):null,
            severeIncidents=canIncident?(int?)await incidents.CountAsync(w=>db.ReferenceValues.Any(r=>r.ReferenceId==w.SeverityId&&(r.Code=="HIGH"||r.Code=="CRITICAL")),ct):null,
            slaOverdue=canIncident?(int?)await incidents.CountAsync(w=>w.DueAt<now,ct):null,
            pmDue=canPm?(int?)await pm.CountAsync(t=>t.NextDueAt<=now,ct):null,
            pmOverdueWork=canPm?(int?)await open.CountAsync(w=>w.WorkTemplateId!=null&&w.DueAt<now,ct):null,
            affectedAssets=canIncident?(int?)await incidents.Where(w=>w.AssetId!=null).Select(w=>w.AssetId).Distinct().CountAsync(ct):null},
            unassigned=await unassigned.OrderBy(w=>w.DueAt).Take(10).Select(w=>new{w.WorkId,w.Kind,w.Title,w.Status,w.DueAt}).ToArrayAsync(ct),
            review=await tasks.Where(w=>w.Status=="COMPLETED").OrderBy(w=>w.CompletedAt).Take(10).Select(w=>new{w.WorkId,w.Kind,w.Title,w.Status,w.CompletedAt}).ToArrayAsync(ct),
            incidents=canIncident?await incidents.OrderBy(w=>w.DueAt==null).ThenBy(w=>w.DueAt).Take(10).Select(w=>new{w.WorkId,w.Kind,w.Title,w.Status,w.DueAt}).ToArrayAsync(ct):null,
            pm=canPm?await pm.Where(t=>t.NextDueAt<=now).OrderBy(t=>t.NextDueAt).Take(10).Select(t=>new{t.WorkTemplateId,t.Title,t.NextDueAt}).ToArrayAsync(ct):null,
            progress=await tasks.GroupBy(w=>w.Status).Select(g=>new{status=g.Key,count=g.Count()}).ToArrayAsync(ct)};
    }
}
