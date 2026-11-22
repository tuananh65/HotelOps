using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class CatalogService(HotelOpsCompactDbContext db,Access access,Atomic atomic,Audit audit)
{
    public sealed record Spec(Type Type,string Read,string Write,bool Scoped,string[] Fields);
    private static Spec S<T>(string read,string write,bool scoped,string fields)=>new(typeof(T),read,write,scoped,fields.Split(','));
    public static readonly IReadOnlyDictionary<string,Spec> Resources=new Dictionary<string,Spec>{
        ["branches"]=S<Branch>("ROOM_VIEW","MASTER_DATA_MANAGE",false,"Code,Name,Address,Phone,Timezone,IsActive"),
        ["departments"]=S<Department>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"Code,Name,ManagerUserId,IsActive"),
        ["locations"]=S<Location>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"ParentLocationId,Kind,Code,Name,FloorNumber,IsActive"),
        ["room-types"]=S<RoomType>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"Code,Name,Capacity,Description,IsActive"),
        ["rooms"]=S<Room>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"LocationId,RoomTypeId,DepartmentId,Code,IsActive"),
        ["warehouses"]=S<Warehouse>("INVENTORY_VIEW","ITEM_MASTER_MANAGE",true,"Code,Name,LocationId,DepartmentId,IsActive"),
        ["items"]=S<InventoryItem>("INVENTORY_VIEW","ITEM_MASTER_MANAGE",false,"Code,Name,CategoryId,BaseUnitId,Description,IsActive"),
        ["stock-policies"]=S<InventoryPolicy>("INVENTORY_VIEW","ITEM_MASTER_MANAGE",true,"WarehouseId,ItemId,MinimumQty,TargetQty"),
        ["references"]=S<ReferenceValue>("ROOM_VIEW","MASTER_DATA_MANAGE",false,"Kind,Code,Name,ParentId,BaseUnitId,ConversionFactor,SettingsJson,SortOrder,IsActive"),
        ["assets"]=S<Asset>("ASSET_VIEW","ASSET_MANAGE",true,"AssetCode,Name,AssetTypeId,RoomId,LocationId,DepartmentId,SerialNumber,ConditionCode,PurchasedOn,WarrantyUntil"),
        ["roles"]=S<Role>("ROLE_MANAGE","ROLE_MANAGE",false,"Code,Name,Description"),
        ["permissions"]=S<Permission>("ROLE_MANAGE","ROLE_MANAGE",false,"")};
    public Task<object> List(string resource,long? branchId,int page,int size,CancellationToken ct)=>Invoke(resource,"ListCore",branchId,page,size,ct);
    public Task<object> Save(string resource,long? id,long? branchId,CatalogWriteRequest request,CancellationToken ct)=>Invoke(resource,"SaveCore",id,branchId,request,ct);
    private Task<object> Invoke(string resource,string method,params object?[] args)
    {
        if(!Resources.TryGetValue(resource,out var spec))throw new ApiException(404,"UNKNOWN_CATALOG");
        return (Task<object>)GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(spec.Type)
            .Invoke(this,[spec,..args])!;
    }
    private async Task<object> ListCore<T>(Spec spec,long? branchId,int page,int size,CancellationToken ct) where T:class
    {
        if(spec.Scoped && branchId==null)throw new ApiException(400,"BRANCH_REQUIRED");
        if(!await access.Allows(spec.Read,branchId,ct:ct))await access.Require(spec.Write,spec.Scoped?branchId:null,ct:ct);
        page=Math.Max(1,page);size=Math.Clamp(size,1,100);
        var query=db.Set<T>().AsNoTracking();if(spec.Scoped)query=query.Where(x=>EF.Property<long>(x,"BranchId")==branchId);
        if(typeof(T)==typeof(Branch)&&!await access.Allows("MASTER_DATA_MANAGE",ct:ct))query=query.Where(x=>EF.Property<long>(x,"BranchId")==branchId);
        var key=db.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties.Single().Name;
        var total=await query.CountAsync(ct);var rows=await query.OrderBy(x=>EF.Property<long>(x,key)).Skip((page-1)*size).Take(size).ToArrayAsync(ct);
        return new PageDto<ResourceDto>(rows.Select(Output.Record).ToArray(),total,page,size);
    }
    private Task<object> SaveCore<T>(Spec spec,long? id,long? branchId,CatalogWriteRequest request,CancellationToken ct) where T:class,new()
        =>atomic.Run(async()=>{
            if(spec.Scoped && branchId==null)throw new ApiException(400,"BRANCH_REQUIRED");
            // Approved V10 exception: inventory items are shared and editable by a
            // storekeeper with ITEM_MASTER_MANAGE in the explicitly selected branch.
            // Other global catalogs still require a global grant.
            await access.Require(spec.Write,spec.Scoped||typeof(T)==typeof(InventoryItem)?branchId:null,ct:ct);
            Output.Require(spec.Fields.Length>0 && spec.Fields[0]!="","READ_ONLY_CATALOG",405);
            Output.Require(request.Fields.ValueKind==JsonValueKind.Object,"FIELDS_OBJECT_REQUIRED",400);
            var row=id==null?new T():await db.Set<T>().FindAsync([id.Value],ct)??throw new ApiException(404,"NOT_FOUND");
            // Operational levels are seeded in SQL and cannot be edited through catalog APIs.
            Output.Require(row is not ReferenceValue fixedRow || !FixedLevel(fixedRow.Kind),"FIXED_OPERATIONAL_REFERENCE",405);
            if(id==null)typeof(T).GetProperty("IsActive")?.SetValue(row,true);
            var before=id==null?null:Output.Record(row);
            if(spec.Scoped){var branch=typeof(T).GetProperty("BranchId")!;
                if(id!=null)Output.Require((long)branch.GetValue(row)! == branchId,"BRANCH_MISMATCH",404);
                else branch.SetValue(row,branchId);}
            if(id!=null)Output.Version((byte[])typeof(T).GetProperty("RowVersion")!.GetValue(row)!,request.RowVersion??"");
            var entry=db.Entry(row);
            foreach(var field in request.Fields.EnumerateObject()){
                var property=typeof(T).GetProperties().SingleOrDefault(p=>string.Equals(p.Name,field.Name,StringComparison.OrdinalIgnoreCase));
                Output.Require(property!=null && spec.Fields.Contains(property.Name),"FIELD_NOT_WRITABLE",400);
                object? value;
                try{value=JsonSerializer.Deserialize(field.Value.GetRawText(),property!.PropertyType);}
                catch(JsonException){throw new ApiException(400,"INVALID_FIELD_VALUE");}
                var metadata=entry.Metadata.FindProperty(property!.Name)!;
                Output.Require(value!=null || metadata.IsNullable,"REQUIRED_FIELD",400);
                if(value is string text)Output.Require(metadata.GetMaxLength()==null || text.Length<=metadata.GetMaxLength(),"FIELD_TOO_LONG",400);
                property.SetValue(row,value);
            }
            db.ChangeTracker.DetectChanges();
            if(row is InventoryItem inventoryItem){
                Output.Require(!string.IsNullOrWhiteSpace(inventoryItem.Code)&&!string.IsNullOrWhiteSpace(inventoryItem.Name),"ITEM_CODE_AND_NAME_REQUIRED",400);
                Output.Require(await db.ReferenceValues.AnyAsync(r=>r.ReferenceId==inventoryItem.BaseUnitId&&r.Kind=="UOM"&&r.IsActive&&r.BaseUnitId==null,ct),"BASE_UNIT_REQUIRED",400);
                if(id!=null&&entry.Property(nameof(InventoryItem.BaseUnitId)).IsModified)
                    Output.Require(!await db.InventoryDocumentLines.AnyAsync(l=>l.ItemId==inventoryItem.ItemId,ct),"ITEM_UNIT_HAS_TRANSACTION_HISTORY");
            }
            Output.Require(row is not ReferenceValue requestedRow || !FixedLevel(requestedRow.Kind),"FIXED_OPERATIONAL_REFERENCE",405);
            if(row is Branch hotel){
                Output.Require(!string.IsNullOrWhiteSpace(hotel.Code)&&!string.IsNullOrWhiteSpace(hotel.Name),"BRANCH_CODE_AND_NAME_REQUIRED",400);
                hotel.Timezone??="Asia/Ho_Chi_Minh";
                try{TimeZoneInfo.FindSystemTimeZoneById(hotel.Timezone);}
                catch(TimeZoneNotFoundException){throw new ApiException(400,"INVALID_TIMEZONE");}
                catch(InvalidTimeZoneException){throw new ApiException(400,"INVALID_TIMEZONE");}
                if(id!=null&&entry.Property(nameof(Branch.Timezone)).IsModified)
                    Output.Require(!await db.WorkdayAssignments.AnyAsync(a=>(a.Status=="PLANNED"||a.Status=="WORKING")&&db.WorkdayShifts.Any(s=>s.ShiftId==a.ShiftId&&s.BranchId==hotel.BranchId),ct),"BRANCH_HAS_WORKDAY_RESPONSIBILITIES");
            }
            if(row is Role role && id!=null)Output.Require(!entry.Property(nameof(Role.Code)).IsModified,"ROLE_CODE_IMMUTABLE");
            if(row is ReferenceValue existing && id!=null)Output.Require(!entry.Property(nameof(ReferenceValue.Kind)).IsModified,"REFERENCE_KIND_IMMUTABLE");
            if(id!=null && entry.Metadata.FindProperty("IsActive")!=null && entry.Property("IsActive").IsModified && Equals(entry.Property("IsActive").CurrentValue,false))
                await new DependencyGuard(db).BeforeDeactivate(row,ct);
            if(row is Room room && id==null)room.OperationalStatus="DIRTY";
            if(row is Asset asset && id!=null){
                Output.Require(!entry.Property(nameof(Asset.RoomId)).IsModified && !entry.Property(nameof(Asset.LocationId)).IsModified
                    && !entry.Property(nameof(Asset.ConditionCode)).IsModified,"USE_ASSET_ACTION_ENDPOINT");}
            if(row is ReferenceValue reference && id!=null && (entry.Property(nameof(ReferenceValue.Kind)).IsModified || entry.Property(nameof(ReferenceValue.ConversionFactor)).IsModified))
                Output.Require(!await db.InventoryDocumentLines.AnyAsync(l=>l.UnitId==reference.ReferenceId,ct),"UNIT_HAS_TRANSACTION_HISTORY");
            if(id==null)db.Set<T>().Add(row);
            await db.SaveChangesAsync(ct);
            var key=Convert.ToInt64(entry.Property(entry.Metadata.FindPrimaryKey()!.Properties.Single().Name).CurrentValue);
            audit.Add(typeof(T).Name,key,id==null?"CREATE":"UPDATE",spec.Scoped?branchId:null,before,Output.Record(row));
            return (object)Output.Record(row);
        },ct);
    private static bool FixedLevel(string? kind)=>string.Equals(kind?.Trim(),"PRIORITY",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(kind?.Trim(),"SEVERITY",StringComparison.OrdinalIgnoreCase);
}
