using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskDefinition
{
    public long TaskId { get; set; }
    public string? RequestedResult { get; set; }
    public int? SlaMinutes { get; set; }
    public Guid? CreationKey { get; set; }
    public byte[]? CreationHash { get; set; }
}
internal static class TaskPlanningStore
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await TaskDeclineStore.InstalledAsync(db, ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.operational_task_definitions','requested_result')=8000
              AND COL_LENGTH('dbo.operational_task_definitions','creation_hash')=32
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.operational_task_definitions') AND name='UQ_operational_task_creation_key' AND is_unique=1 AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE name='TR_task_definition_v1' AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_operational_task_definitions_task','FK_task_category_processes_category') AND is_disabled=0 AND is_not_trusted=0)=2
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static IQueryable<StoredTaskDefinition> Definitions(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskDefinition>($"""
        SELECT task_id AS TaskId,requested_result AS RequestedResult,sla_minutes AS SlaMinutes,
          creation_key AS CreationKey,creation_hash AS CreationHash FROM dbo.operational_task_definitions
        """);
    internal static IQueryable<long> OperationalCategories(HotelOpsDbContext db) => db.Database.SqlQuery<long>($"""
        SELECT category_id AS Value FROM dbo.task_category_processes WHERE process_code='OPERATIONAL'
        """);
}
