using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Data;

public partial class HotelOpsCompactDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        MapWorkday(modelBuilder);
        MapSupplies(modelBuilder);
        MapRequests(modelBuilder);
        MapInvitations(modelBuilder);
        MapInventoryLifecycle(modelBuilder);
        MapReusable(modelBuilder);
        modelBuilder.Entity<HotelOps.Api.Compact.Entities.RoomBlock>().HasIndex(e=>e.RoomId,"UX_room_blocks_room_id").IsUnique(false).HasDatabaseName("IX_room_blocks_open");
        // OUTPUT INSERTED is incompatible with SQL Server triggers. Never disable DB triggers.
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(t => t.GetViewName() == null && t.GetTableName() != null))
            modelBuilder.Entity(type.ClrType).ToTable(type.GetTableName()!, t => t.UseSqlOutputClause(false));
    }
}
