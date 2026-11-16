using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

// Additive database-first storage. Does not grant incident or report access.
internal static class IncidentCategoryStore
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties
              WHERE class=0 AND name=N'HotelOps.IncidentCategory.V1' AND CONVERT(nvarchar(20),value)=N'1')
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.incidents')
                AND name=N'category_id' AND system_type_id=127 AND is_nullable=1)
              AND EXISTS(SELECT 1 FROM sys.foreign_keys f
                JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
                WHERE f.parent_object_id=OBJECT_ID(N'dbo.incidents')
                AND f.referenced_object_id=OBJECT_ID(N'dbo.reference_values')
                AND f.name=N'FK_incidents_category' AND f.is_disabled=0 AND f.is_not_trusted=0
                AND f.delete_referential_action=0 AND f.update_referential_action=0
                AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'category_id'
                AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'reference_id')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task ValidateAsync(HotelOpsDbContext db, long? category, CancellationToken ct)
    {
        if (category is null) return;
        if (category <= 0) throw new ValidationException("CategoryId must be positive.");
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Incident category requires the command transaction.");
        if (!await InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        // Keep group, active state and identity stable until the incident/audit commit.
        if (!await db.ReferenceValues.FromSqlInterpolated($"""
            SELECT * FROM dbo.reference_values WITH (UPDLOCK,HOLDLOCK)
            WHERE reference_id={category.Value} AND group_code=N'INCIDENT_CATEGORY' AND is_active=1
            """).AsNoTracking().AnyAsync(ct))
            throw new ValidationException("CategoryId must be an active INCIDENT_CATEGORY reference.");
    }

    internal static async Task WriteAsync(HotelOpsDbContext db, long incident, long? category, CancellationToken ct)
    {
        if (category is null) return;
        await ValidateAsync(db, category, ct);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.incidents SET category_id={category.Value}
            WHERE incident_id={incident} AND category_id IS NULL
            """, ct);
        if (changed != 1) throw new InvalidOperationException("Incident category binding failed.");
    }

    // Callers must intersect these rows with their already-authorized incident query.
    internal static IQueryable<CategoryRow> Rows(HotelOpsDbContext db)
        => db.Database.SqlQuery<CategoryRow>($"""
            SELECT i.incident_id AS IncidentId, r.reference_id AS CategoryId,
              r.value_code AS CategoryCode, r.display_name AS CategoryName
            FROM dbo.incidents i LEFT JOIN dbo.reference_values r
              ON r.reference_id=i.category_id AND r.group_code=N'INCIDENT_CATEGORY'
            """);

    internal static async Task EnrichAsync(HotelOpsDbContext db, IncidentDetailDto detail, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct))
        {
            detail.CategoryUnavailableReason = "INCIDENT_CATEGORY_STORAGE_UNAVAILABLE";
            return;
        }
        var row = await Rows(db).SingleAsync(r => r.IncidentId == detail.IncidentId, ct);
        detail.CategoryId = row.CategoryId;
        detail.CategoryCode = row.CategoryCode;
        detail.CategoryName = row.CategoryName;
        detail.CategoryUnavailableReason = row.CategoryId is null ? "INCIDENT_CATEGORY_NOT_RECORDED" : null;
    }

    internal sealed class CategoryRow
    {
        public long IncidentId { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryCode { get; set; }
        public string? CategoryName { get; set; }
    }
}
