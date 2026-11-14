using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using HotelOps.Api.Services.Dev2Authorization;
using Row=RecoverySnapshot.Row;

internal static class RecoveryVerification
{
    internal static async Task<int> Run(string output)
    {
        var count=0;void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);count++;}
        Check(FixtureRunSafety.ValidMarker("D2A3994a0e4cd")&&!FixtureRunSafety.ValidMarker("D2A3994a0e4cd%")&&!FixtureRunSafety.ValidMarker("D2A3994a0e4c"),"Exact marker validation");
        using var document=SyntheticRecoveryFixture.Create();var manifest=document.RootElement;var marker=manifest.GetProperty("Marker").GetString()!;
        await using var model=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=lpc:KIENPC;Database=HotelOps_DEV2_FullStack_Trial;Integrated Security=true;Encrypt=false").Options);
        var snapshot=new RecoverySnapshot();
        foreach(var entity in model.Model.GetEntityTypes().Where(e=>e.GetTableName() is not null))
        {
            var name=(entity.GetSchema()??"dbo")+"."+entity.GetTableName();if(snapshot.Tables.ContainsKey(name))continue;
            var table=new RecoverySnapshot.Table(entity.GetSchema()??"dbo",entity.GetTableName()!);snapshot.Tables[name]=table;
            var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());table.Columns=entity.GetProperties().Select(p=>p.GetColumnName(store)!).Distinct().ToList();table.PrimaryKey=entity.FindPrimaryKey()?.Properties.Select(p=>p.GetColumnName(store)!).ToList()??[];
        }
        foreach(var entity in model.Model.GetEntityTypes().Where(e=>e.GetTableName() is not null))foreach(var key in entity.GetForeignKeys().Where(k=>k.PrincipalEntityType.GetTableName() is not null))
        {var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());var principal=key.PrincipalEntityType;var parentStore=StoreObjectIdentifier.Table(principal.GetTableName()!,principal.GetSchema());snapshot.Keys.Add(new(key.GetConstraintName()??"test",(entity.GetSchema()??"dbo")+"."+entity.GetTableName(),(principal.GetSchema()??"dbo")+"."+principal.GetTableName(),key.Properties.Select(p=>p.GetColumnName(store)!).ToArray(),key.PrincipalKey.Properties.Select(p=>p.GetColumnName(parentStore)!).ToArray()));}
        Row Add<T>(long id,params (string property,object? value)[] fields) where T:class
        {
            var entity=model.Model.FindEntityType(typeof(T))!;var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());var table=snapshot.Tables[(entity.GetSchema()??"dbo")+"."+entity.GetTableName()];
            var values=table.Columns.ToDictionary(c=>c,c=>(object?)null,StringComparer.OrdinalIgnoreCase);values[table.PrimaryKey.Single()]=id;
            foreach(var (name,value) in fields)values[entity.FindProperty(name)!.GetColumnName(store)!]=value;
            var row=new Row(table,values);table.Rows.Add(row);return row;
        }
        long[] Ids(string name)=>manifest.GetProperty(name).EnumerateArray().Select(x=>x.GetInt64()).ToArray();
        var users=Ids("users");var branches=Ids("branches");var roles=Ids("roles");
        for(var i=0;i<2;i++)Add<Branch>(branches[i],("BranchCode",marker+(i==0?"A":"B")),("BranchName",marker));
        for(var i=0;i<7;i++)Add<User>(users[i],("Username",marker+"u"+i),("EmployeeCode",marker+"E"+i),("FullName",marker),("PrimaryBranchId",branches[i==4?1:0]));
        for(var i=0;i<2;i++)Add<Role>(roles[i],("RoleCode",marker+(i==0?"MAIN":"OWN")),("RoleName",marker));
        Add<Warehouse>(manifest.GetProperty("warehouse").GetInt64(),("WarehouseCode",marker),("WarehouseName",marker),("BranchId",branches[0]));
        Add<AssetType>(manifest.GetProperty("assetType").GetInt64(),("TypeCode",marker));Add<ItemCategory>(manifest.GetProperty("category").GetInt64(),("CategoryCode",marker));Add<UnitsOfMeasure>(manifest.GetProperty("uom").GetInt64(),("UomCode",marker));
        for(var i=1;i<=2;i++)Add<Department>(900+i,("DepartmentCode",marker+"D"+i));
        var n=0;foreach(var suffix in new[]{"A1","A2","FA"})Add<Area>(910+n++,("AreaCode",marker+suffix));
        foreach(var number in new[]{1,56,57,58,59})Add<InventoryItem>(1000+number,("ItemCode",marker+"IT"+number));
        foreach(var p in manifest.GetProperty("assets").EnumerateObject())Add<Asset>(p.Value.GetInt64(),("AssetCode",marker+"AS"+p.Name));
        foreach(var p in manifest.GetProperty("incidents").EnumerateObject())Add<Incident>(p.Value.GetInt64(),("IncidentNo",marker+"I"+p.Name),("ReportedBy",users[0]));
        foreach(var p in manifest.GetProperty("works").EnumerateObject())Add<MaintenanceWork>(p.Value.GetInt64(),("WorkNo",marker+"W"+p.Name),("CreatedBy",users[0]));
        foreach(var p in manifest.GetProperty("requests").EnumerateObject())Add<MaterialRequest>(p.Value.GetInt64(),("RequestNo",marker+"MR"+p.Name),("RequesterId",users[0]));
        foreach(var p in manifest.GetProperty("replenishments").EnumerateObject())Add<MaterialReplenishment>(p.Value.GetInt64(),("ReplenishmentNo",marker+"R"+p.Name),("RequestedBy",users[0]));
        Add<StockDocument>(6000,("DocumentNo",marker+"REC1"));foreach(var p in manifest.GetProperty("receipts").EnumerateObject())Add<StockDocument>(p.Value.GetInt64(),("DocumentNo",marker+"REC"+p.Name));Add<StockDocument>(6001,("DocumentNo",marker+"ISS50"));Add<MaterialFulfillment>(6002,("FulfillmentNo",marker+"FUL50"));
        foreach(var id in Ids("createdReferenceIds"))Add<ReferenceValue>(id,("GroupCode","SEVERITY"),("ValueCode",marker));
        var priority=Add<ReferenceValue>(manifest.GetProperty("borrowedPriority").GetProperty("referenceId").GetInt64(),("GroupCode","PRIORITY"),("ValueCode","NORMAL"),("DisplayName","Normal"));
        var demoCodes=Dev2DemoBindings.BoundDecisionIds.SelectMany(Dev2DemoBindings.PermissionCodes).Where(x=>x.StartsWith("DEV2_DEMO_",StringComparison.Ordinal)).Distinct().ToArray();var permissionIndex=0;
        foreach(var id in Ids("newPermissionIds"))Add<Permission>(id,("PermissionCode",demoCodes[permissionIndex++]),("ModuleCode","DEV2_DEMO"),("ResourceCode","DEV2_TRIAL"),("Description","Fixture-only owner-authorized V2 test catalog"));
        var shared=Add<User>(90000,("Username","canonical"));var session=Add<UserSession>(8000,("UserId",users[0]));var authAudit=Add<AuditLog>(8001,("EntityType","users"),("EntityId",users[0].ToString()));
        var proof=new RecoveryOwnership(snapshot,model,manifest,marker);var owned=proof.Build();
        Check(owned.Contains(session)&&owned.Contains(authAudit),"Sessions and nullable-actor authentication audits included by proven ownership");Check(!owned.Contains(priority)&&!owned.Contains(shared),"Borrowed priority/canonical user never selected");
        var order=RecoverySnapshot.DeleteOrder(owned,snapshot.Edges());Check(order.IndexOf(session)<order.FindIndex(r=>r.Table.Id=="dbo.users"&&r.Values.Values.Contains(users[0])),"Dependent session precedes user deletion");
        var attachment=Add<Attachment>(8800,("EntityType","assets"),("EntityId",manifest.GetProperty("assets").GetProperty("1").GetInt64()),("UploadedBy",90000L));
        try{_=new RecoveryOwnership(snapshot,model,manifest,marker).Build();throw new Exception("Soft reference accepted");}catch(AcceptanceFailure e){Check(e.Category=="RECOVERY_EXTERNAL_POLYMORPHIC_REFERENCE","Canonical attachment soft reference blocks orphan deletion");}attachment.Table.Rows.Remove(attachment);
        var permission=owned.First(r=>r.Table.Id=="dbo.permissions");var permissionColumn=model.Model.FindEntityType(typeof(Permission))!.FindProperty("PermissionCode")!.GetColumnName()!;var originalCode=permission.Values[permissionColumn];permission.Values[permissionColumn]="ASSET_TRANSFER";
        try{_=new RecoveryOwnership(snapshot,model,manifest,marker).Build();throw new Exception("Canonical permission accepted for deletion");}catch(AcceptanceFailure e){Check(e.Category=="RECOVERY_CANONICAL_PERMISSION_DELETE","Canonical permission cannot be deleted even if manifest claims its ID");}permission.Values[permissionColumn]=originalCode;
        var user=model.Model.FindEntityType(typeof(User))!;var branchColumn=user.FindProperty("PrimaryBranchId")!.GetColumnName(StoreObjectIdentifier.Table(user.GetTableName()!,user.GetSchema()))!;
        shared.Values[branchColumn]=branches[0];try{_=new RecoveryOwnership(snapshot,model,manifest,marker).Build();throw new Exception("External reference accepted");}catch(AcceptanceFailure e){Check(e.Category=="RECOVERY_EXTERNAL_REFERENCE_BLOCKER","Unrelated user referencing fixture branch blocks all deletion");}shared.Values[branchColumn]=null;
        var branchCode=model.Model.FindEntityType(typeof(Branch))!.FindProperty("BranchCode")!.GetColumnName()!;var branch=owned.First(r=>r.Table.Id=="dbo.branches");var original=branch.Values[branchCode];branch.Values[branchCode]="NOT_OWNED";
        try{_=new RecoveryOwnership(snapshot,model,manifest,marker).Build();throw new Exception("Bad ownership accepted");}catch(AcceptanceFailure e){Check(e.Category=="RECOVERY_ROOT_OWNERSHIP_MISMATCH","Wrong marker/ID ownership aborts");}branch.Values[branchCode]=original;
        var keys=owned.Select(x=>x.Key).ToHashSet();var before=snapshot.PreservedHash(keys);foreach(var table in snapshot.Tables.Values)table.Rows.RemoveAll(owned.Contains);
        Check(snapshot.PreservedHash(keys)==before,"Deleting only selected PKs preserves all unrelated row hashes");
        priority.Values[model.Model.FindEntityType(typeof(ReferenceValue))!.FindProperty("DisplayName")!.GetColumnName()!]="CHANGED";Check(snapshot.PreservedHash(keys)!=before,"Borrowed shared row mutation is detected");
        var table2=new RecoverySnapshot.Table("dbo","cycle"){Columns=["id"],PrimaryKey=["id"]};var a=new Row(table2,new(){{"id",1}});var b=new Row(table2,new(){{"id",2}});
        try{_=RecoverySnapshot.DeleteOrder(new HashSet<Row>{a,b},new[]{(a,b),(b,a)});throw new Exception("Cycle accepted");}catch(AcceptanceFailure e){Check(e.Category=="RECOVERY_DEPENDENCY_CYCLE","Cycles abort before delete");}
        await File.WriteAllTextAsync(Path.Combine(output,"FIX06_RECOVERY_OFFLINE.json"),JsonSerializer.Serialize(new{status="PASS",assertions=count,sqlConnections=0,scope="Synthetic in-memory snapshot built from actual EF metadata and deterministic synthetic manifest; no historical trial data or recovery SQL",orphanRecoverySql="NOT_RUN"},new JsonSerializerOptions{WriteIndented=true}));return count;
    }
}
