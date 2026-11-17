using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class ReportHousekeepingStates
{
    internal sealed class StateRow
    {
        public long Id { get; set; }
        public string? State { get; set; }
        public string Physical { get; set; } = "";
    }
    internal static async Task<Dictionary<long, string?>> ReadAsync(HotelOpsDbContext db, bool rework, long[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return [];
        // Table/column names are a closed source-owned choice, never request input.
        var table = rework ? "cleaning_reworks" : "cleaning_tasks";
        var installed = await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH({"dbo." + table},'workflow_status') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync(ct);
        if (installed != 1) return ids.ToDictionary(id => id, _ => (string?)null);
        var query = rework
            ? db.Database.SqlQueryRaw<StateRow>("SELECT rework_id AS Id,workflow_status AS State,status AS Physical FROM dbo.cleaning_reworks")
            : db.Database.SqlQueryRaw<StateRow>("SELECT cleaning_task_id AS Id,workflow_status AS State,status AS Physical FROM dbo.cleaning_tasks");
        var rows = await query
            .Where(r => ids.Contains(r.Id)).ToArrayAsync(ct);
        return rows.ToDictionary(r => r.Id, r => Valid(r, rework) ? r.State : null);
    }
    private static bool Valid(StateRow r, bool rework) => (r.State, r.Physical) switch
    {
        ("COMPLETED", "COMPLETED") or ("CANCELLED", "CANCELLED") => true,
        ("IN_PROGRESS" or "ON_HOLD", "IN_PROGRESS") => true,
        ("PENDING_ACCEPTANCE", "PENDING") when rework => true,
        ("PENDING_REINSPECTION", "IN_PROGRESS") when rework => true,
        ("FAILED", "COMPLETED") when rework => true,
        ("PENDING_ASSIGNMENT" or "ASSIGNED", "PENDING") when !rework => true,
        ("PENDING_INSPECTION" or "REWORK_REQUIRED", "IN_PROGRESS") when !rework => true,
        _ => false
    };
}
