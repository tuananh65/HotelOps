using HotelOps.Api.Services.Notifications;
if(args.Length!=1||args[0]!="--offline")throw new InvalidOperationException("Only explicit --offline permitted; no SQL adapter exists");
var checks=0;
void Check(string id,Action run){run();Console.WriteLine("NOSQL_PASS "+id);checks++;}
void Deny(Action run){try{run();}catch(InvalidOperationException){return;}throw new Exception("Expected rejection");}
// Explicitly synthetic policy verifies only pure preflight predicates, never owner authorization.
var p=new NotifWriter004AOwnerContract("TEST_ONLY_NOT_OWNER_ACCEPTED","TEST_ONLY_LOGIN","TEST_ONLY_USER","TEST_ONLY_AUTH","dbo.fs013_assign_with_evidence","CALLER_STATIC_DBO_CHAIN","TEST_ONLY_LEASE");
var now=NotifWriter004AContract.HardExpiry.AddMinutes(-1);
var o=new NotifWriter004AObservation(NotifWriter004AContract.Server,NotifWriter004AContract.Database,NotifWriter004AContract.DatabaseGuid,NotifWriter004AContract.Owner,p.LeaseId,NotifWriter004AContract.HardExpiry,true,p.Login,p.DatabaseUser,p.Authentication,p.Module,p.ExecutionContext,1,true,true,true,false,true);
Check("W01_exact_target_structural_only",()=>NotifWriter004AContract.Require(o,p,now));
Check("W02_wrong_database",()=>Deny(()=>NotifWriter004AContract.Require(o with{Database="HotelOpsDB"},p,now)));
Check("W03_wrong_actual_GUID",()=>Deny(()=>NotifWriter004AContract.Require(o with{DatabaseGuid=Guid.Empty},p,now)));
Check("W04_wrong_writer",()=>Deny(()=>NotifWriter004AContract.Require(o with{Login="other"},p,now)));
Check("W05_expired_extended_or_missing_lease",()=>{Deny(()=>NotifWriter004AContract.Require(o,p,NotifWriter004AContract.HardExpiry));Deny(()=>NotifWriter004AContract.Require(o with{LeaseExpiresUtc=o.LeaseExpiresUtc.AddTicks(1)},p,now));Deny(()=>NotifWriter004AContract.Require(o with{CentralLeaseHeld=false},p,now));});
Check("W06_transaction",()=>{Deny(()=>NotifWriter004AContract.Require(o with{TransactionCount=0},p,now));Deny(()=>NotifWriter004AContract.Require(o with{CallerOwnsTransaction=false},p,now));});
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
Check("W07_static_existing_DENY",()=>{var sql=File.ReadAllText(Path.Combine(root,"scripts/database/release/fs013-candidate/10_SqlWriteBoundary.sql"));foreach(var t in new[]{"notification_operation_reservations","notification_publications"})if(!sql.Contains("DENY INSERT,UPDATE,DELETE ON dbo."+t+" TO fs013_app"))throw new Exception("DENY drift");if(!sql.Contains("DENY INSERT,DELETE ON dbo.notification_delivery_intents TO fs013_app"))throw new Exception("Intent DENY drift");});
Check("W08_legacy_guard_not_widened",()=>{var guard=File.ReadAllText(Path.Combine(root,"src/HotelOps.Api/Data/IntegrationSqlGuard.cs"));if(!guard.Contains("KIENPC")||guard.Contains(NotifWriter004AContract.Database))throw new Exception("Legacy guard drift");});
Check("W09_wrong_owner",()=>Deny(()=>NotifWriter004AContract.Require(o with{Owner="other"},p,now)));
Check("W10_wrong_server",()=>Deny(()=>NotifWriter004AContract.Require(o with{Server="KIENPC"},p,now)));
Check("W11_privileged_fallback",()=>{Deny(()=>NotifWriter004AContract.Require(o with{IsPrivileged=true},p,now));Deny(()=>NotifWriter004AContract.Require(o with{Login="DESKTOP-JI8U50T\\hax"},p,now));});
Check("W12_module_context_source",()=>{Deny(()=>NotifWriter004AContract.Require(o with{Module="dbo.fs013_unapproved_writer_probe"},p,now));Deny(()=>NotifWriter004AContract.Require(o with{ExecutionContext="OWNER"},p,now));Deny(()=>NotifWriter004AContract.Require(o with{SessionAndSourceVerified=false},p,now));});
Check("W13_missing_auth_mapping_contract",()=>{Deny(()=>NotifWriter004AContract.Require(o,null,now));Deny(()=>NotifWriter004AContract.Require(o,p with{Authentication=""},now));Deny(()=>NotifWriter004AContract.Require(o with{DatabaseUser="fs013_app"},p,now));});
Check("W14_no_credential_or_grant_in_new_writer_source",()=>{var sql=File.ReadAllText(Path.Combine(root,"scripts/database/notifications/NotifWriter004A/ENVNOTIF001_RestrictedWriter_Provision.sql"));foreach(var line in sql.Split('\n').Select(x=>x.TrimStart()))if(line.StartsWith("GRANT ",StringComparison.OrdinalIgnoreCase)||line.StartsWith("CREATE LOGIN ",StringComparison.OrdinalIgnoreCase)||line.StartsWith("CREATE USER ",StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected executable provisioning");});
Console.WriteLine($"NOSQL_PASS Checks={checks}; SQL connections=0; production contract=BLOCKED; synthetic predicate tests are not owner acceptance");
