using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public partial class HotelOpsDbContext
{
    public DbSet<RoomTypeBranchV1> BranchRoomTypes => Set<RoomTypeBranchV1>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // UC009 grant changes advance the owning role's native version through a SQL trigger.
        modelBuilder.Entity<RolePermission>().ToTable("role_permissions", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs", table => table.UseSqlOutputClause(false));
        ConfigureChecklists(modelBuilder);
        ConfigureWorkTime(modelBuilder);
        // UC012 branch admission triggers coordinate these writers with deactivation.
        modelBuilder.Entity<InternalRequest>().ToTable("internal_requests", t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<MaterialRequest>().ToTable("material_requests", t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<MaterialReplenishment>().ToTable("material_replenishments", t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<StockMovement>().ToTable("stock_movements", t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<WorkInterval>().ToTable("work_intervals", t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<Warehouse>().ToTable("warehouses", t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<CleaningRework>().ToTable("cleaning_reworks", t => t.UseSqlOutputClause(false));
        ConfigureNotificationCandidate(modelBuilder);
        // SQL triggers enforce scoped assignments after deployment. This also works
        // before deployment and prevents SQL334 from EF's bare OUTPUT on rooms.
        modelBuilder.Entity<Room>().ToTable("rooms", table => table.UseSqlOutputClause(false));
        // UC016 parent-location SQL guards also cover non-EF/legacy writers.
        modelBuilder.Entity<Area>().ToTable("areas", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<Asset>().ToTable("assets", table => table.UseSqlOutputClause(false));
        // UC074 retirement guards also coordinate pending handover writers.
        modelBuilder.Entity<AssetHandover>().ToTable("asset_handovers", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<Incident>().ToTable("incidents", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<MaintenanceWork>().ToTable("maintenance_works", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<PreventiveMaintenanceSchedule>().ToTable("preventive_maintenance_schedules", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<PreventiveMaintenanceRun>().ToTable("preventive_maintenance_runs", table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<RoomType>().ToTable("room_types", table => table.UseSqlOutputClause(false));
        var legacy = modelBuilder.Entity<RoomType>();
        legacy.Metadata.RemoveIndex(legacy.Metadata.FindIndex("UQ__room_typ__2CB4DBF5CD9B49E6")!);

        modelBuilder.Entity<RoomTypeBranchV1>(entity =>
        {
            // SQL table mapped as a read-only view: legacy contexts do not select or
            // INSERT new columns, and V1 consumers explicitly opt into this projection.
            entity.ToView("room_types");
            entity.HasKey(x => x.RoomTypeId).HasName("PK__room_typ__42395E848D05FB98");
            entity.Property(x => x.RoomTypeId).HasColumnName("room_type_id");
            entity.Property(x => x.TypeCode).HasColumnName("type_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.TypeName).HasColumnName("type_name").HasMaxLength(150);
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.StandardCapacity).HasColumnName("standard_capacity");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.BranchId).HasColumnName("branch_id");
            entity.Property(x => x.StandardCleaningMinutes).HasColumnName("standard_cleaning_minutes");
            entity.Property(x => x.DefaultChecklistId).HasColumnName("default_checklist_id");
            entity.HasIndex(x => new { x.BranchId, x.TypeCode }, "UX_room_types_branch_code")
                .IsUnique().HasFilter("[branch_id] IS NOT NULL");
            entity.HasIndex(x => x.TypeCode, "UX_room_types_legacy_code")
                .IsUnique().HasFilter("[branch_id] IS NULL");
            entity.HasIndex(x => x.DefaultChecklistId, "IX_room_types_default_checklist_v1");
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_room_types_branch_v1");
            entity.HasOne(x => x.DefaultChecklist).WithMany().HasForeignKey(x => x.DefaultChecklistId)
                .OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_room_types_default_checklist_v1");
        });
    }
}
