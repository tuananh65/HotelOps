using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class CatalogService(HotelOpsCompactDbContext db,Access access,Atomic atomic,Audit audit,LowStockService lowStock)
{
    public sealed record Spec(Type Type,string Read,string Write,bool Scoped,string[] Fields);
    private static Spec S<T>(string read,string write,bool scoped,string fields)=>new(typeof(T),read,write,scoped,fields.Split(','));
    public static readonly IReadOnlyDictionary<string,Spec> Resources=new Dictionary<string,Spec>{
        ["branches"]=S<Branch>("ROOM_VIEW","MASTER_DATA_MANAGE",false,"Code,Name,Address,Phone,Timezone,IsActive"),
        ["departments"]=S<Department>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"Code,Name,Description,ManagerUserId,IsActive"),
        ["locations"]=S<Location>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"ParentLocationId,Kind,Code,Name,FloorNumber,IsActive"),
        ["room-types"]=S<RoomType>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"Code,Name,Capacity,Description,IsActive"),
        ["rooms"]=S<Room>("ROOM_VIEW","MASTER_DATA_MANAGE",true,"LocationId,RoomTypeId,DepartmentId,Code,IsActive"),
        ["warehouses"]=S<Warehouse>("INVENTORY_VIEW","ITEM_MASTER_MANAGE",true,"Code,Name,LocationId,DepartmentId,IsActive"),
        ["items"]=S<InventoryItem>("INVENTORY_VIEW","ITEM_MASTER_MANAGE",false,"Code,Name,CategoryId,BaseUnitId,Description,IsActive"),
        ["stock-policies"]=S<InventoryPolicy>("INVENTORY_VIEW","ITEM_MASTER_MANAGE",true,"WarehouseId,ItemId,MinimumQty,TargetQty"),
        ["references"]=S<ReferenceValue>("ROOM_VIEW","MASTER_DATA_MANAGE",false,"Kind,Code,Name,ParentId,BaseUnitId,ConversionFactor,SettingsJson,SortOrder,IsActive"),
        ["assets"]=S<Asset>("ASSET_VIEW","ASSET_MANAGE",true,"AssetCode,Name,AssetTypeId,RoomId,LocationId,DepartmentId,SerialNumber,ConditionCode,PurchasedOn,WarrantyUntil"),
        ["roles"]=S<Role>("ROLE_MANAGE","ROLE_MANAGE",false,"Code,Name,Description"),
        ["permissions"]=S<Permission>("ROLE_MANAGE","ROLE_MANAGE",false,"IsActive")};
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
        if(typeof(T)==typeof(ReferenceValue))query=query.Where(x=>EF.Property<string>(x,"Kind")!="BRANCH_CONTACT");
        if(typeof(T)==typeof(Branch)&&!await access.Allows("MASTER_DATA_MANAGE",ct:ct))query=query.Where(x=>EF.Property<long>(x,"BranchId")==branchId);
        var key=db.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties.Single().Name;
        var total=await query.CountAsync(ct);var rows=await query.OrderBy(x=>EF.Property<long>(x,key)).Skip((page-1)*size).Take(size).ToArrayAsync(ct);
        var records=rows.Select(Output.Record).ToArray();
        if(typeof(T)==typeof(Branch)){
            var codes=rows.Cast<Branch>().Select(b=>$"BRANCH-{b.BranchId}").ToArray();
            var contacts=await db.ReferenceValues.AsNoTracking().Where(v=>v.Kind=="BRANCH_CONTACT"&&codes.Contains(v.Code)).ToDictionaryAsync(v=>v.Code,ct);
            records=rows.Cast<Branch>().Select(b=>BranchOutput(b,contacts.GetValueOrDefault($"BRANCH-{b.BranchId}"))).ToArray();
        }
        return new PageDto<ResourceDto>(records,total,page,size);
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
            Output.Require(typeof(T)!=typeof(Permission)||id!=null,"READ_ONLY_CATALOG",405);
            var row=id==null?new T():await db.Set<T>().FindAsync([id.Value],ct)??throw new ApiException(404,"NOT_FOUND");
            Output.Require(row is not ReferenceValue contactRow||contactRow.Kind!="BRANCH_CONTACT","USE_BRANCH_CONTACT_FORM",405);
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
                if(typeof(T)==typeof(Branch)&&field.Name.Equals("representativeName",StringComparison.OrdinalIgnoreCase))continue;
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
            // Codes are generated server-side when omitted by a creation form.
            // Serializable transaction and unique indexes protect concurrent requests.
            var codeProperty=typeof(T).GetProperty(typeof(T)==typeof(Asset)?"AssetCode":"Code");
            if(id==null&&codeProperty!=null&&string.IsNullOrWhiteSpace(codeProperty.GetValue(row) as string)){
                if(row is Room newRoom){
                    var floor=await db.Locations.AsNoTracking().Where(l=>l.LocationId==newRoom.LocationId&&l.BranchId==newRoom.BranchId).Select(l=>l.FloorNumber).SingleOrDefaultAsync(ct);
                    var existingCodes=(await db.Rooms.AsNoTracking().Where(r=>r.BranchId==newRoom.BranchId).Select(r=>r.Code).ToArrayAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var number=Math.Max(0,floor??0)*100+1;
                    while(existingCodes.Contains(number.ToString(System.Globalization.CultureInfo.InvariantCulture)))number++;
                    newRoom.Code=number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }else codeProperty.SetValue(row,$"{typeof(T).Name.ToUpperInvariant()[..Math.Min(3,typeof(T).Name.Length)]}-{Guid.NewGuid():N}"[..20]);
            }
            var nameProperty=typeof(T).GetProperty("Name");
            if(nameProperty!=null){
                var name=(nameProperty.GetValue(row) as string)?.Trim();
                Output.Require(!string.IsNullOrWhiteSpace(name),"NAME_REQUIRED",400);
                nameProperty.SetValue(row,name);
                var duplicates=db.Set<T>().AsNoTracking().Where(x=>EF.Property<string>(x,"Name")==name);
                var primaryKey=entry.Metadata.FindPrimaryKey()!.Properties.Single().Name;
                if(id!=null)duplicates=duplicates.Where(x=>EF.Property<long>(x,primaryKey)!=id.Value);
                if(spec.Scoped)duplicates=duplicates.Where(x=>EF.Property<long>(x,"BranchId")==branchId);
                if(row is ReferenceValue rv)duplicates=duplicates.Where(x=>EF.Property<string>(x,"Kind")==rv.Kind);
                Output.Require(!await duplicates.AnyAsync(ct),"DUPLICATE_NAME");
            }
            if(row is Warehouse warehouse&&warehouse.IsActive)
                Output.Require(!await db.Warehouses.AsNoTracking().AnyAsync(w=>w.BranchId==warehouse.BranchId&&w.IsActive&&w.WarehouseId!=warehouse.WarehouseId,ct),"BRANCH_ALREADY_HAS_ACTIVE_WAREHOUSE");
            if(row is Warehouse inactiveWarehouse&&!inactiveWarehouse.IsActive&&id!=null)
                Output.Require(await db.Warehouses.AsNoTracking().AnyAsync(w=>w.BranchId==inactiveWarehouse.BranchId&&w.IsActive&&w.WarehouseId!=inactiveWarehouse.WarehouseId,ct),"BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE");
            if(row is Permission permission&&!permission.IsActive){
                var now=DateTime.UtcNow;
                Output.Require(!await (from grant in db.RolePermissions
                    join assignment in db.UserRoles on grant.RoleId equals assignment.RoleId
                    join user in db.Users on assignment.UserId equals user.UserId
                    where grant.PermissionId==permission.PermissionId&&assignment.RevokedAt==null
                        &&(assignment.ExpiresAt==null||assignment.ExpiresAt>now)&&user.AccountStatus=="ACTIVE"
                    select user.UserId).AnyAsync(ct),"PERMISSION_USED_BY_ACTIVE_USER");
            }
            db.ChangeTracker.DetectChanges();
            if(row is InventoryItem inventoryItem){
                Output.Require(!string.IsNullOrWhiteSpace(inventoryItem.Code)&&!string.IsNullOrWhiteSpace(inventoryItem.Name),"ITEM_CODE_AND_NAME_REQUIRED",400);
                Output.Require(await db.ReferenceValues.AnyAsync(r=>r.ReferenceId==inventoryItem.BaseUnitId&&r.Kind=="UOM"&&r.IsActive&&r.BaseUnitId==null,ct),"BASE_UNIT_REQUIRED",400);
                if(id!=null&&entry.Property(nameof(InventoryItem.BaseUnitId)).IsModified)
                    Output.Require(!await db.InventoryDocumentLines.AnyAsync(l=>l.ItemId==inventoryItem.ItemId,ct),"ITEM_UNIT_HAS_TRANSACTION_HISTORY");
            }
            Output.Require(row is not ReferenceValue requestedRow || !FixedLevel(requestedRow.Kind),"FIXED_OPERATIONAL_REFERENCE",405);
            Output.Require(row is not ReferenceValue reservedRow||reservedRow.Kind!="BRANCH_CONTACT","USE_BRANCH_CONTACT_FORM",405);
            if(row is Branch hotel){
                Output.Require(!string.IsNullOrWhiteSpace(hotel.Code)&&!string.IsNullOrWhiteSpace(hotel.Name),"BRANCH_CODE_AND_NAME_REQUIRED",400);
                hotel.Phone=hotel.Phone?.Trim();
                Output.Require(string.IsNullOrEmpty(hotel.Phone)||System.Text.RegularExpressions.Regex.IsMatch(hotel.Phone,@"^\+?[0-9][0-9 ()-]{6,28}[0-9]$"),"INVALID_PHONE",400);
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
            if(row is InventoryPolicy stockPolicy)await lowStock.Reconcile(ct,stockPolicy.BranchId);
            if(row is Branch contactBranch&&request.Fields.TryGetProperty("representativeName",out var representative)){
                Output.Require(representative.ValueKind is JsonValueKind.String or JsonValueKind.Null,"INVALID_FIELD_VALUE",400);
                var name=representative.ValueKind==JsonValueKind.Null?null:representative.GetString()?.Trim();
                Output.Require(name==null||name.Length<=150,"FIELD_TOO_LONG",400);
                var contactCode=$"BRANCH-{contactBranch.BranchId}";
                var contact=await db.ReferenceValues.SingleOrDefaultAsync(v=>v.Kind=="BRANCH_CONTACT"&&v.Code==contactCode,ct);
                if(contact==null){contact=new ReferenceValue{Kind="BRANCH_CONTACT",Code=contactCode,Name=$"Liên hệ {contactBranch.Code}",IsActive=true};db.ReferenceValues.Add(contact);}
                contact.SettingsJson=JsonSerializer.Serialize(new{representativeName=name});
                // Advance branch version so contact edits share the branch concurrency contract.
                db.Entry(contactBranch).Property(b=>b.Name).IsModified=true;
                await db.SaveChangesAsync(ct);
                audit.Add("Branch",contactBranch.BranchId,"UPDATE_CONTACT",after:new{representativeName=name});
            }
            var key=Convert.ToInt64(entry.Property(entry.Metadata.FindPrimaryKey()!.Properties.Single().Name).CurrentValue);
            audit.Add(typeof(T).Name,key,id==null?"CREATE":"UPDATE",spec.Scoped?branchId:null,before,Output.Record(row));
            return (object)Output.Record(row);
        },ct);
    private static ResourceDto BranchOutput(Branch branch,ReferenceValue? contact){
        var fields=new Dictionary<string,object?>(Output.Record(branch).Fields);
        string? name=null;
        if(contact?.SettingsJson is string json){try{using var doc=JsonDocument.Parse(json);if(doc.RootElement.TryGetProperty("representativeName",out var value)&&value.ValueKind==JsonValueKind.String)name=value.GetString();}catch(JsonException){}}
        fields["representativeName"]=name;return new ResourceDto(nameof(Branch),fields);
    }
    private static bool FixedLevel(string? kind)=>string.Equals(kind?.Trim(),"PRIORITY",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(kind?.Trim(),"SEVERITY",StringComparison.OrdinalIgnoreCase);
}
