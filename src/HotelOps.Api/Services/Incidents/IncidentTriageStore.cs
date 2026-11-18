using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentTriageStore
{
    internal sealed record Deadlines(DateTime Response, DateTime Resolution, string? OverrideReason);
    internal sealed class Target
    {
        public int ResponseMinutes { get; set; }
        public int ResolutionMinutes { get; set; }
        public string Version { get; set; } = "";
    }
    internal sealed class NamedTarget
    {
        public long PriorityId { get; set; }
        public string Name { get; set; } = "";
        public int ResponseMinutes { get; set; }
        public int ResolutionMinutes { get; set; }
        public string Version { get; set; } = "";
    }

    internal static async Task<IncidentTriageOptionDto[]> OptionsAsync(HotelOpsDbContext db, DateTime discovered, CancellationToken ct)
    {
        var targets = await db.Database.SqlQuery<NamedTarget>($"""
            SELECT r.reference_id AS PriorityId,r.display_name AS Name,t.response_minutes AS ResponseMinutes,
              t.resolution_minutes AS ResolutionMinutes,CONVERT(varchar(16),CONVERT(binary(8),t.row_version),2) AS Version
            FROM dbo.incident_priority_sla_targets t JOIN dbo.reference_values r ON r.reference_id=t.priority_id
            WHERE t.is_active=1 AND r.is_active=1 AND r.group_code='PRIORITY'
            """).ToArrayAsync(ct);
        var options = new List<IncidentTriageOptionDto>();
        foreach (var t in targets.OrderBy(t => t.PriorityId))
        {
            try
            {
                var d = Calculate(discovered, new() { ResponseMinutes = t.ResponseMinutes, ResolutionMinutes = t.ResolutionMinutes }, new());
                options.Add(new(t.PriorityId, t.Name, d.Response, d.Resolution, t.Version));
            }
            catch (ValidationException) { /* Invalid configuration cannot be offered as a usable target. */ }
        }
        return options.ToArray();
    }
    internal static Deadlines Calculate(DateTime discovered, Target target, TriageIncidentRequest input)
    {
        if (target.ResponseMinutes <= 0 || target.ResolutionMinutes <= 0)
            throw new ValidationException("Approved positive SLA targets are required.");
        discovered = DateTime.SpecifyKind(discovered, DateTimeKind.Utc);
        DateTime response, resolution;
        try { response = discovered.AddMinutes(target.ResponseMinutes); resolution = discovered.AddMinutes(target.ResolutionMinutes); }
        catch (ArgumentOutOfRangeException) { throw new ValidationException("SLA targets exceed the supported date range."); }
        if (input.ResponseDeadlineAt is {} r)
        {
            if (r.Kind != DateTimeKind.Utc || r < discovered) throw new ValidationException("Response deadline must be UTC and not before discovery.");
        }
        if (input.ResolutionDeadlineAt is {} s)
        {
            if (s.Kind != DateTimeKind.Utc || s < discovered) throw new ValidationException("Resolution deadline must be UTC and not before discovery.");
        }
        var changed = input.ResponseDeadlineAt is {} a && a != response || input.ResolutionDeadlineAt is {} b && b != resolution;
        var reason = input.DeadlineChangeReason?.Trim();
        if (changed && (reason == null || reason.Length < 10)) throw new ValidationException("Changing default SLA deadlines requires a reason of at least 10 characters.");
        return new(input.ResponseDeadlineAt ?? response, input.ResolutionDeadlineAt ?? resolution, changed ? reason : null);
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.IncidentTriage.V1' AND CONVERT(nvarchar(20),value)='1')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.IncidentSla.V1' AND CONVERT(nvarchar(20),value)='1')
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents')
                AND name IN('response_deadline_at','resolution_deadline_at') AND system_type_id=42 AND scale=7 AND is_nullable=1)=2
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
              AND COL_LENGTH('dbo.incident_priority_sla_targets','response_minutes')=4
              AND COL_LENGTH('dbo.incident_priority_sla_targets','resolution_minutes')=4
              AND COL_LENGTH('dbo.incident_priority_sla_targets','row_version')=8
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_priority_sla_targets')
                AND name IN('response_minutes','resolution_minutes') AND system_type_id=56 AND is_nullable=0)=2
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_priority_sla_targets') AND name='is_active' AND system_type_id=104 AND is_nullable=0)
              AND EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_priority_sla_targets')
                AND name='FK_incident_sla_priority' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task<string?> TagAsync(HotelOpsDbContext db, long id, CancellationToken ct)
        => !await InstalledAsync(db, ct) ? null : await db.Database.SqlQuery<string>($"""
            SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) AS Value FROM dbo.incidents WITH(HOLDLOCK) WHERE incident_id={id}
            """).SingleOrDefaultAsync(ct);

    internal static async Task<Target> TargetAsync(HotelOpsDbContext db, long priority, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        return await db.Database.SqlQuery<Target>($"""
            SELECT t.response_minutes AS ResponseMinutes,t.resolution_minutes AS ResolutionMinutes,
              CONVERT(varchar(16),CONVERT(binary(8),t.row_version),2) AS Version
            FROM dbo.incident_priority_sla_targets t WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.reference_values r WITH(HOLDLOCK) ON r.reference_id=t.priority_id
            WHERE t.priority_id={priority} AND t.is_active=1 AND r.is_active=1 AND r.group_code='PRIORITY'
            """).SingleOrDefaultAsync(ct) ?? throw new ValidationException("No approved active SLA targets are configured for this priority.");
    }

    internal static async Task WriteAsync(HotelOpsDbContext db, long id, Deadlines deadlines, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Owning transaction required.");
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.incidents SET response_deadline_at={deadlines.Response},resolution_deadline_at={deadlines.Resolution}
            WHERE incident_id={id} AND response_deadline_at IS NULL AND resolution_deadline_at IS NULL AND status='OPEN'
            """, ct);
        if (count != 1) throw new ValidationException("Existing SLA evidence cannot be overwritten by initial Triage.");
    }
}
