using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Data;
public partial class HotelOpsCompactDbContext
{
    public DbSet<SupplyConfiguration> SupplyConfigurations => Set<SupplyConfiguration>();
    public DbSet<StaffSupplyAllocation> StaffSupplyAllocations => Set<StaffSupplyAllocation>();
    public DbSet<StaffSupplyBalance> StaffSupplyBalances => Set<StaffSupplyBalance>();
    public DbSet<SupplyReservation> SupplyReservations => Set<SupplyReservation>();
    public DbSet<StaffSupplyLedger> StaffSupplyLedgers => Set<StaffSupplyLedger>();
    public DbSet<SupplyStockDocument> SupplyStockDocuments => Set<SupplyStockDocument>();

    private static void MapSupplies(ModelBuilder model)
    {
        var config=model.Entity<SupplyConfiguration>();config.ToTable("supply_configurations");config.HasKey(x=>x.ConfigurationId);
        config.Property(x=>x.SupplyGroup).HasMaxLength(1).IsUnicode(false);config.Property(x=>x.RowVersion).IsRowVersion();
        var allocation=model.Entity<StaffSupplyAllocation>();allocation.ToTable("staff_supply_allocations");allocation.HasKey(x=>x.AllocationId);
        allocation.Property(x=>x.Status).HasMaxLength(20).IsUnicode(false);allocation.Property(x=>x.ShortageReason).HasMaxLength(2000);allocation.Property(x=>x.RowVersion).IsRowVersion();
        var balance=model.Entity<StaffSupplyBalance>();balance.ToTable("staff_supply_balances");balance.HasKey(x=>x.BalanceId);balance.Property(x=>x.RowVersion).IsRowVersion();
        var reservation=model.Entity<SupplyReservation>();reservation.ToTable("supply_reservations");reservation.HasKey(x=>x.ReservationId);
        reservation.Property(x=>x.Status).HasMaxLength(20).IsUnicode(false);reservation.Property(x=>x.RowVersion).IsRowVersion();
        reservation.Property(x=>x.UsableQuantity).HasPrecision(18,4);
        var ledger=model.Entity<StaffSupplyLedger>();ledger.ToTable("staff_supply_ledger");ledger.HasKey(x=>x.LedgerId);ledger.Property(x=>x.Kind).HasMaxLength(20).IsUnicode(false);
        var document=model.Entity<SupplyStockDocument>();document.ToTable("supply_stock_documents");document.HasKey(x=>x.DocumentId);document.Property(x=>x.DocumentId).ValueGeneratedNever();document.Property(x=>x.Purpose).HasMaxLength(20).IsUnicode(false);
        foreach(var type in new[]{typeof(SupplyConfiguration),typeof(StaffSupplyAllocation),typeof(StaffSupplyBalance),typeof(SupplyReservation),typeof(StaffSupplyLedger),typeof(SupplyStockDocument)})
            foreach(var property in model.Entity(type).Metadata.GetProperties()){
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
                if(property.ClrType==typeof(decimal)){property.SetPrecision(18);property.SetScale(4);}
            }
    }
}
