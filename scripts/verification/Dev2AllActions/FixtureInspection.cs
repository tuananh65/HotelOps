using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

internal sealed partial class Fixtures
{
    // Fixed, fixture-owned feature graph. EF supplies quoted table/column names; predicates use parameters.
    private Dictionary<Type,IQueryable> Graph(HotelOpsDbContext db)
    {
        var branches=new[]{Branch.BranchId,ForeignBranch.BranchId};var users=UserIds;
        var assets=db.Assets.Where(x=>branches.Contains(x.BranchId));var incidents=db.Incidents.Where(x=>branches.Contains(x.BranchId));
        var works=db.MaintenanceWorks.Where(x=>x.IncidentId!=null&&incidents.Select(i=>i.IncidentId).Contains(x.IncidentId.Value));
        var requests=db.MaterialRequests.Where(x=>branches.Contains(x.BranchId));var repl=db.MaterialReplenishments.Where(x=>x.WarehouseId==Warehouse.WarehouseId);
        var docs=db.StockDocuments.Where(x=>users.Contains(x.CreatedBy));
        IQueryable[] queries=[assets,db.AssetHandovers.Where(x=>assets.Select(a=>a.AssetId).Contains(x.AssetId)),db.AssetConditionHistories.Where(x=>assets.Select(a=>a.AssetId).Contains(x.AssetId)),db.AssetLocationHistories.Where(x=>assets.Select(a=>a.AssetId).Contains(x.AssetId)),incidents,db.IncidentEvents.Where(x=>incidents.Select(a=>a.IncidentId).Contains(x.IncidentId)),works,db.MaintenanceWorkLogs.Where(x=>works.Select(a=>a.MaintenanceWorkId).Contains(x.MaintenanceWorkId)),db.MaintenanceMaterialUsages.Where(x=>works.Select(a=>a.MaintenanceWorkId).Contains(x.MaintenanceWorkId)),requests,db.MaterialRequestLines.Where(x=>requests.Select(a=>a.RequestId).Contains(x.RequestId)),db.MaterialRequestApprovalCycles.Where(x=>requests.Select(a=>a.RequestId).Contains(x.RequestId)),db.MaterialRequestApprovalLineSnapshots.Where(x=>requests.Select(a=>a.RequestId).Contains(x.ApprovalCycle.RequestId)),db.MaterialRequestApprovals.Where(x=>requests.Select(a=>a.RequestId).Contains(x.RequestId)),db.MaterialFulfillments.Where(x=>requests.Select(a=>a.RequestId).Contains(x.RequestId)),db.MaterialFulfillmentLines.Where(x=>requests.Select(a=>a.RequestId).Contains(x.Fulfillment.RequestId)),docs,db.StockDocumentLines.Where(x=>docs.Select(d=>d.DocumentId).Contains(x.DocumentId)),db.StockMovements.Where(x=>x.WarehouseId==Warehouse.WarehouseId),db.StockReceiptLineDetails.Where(x=>docs.Select(d=>d.DocumentId).Contains(x.StockDocumentLine.DocumentId)),repl,db.MaterialReplenishmentLines.Where(x=>repl.Select(a=>a.ReplenishmentId).Contains(x.ReplenishmentId)),db.MaterialReplenishmentReceiptLines.Where(x=>repl.Select(a=>a.ReplenishmentId).Contains(x.ReplenishmentLine.ReplenishmentId)),db.AuditLogs.Where(x=>x.BranchId!=null&&branches.Contains(x.BranchId.Value))];
        return queries.ToDictionary(x=>x.ElementType,x=>x);
    }

    internal async Task<string> Snapshot()
    {
        await using var db=open();var rows=new List<string>();
        foreach(var entry in Graph(db).OrderBy(x=>x.Key.Name))
        {
            // Materialize only scalar mapped values; navigation cycles and auth session timestamps are excluded.
            var entity=db.Model.FindEntityType(entry.Key)!;var scalars=entity.GetProperties().OrderBy(x=>x.Name).ToArray();
            var values=new List<string>();
            var materialized=await (Task<System.Collections.IEnumerable>)typeof(Fixtures).GetMethod(nameof(Materialize),System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.MakeGenericMethod(entry.Key).Invoke(null,[entry.Value])!;
            foreach(var row in materialized)
                values.Add(System.Text.Json.JsonSerializer.Serialize(scalars.Select(p=>p.PropertyInfo?.GetValue(row)).ToArray()));
            rows.Add(entry.Key.Name+string.Join("\n",values.Order(StringComparer.Ordinal)));
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",rows))));
    }

    internal async Task Cleanup()
    {
        await using var db=open();await HostAcceptance.VerifyIdentity(db);await using var tx=await db.Database.BeginTransactionAsync();
        var graph=Graph(db);
        Type[] order=[typeof(HotelOps.Api.Entities.MaterialReplenishmentReceiptLine),typeof(HotelOps.Api.Entities.MaterialReplenishmentLine),typeof(HotelOps.Api.Entities.MaterialReplenishment),typeof(HotelOps.Api.Entities.MaintenanceMaterialUsage),typeof(HotelOps.Api.Entities.MaintenanceWorkLog),typeof(HotelOps.Api.Entities.MaintenanceWork),typeof(HotelOps.Api.Entities.IncidentEvent),typeof(HotelOps.Api.Entities.MaterialFulfillmentLine),typeof(HotelOps.Api.Entities.MaterialFulfillment),typeof(HotelOps.Api.Entities.StockReceiptLineDetail),typeof(HotelOps.Api.Entities.StockMovement),typeof(HotelOps.Api.Entities.StockDocumentLine),typeof(HotelOps.Api.Entities.StockDocument),typeof(HotelOps.Api.Entities.MaterialRequestApproval),typeof(HotelOps.Api.Entities.MaterialRequestApprovalLineSnapshot),typeof(HotelOps.Api.Entities.MaterialRequestApprovalCycle),typeof(HotelOps.Api.Entities.MaterialRequestLine),typeof(HotelOps.Api.Entities.MaterialRequest),typeof(HotelOps.Api.Entities.Incident),typeof(HotelOps.Api.Entities.AssetHandover),typeof(HotelOps.Api.Entities.AssetConditionHistory),typeof(HotelOps.Api.Entities.AssetLocationHistory),typeof(HotelOps.Api.Entities.Asset),typeof(HotelOps.Api.Entities.AuditLog)];
        // EF's generic ExecuteDelete is used so model/schema identifiers cannot drift from the query.
        foreach(var type in order)
            await (Task<int>)typeof(Fixtures).GetMethod(nameof(Delete),System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.MakeGenericMethod(type).Invoke(null,[graph[type]])!;
        var users=UserIds;var roles=new[]{MainRole.RoleId,OwnRole.RoleId};var branches=new[]{Branch.BranchId,ForeignBranch.BranchId};
        await db.StockThresholds.Where(x=>x.WarehouseId==Warehouse.WarehouseId).ExecuteDeleteAsync();
        await db.InventoryItems.Where(x=>x.ItemCode.StartsWith(Marker)).ExecuteDeleteAsync();
        await db.AuditLogs.Where(x=>x.ActorUserId!=null&&users.Contains(x.ActorUserId.Value)).ExecuteDeleteAsync();
        // Auth audits intentionally have null ActorUserId; their exact users EntityId is fixture-owned.
        var userKeys=users.Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if(await db.AuditLogs.AnyAsync(x=>x.EntityType=="users"&&userKeys.Contains(x.EntityId)&&x.ActorUserId!=null&&!users.Contains(x.ActorUserId.Value)))throw new AcceptanceFailure("FIXTURE_AUTH_AUDIT_EXTERNAL_ACTOR");
        await db.AuditLogs.Where(x=>x.EntityType=="users"&&userKeys.Contains(x.EntityId)).ExecuteDeleteAsync();
        await db.UserSessions.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();await db.UserRoles.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();await db.UserBranchAccesses.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();await db.Users.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.RolePermissions.Where(x=>roles.Contains(x.RoleId)).ExecuteDeleteAsync();await db.Roles.Where(x=>roles.Contains(x.RoleId)).ExecuteDeleteAsync();
        await db.Permissions.Where(x=>NewPermissionIds.Contains(x.PermissionId)).ExecuteDeleteAsync();
        await db.Warehouses.Where(x=>x.WarehouseId==Warehouse.WarehouseId).ExecuteDeleteAsync();await db.Areas.Where(x=>branches.Contains(x.BranchId)).ExecuteDeleteAsync();await db.Departments.Where(x=>branches.Contains(x.BranchId)).ExecuteDeleteAsync();
        await db.AssetTypes.Where(x=>x.AssetTypeId==AssetType.AssetTypeId).ExecuteDeleteAsync();await db.ItemCategories.Where(x=>x.ItemCategoryId==Category.ItemCategoryId).ExecuteDeleteAsync();await db.UnitsOfMeasures.Where(x=>x.UomId==Uom.UomId).ExecuteDeleteAsync();
        await references.OwnedQuery(db).ExecuteDeleteAsync();await db.Branches.Where(x=>branches.Contains(x.BranchId)).ExecuteDeleteAsync();
        await tx.CommitAsync();
        if(await db.Branches.AnyAsync(x=>branches.Contains(x.BranchId))||await db.Users.AnyAsync(x=>users.Contains(x.UserId))||await db.RolePermissions.AnyAsync(x=>roles.Contains(x.RoleId)))throw new InvalidOperationException("Fixture cleanup verification failed");
    }
    private static Task<int> Delete<T>(IQueryable query) where T:class=>((IQueryable<T>)query).ExecuteDeleteAsync();
    private static async Task<System.Collections.IEnumerable> Materialize<T>(IQueryable query) where T:class=>await ((IQueryable<T>)query).AsNoTracking().ToListAsync();
}
