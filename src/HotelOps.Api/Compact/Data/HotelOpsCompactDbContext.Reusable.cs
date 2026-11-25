using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Data;
public partial class HotelOpsCompactDbContext
{
    public DbSet<ReusablePosition> ReusablePositions=>Set<ReusablePosition>();
    public DbSet<ReusableMovement> ReusableMovements=>Set<ReusableMovement>();
    public DbSet<ReusableWriteOff> ReusableWriteOffs=>Set<ReusableWriteOff>();
    private static void MapReusable(ModelBuilder model)
    {
        var p=model.Entity<ReusablePosition>();p.ToTable("reusable_positions");p.HasKey(x=>x.PositionId);p.Property(x=>x.Status).HasMaxLength(20).IsUnicode(false);p.Property(x=>x.LocationKey).HasMaxLength(40).IsUnicode(false);
        var m=model.Entity<ReusableMovement>();m.ToTable("reusable_movements");m.HasKey(x=>x.ReusableMovementId);m.Property(x=>x.Kind).HasMaxLength(20).IsUnicode(false);m.Property(x=>x.Reason).HasMaxLength(2000);
        var w=model.Entity<ReusableWriteOff>();w.ToTable("reusable_write_offs");w.HasKey(x=>x.WriteOffId);w.Property(x=>x.Status).HasMaxLength(20).IsUnicode(false);w.Property(x=>x.Reason).HasMaxLength(2000);w.Property(x=>x.DecisionReason).HasMaxLength(2000);
        foreach(var type in new[]{typeof(ReusablePosition),typeof(ReusableMovement),typeof(ReusableWriteOff)})
            foreach(var property in model.Entity(type).Metadata.GetProperties()){
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
                if(property.ClrType==typeof(decimal)){property.SetPrecision(18);property.SetScale(4);}
                if(property.Name=="RowVersion")model.Entity(type).Property(property.Name).IsRowVersion();
            }
    }
}
