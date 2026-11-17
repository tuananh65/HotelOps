using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public partial class HotelOpsDbContext
{
    public virtual DbSet<AssetWorkSourceScope> AssetWorkSourceScopes { get; set; }
    public virtual DbSet<AssetScheduleSourceScope> AssetScheduleSourceScopes { get; set; }
    private static void ConfigureAssetWorkScope(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetScheduleSourceScope>(e =>
        {
            e.ToTable("asset_schedule_source_scopes");
            e.HasKey(x => x.ScheduleId);
            e.Property(x => x.ScheduleId).HasColumnName("schedule_id").ValueGeneratedNever();
            e.Property(x => x.AssetId).HasColumnName("asset_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.HandoverId).HasColumnName("handover_id");
            e.Property(x => x.AuditId).HasColumnName("audit_id");
            e.Property(x => x.CapturedAt).HasColumnName("captured_at");
        });
        modelBuilder.Entity<AssetWorkSourceScope>(e =>
        {
            e.ToTable("asset_work_source_scopes");
            e.HasKey(x => x.ScopeId);
            e.Property(x => x.ScopeId).HasColumnName("scope_id");
            e.Property(x => x.AssetId).HasColumnName("asset_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.IncidentId).HasColumnName("incident_id");
            e.Property(x => x.RunId).HasColumnName("run_id");
            e.Property(x => x.HandoverId).HasColumnName("handover_id");
            e.Property(x => x.AuditId).HasColumnName("audit_id");
            e.Property(x => x.AssetCode).HasColumnName("asset_code").HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.AssetName).HasColumnName("asset_name").HasMaxLength(200);
            e.Property(x => x.CapturedAt).HasColumnName("captured_at");
            e.HasOne(x => x.Incident).WithMany(i => i.AssetSourceScopes).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Run).WithMany(r => r.AssetSourceScopes).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
