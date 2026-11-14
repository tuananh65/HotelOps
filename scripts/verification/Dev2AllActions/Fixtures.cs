using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal sealed partial class Fixtures(Func<HotelOpsDbContext> open,string output)
{
    internal readonly string Marker="D2A"+Guid.NewGuid().ToString("N")[..10];
    internal string Password="D2!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20))+"a";
    internal Branch Branch=null!,ForeignBranch=null!;internal Department Department=null!,OtherDepartment=null!;
    internal Area Area=null!,OtherArea=null!,ForeignArea=null!;internal AssetType AssetType=null!;
    internal User[] Users=[];internal Role MainRole=null!,OwnRole=null!;
    internal Warehouse Warehouse=null!;internal UnitsOfMeasure Uom=null!;internal ItemCategory Category=null!;
    internal ReferenceValue Severity=null!,Priority=null!;
    internal Dictionary<int,Asset> Assets=[];internal Dictionary<int,AssetHandover> Handovers=[];
    internal Dictionary<int,Incident> Incidents=[];internal Dictionary<int,MaintenanceWork> Works=[];
    internal Dictionary<int,MaterialRequest> Requests=[];internal Dictionary<int,MaterialReplenishment> Replenishments=[];
    internal Dictionary<int,InventoryItem> Items=[];internal Dictionary<int,StockThreshold> Thresholds=[];
    internal Dictionary<int,StockDocument> Receipts=[];internal List<long> NewPermissionIds=[];
    internal long FulfillmentId;internal long[] UserIds=>Users.Select(x=>x.UserId).ToArray();
    internal bool Committed {get;private set;}
    private DateTime Now=>DateTime.UtcNow;

    internal async Task Create()
    {
        await FixtureRunSafety.State(output,Marker,"FIXTURE_INTENT_BEFORE_SQL");
        await using var db=open();await using var tx=await db.Database.BeginTransactionAsync();
        Stage="01 permission-catalog";
        var codes=Dev2DemoBindings.BoundDecisionIds.SelectMany(Dev2DemoBindings.PermissionCodes).Distinct().ToArray();
        var permissions=await db.Permissions.Where(x=>codes.Contains(x.PermissionCode)).ToDictionaryAsync(x=>x.PermissionCode);
        var insertedPermissions=new List<Permission>();
        foreach(var code in codes)
        {
            if(permissions.ContainsKey(code))continue;
            if(!code.StartsWith("DEV2_DEMO_",StringComparison.Ordinal))throw new InvalidOperationException("Missing actual canonical permission; never seed guessed replacement: "+code);
            var permission=new Permission{PermissionCode=code,PermissionName=code,ModuleCode="DEV2_DEMO",ResourceCode="DEV2_TRIAL",ActionCode="EXECUTE",Description="Fixture-only owner-authorized V2 test catalog"};db.Add(permission);permissions.Add(code,permission);insertedPermissions.Add(permission);
        }
        await Save(db,"01 permission-catalog","01.catalog-insert");
        // Only IDs inserted by this invocation may be removed. Existing test codes are references, not owned rows.
        NewPermissionIds=insertedPermissions.Select(x=>x.PermissionId).ToList();
        Branch=new(){BranchCode=Marker+"A",BranchName=Marker,IsActive=true,CreatedAt=Now};ForeignBranch=new(){BranchCode=Marker+"B",BranchName=Marker,IsActive=true,CreatedAt=Now};db.AddRange(Branch,ForeignBranch);await Save(db,"02 branches","02.branch-roots");
        Department=new(){BranchId=Branch.BranchId,DepartmentCode=Marker+"D1",DepartmentName=Marker,IsActive=true};OtherDepartment=new(){BranchId=Branch.BranchId,DepartmentCode=Marker+"D2",DepartmentName=Marker,IsActive=true};
        Area=new(){BranchId=Branch.BranchId,AreaCode=Marker+"A1",AreaName=Marker,IsActive=true};OtherArea=new(){BranchId=Branch.BranchId,AreaCode=Marker+"A2",AreaName=Marker,IsActive=true};ForeignArea=new(){BranchId=ForeignBranch.BranchId,AreaCode=Marker+"FA",AreaName=Marker,IsActive=true};
        AssetType=new(){TypeCode=Marker,TypeName=Marker,IsActive=true};
        Uom=new(){UomCode=Marker,UomName=Marker,IsActive=true};Category=new(){CategoryCode=Marker,CategoryName=Marker,IsActive=true};
        Warehouse=new(){BranchId=Branch.BranchId,WarehouseCode=Marker,WarehouseName=Marker,IsActive=true,CreatedAt=Now};
        Priority=await CanonicalPriority(db);
        Severity=new(){GroupCode="SEVERITY",ValueCode=Marker,DisplayName=Marker,IsActive=true};references.OwnNewSeverity(Severity,Marker);
        MainRole=new(){RoleCode=Marker+"MAIN",RoleName=Marker,IsActive=true,CreatedAt=Now};OwnRole=new(){RoleCode=Marker+"OWN",RoleName=Marker,IsActive=true,CreatedAt=Now};
        db.AddRange(Department,OtherDepartment,Area,OtherArea,ForeignArea,AssetType,Uom,Category,Warehouse,Severity,MainRole,OwnRole);await Save(db,"03 departments-areas-warehouse","03.branch-and-global-catalogs");
        var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        // Keep the temporary credential beyond the 15-minute session cap. SQL materializes
        // its datetime2 as Unspecified; the earlier cap retains server UTC in existing Auth.
        // This is a restricted user, not a normal-user substitute or an expiry bypass.
        Users=Enumerable.Range(0,7).Select(i=>new User{Username=Marker+"u"+i,EmployeeCode=Marker+"E"+i,Email=Marker+i+"@example.invalid",FullName=Marker,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=Now,PrimaryBranchId=i==4?ForeignBranch.BranchId:Branch.BranchId,TemporaryPasswordExpiresAt=i==6?Now.AddDays(1):null}).ToArray();
        foreach(var user in Users){user.PasswordHash=hasher.HashPassword(user,Password);db.Add(user);}await Save(db,"04 users","04.hashed-users");
        for(var i=0;i<Users.Length;i++)
        {
            var branch=i==4?ForeignBranch.BranchId:Branch.BranchId;
            db.Add(new UserBranchAccess{UserId=Users[i].UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=Now,IsPrimary=true});
            if(i!=3)db.Add(new UserRole{UserId=Users[i].UserId,BranchId=branch,RoleId=i==5?OwnRole.RoleId:MainRole.RoleId,IsActive=true,AssignedAt=Now.AddMinutes(-1)});
        }
        foreach(var p in permissions.Values){db.Add(new RolePermission{RoleId=MainRole.RoleId,PermissionId=p.PermissionId,DataScope="BRANCH",GrantedAt=Now});db.Add(new RolePermission{RoleId=OwnRole.RoleId,PermissionId=p.PermissionId,DataScope="OWN",GrantedAt=Now});}
        await Save(db,"05 role-grants-memberships","05.branch-membership-and-test-role-grants");
        foreach(var n in new[]{1,4,5,6,7,15,16,17,18,99})
        {
            var asset=new Asset{AssetCode=Marker+"AS"+n,AssetName=Marker,AssetTypeId=AssetType.AssetTypeId,BranchId=n==99?ForeignBranch.BranchId:Branch.BranchId,CurrentDepartmentId=n==99?null:Department.DepartmentId,CustodianUserId=n==99?null:Users[0].UserId,ConditionStatus="GOOD",LifecycleStatus="IN_USE",CreatedAt=Now};
            if(n==17)asset.CurrentAreaId=Area.AreaId;Assets[n]=asset;db.Add(asset);
        }
        await Save(db,"06 assets","06.asset-roots");
        foreach(var n in new[]{4,5}){var handover=new AssetHandover{AssetId=Assets[n].AssetId,HandoverNo=Marker+"H"+n,FromDepartmentId=Department.DepartmentId,ToDepartmentId=OtherDepartment.DepartmentId,FromUserId=Users[0].UserId,ToUserId=Users[1].UserId,HandoverStatus="PENDING",CreatedBy=Users[0].UserId,CreatedAt=Now,HandedOverAt=Now};Handovers[n]=handover;db.Add(handover);}
        await Save(db,"07 asset-handovers","07.pending-handovers");
        foreach(var n in new[]{7,19,20,24,25,26,27,28,29,30,31,32,33,34,99})
        {
            var state=n is 27 or 29 or 30 or 34?"IN_PROGRESS":n==28?"IN_PROGRESS":n is 31 or 32?"RESOLVED":"OPEN";
            var incident=new Incident{IncidentNo=Marker+"I"+n,BranchId=n==99?ForeignBranch.BranchId:Branch.BranchId,ReportedBy=Users[0].UserId,IncidentTitle=Marker+"I"+n,Description=Marker,DiscoveredAt=Now,ReportedAt=Now,Status=state,AssetId=n==7?Assets[7].AssetId:null,AreaId=n==99?ForeignArea.AreaId:Area.AreaId,ResolvedAt=state=="RESOLVED"?Now:null};Incidents[n]=incident;db.Add(incident);
        }
        await Save(db,"08 incidents","08.incident-roots");
        foreach(var n in new[]{7,25,26,27,28,29,30,31,32,33,34})
        {
            var state=n==28?"ON_HOLD":n is 27 or 29 or 30 or 34?"IN_PROGRESS":n is 31 or 32?"COMPLETED":"PENDING";
            var work=new MaintenanceWork{WorkNo=Marker+"W"+n,IncidentId=Incidents[n].IncidentId,Title=Marker,Status=state,AssigneeId=Users[0].UserId,DepartmentId=Department.DepartmentId,CreatedBy=Users[0].UserId,CreatedAt=Now,ActualStartAt=state=="PENDING"?null:Now,ResolvedAt=state=="COMPLETED"?Now:null,ResolutionNote=state=="COMPLETED"?Marker:null};Works[n]=work;db.Add(work);
        }
        await Save(db,"09 maintenance-work","09.corrective-work-roots");
        foreach(var n in new[]{1,56,57,58,59}){var item=new InventoryItem{ItemCode=Marker+"IT"+n,ItemName=Marker,CategoryId=Category.ItemCategoryId,BaseUomId=Uom.UomId,IsActive=true,IsConsumable=true,CreatedAt=Now};Items[n]=item;db.Add(item);}await Save(db,"10 inventory-items","10.item-catalog");
        foreach(var n in new[]{56,57,58,59}){var threshold=new StockThreshold{WarehouseId=Warehouse.WarehouseId,ItemId=Items[n].ItemId,MinimumQty=10,AlertEnabled=true};Thresholds[n]=threshold;db.Add(threshold);}await Save(db,"11 inventory-thresholds","11.warehouse-thresholds");
        foreach(var n in new[]{44,45,46,47,48,49,50,51,61})
        {
            var state=n is 46 or 47 or 48?"PENDING_APPROVAL":n==50?"PARTIALLY_ISSUED":n is 51 or 61?"APPROVED":"DRAFT";
            var request=new MaterialRequest{RequestNo=Marker+"MR"+n,BranchId=Branch.BranchId,DepartmentId=Department.DepartmentId,RequesterId=Users[0].UserId,SourceWarehouseId=Warehouse.WarehouseId,Purpose=Marker,NeededAt=Now.AddDays(1),Status=state,CreatedAt=Now,SubmittedAt=state=="DRAFT"?null:Now,IncidentId=n==51?Incidents[34].IncidentId:null};
            request.MaterialRequestLines.Add(new(){LineNo=1,ItemId=Items[1].ItemId,UomId=Uom.UomId,RequestedQty=n==61?250:10,ApprovedQty=state is "APPROVED" or "PARTIALLY_ISSUED"?n==61?250:10:null,FulfilledQty=n==50?2:0,IsActive=true});
            Requests[n]=request;db.Add(request);
        }
        await Save(db,"12 material-requests","12.requests-and-lines");
        foreach(var pair in Requests)
        {
            var r=pair.Value;var line=r.MaterialRequestLines.Single();
            db.Add(new AuditLog{BranchId=Branch.BranchId,ActorUserId=Users[0].UserId,EntityType="material_requests",EntityId=r.RequestId.ToString(),Action="CREATE",NewValues="{\"BusinessEvent\":\"REQUEST_CREATED\",\"Fixture\":true}",OccurredAt=Now});
            if(r.Status=="DRAFT")continue;
            var approved=r.Status is "APPROVED" or "PARTIALLY_ISSUED";
            var cycle=new MaterialRequestApprovalCycle{RequestId=r.RequestId,RevisionNo=1,SubmittedBy=Users[0].UserId,AssignedApproverId=Users[1].UserId,Status=approved?"APPROVED":"PENDING",SubmittedAt=Now,ResolvedAt=approved?Now:null};
            cycle.MaterialRequestApprovalLineSnapshots.Add(new(){RequestLineId=line.LineId,LineNo=1,ItemId=line.ItemId,UomId=line.UomId,RequestedQty=line.RequestedQty,ApprovedQty=line.ApprovedQty});
            if(approved)cycle.MaterialRequestApproval=new(){RequestId=r.RequestId,ApprovalLevel=1,ApproverId=Users[1].UserId,Decision="APPROVED",DecidedAt=Now};db.Add(cycle);
        }
        await Save(db,"13 material-approval-cycles","13.cycles-snapshots-decisions-and-fixture-audits");
        var initial=Receipt(1,"POSTED",100);db.Add(initial);await Save(db,"14 stock-receipts","14.posted-opening-receipt");Move(db,initial,100);await Save(db,"14 stock-receipts","14.opening-stock-movement");
        var partial=Requests[50];var batch=new MaterialFulfillment{FulfillmentNo=Marker+"FUL50",RequestId=partial.RequestId,WarehouseId=Warehouse.WarehouseId,FulfilledBy=Users[2].UserId,Status="ISSUED",CreatedAt=Now,FulfilledAt=Now};db.Add(batch);await Save(db,"15 material-fulfillments","15.fulfillment-header");
        var issue=new StockDocument{DocumentNo=Marker+"ISS50",DocumentType="ISSUE",SourceWarehouseId=Warehouse.WarehouseId,Status="POSTED",CreatedBy=Users[2].UserId,PostedBy=Users[2].UserId,CreatedAt=Now,PostedAt=Now,DocumentDate=DateOnly.FromDateTime(Now),ReferenceType="MATERIAL_FULFILLMENT",ReferenceId=batch.FulfillmentId};issue.StockDocumentLines.Add(new(){LineNo=1,ItemId=Items[1].ItemId,UomId=Uom.UomId,ConversionFactor=1,RequestedQty=2,ActualQty=2,BaseQty=2});db.Add(issue);await Save(db,"15 material-fulfillments","15.issue-document-and-line");batch.StockDocumentId=issue.DocumentId;batch.MaterialFulfillmentLines.Add(new(){RequestLineId=partial.MaterialRequestLines.Single().LineId,ItemId=Items[1].ItemId,UomId=Uom.UomId,IssuedQty=2,StockDocumentLineId=issue.StockDocumentLines.Single().LineId});Move(db,issue,-2);
        await Save(db,"15 material-fulfillments","15.fulfillment-lines-and-issued-stock");
        foreach(var n in new[]{57,58,59}){var repl=new MaterialReplenishment{ReplenishmentNo=Marker+"R"+n,WarehouseId=Warehouse.WarehouseId,RequestedBy=Users[0].UserId,Status="REQUESTED",CreatedAt=Now};repl.MaterialReplenishmentLines.Add(new(){LineNo=1,WarehouseId=Warehouse.WarehouseId,ItemId=Items[n].ItemId,BaseUomId=Uom.UomId,SourceType="LOW_STOCK",SourceThresholdId=Thresholds[n].ThresholdId,RequestedBaseQty=10,ReceivedBaseQty=0,IsOpenCoverage=true});Replenishments[n]=repl;db.Add(repl);}
        await Save(db,"16 replenishments","16.replenishment-roots-and-lines");
        Receipts[58]=Receipt(58,"POSTED",3);Receipts[59]=Receipt(59,"DRAFT",3);db.AddRange(Receipts.Values);await Save(db,"17 final-stock-effects","17.link-and-post-receipt-fixtures");Move(db,Receipts[58],3);await Save(db,"17 final-stock-effects","17.receipt-stock-movement");
        Stage="18 transaction-commit";SaveBoundary=null;
        await WriteManifest();await FixtureRunSafety.State(output,Marker,"FIXTURE_COMMITTING_MANIFEST_DURABLE");
        try{await tx.CommitAsync();Committed=true;stages.Add(new{stage=Stage,status="PASS"});await WriteStages();}
        catch(Exception error){Failure=FailureDiagnostics.Capture(error,"fixture-create",Stage,null,db,output);throw;}
        await FixtureRunSafety.State(output,Marker,"FIXTURE_COMMITTED");
    }

    private StockDocument Receipt(int item,string state,decimal quantity)
    {
        var doc=new StockDocument{DocumentNo=Marker+"REC"+item,DocumentType="RECEIPT",DestinationWarehouseId=Warehouse.WarehouseId,Status=state,CreatedBy=Users[0].UserId,ApprovedBy=Users[1].UserId,PostedBy=state=="POSTED"?Users[2].UserId:null,CreatedAt=Now,ApprovedAt=Now,PostedAt=state=="POSTED"?Now:null,DocumentDate=DateOnly.FromDateTime(Now)};
        doc.StockDocumentLines.Add(new(){LineNo=1,ItemId=Items[item].ItemId,UomId=Uom.UomId,ConversionFactor=1,RequestedQty=quantity,ActualQty=state=="POSTED"?quantity:0,BaseQty=state=="POSTED"?quantity:0});return doc;
    }
    private void Move(HotelOpsDbContext db,StockDocument document,decimal quantity)=>db.StockMovements.Add(new(){DocumentLineId=document.StockDocumentLines.Single().LineId,WarehouseId=Warehouse.WarehouseId,ItemId=document.StockDocumentLines.Single().ItemId,SignedBaseQty=quantity,CreatedBy=Users[2].UserId,OccurredAt=Now});
    private Task WriteManifest()=>FixtureRunSafety.Write(Path.Combine(output,"FIXTURE_MANIFEST.json"),new{Marker,users=UserIds,branches=new[]{Branch.BranchId,ForeignBranch.BranchId},roles=new[]{MainRole.RoleId,OwnRole.RoleId},warehouse=Warehouse.WarehouseId,newPermissionIds=NewPermissionIds,assetType=AssetType.AssetTypeId,category=Category.ItemCategoryId,uom=Uom.UomId,createdReferenceIds=references.CreatedIds,borrowedPriority=new{referenceId=Priority.ReferenceId,groupCode="PRIORITY",valueCode="NORMAL",createdByFixture=false},assets=Assets.ToDictionary(x=>x.Key,x=>x.Value.AssetId),incidents=Incidents.ToDictionary(x=>x.Key,x=>x.Value.IncidentId),works=Works.ToDictionary(x=>x.Key,x=>x.Value.MaintenanceWorkId),requests=Requests.ToDictionary(x=>x.Key,x=>x.Value.RequestId),replenishments=Replenishments.ToDictionary(x=>x.Key,x=>x.Value.ReplenishmentId),receipts=Receipts.ToDictionary(x=>x.Key,x=>x.Value.DocumentId),seeding="Isolated source-consistent preconditions; fixture preparation is not API acceptance",passwordPersisted=false});
}
