using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.PreventiveMaintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Incidents;

// Source participant only. The existing INCIDENT_CREATE admission still authorizes the target command.
internal static class IncidentOccurrenceCreation
{
    private sealed class Source
    {
        public string Definition { get; set; } = "";
        public string Version { get; set; } = "";
        public long? Asset { get; set; }
        public long? Room { get; set; }
        public long? Area { get; set; }
    }
    internal static async Task PrepareAsync(HotelOpsDbContext db,long actor,CreateIncidentRequest input,CancellationToken ct)
    {
        if(input.OccurrenceLink is not {} link)return;
        if(db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Occurrence link requires the existing incident transaction.");
        if(link.RunId<=0||link.Version is not {Length:18}||link.Version[0]!='"'||link.Version[^1]!='"'
            ||link.Version[1..^1].Any(c=>!char.IsAsciiHexDigitUpper(c))||string.IsNullOrWhiteSpace(link.FailedCheckKey)||link.FailedCheckKey.Length>64)
            throw new InternalRequestReadException(400,"INVALID_OCCURRENCE_LINK");
        if(input.InternalRequestId!=null||input.RequestLink!=null)throw new InternalRequestReadException(400,"ONE_CREATION_SOURCE_REQUIRED");
        await new UserEligibilityLock(db).LockAsync([actor],ct);
        if(!await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.preventive_occurrence_incidents','U') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_pm_incident_link_immutable') AND is_disabled=0) THEN 1 ELSE 0 END AS Value").AnyAsync(x=>x==1,ct))
            throw new InternalRequestReadException(503,"OCCURRENCE_INCIDENT_LINK_STORAGE_REQUIRED");
        var read=await MaintenanceReadAccess.ReadAsync(db,actor,input.BranchId,ct);
        if((!read.Lead&&!read.Staff)||!await MaintenanceExecutionAccess.GrantsAsync(db,actor,input.BranchId,ct))
            throw new InternalRequestReadException(403,"OCCURRENCE_EXECUTION_AUTHORITY_REQUIRED");
        var source=await db.Database.SqlQuery<Source>($"""
            SELECT d.definition_json AS Definition,CONVERT(varchar(16),d.row_version,2) AS Version,
              s.asset_id AS Asset,s.room_id AS Room,s.area_id AS Area
            FROM dbo.preventive_occurrence_details d WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.preventive_maintenance_runs r ON r.run_id=d.run_id
            JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
            WHERE d.run_id={link.RunId} AND d.branch_id={input.BranchId} AND d.assignee_id={actor}
              AND d.workflow_status='IN_PROGRESS' AND r.performed_at IS NULL
            """).SingleOrDefaultAsync(ct)??throw new InternalRequestReadException(404,"RUNNING_OWN_OCCURRENCE_REQUIRED");
        if('"'+source.Version+'"'!=link.Version)throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
        if(source.Asset!=input.AssetId||source.Room!=input.RoomId||source.Area!=input.AreaId)
            throw new InternalRequestReadException(400,"OCCURRENCE_TARGET_MISMATCH");
        try
        {
            using var doc=JsonDocument.Parse(source.Definition);
            var root=doc.RootElement;
            var checks=root.GetProperty("checkItems").Deserialize<PreventiveCheckItem[]>();
            static long? Target(JsonElement root,string name)=>root.GetProperty(name).ValueKind==JsonValueKind.Null?null:root.GetProperty(name).GetInt64();
            if(root.GetProperty("schemaVersion").GetInt32()!=1||checks==null||!checks.Any(c=>c.Key==link.FailedCheckKey)
                ||Target(root,"assetId")!=source.Asset||Target(root,"roomId")!=source.Room||Target(root,"areaId")!=source.Area)
                throw new InternalRequestReadException(409,"OCCURRENCE_CHECK_OR_TARGET_INVALID");
        }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {throw new InternalRequestReadException(409,"OCCURRENCE_SNAPSHOT_INVALID");}
    }
    internal static async Task WriteAsync(HotelOpsDbContext db,long actor,CreateIncidentRequest input,long incident,CancellationToken ct)
    {
        if(input.OccurrenceLink is not {} link)return;
        await PrepareAsync(db,actor,input,ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var audit=new AuditLog{BranchId=input.BranchId,ActorUserId=actor,EntityType="preventive_maintenance_runs",
            EntityId=link.RunId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            NewValues=JsonSerializer.Serialize(new{eventType="PREVENTIVE_PROBLEM_REPORTED",runId=link.RunId,incidentId=incident,failedCheckKey=link.FailedCheckKey})};
        db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.preventive_occurrence_incidents(run_id,incident_id,check_key,actor_id,audit_id,linked_at) VALUES({link.RunId},{incident},{link.FailedCheckKey},{actor},{audit.AuditId},{now})",ct);
        if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.preventive_occurrence_details SET workflow_status=workflow_status WHERE run_id={link.RunId} AND CONVERT(varchar(16),row_version,2)={link.Version[1..^1]}",ct)!=1)
            throw new InternalRequestReadException(412,"OCCURRENCE_CHANGED");
    }
}
