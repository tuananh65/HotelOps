using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.Insights;

public class InsightQuery
{
    [Range(1, long.MaxValue)] public long? BranchId { get; set; }
    [Range(1, long.MaxValue)] public long? DepartmentId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class ReportQuery : InsightQuery
{
    [Range(1, long.MaxValue)] public long? FloorId { get; set; }
    [Range(1, long.MaxValue)] public long? RoomTypeId { get; set; }
    [Range(1, long.MaxValue)] public long? EmployeeId { get; set; }
    [Range(1, long.MaxValue)] public long? InspectorId { get; set; }
    [Range(1, long.MaxValue)] public long? HousekeeperId { get; set; }
    [Range(1, long.MaxValue)] public long? WarehouseId { get; set; }
    [Range(1, long.MaxValue)] public long? ItemId { get; set; }
    [Range(1, long.MaxValue)] public long? SeverityId { get; set; }
    [Range(1, long.MaxValue)] public long? CategoryId { get; set; }
    [StringLength(40)] public string? Condition { get; set; }
    [StringLength(40)] public string? Type { get; set; }
}

public sealed class AuditQuery : InsightQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [Range(1, long.MaxValue)] public long? ActorId { get; set; }
    [StringLength(100)] public string? EntityType { get; set; }
    [StringLength(100)] public string? EntityId { get; set; }
    [StringLength(100)] public string? Action { get; set; }
    [StringLength(100)] public string? Search { get; set; }
}

public sealed record InsightBranch(long Id, string Code, string Name);
public sealed record InsightDepartment(long Id, long BranchId, string Name);
public sealed record InsightOptions(IReadOnlyList<InsightBranch> Branches, IReadOnlyList<InsightDepartment> Departments,
    string Calendar, int DefaultPeriodDays, int ExportRowLimit);
public sealed record InsightFilters(long[] BranchIds, long? DepartmentId, DateOnly From, DateOnly To, string Calendar = "UTC");
public sealed record MetricRow(string Key, string Label, decimal? Value, string Unit, string Source, string? DrillDown = null);
public sealed record DistributionRow(string Source, string Status, long Count, string? Severity = null);
public sealed record QuantityRow(string Kind, long ItemId, long UomId, long? WarehouseId,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal Quantity);
public sealed record DashboardResult(DateTimeOffset Snapshot, InsightFilters Filters,
    IReadOnlyList<MetricRow> Metrics, IReadOnlyList<DistributionRow> Distributions,
    IReadOnlyList<QuantityRow> Quantities, IReadOnlyList<string> MissingSources);
public sealed record ReportSection(string Key, string[] Columns, IReadOnlyList<Dictionary<string, object?>> Rows);
public sealed record ReportResult(string SnapshotId, string Key, DateTimeOffset Snapshot, InsightFilters Filters,
    string[] Columns, IReadOnlyList<Dictionary<string, object?>> Rows, int TotalCount,
    IReadOnlyList<ReportSection> Sections, IReadOnlyList<string> MissingSources, string[] ExportFormats,
    IReadOnlyDictionary<string, string?>? AppliedFilters = null);
public sealed class ExportRequest
{
    public ExportRequest() { }
    public ExportRequest(string format) => Format = format;
    [Required, StringLength(8)] public string Format { get; set; } = string.Empty;
}
public sealed record ExportFile(byte[] Content, string ContentType, string Name);
public sealed record AuditListRow(long Id, long? BranchId, long? ActorId, string? Actor, string Action,
    string EntityType, string EntityId, DateTimeOffset OccurredAt);
public sealed record AuditPage(IReadOnlyList<AuditListRow> Items, int TotalCount, int Page, int PageSize);
public sealed record AuditDetail(AuditListRow Entry, string? RequestId, object? Before, object? After,
    string Provenance, bool Redacted);
