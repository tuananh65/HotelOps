using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using HotelOps.Api.Services.Dev2Authorization;
using Row=RecoverySnapshot.Row;

internal sealed class RecoveryOwnership(RecoverySnapshot snapshot,HotelOpsDbContext model,JsonElement manifest,string marker)
{
    internal readonly HashSet<Row> Owned=[];private readonly HashSet<string> protectedRoots=new(StringComparer.OrdinalIgnoreCase);
    private RecoverySnapshot.Table Table<T>() where T:class
    {var entity=model.Model.FindEntityType(typeof(T))??throw new AcceptanceFailure("RECOVERY_ENTITY_NOT_MAPPED");return snapshot.Tables[(entity.GetSchema()??"dbo")+"."+entity.GetTableName()];}
    private string Column<T>(string property) where T:class
    {var entity=model.Model.FindEntityType(typeof(T))!;return entity.FindProperty(property)?.GetColumnName(StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema()))??throw new AcceptanceFailure("RECOVERY_COLUMN_NOT_MAPPED");}
    private object? Value<T>(Row row,string property) where T:class=>row.Values[Column<T>(property)];
    private static long Number(object? value)=>Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture);
    private void Equal<T>(Row row,string property,object? expected) where T:class
    {if(RecoverySnapshot.Tuple([Value<T>(row,property)])!=RecoverySnapshot.Tuple([expected])){var error=new AcceptanceFailure("RECOVERY_ROOT_OWNERSHIP_MISMATCH");error.Data["DEV2.Recovery.Table"]=row.Table.Id;error.Data["DEV2.Recovery.Property"]=property;error.Data["DEV2.Recovery.KeyHash"]=RecoverySnapshot.HashText(row.Key);throw error;}}
    private Row Find<T>(long id) where T:class
    {var table=Table<T>();if(table.PrimaryKey.Count!=1)throw new AcceptanceFailure("RECOVERY_ROOT_REQUIRES_SINGLE_PK");var rows=table.Rows.Where(r=>Number(r.Values[table.PrimaryKey[0]])==id).ToArray();if(rows.Length!=1)throw new AcceptanceFailure("RECOVERY_ROOT_MISSING_OR_CONFLICTING");return rows[0];}
    private Row Seed<T>(long id,string property,string code) where T:class
    {var row=Find<T>(id);Equal<T>(row,property,code);Owned.Add(row);protectedRoots.Add(row.Table.Id);return row;}
    private Row SeedCode<T>(string property,string code) where T:class
    {var rows=Table<T>().Rows.Where(r=>Equals(Value<T>(r,property),code)).ToArray();if(rows.Length!=1)throw new AcceptanceFailure("RECOVERY_MARKER_CODE_CARDINALITY");return Seed<T>(Number(rows[0].Values[rows[0].Table.PrimaryKey.Single()]),property,code);}
    private bool IsOwned<T>(object? id) where T:class=>id is not null&&Owned.Any(r=>r.Table==Table<T>()&&Number(r.Values[r.Table.PrimaryKey.Single()])==Number(id));
    private long Id(string name)=>manifest.GetProperty(name).GetInt64();
    private long[] Ids(string name)=>manifest.GetProperty(name).EnumerateArray().Select(x=>x.GetInt64()).ToArray();
    private void Anchor<T,TP>(string property) where T:class where TP:class
    {foreach(var row in Table<T>().Rows)if(IsOwned<TP>(Value<T>(row,property)))Owned.Add(row);}
    internal HashSet<Row> Build()
    {
        if(manifest.GetProperty("Marker").GetString()!=marker||!FixtureRunSafety.ValidMarker(marker))throw new AcceptanceFailure("RECOVERY_EXACT_MARKER_MISMATCH");
        var branches=Ids("branches");var users=Ids("users");var roles=Ids("roles");
        if(branches.Length!=2||users.Length!=7||roles.Length!=2)throw new AcceptanceFailure("RECOVERY_MANIFEST_SHAPE");
        for(var i=0;i<2;i++){var row=Seed<Branch>(branches[i],"BranchCode",marker+(i==0?"A":"B"));Equal<Branch>(row,"BranchName",marker);}
        for(var i=0;i<7;i++){var row=Seed<User>(users[i],"Username",marker+"u"+i);Equal<User>(row,"EmployeeCode",marker+"E"+i);Equal<User>(row,"FullName",marker);Equal<User>(row,"PrimaryBranchId",branches[i==4?1:0]);}
        for(var i=0;i<2;i++){var row=Seed<Role>(roles[i],"RoleCode",marker+(i==0?"MAIN":"OWN"));Equal<Role>(row,"RoleName",marker);}
        var warehouse=Seed<Warehouse>(Id("warehouse"),"WarehouseCode",marker);Equal<Warehouse>(warehouse,"WarehouseName",marker);Equal<Warehouse>(warehouse,"BranchId",branches[0]);
        Seed<AssetType>(Id("assetType"),"TypeCode",marker);Seed<ItemCategory>(Id("category"),"CategoryCode",marker);Seed<UnitsOfMeasure>(Id("uom"),"UomCode",marker);
        foreach(var suffix in new[]{"D1","D2"})SeedCode<Department>("DepartmentCode",marker+suffix);
        foreach(var suffix in new[]{"A1","A2","FA"})SeedCode<Area>("AreaCode",marker+suffix);
        foreach(var n in new[]{1,56,57,58,59})SeedCode<InventoryItem>("ItemCode",marker+"IT"+n);
        foreach(var p in manifest.GetProperty("assets").EnumerateObject())Seed<Asset>(p.Value.GetInt64(),"AssetCode",marker+"AS"+p.Name);
        foreach(var p in manifest.GetProperty("incidents").EnumerateObject()){var row=Seed<Incident>(p.Value.GetInt64(),"IncidentNo",marker+"I"+p.Name);Equal<Incident>(row,"ReportedBy",users[0]);}
        foreach(var p in manifest.GetProperty("works").EnumerateObject()){var row=Seed<MaintenanceWork>(p.Value.GetInt64(),"WorkNo",marker+"W"+p.Name);Equal<MaintenanceWork>(row,"CreatedBy",users[0]);}
        foreach(var p in manifest.GetProperty("requests").EnumerateObject()){var row=Seed<MaterialRequest>(p.Value.GetInt64(),"RequestNo",marker+"MR"+p.Name);Equal<MaterialRequest>(row,"RequesterId",users[0]);}
        foreach(var p in manifest.GetProperty("replenishments").EnumerateObject()){var row=Seed<MaterialReplenishment>(p.Value.GetInt64(),"ReplenishmentNo",marker+"R"+p.Name);Equal<MaterialReplenishment>(row,"RequestedBy",users[0]);}
        foreach(var n in new[]{1,58,59})SeedCode<StockDocument>("DocumentNo",marker+"REC"+n);
        SeedCode<StockDocument>("DocumentNo",marker+"ISS50");SeedCode<MaterialFulfillment>("FulfillmentNo",marker+"FUL50");
        foreach(var p in manifest.GetProperty("receipts").EnumerateObject())if(!IsOwned<StockDocument>(p.Value.GetInt64()))throw new AcceptanceFailure("RECOVERY_RECEIPT_MANIFEST_MISMATCH");
        var borrowed=manifest.GetProperty("borrowedPriority");if(borrowed.GetProperty("createdByFixture").GetBoolean())throw new AcceptanceFailure("RECOVERY_BORROWED_PRIORITY_MARKED_OWNED");
        var priority=Find<ReferenceValue>(borrowed.GetProperty("referenceId").GetInt64());Equal<ReferenceValue>(priority,"GroupCode","PRIORITY");Equal<ReferenceValue>(priority,"ValueCode","NORMAL");
        foreach(var id in Ids("createdReferenceIds")){var row=Seed<ReferenceValue>(id,"ValueCode",marker);Equal<ReferenceValue>(row,"GroupCode","SEVERITY");if(row==priority)throw new AcceptanceFailure("RECOVERY_BORROWED_REFERENCE_DELETE");}
        var allowedCodes=Dev2DemoBindings.BoundDecisionIds.SelectMany(Dev2DemoBindings.PermissionCodes).ToHashSet(StringComparer.Ordinal);
        foreach(var id in Ids("newPermissionIds"))
        {var row=Find<Permission>(id);if(Value<Permission>(row,"PermissionCode") is not string code||!code.StartsWith("DEV2_DEMO_",StringComparison.Ordinal)||!allowedCodes.Contains(code))throw new AcceptanceFailure("RECOVERY_CANONICAL_PERMISSION_DELETE");Equal<Permission>(row,"ModuleCode","DEV2_DEMO");Equal<Permission>(row,"ResourceCode","DEV2_TRIAL");Equal<Permission>(row,"Description","Fixture-only owner-authorized V2 test catalog");Owned.Add(row);}
        Anchor<AssetHandover,Asset>("AssetId");Anchor<AssetConditionHistory,Asset>("AssetId");Anchor<AssetLocationHistory,Asset>("AssetId");Anchor<IncidentEvent,Incident>("IncidentId");
        Anchor<MaintenanceWorkLog,MaintenanceWork>("MaintenanceWorkId");Anchor<MaintenanceMaterialUsage,MaintenanceWork>("MaintenanceWorkId");
        Anchor<MaterialRequestLine,MaterialRequest>("RequestId");Anchor<MaterialRequestApprovalCycle,MaterialRequest>("RequestId");Anchor<MaterialRequestApprovalLineSnapshot,MaterialRequestApprovalCycle>("ApprovalCycleId");Anchor<MaterialRequestApproval,MaterialRequest>("RequestId");Anchor<MaterialFulfillmentLine,MaterialFulfillment>("FulfillmentId");
        Anchor<StockDocumentLine,StockDocument>("DocumentId");Anchor<StockMovement,StockDocumentLine>("DocumentLineId");Anchor<StockReceiptLineDetail,StockDocumentLine>("StockDocumentLineId");
        Anchor<MaterialReplenishmentLine,MaterialReplenishment>("ReplenishmentId");Anchor<MaterialReplenishmentReceiptLine,MaterialReplenishmentLine>("ReplenishmentLineId");Anchor<StockThreshold,Warehouse>("WarehouseId");
        Anchor<UserSession,User>("UserId");Anchor<UserRole,User>("UserId");Anchor<UserBranchAccess,User>("UserId");Anchor<RolePermission,Role>("RoleId");
        // Authentication audit has nullable ActorUserId/BranchId: the exact users EntityId is ownership evidence.
        var entityIds=Owned.Where(r=>r.Table.PrimaryKey.Count==1).GroupBy(r=>r.Table.Id.Split('.')[1]).ToDictionary(g=>g.Key,g=>g.Select(r=>Convert.ToString(r.Values[r.Table.PrimaryKey[0]],System.Globalization.CultureInfo.InvariantCulture)).ToHashSet());
        foreach(var row in Table<AuditLog>().Rows)
        {
            var type=Value<AuditLog>(row,"EntityType") as string;var id=Value<AuditLog>(row,"EntityId") as string;
            var target=type is not null&&entityIds.TryGetValue(type,out var ids)&&ids.Contains(id);
            var actor=Value<AuditLog>(row,"ActorUserId");var branch=Value<AuditLog>(row,"BranchId");
            if(!target&&!IsOwned<User>(actor)&&!IsOwned<Branch>(branch))continue;
            if(!target||actor is not null&&!IsOwned<User>(actor)||branch is not null&&!IsOwned<Branch>(branch))throw new AcceptanceFailure("RECOVERY_AUDIT_OWNERSHIP_CONFLICT");
            Owned.Add(row);
        }
        // Known polymorphic references are not SQL FKs. Preserve/block unowned attachment,
        // notification, audit or document rows pointing at an owned entity; never guess ownership.
        static string Alias(string value)=>new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var aliases=new Dictionary<string,HashSet<string?>>(StringComparer.Ordinal);
        foreach(var entity in model.Model.GetEntityTypes().Where(e=>e.GetTableName() is not null))
        {
            var table=(entity.GetSchema()??"dbo")+"."+entity.GetTableName();
            var ids=Owned.Where(r=>r.Table.Id==table&&r.Table.PrimaryKey.Count==1).Select(r=>Convert.ToString(r.Values[r.Table.PrimaryKey[0]],System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            foreach(var alias in new[]{table,entity.GetTableName()!,entity.ClrType.Name})aliases[Alias(alias)]=ids;
        }
        foreach(var row in snapshot.Tables.Values.SelectMany(t=>t.Rows).Where(r=>!Owned.Contains(r)))foreach(var (typeColumn,idColumn) in new[]{("entity_type","entity_id"),("reference_type","reference_id"),("source_type","source_id")})
            if(row.Values.TryGetValue(typeColumn,out var typeValue)&&typeValue is string type&&row.Values.TryGetValue(idColumn,out var idValue)&&idValue is not null&&aliases.TryGetValue(Alias(type),out var targets)&&targets.Contains(Convert.ToString(idValue,System.Globalization.CultureInfo.InvariantCulture)))throw new AcceptanceFailure("RECOVERY_EXTERNAL_POLYMORPHIC_REFERENCE");
        protectedRoots.UnionWith(Owned.Select(r=>r.Table.Id));
        snapshot.ValidateOwnedForeignKeys(Owned);var edges=snapshot.Edges().ToArray();
        foreach(var (child,parent) in edges.Where(e=>Owned.Contains(e.child)&&!Owned.Contains(e.parent)))
            if(protectedRoots.Contains(parent.Table.Id)&&parent!=priority&&parent.Table!=Table<Permission>())throw new AcceptanceFailure("RECOVERY_CHILD_POINTS_OUTSIDE_OWNED_ROOTS");
        if(Owned.Any(r=>r.Table.PrimaryKey.Count==0))throw new AcceptanceFailure("RECOVERY_UNKEYED_OWNED_TABLE");
        foreach(var table in Owned.Select(r=>r.Table).Distinct())
        {
            var entity=model.Model.GetEntityTypes().SingleOrDefault(e=>(e.GetSchema()??"dbo")+"."+e.GetTableName()==table.Id)??throw new AcceptanceFailure("RECOVERY_TABLE_NOT_MAPPED");
            var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());var expected=entity.FindPrimaryKey()?.Properties.Select(p=>p.GetColumnName(store)!).ToArray()??[];
            if(!expected.SequenceEqual(table.PrimaryKey,StringComparer.OrdinalIgnoreCase))throw new AcceptanceFailure("RECOVERY_PRIMARY_KEY_SCHEMA_DRIFT");
        }
        _=RecoverySnapshot.DeleteOrder(Owned,edges);return Owned;
    }
}
