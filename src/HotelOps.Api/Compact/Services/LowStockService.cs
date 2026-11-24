using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// Runs inside the inventory/configuration transaction; notification failure rolls back the change.
public sealed class LowStockService(HotelOpsCompactDbContext db,NotificationService notifications,TimeProvider clock)
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
            var stock=await db.StockMovements.Where(m=>m.WarehouseId==policy.WarehouseId&&m.ItemId==policy.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
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
            var recipients=await Recipients(policy.BranchId,"STOREKEEPER","INVENTORY_VIEW",ct);
            var message=$"{item.Name}: còn {Qty(stock)} {unit}; mức tối thiểu {Qty(policy.MinimumQty)} {unit}; thiếu {Qty(policy.MinimumQty-stock)} {unit}.";
            // Query persisted recipients too: new storekeepers can receive the still-open episode
            // without sending a second notification to existing recipients.
            foreach(var recipient in recipients)await Add(policy.BranchId,recipient,"LOW_STOCK",message,null,policy.LowStockEpisode.Value,ct);
            if(recipients.Length>0)policy.AlertedAt??=Now;
        }
        await db.SaveChangesAsync(ct);
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
            var stock=await db.StockMovements.Where(m=>m.WarehouseId==balance.WarehouseId&&m.ItemId==balance.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
            if(stock>=balance.InitialTarget)continue;
            var name=await db.InventoryItems.AsNoTracking().Where(i=>i.ItemId==balance.ItemId).Select(i=>i.Name).SingleAsync(ct);
            missing.Add($"{name}: thiếu {Qty(balance.InitialTarget-stock)}");
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
        if(await db.Notifications.AnyAsync(n=>n.RecipientUserId==recipient&&n.EventKey==key,ct))return;
        notifications.Add(branch,recipient,type,text.Length>240?text[..237]+"…":text,null,document,key);
        var pending=db.Notifications.Local.FirstOrDefault(n=>n.RecipientUserId==recipient&&n.EventKey==key);
        if(pending!=null)pending.Message=text;
    }
}
