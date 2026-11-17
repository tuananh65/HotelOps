using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskSeries
{
    public long SeriesId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public long CreatedBy { get; set; }
    public long DefaultAssigneeId { get; set; }
    public string Rule { get; set; } = "";
    public int WeekdayMask { get; set; }
    public int? MonthDay { get; set; }
    public DateTime FirstDueAt { get; set; }
    public DateTime? NextDueAt { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string TemplateJson { get; set; } = "";
    public Guid CreationKey { get; set; }
    public byte[] CreationHash { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public long? StoppedBy { get; set; }
    public byte[] Revision { get; set; } = [];
}

internal static class TaskSeriesStore
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await TaskPlanningStore.InstalledAsync(db, ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.operational_task_series','row_version')=8
              AND COL_LENGTH('dbo.operational_task_series','creation_hash')=32
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_task_series_branch','FK_task_series_department','FK_task_series_creator','FK_task_series_executor','FK_task_series_stopper','FK_task_series_occurrence_series','FK_task_series_occurrence_task') AND is_disabled=0 AND is_not_trusted=0)=7
              AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_task_series_definition_v1','TR_task_series_occurrence_v1') AND is_disabled=0)=2
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_task_series_rule','CK_task_series_template','CK_task_series_next','CK_task_series_stop') AND is_disabled=0 AND is_not_trusted=0)=4
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.task_series_occurrences') AND name='PK_task_series_occurrences' AND is_unique=1 AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static IQueryable<StoredTaskSeries> Query(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskSeries>($"""
        SELECT series_id AS SeriesId,branch_id AS BranchId,department_id AS DepartmentId,created_by AS CreatedBy,
          default_assignee_id AS DefaultAssigneeId,rule_code AS [Rule],weekday_mask AS WeekdayMask,month_day AS MonthDay,
          first_due_at AS FirstDueAt,next_due_at AS NextDueAt,end_date AS EndDate,is_active AS IsActive,
          template_json AS TemplateJson,creation_key AS CreationKey,creation_hash AS CreationHash,created_at AS CreatedAt,
          stopped_at AS StoppedAt,stopped_by AS StoppedBy,CONVERT(binary(8),row_version) AS Revision
        FROM dbo.operational_task_series
        """);

    internal static int[] Weekdays(StoredTaskSeries row) => Enumerable.Range(0, 7).Where(d => (row.WeekdayMask & (1 << d)) != 0).ToArray();
    internal static byte[] Hash(CreateOperationalTaskRequest template, TaskRecurrenceRequest rule) =>
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new CreateTaskSeriesRequest(template, rule)));
    internal static TaskRecurrenceRequest Rule(StoredTaskSeries row) => TaskRecurrenceSchedule.Normalize(
        new(row.Rule, Weekdays(row), row.MonthDay, row.DefaultAssigneeId, row.EndDate), row.FirstDueAt);
    internal static CreateOperationalTaskRequest Template(StoredTaskSeries row)
    {
        var template = JsonSerializer.Deserialize<CreateOperationalTaskRequest>(row.TemplateJson)
            ?? throw new ValidationException("Recurring template is unavailable.");
        template = RoomTypeCreationService.NormalizeTaskCreation(template);
        if (template.DepartmentId != row.DepartmentId || template.DueAt?.UtcDateTime != row.FirstDueAt
            || template.RequestKey != row.CreationKey
            || row.CreationHash.Length != 32 || !CryptographicOperations.FixedTimeEquals(Hash(template, Rule(row)), row.CreationHash))
            throw new ValidationException("Recurring template provenance is inconsistent.");
        return template;
    }
    internal static async Task<TaskSeriesDto> DtoAsync(HotelOpsDbContext db, StoredTaskSeries row, CancellationToken ct)
    {
        var rule = Rule(row); var template = Template(row);
        var name = await db.Users.AsNoTracking().Where(u => u.UserId == row.DefaultAssigneeId).Select(u => u.FullName).SingleAsync(ct);
        var occurrences = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_series_occurrences WHERE series_id={row.SeriesId}").SingleAsync(ct);
        return new(row.SeriesId, row.BranchId, template.Title, rule.Rule, rule.Weekdays!, rule.MonthDay, row.DefaultAssigneeId, name,
            new(DateTime.SpecifyKind(row.FirstDueAt, DateTimeKind.Utc)), row.NextDueAt is { } next ? new DateTimeOffset(DateTime.SpecifyKind(next, DateTimeKind.Utc)) : null,
            row.EndDate, row.IsActive, occurrences, '"' + Convert.ToHexString(row.Revision) + '"');
    }
}
