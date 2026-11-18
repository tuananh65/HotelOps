using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Housekeeping;

public sealed record CleaningFilterOption(long Id, string Name);
public sealed record CleaningMonitorOptions(IReadOnlyList<CleaningFilterOption> Floors,
    IReadOnlyList<CleaningFilterOption> Assignees, IReadOnlyList<CleaningFilterOption> Priorities, IReadOnlyList<string> Statuses);

public sealed partial class CleaningMonitorService
{
    public Task<CleaningMonitorResult> OptionsAsync(SessionReference ticket, long sid, long branch, CancellationToken ct)
        => ReadAsync(ticket, sid, branch, async token =>
        {
            var rows = await RowsAsync(branch, clock.GetUtcNow().UtcDateTime, token);
            var floors = await rows.Select(r => new { r.FloorId, r.FloorName }).Distinct().OrderBy(r => r.FloorName).ToArrayAsync(token);
            var assignees = await rows.Where(r => r.AssigneeId != null).Select(r => new { r.AssigneeId, r.AssigneeName }).Distinct().OrderBy(r => r.AssigneeName).ToArrayAsync(token);
            var priorities = await rows.Where(r => r.PriorityId != null).Select(r => new { r.PriorityId, r.PriorityCode }).Distinct().OrderBy(r => r.PriorityId).ToArrayAsync(token);
            var statuses = await rows.Select(r => r.WorkflowState ?? r.RecordedStatus).Distinct().OrderBy(s => s).ToArrayAsync(token);
            return new(CleaningMonitorStatus.Success, new CleaningMonitorOptions(
                floors.Select(r => new CleaningFilterOption(r.FloorId, r.FloorName ?? "Chưa đặt tên tầng")).ToArray(),
                assignees.Select(r => new CleaningFilterOption(r.AssigneeId!.Value, r.AssigneeName ?? "Chưa có tên nhân viên")).ToArray(),
                priorities.Select(r => new CleaningFilterOption(r.PriorityId!.Value, r.PriorityCode ?? "Chưa đặt tên")).ToArray(), statuses));
        }, ct);
}
