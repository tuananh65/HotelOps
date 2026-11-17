using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal sealed class WorkAlertEpisodeRow
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public DateTime LastObservedAt { get; set; }
    public long CycleId { get; set; }
    public string InitialCondition { get; set; } = "";
}

internal sealed class WorkAlertSourceRow
{
    public long Id { get; set; }
    public string SourceType { get; set; } = "";
    public long SourceId { get; set; }
    public long BranchId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string InitialCondition { get; set; } = "";
    public long CycleId { get; set; }
    public string Metric { get; set; } = "";
}

internal static class WorkAlertEpisodeStore
{
    private static readonly string[] Sources = ["CLEANING", "REWORK", "INCIDENT", "MAINTENANCE_OCCURRENCE", "TASK", "INTERNAL_REQUEST"];

    internal static IQueryable<WorkAlertSourceRow> SourcesQuery(HotelOpsDbContext db) => db.Database.SqlQuery<WorkAlertSourceRow>($"""
        SELECT e.episode_id AS Id,e.source_type AS SourceType,e.source_id AS SourceId,e.branch_id AS BranchId,
          e.opened_at AS OpenedAt,e.closed_at AS ClosedAt,e.initial_condition AS InitialCondition,e.cycle_id AS CycleId,e.metric AS Metric
        FROM dbo.work_alert_episodes e
        WHERE EXISTS(SELECT 1 FROM dbo.audit_logs a
          CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
          WHERE a.entity_type='work_alert_episodes' AND a.entity_id=CONVERT(nvarchar(30),e.episode_id)
            AND a.branch_id=e.branch_id AND a.actor_user_id IS NULL AND a.action='CREATE' AND a.occurred_at=e.opened_at
            AND JSON_VALUE(j.payload,'$.operation')='OPEN' AND JSON_VALUE(j.payload,'$.sourceType')=e.source_type
            AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.sourceId'))=e.source_id
            AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.cycleId'))=e.cycle_id
            AND JSON_VALUE(j.payload,'$.metric')=e.metric
            AND JSON_VALUE(j.payload,'$.condition')=CASE e.initial_condition WHEN 'AT_RISK' THEN 'AtRisk' ELSE 'Overdue' END)
        """);

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.WorkAlerts.V2' AND CONVERT(nvarchar(20),value)='2')
              AND COL_LENGTH('dbo.work_alert_episodes','episode_id')=8
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_alert_episodes')
                AND name='UX_work_alert_open' AND is_unique=1 AND has_filter=1 AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.work_alert_episodes')
                AND name='FK_work_alert_branch' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.work_alert_episodes')
                AND is_disabled=0 AND is_not_trusted=0)=4
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    // Caller owns the Serializable transaction and must read the source under
    // that same transaction. A failed scan cannot close or create an episode.
    internal static async Task<long?> ObserveAsync(HotelOpsDbContext db, string kind, long id, long branch,
        WorkAlertCondition condition, DateTime? due, DateTime? allowedFrom, DateTime now, CancellationToken ct,long cycleId=0,string metric="DUE")
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable alert/source observation required.");
        if (!Sources.Contains(kind) || id <= 0 || branch <= 0 || cycleId<0 || metric is not ("DUE" or "RESPONSE" or "RESOLUTION") || !Enum.IsDefined(condition))
            throw new ArgumentException("Invalid alert identity or condition.");
        if (condition == WorkAlertCondition.Unknown) return null;
        if (condition is WorkAlertCondition.AtRisk or WorkAlertCondition.Overdue
            && (due == null || allowedFrom is { } start && due <= start))
            throw new ArgumentException("An alert requires a valid source deadline.");
        var rows = await db.Database.SqlQuery<WorkAlertEpisodeRow>($"""
            SELECT episode_id AS Id,branch_id AS BranchId,last_observed_at AS LastObservedAt,cycle_id AS CycleId,initial_condition AS InitialCondition
            FROM dbo.work_alert_episodes WITH(UPDLOCK,HOLDLOCK)
            WHERE source_type={kind} AND source_id={id} AND metric={metric} AND closed_at IS NULL
            """).ToArrayAsync(ct);
        if (rows.Any(row=>row.BranchId != branch || row.LastObservedAt > now))
            throw new InvalidOperationException("Alert source identity or observation time changed.");
        async Task Audit(long episode,string operation)
        {
            var audit=new AuditLog{BranchId=branch,EntityType="work_alert_episodes",EntityId=episode.ToString(CultureInfo.InvariantCulture),
                Action=operation=="OPEN"?"CREATE":"UPDATE",OccurredAt=now,
                NewValues=JsonSerializer.Serialize(new{operation,sourceType=kind,sourceId=id,cycleId,metric,condition=condition.ToString(),due,allowedFrom})};
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(ct);
            db.Entry(audit).State=EntityState.Detached;
        }
        foreach(var closing in rows.Where(r=>r.CycleId!=cycleId||condition==WorkAlertCondition.Clear
            ||condition==WorkAlertCondition.AtRisk&&r.InitialCondition=="OVERDUE"
            ||condition==WorkAlertCondition.NotEvaluable&&due>=now&&r.InitialCondition=="OVERDUE"))
        {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.work_alert_episodes SET closed_at={now},last_observed_at={now},current_condition='CLEAR'
                    WHERE episode_id={closing.Id} AND closed_at IS NULL
                    """, ct);
                await Audit(closing.Id,"CLOSE");
        }
        if(condition is WorkAlertCondition.Clear or WorkAlertCondition.NotEvaluable)return null;
        var state = condition == WorkAlertCondition.AtRisk ? "AT_RISK" : "OVERDUE";
        var row=rows.SingleOrDefault(r=>r.CycleId==cycleId&&r.InitialCondition==state);
        if (row != null)
        {
            // Repeated observations of this cycle/threshold reuse its intent.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.work_alert_episodes SET current_condition={state},last_observed_at={now},due_at={due},allowed_from={allowedFrom}
                WHERE episode_id={row.Id} AND closed_at IS NULL
                """, ct);
            return row.Id;
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.work_alert_episodes(source_type,source_id,branch_id,opened_at,last_observed_at,initial_condition,current_condition,due_at,allowed_from,cycle_id,metric)
            VALUES({kind},{id},{branch},{now},{now},{state},{state},{due},{allowedFrom},{cycleId},{metric})
            """, ct);
        var created=await db.Database.SqlQuery<long>($"""
            SELECT episode_id AS Value FROM dbo.work_alert_episodes WHERE source_type={kind} AND source_id={id}
              AND cycle_id={cycleId} AND metric={metric} AND initial_condition={state} AND closed_at IS NULL
            """).SingleAsync(ct);
        await Audit(created,"OPEN");
        return created;
    }
}
