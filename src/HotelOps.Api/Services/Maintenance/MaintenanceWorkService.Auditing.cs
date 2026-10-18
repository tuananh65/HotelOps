using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private static Dictionary<string, object?> WorkSnapshot(MaintenanceWork work) => new()
    {
        ["maintenance_work_id"] = work.MaintenanceWorkId,
        ["work_no"] = work.WorkNo,
        ["incident_id"] = work.IncidentId,
        ["assignee_id"] = work.AssigneeId,
        ["department_id"] = work.DepartmentId,
        ["title"] = work.Title,
        ["instruction"] = work.Instruction,
        ["priority_id"] = work.PriorityId,
        ["status"] = work.Status,
        ["planned_start_at"] = work.PlannedStartAt,
        ["planned_end_at"] = work.PlannedEndAt,
        ["actual_start_at"] = work.ActualStartAt,
        ["resolved_at"] = work.ResolvedAt,
        ["resolution_note"] = work.ResolutionNote,
        ["reviewed_by"] = work.ReviewedBy,
        ["reviewed_at"] = work.ReviewedAt,
        ["created_by"] = work.CreatedBy,
        ["created_at"] = work.CreatedAt
    };

    private static Dictionary<string, object?> IncidentSnapshot(Incident incident) => new()
    {
        ["status"] = incident.Status,
        ["resolved_at"] = incident.ResolvedAt,
        ["closed_at"] = incident.ClosedAt,
        ["closure_note"] = incident.ClosureNote
    };

    private void AddAudit(string entityType, long id, long branchId, long actorId, DateTime now,
        Dictionary<string, object?>? before, Dictionary<string, object?> after)
    {
        var changed = before is null ? null :
            after.Keys.Where(key => !Equals(before[key], after[key])).ToArray();
        if (changed is { Length: 0 })
            return;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            BranchId = branchId,
            ActorUserId = actorId,
            EntityType = entityType,
            EntityId = id.ToString(CultureInfo.InvariantCulture),
            Action = before is null ? "CREATE" : "UPDATE",
            OldValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            ChangedFields = changed is null ? null : JsonSerializer.Serialize(changed),
            OccurredAt = now
        });
    }
}
