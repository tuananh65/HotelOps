using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// Runs inside the inventory/configuration transaction; notification failure rolls back the change.
public sealed class LowStockService(HotelOpsCompactDbContext db,NotificationService notifications,TimeProvider clock,IConfiguration? configuration=null)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private static string Qty(decimal value)=>value.ToString("0.####",CultureInfo.GetCultureInfo("vi-VN"));
    private static Guid Key(string value)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0,16));

    public async Task Reconcile(CancellationToken ct,long? branchId=null)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"INVENTORY_TRANSACTION_REQUIRED");
        // Shared by immediate writers and the scheduler. SQL deadlocks/concurrency failures must
        // roll back the entire caller transaction, never commit stock without its alert state.
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.LowStock',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51501,'Low stock reconciliation lock unavailable.',1;",ct);
        var policies=await db.InventoryPolicies.Where(p=>branchId==null||p.BranchId==branchId).OrderBy(p=>p.InventoryPolicyId).ToArrayAsync(ct);
        foreach(var policy in policies){
            var reusable=await db.ItemLifecycleProfiles.AnyAsync(p=>p.BranchId==policy.BranchId&&p.ItemId==policy.ItemId&&p.Classification=="REUSABLE",ct);
            var stock=await StockBatchAccounting.Available(db,policy.WarehouseId,policy.ItemId,Now,ct);
            var enabled=await db.Warehouses.AnyAsync(w=>w.WarehouseId==policy.WarehouseId&&w.IsActive,ct)
                &&await db.InventoryItems.AnyAsync(i=>i.ItemId==policy.ItemId&&i.IsActive,ct)
                &&await db.Branches.AnyAsync(b=>b.BranchId==policy.BranchId&&b.IsActive,ct);
            if(!enabled||stock>=policy.MinimumQty){
                if(policy.LowStockEpisode is Guid previous){
                    foreach(var n in await db.Notifications.Where(n=>n.BranchId==policy.BranchId&&n.NotificationType=="LOW_STOCK"&&n.EventKey==previous&&n.ExpiresAt==null).ToArrayAsync(ct))n.ExpiresAt=Now;
                }
                policy.LowStockEpisode=null;policy.AlertedAt=null;continue;
            }
            policy.LowStockEpisode??=Guid.NewGuid();
            var item=await db.InventoryItems.AsNoTracking().SingleAsync(i=>i.ItemId==policy.ItemId,ct);
            var unit=await db.ReferenceValues.AsNoTracking().Where(u=>u.ReferenceId==item.BaseUnitId).Select(u=>u.Name).SingleAsync(ct);
            var branch=await db.Branches.AsNoTracking().Where(b=>b.BranchId==policy.BranchId).Select(b=>b.Name).SingleAsync(ct);
            var warehouse=await db.Warehouses.AsNoTracking().Where(w=>w.WarehouseId==policy.WarehouseId).Select(w=>w.Name).SingleAsync(ct);
            var laundry=reusable?await db.ReusablePositions.AsNoTracking().Where(p=>p.BranchId==policy.BranchId&&p.WarehouseId==policy.WarehouseId&&p.ItemId==policy.ItemId&&p.Status=="LAUNDRY").SumAsync(p=>(decimal?)p.Quantity,ct)??0:0;
            var transit=await TransfersInTransit(policy,ct);
            var recipients=await Recipients(policy.BranchId,"STOREKEEPER","INVENTORY_VIEW",ct);
            var message=LowStockAlertText.Format(branch,warehouse,item.Name,unit,reusable,stock,policy.MinimumQty,laundry,transit.Incoming,transit.Outgoing);
            // Query persisted recipients too: new storekeepers can receive the still-open episode
            // without sending a second notification to existing recipients.
            foreach(var recipient in recipients)await Add(policy.BranchId,recipient,"LOW_STOCK",message,null,policy.LowStockEpisode.Value,ct);
            if(recipients.Length>0)policy.AlertedAt??=Now;
        }
        await ExpiryWarnings(branchId,ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<(decimal Incoming,decimal Outgoing)> TransfersInTransit(InventoryPolicy policy,CancellationToken ct)
    {
        // Only goods actually dispatched count as in transit. Pending/approved requests do
        // not reserve stock here, and an unreceived discrepancy remains in transit.
        var shipments=await (from d in db.InventoryDocuments.AsNoTracking()
            join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            join m in db.StockMovements on l.LineId equals m.LineId
            where d.Kind=="TRANSFER"&&d.PostedAt!=null
                &&((d.DestinationBranchId==policy.BranchId&&d.DestinationWarehouseId==policy.WarehouseId)
                    ||(d.BranchId==policy.BranchId&&d.SourceWarehouseId==policy.WarehouseId))
                &&l.ItemId==policy.ItemId&&m.SignedBaseQty<0
                &&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
            select new{d.DocumentId,l.LineId,d.DestinationBranchId,d.DestinationWarehouseId,Quantity=-m.SignedBaseQty}).ToArrayAsync(ct);
        decimal incoming=0,outgoing=0;
        foreach(var shipment in shipments.GroupBy(s=>new{s.DocumentId,s.LineId,s.DestinationBranchId,s.DestinationWarehouseId})){
            var received=await (from d in db.InventoryDocuments.AsNoTracking()
                join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
                join m in db.StockMovements on l.LineId equals m.LineId
                where d.Kind=="TRANSFER_RECEIPT"&&d.PostedAt!=null&&d.SourceDocumentId==shipment.Key.DocumentId
                    &&d.BranchId==shipment.Key.DestinationBranchId&&d.DestinationWarehouseId==shipment.Key.DestinationWarehouseId
                    &&l.SourceLineId==shipment.Key.LineId&&l.ItemId==policy.ItemId&&m.SignedBaseQty>0
                    &&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
                select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;
            var remaining=Math.Max(0,shipment.Sum(s=>s.Quantity)-received);
            if(shipment.Key.DestinationWarehouseId==policy.WarehouseId)incoming+=remaining;
            else outgoing+=remaining;
        }
        return(incoming,outgoing);
    }

    private async Task ExpiryWarnings(long? branchId,CancellationToken ct)
    {
        var today=DateOnly.FromDateTime(Now.AddHours(7));
        var batches=await (from b in db.StockBatches.AsNoTracking() join i in db.InventoryItems on b.ItemId equals i.ItemId
            where (branchId==null||b.BranchId==branchId)&&i.IsActive&&b.ExpiresOn!=null
                &&(b.WarehouseQuantity>0||db.StaffBatchBalances.Any(h=>h.BatchId==b.BatchId&&h.Quantity>0))
            select new{b.BatchId,b.BranchId,b.LotNumber,b.ExpiresOn,i.Name}).ToArrayAsync(ct);
        var live=new HashSet<Guid>();
        var recipients=new Dictionary<long,long[]>();
        foreach(var batch in batches){
            if(batch.ExpiresOn>today.AddDays(ExpiryWarningPolicy.Days(configuration,batch.BranchId)))continue;
            var expired=batch.ExpiresOn<today;
            var key=Key($"expiry:{batch.BatchId}:{batch.ExpiresOn:yyyy-MM-dd}:{expired}");live.Add(key);
            if(!recipients.TryGetValue(batch.BranchId,out var users))recipients[batch.BranchId]=users=await Recipients(batch.BranchId,"STOREKEEPER","INVENTORY_VIEW",ct);
            var text=$"{batch.Name} · lô {batch.LotNumber} · {(expired?"Đã hết hạn":"Sắp hết hạn")} {batch.ExpiresOn:dd/MM/yyyy}.";
            foreach(var user in users)await Add(batch.BranchId,user,"EXPIRY_WARNING",text,null,key,ct);
        }
        var previous=await db.Notifications.Where(n=>n.NotificationType=="EXPIRY_WARNING"&&n.ExpiresAt==null&&(branchId==null||n.BranchId==branchId)).ToArrayAsync(ct);
        foreach(var notification in previous)if(!live.Contains(notification.EventKey))notification.ExpiresAt=Now;
    }

    internal async Task SupplyPosted(StaffSupplyAllocation allocation,InventoryDocument document,string purpose,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"INVENTORY_TRANSACTION_REQUIRED");
        var name=await db.Users.AsNoTracking().Where(u=>u.UserId==allocation.UserId).Select(u=>u.FullName).SingleAsync(ct);
        var text=purpose=="RETURN"?$"Đã hoàn trả vật tư cuối ca của {name} về kho.":purpose=="INITIAL"?$"Đã cấp vật tư đầu ca cho {name}.":$"Đã cấp bổ sung vật tư cho {name}.";
        var key=Key($"supply-posted:{document.DocumentId}");
        foreach(var recipient in await Recipients(allocation.BranchId,"STOREKEEPER","INVENTORY_VIEW",ct))
            await Add(allocation.BranchId,recipient,"SUPPLY_POSTED",text,document.DocumentId,key,ct);
        await Reconcile(ct,allocation.BranchId);
    }

    // Call once the SHORT allocation and its snapshot balances have been saved, in Start's transaction.
    internal async Task InitialShortage(StaffSupplyAllocation allocation,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"INVENTORY_TRANSACTION_REQUIRED");
        var balances=await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
        var missing=new List<string>();
        foreach(var balance in balances){
            var stock=await StockBatchAccounting.Available(db,balance.WarehouseId,balance.ItemId,Now,ct);
            if(stock>=balance.InitialTarget)continue;
            var name=await db.InventoryItems.AsNoTracking().Where(i=>i.ItemId==balance.ItemId).Select(i=>i.Name).SingleAsync(ct);
            missing.Add($"{name}: thiếu {Qty(balance.InitialTarget-stock)}");
        }
        var reusable=await db.SupplyConfigurations.AsNoTracking().Where(c=>c.BranchId==allocation.BranchId&&c.IsCurrent&&c.IsActive&&c.InitialQuantity>0&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==c.BranchId&&p.ItemId==c.ItemId&&p.Classification=="REUSABLE")).ToArrayAsync(ct);
        foreach(var c in reusable){
            var ready=await db.ReusablePositions.Where(p=>p.BranchId==allocation.BranchId&&p.ItemId==c.ItemId&&p.WarehouseId==c.WarehouseId&&p.Status=="READY").SumAsync(p=>(decimal?)p.Quantity,ct)??0;
            if(ready<c.InitialQuantity){var name=await db.InventoryItems.Where(i=>i.ItemId==c.ItemId).Select(i=>i.Name).SingleAsync(ct);missing.Add($"{name}: thiếu {Qty(c.InitialQuantity-ready)} vật tư sạch sẵn sàng");}
        }
        var text=missing.Count>0?$"Chưa cấp vật tư đầu ca. {string.Join("; ",missing)}.":"Chưa cấp vật tư đầu ca: cần kiểm tra cấu hình vật tư hoặc kho đang ngừng hoạt động.";
        var recipients=(await Recipients(allocation.BranchId,"STOREKEEPER","INVENTORY_VIEW",ct))
            .Concat(await Recipients(allocation.BranchId,"HOUSEKEEPING_SUPERVISOR","STAFF_SUPPLY_VIEW",ct)).Append(allocation.UserId).Distinct();
        foreach(var recipient in recipients)await Add(allocation.BranchId,recipient,"SUPPLY_INITIAL_SHORT",text,null,Key($"supply-short:{allocation.AllocationId}"),ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<long[]> Recipients(long branch,string roleCode,string permission,CancellationToken ct)
    {
        var now=Now;
        return await (from ur in db.UserRoles.AsNoTracking() join role in db.Roles on ur.RoleId equals role.RoleId
            join rp in db.RolePermissions on role.RoleId equals rp.RoleId join p in db.Permissions on rp.PermissionId equals p.PermissionId
            join user in db.Users on ur.UserId equals user.UserId
            where role.Code==roleCode&&role.IsActive&&p.Code==permission&&p.IsActive&&rp.DataScope!="OWN"
                &&ur.BranchId==branch&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)
                &&user.AccountStatus=="ACTIVE"&&user.EmploymentStatus=="ACTIVE"&&user.DeletedAt==null
                &&db.Branches.Any(b=>b.BranchId==branch&&b.IsActive)
                &&db.Memberships.Any(m=>m.UserId==user.UserId&&m.BranchId==branch&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select user.UserId).Distinct().ToArrayAsync(ct);
    }

    private async Task Add(long branch,long recipient,string type,string text,long? document,Guid key,CancellationToken ct)
    {
        var existing=await db.Notifications.SingleOrDefaultAsync(n=>n.RecipientUserId==recipient&&n.EventKey==key,ct);
        if(existing!=null){
            if(type=="LOW_STOCK")LowStockAlertText.Refresh(existing,text);
            // A returning expiry condition is actionable again. Reuse the unique alert identity
            // rather than duplicate it, and surface it as unread after the previous episode closed.
            if(type=="EXPIRY_WARNING"&&existing.ExpiresAt!=null){
                existing.ExpiresAt=null;existing.ReadAt=null;existing.Message=text;
            }
            return;
        }
        notifications.Add(branch,recipient,type,text.Length>240?text[..237]+"…":text,null,document,key);
        var pending=db.Notifications.Local.FirstOrDefault(n=>n.RecipientUserId==recipient&&n.EventKey==key);
        if(pending!=null)pending.Message=text;
    }
}
