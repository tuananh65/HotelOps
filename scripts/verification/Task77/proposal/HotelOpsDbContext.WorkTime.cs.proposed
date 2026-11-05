using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public partial class HotelOpsDbContext
{
    public virtual DbSet<Workday> Workdays { get; set; }
    public virtual DbSet<WorkInterval> WorkIntervals { get; set; }
    public virtual DbSet<TimeCorrectionRequest> TimeCorrectionRequests { get; set; }

    // Called explicitly before the existing OnModelCreatingPartial; no second partial implementation.
    private static void ConfigureWorkTime(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workday>(e =>
        {
            e.ToTable("workdays");
            e.HasKey(x => x.WorkdayId).HasName("PK_workdays");
            e.HasAlternateKey(x => new { x.WorkdayId, x.UserId, x.BranchId }).HasName("UQ_workdays_identity");
            e.Property(x => x.WorkdayId).HasColumnName("workday_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.DepartmentId).HasColumnName("department_id");
            e.Property(x => x.WorkDate).HasColumnName("work_date").HasColumnType("date");
            e.Property(x => x.ShiftAssignmentId).HasColumnName("shift_assignment_id");
            e.Property(x => x.IsUnscheduled).HasColumnName("is_unscheduled");
            e.Property(x => x.UnscheduledReason).HasColumnName("unscheduled_reason").HasMaxLength(1000);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            e.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.EndedAt).HasColumnName("ended_at").HasColumnType("datetime2(7)");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("[status] = 'WORKING'").HasDatabaseName("UX_workdays_one_working");
            e.HasIndex(x => new { x.UserId, x.WorkDate, x.WorkdayId }).HasDatabaseName("IX_workdays_user_date");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_workdays_user");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_workdays_branch");
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_workdays_department");
            e.HasOne<ShiftAssignment>().WithMany().HasForeignKey(x => x.ShiftAssignmentId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_workdays_assignment");
        });
        modelBuilder.Entity<WorkInterval>(e =>
        {
            e.ToTable("work_intervals");
            e.HasKey(x => x.IntervalId).HasName("PK_work_intervals");
            e.Property(x => x.IntervalId).HasColumnName("interval_id");
            e.Property(x => x.WorkdayId).HasColumnName("workday_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.WorkItemKind).HasColumnName("work_item_kind").HasMaxLength(24).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            e.Property(x => x.WorkItemId).HasColumnName("work_item_id");
            e.Property(x => x.OpenedBy).HasColumnName("opened_by");
            e.Property(x => x.OpenedAt).HasColumnName("opened_at").HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.ClosedBy).HasColumnName("closed_by");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("datetime2(7)");
            e.Property(x => x.EffectiveStartedAt).HasColumnName("effective_started_at").HasColumnType("datetime2(7)");
            e.Property(x => x.EffectiveEndedAt).HasColumnName("effective_ended_at").HasColumnType("datetime2(7)");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("[closed_at] IS NULL").HasDatabaseName("UX_work_intervals_one_open");
            e.HasIndex(x => new { x.UserId, x.EffectiveStartedAt, x.IntervalId }).HasDatabaseName("IX_work_intervals_user_time");
            e.HasIndex(x => new { x.WorkItemKind, x.WorkItemId, x.IntervalId }).HasDatabaseName("IX_work_intervals_item");
            e.HasOne<Workday>().WithMany().HasForeignKey(x => new { x.WorkdayId, x.UserId, x.BranchId })
                .HasPrincipalKey(x => new { x.WorkdayId, x.UserId, x.BranchId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_work_intervals_workday");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OpenedBy).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_work_intervals_opened_by");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ClosedBy).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_work_intervals_closed_by");
        });
        modelBuilder.Entity<TimeCorrectionRequest>(e =>
        {
            e.ToTable("time_correction_requests");
            e.HasKey(x => x.CorrectionId).HasName("PK_time_correction_requests");
            e.Property(x => x.CorrectionId).HasColumnName("correction_id");
            e.Property(x => x.IntervalId).HasColumnName("interval_id");
            e.Property(x => x.RequestedBy).HasColumnName("requested_by");
            e.Property(x => x.RequestedAt).HasColumnName("requested_at").HasColumnType("datetime2(7)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.OriginalStartedAt).HasColumnName("original_started_at").HasColumnType("datetime2(7)");
            e.Property(x => x.OriginalEndedAt).HasColumnName("original_ended_at").HasColumnType("datetime2(7)");
            e.Property(x => x.OriginalIntervalVersion).HasColumnName("original_interval_version").HasColumnType("binary(8)").HasMaxLength(8).IsFixedLength();
            e.Property(x => x.ProposedStartedAt).HasColumnName("proposed_started_at").HasColumnType("datetime2(7)");
            e.Property(x => x.ProposedEndedAt).HasColumnName("proposed_ended_at").HasColumnType("datetime2(7)");
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            e.Property(x => x.DecidedBy).HasColumnName("decided_by");
            e.Property(x => x.DecidedAt).HasColumnName("decided_at").HasColumnType("datetime2(7)");
            e.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(1000);
            e.Property(x => x.AppliedStartedAt).HasColumnName("applied_started_at").HasColumnType("datetime2(7)");
            e.Property(x => x.AppliedEndedAt).HasColumnName("applied_ended_at").HasColumnType("datetime2(7)");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            e.HasIndex(x => new { x.IntervalId, x.RequestedAt, x.CorrectionId }).HasDatabaseName("IX_time_corrections_interval");
            e.HasOne<WorkInterval>().WithMany().HasForeignKey(x => x.IntervalId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_time_corrections_interval");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_time_corrections_requester");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_time_corrections_decider");
        });
    }
}
