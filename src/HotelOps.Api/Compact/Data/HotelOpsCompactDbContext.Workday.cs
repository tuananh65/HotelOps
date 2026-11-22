using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Data;

public partial class HotelOpsCompactDbContext
{
    public DbSet<WorkdayShift> WorkdayShifts => Set<WorkdayShift>();
    public DbSet<WorkdayAssignment> WorkdayAssignments => Set<WorkdayAssignment>();
    public DbSet<WorkdayEvent> WorkdayEvents => Set<WorkdayEvent>();
    private static void MapWorkday(ModelBuilder model)
    {
        var shift=model.Entity<WorkdayShift>();shift.ToTable("workday_shifts");shift.HasKey(x=>x.ShiftId);
        shift.Property(x=>x.Name).HasMaxLength(150);shift.Property(x=>x.RowVersion).IsRowVersion();
        var assignment=model.Entity<WorkdayAssignment>();assignment.ToTable("workday_assignments");assignment.HasKey(x=>x.AssignmentId);
        assignment.Property(x=>x.Status).HasMaxLength(20).IsUnicode(false);assignment.Property(x=>x.RowVersion).IsRowVersion();
        var history=model.Entity<WorkdayEvent>();history.ToTable("workday_events");history.HasKey(x=>x.EventId);
        history.Property(x=>x.Action).HasMaxLength(20).IsUnicode(false);history.Property(x=>x.Reason).HasMaxLength(2000);
        foreach(var type in new[]{typeof(WorkdayShift),typeof(WorkdayAssignment),typeof(WorkdayEvent)})
            foreach(var property in model.Entity(type).Metadata.GetProperties())
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
    }
}
