using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Data;
public partial class HotelOpsCompactDbContext
{
    public DbSet<ItemLifecycleProfile> ItemLifecycleProfiles => Set<ItemLifecycleProfile>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<StockBatchEntry> StockBatchEntries => Set<StockBatchEntry>();
    public DbSet<StaffBatchBalance> StaffBatchBalances => Set<StaffBatchBalance>();
    private static void MapInventoryLifecycle(ModelBuilder model)
    {
        var profile=model.Entity<ItemLifecycleProfile>();profile.ToTable("item_lifecycle_profiles");profile.HasKey(x=>new{x.BranchId,x.ItemId});
        profile.Property(x=>x.Classification).HasMaxLength(20).IsUnicode(false);profile.Property(x=>x.ExpiryPolicy).HasMaxLength(10).IsUnicode(false);
        var batch=model.Entity<StockBatch>();batch.ToTable("stock_batches");batch.HasKey(x=>x.BatchId);
        batch.Property(x=>x.LotNumber).HasMaxLength(100);batch.Property(x=>x.Status).HasMaxLength(20).IsUnicode(false);
        var entry=model.Entity<StockBatchEntry>();entry.ToTable("stock_batch_entries");entry.HasKey(x=>x.EntryId);entry.Property(x=>x.Kind).HasMaxLength(20).IsUnicode(false);
        var balance=model.Entity<StaffBatchBalance>();balance.ToTable("staff_batch_balances");balance.HasKey(x=>x.StaffBatchBalanceId);
        foreach(var type in new[]{typeof(ItemLifecycleProfile),typeof(StockBatch),typeof(StockBatchEntry),typeof(StaffBatchBalance)})
            foreach(var property in model.Entity(type).Metadata.GetProperties()){
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
                if(property.ClrType==typeof(decimal)){property.SetPrecision(18);property.SetScale(4);}
                if(property.Name=="RowVersion")model.Entity(type).Property(property.Name).IsRowVersion();
            }
    }
}
