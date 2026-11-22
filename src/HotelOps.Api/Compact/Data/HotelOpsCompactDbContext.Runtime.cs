using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Data;

public partial class HotelOpsCompactDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        MapWorkday(modelBuilder);
        // OUTPUT INSERTED is incompatible with SQL Server triggers. Never disable DB triggers.
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(t => t.GetViewName() == null && t.GetTableName() != null))
            modelBuilder.Entity(type.ClrType).ToTable(type.GetTableName()!, t => t.UseSqlOutputClause(false));
    }
}
