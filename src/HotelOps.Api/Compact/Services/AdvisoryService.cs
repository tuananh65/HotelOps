using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;

public sealed class AdvisoryService(HotelOpsCompactDbContext db,Access access,IAdvisoryProvider provider,TimeProvider clock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    public async Task<AdvisoryResult> Analyze(AdvisoryRequest request,CancellationToken ct)
    {
        Output.Require(request.Days is >=1 and <=90,"ADVISORY_HORIZON_INVALID",400);
        object evidence;string explanation;
        switch(request.Kind){
            case "DEMAND":
                await access.Require("INVENTORY_VIEW",request.BranchId,ct:ct);
                evidence=await Forecast(request.BranchId,request.Days,ct);
                explanation="Ước tính thống kê từ tiêu hao 30 ngày và xu hướng 7 ngày, có đối chiếu cấp/hoàn trả. Cột phòng cần dọn là kịch bản tối đa theo sức chứa, chưa phải lượng thiếu thực tế. Lô chưa xác minh hoặc hết hạn không được tính vào khả dụng. Không tự cấp theo định mức hoặc tự đặt hàng.";
                break;
            case "TRANSFER":
                await access.RequireRolePermission("OPERATION_MANAGER","TRANSFER_REQUEST_CREATE",request.BranchId,ct);
                evidence=await Transfers(request.BranchId,request.Days,ct);
                explanation="Đề xuất theo quy tắc: chỉ chuyển phần dư sau mức tối thiểu, nhu cầu thống kê và yêu cầu điều chuyển đang mở. Không tự tạo phiếu hoặc phê duyệt. Nguồn chỉ gồm lô đã xác minh còn hạn qua kỳ dự báo.";
                break;
            default:throw new ApiException(400,"ADVISORY_KIND_INVALID");
        }
        var advice=await provider.Explain(request.Kind,evidence,ct);
        return new(advice.Status=="AVAILABLE"?"AI_ASSISTED":"STATISTICAL_RULES",advice.Status,advice.Provider,advice.Model,Now,evidence,advice.Text??explanation);
    }

    private async Task<DemandEstimate[]> Forecast(long branch,int days,CancellationToken ct)
    {
        var now=Now;var since=now.AddDays(-30);var today=DateOnly.FromDateTime(now.AddHours(7));var until=today.AddDays(days);
        var configurations=await (from p in db.ItemLifecycleProfiles.AsNoTracking() join i in db.InventoryItems on p.ItemId equals i.ItemId
            join u in db.ReferenceValues on i.BaseUnitId equals u.ReferenceId
            where p.BranchId==branch&&p.Classification=="CONSUMABLE"&&i.IsActive select new{i.ItemId,i.Name,unit=u.Name}).ToArrayAsync(ct);
        var warehouses=await db.Warehouses.AsNoTracking().Where(w=>w.BranchId==branch&&w.IsActive).Select(w=>w.WarehouseId).ToArrayAsync(ct);
        var rooms=await(from r in db.Rooms.AsNoTracking() join t in db.RoomTypes on r.RoomTypeId equals t.RoomTypeId
            join readiness in db.VRoomReadinesses on r.RoomId equals readiness.RoomId
            where r.BranchId==branch&&r.IsActive&&r.OperationalStatus!="OCCUPIED"&&readiness.Readiness=="PENDING_CLEANING"
            select new{r.RoomId,t.Capacity}).ToArrayAsync(ct);
        var capacity=rooms.Sum(r=>r.Capacity);
        var result=new List<DemandEstimate>();
        foreach(var warehouse in warehouses)foreach(var item in configurations){
            var usage=from l in db.StaffSupplyLedgers.AsNoTracking() join b in db.StaffSupplyBalances on l.BalanceId equals b.BalanceId
                join a in db.StaffSupplyAllocations on b.AllocationId equals a.AllocationId
                where a.BranchId==branch&&b.WarehouseId==warehouse&&b.ItemId==item.ItemId&&l.Kind=="USE"&&l.OccurredAt>=since select l;
            var used=await usage.SumAsync(l=>(decimal?)l.Quantity,ct)??0;
            var observed=await usage.Select(l=>l.OccurredAt.Date).Distinct().CountAsync(ct);
            var recentSince=now.AddDays(-7);var previousSince=now.AddDays(-14);
            var recent=await usage.Where(l=>l.OccurredAt>=recentSince).SumAsync(l=>(decimal?)l.Quantity,ct)??0;
            var previous=await usage.Where(l=>l.OccurredAt>=previousSince&&l.OccurredAt<recentSince).SumAsync(l=>(decimal?)l.Quantity,ct)??0;
            var movements=from m in db.StockMovements.AsNoTracking() join l in db.InventoryDocumentLines on m.LineId equals l.LineId
                join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
                where m.WarehouseId==warehouse&&m.ItemId==item.ItemId&&m.OccurredAt>=since&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
                select new{m.SignedBaseQty,d.Kind};
            var issued=-(await movements.Where(m=>m.Kind=="ISSUE"||m.Kind=="SUPPLY_ISSUE").SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0);
            var returned=await movements.Where(m=>m.Kind=="RETURN"||m.Kind=="SUPPLY_RETURN").SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
            var norm=await db.SupplyConfigurations.AsNoTracking().Where(c=>c.BranchId==branch&&c.WarehouseId==warehouse&&c.ItemId==item.ItemId&&c.IsCurrent&&c.IsActive).Select(c=>(decimal?)c.PerGuestQuantity).SingleOrDefaultAsync(ct)??0;
            var available=await StockBatchAccounting.Available(db,warehouse,item.ItemId,now,ct);
            var minimum=await db.InventoryPolicies.AsNoTracking().Where(p=>p.BranchId==branch&&p.WarehouseId==warehouse&&p.ItemId==item.ItemId).Select(p=>(decimal?)p.MinimumQty).SingleOrDefaultAsync(ct)??0;
            var expiring=await db.StockBatches.AsNoTracking().Where(b=>b.WarehouseId==warehouse&&b.ItemId==item.ItemId&&b.Status=="READY"&&b.ExpiresOn>=today&&b.ExpiresOn<=until)
                .SumAsync(b=>(decimal?)(b.WarehouseQuantity-b.ReservedQuantity),ct)??0;
            expiring=Math.Min(available,expiring);
            var demand=Math.Max(AdvisoryMath.Forecast(used,30,days),AdvisoryMath.Forecast(recent,7,days));
            result.Add(new(branch,warehouse,item.ItemId,item.Name,item.unit,days,used,observed,available,minimum,demand,expiring,
                AdvisoryMath.Replenishment(demand,minimum,available,expiring),observed<7?"Dữ liệu hạn chế: dưới 7 ngày có tiêu hao; số 0 không chứng minh không có nhu cầu. Đối chiếu trần nhu cầu phòng trước khi quyết định.":"Lấy mức cao hơn giữa trung bình 30 và 7 ngày; trần nhu cầu phòng không thay thế kiểm đếm lượng còn dùng được.",
                issued,returned,recent/7,previous/7,rooms.Length,capacity,capacity*norm));
        }
        return result.ToArray();
    }

    private async Task<TransferSuggestion[]> Transfers(long destination,int days,CancellationToken ct)
    {
        // Same limited cross-branch stock projection authorized by TransferCatalog, never people/documents.
        var targets=await Forecast(destination,days,ct);var sources=await db.Branches.AsNoTracking().Where(b=>b.IsActive&&b.BranchId!=destination).Select(b=>b.BranchId).ToArrayAsync(ct);
        var rows=new List<TransferSuggestion>();var remaining=targets.ToDictionary(t=>(t.WarehouseId,t.ItemId),t=>t.SuggestedReplenishment);
        foreach(var target in targets){
            var incoming=await OutstandingTransfers(target.WarehouseId,target.ItemId,true,ct);
            remaining[(target.WarehouseId,target.ItemId)]=Math.Max(0,remaining[(target.WarehouseId,target.ItemId)]-incoming);
        }
        foreach(var source in sources)foreach(var supply in await Forecast(source,days,ct)){
            var pending=await OutstandingTransfers(supply.WarehouseId,supply.ItemId,false,ct)+await OutstandingIssues(supply.WarehouseId,supply.ItemId,ct);
            var surplus=AdvisoryMath.Transfer(decimal.MaxValue,supply.Available-supply.ExpiringBeforeHorizon,supply.Minimum,supply.ExpectedDemand,pending);
            foreach(var target in targets.Where(t=>t.ItemId==supply.ItemId)){
                var quantity=Math.Min(remaining[(target.WarehouseId,target.ItemId)],surplus);
                if(quantity<=0)continue;
                rows.Add(new(source,supply.WarehouseId,destination,target.WarehouseId,target.ItemId,target.Item,quantity,"Đã trừ cấp phát nội bộ đã duyệt, phần nguồn còn chờ xuất và phần nơi nhận đang chờ giao/đang vận chuyển; giữ mức tối thiểu và nhu cầu dự báo tại nguồn."));
                remaining[(target.WarehouseId,target.ItemId)]-=quantity;surplus-=quantity;
            }
        }
        return rows.ToArray();
    }
    private async Task<decimal> OutstandingIssues(long warehouse,long item,CancellationToken ct)
    {
        var lines=await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            where d.Kind=="MATERIAL_REQUEST"&&d.SourceWarehouseId==warehouse&&l.ItemId==item&&d.ClosedAt==null&&(d.Status=="APPROVED"||d.Status=="PARTIALLY_ISSUED")
            select new{l.LineId,quantity=(l.ApprovedQty??0)*l.ConversionFactor}).ToArrayAsync(ct);
        decimal total=0;
        foreach(var line in lines){
            var issued=await (from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
                join m in db.StockMovements on l.LineId equals m.LineId
                where d.Kind=="ISSUE"&&l.SourceLineId==line.LineId&&m.SignedBaseQty<0&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
                select (decimal?)-m.SignedBaseQty).SumAsync(ct)??0;
            total+=Math.Max(0,line.quantity-issued);
        }
        return total;
    }
    private async Task<decimal> OutstandingTransfers(long warehouse,long item,bool incoming,CancellationToken ct)
    {
        var lines=await (from d in db.InventoryDocuments.AsNoTracking() join l in db.InventoryDocumentLines on d.DocumentId equals l.DocumentId
            where d.Kind=="TRANSFER_REQUEST"&&l.ItemId==item&&d.ClosedAt==null&&(d.Status=="PENDING_APPROVAL"||d.Status=="APPROVED")
                &&(incoming?d.DestinationWarehouseId==warehouse:d.SourceWarehouseId==warehouse)
            select new{l.LineId,quantity=(l.ApprovedQty??l.ActualQty)*l.ConversionFactor}).ToArrayAsync(ct);
        decimal total=0;
        foreach(var line in lines){
            var shipments=await (from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
                join m in db.StockMovements on l.LineId equals m.LineId
                where d.Kind=="TRANSFER"&&l.SourceLineId==line.LineId&&m.SignedBaseQty<0&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
                select new{l.LineId,quantity=-m.SignedBaseQty}).ToArrayAsync(ct);
            var fulfilled=shipments.Sum(s=>s.quantity);
            if(incoming){var ids=shipments.Select(s=>s.LineId).ToArray();fulfilled=await(from l in db.InventoryDocumentLines.AsNoTracking() join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId
                join m in db.StockMovements on l.LineId equals m.LineId where d.Kind=="TRANSFER_RECEIPT"&&l.SourceLineId!=null&&ids.Contains(l.SourceLineId.Value)&&m.SignedBaseQty>0&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId)
                select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;}
            total+=Math.Max(0,line.quantity-fulfilled);
        }
        return total;
    }
}
