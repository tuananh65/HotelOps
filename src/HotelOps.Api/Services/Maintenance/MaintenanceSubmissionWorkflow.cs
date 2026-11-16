using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

// Read projection over the committed submission, never a second stored state.
internal static class MaintenanceSubmissionWorkflow
{
    internal static async Task<string?> ReadAsync(HotelOpsDbContext db,long work,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null) throw new InvalidOperationException("Owning read transaction required.");
        var row=await db.MaintenanceWorks.AsNoTracking().Where(w=>w.MaintenanceWorkId==work)
            .Select(w=>new {w.Status,w.ResolvedAt,w.IncidentId}).SingleOrDefaultAsync(ct);
        if(row?.IncidentId==null || row.Status!="COMPLETED") return null;
        if(row.ResolvedAt==null) return "UNVERIFIED";
        var key=work.ToString(CultureInfo.InvariantCulture);
        var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="maintenance_works" && a.EntityId==key && a.OccurredAt==row.ResolvedAt).ToArrayAsync(ct);
        var submissions=audits.Where(a=>MaintenanceReviewSnapshot.Parse(a)!=null).ToArray();
        if(submissions.Length!=1 || await MaintenanceSubmissionSource.ReadAsync(db,submissions[0].AuditId,ct)==null) return "UNVERIFIED";
        return "PENDING_VERIFICATION";
    }
    internal static async Task<string?> ForIncidentAsync(HotelOpsDbContext db,long incident,CancellationToken ct)
    {
        var status=await db.Incidents.AsNoTracking().Where(i=>i.IncidentId==incident).Select(i=>i.Status).SingleAsync(ct);
        if(status!="RESOLVED") return null;
        var works=await db.MaintenanceWorks.AsNoTracking().Where(w=>w.IncidentId==incident).Select(w=>w.MaintenanceWorkId).Take(2).ToArrayAsync(ct);
        return works.Length==1 ? await ReadAsync(db,works[0],ct) ?? "UNVERIFIED" : "UNVERIFIED";
    }
}
