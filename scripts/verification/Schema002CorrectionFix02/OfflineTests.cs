using System.Text.Json;
using HotelOps.Schema002;
using HotelOps.Sql09;

if(args.Length!=3||args[0]!="--offline")throw new InvalidOperationException("Only --offline <repository root> <candidate directory> is permitted. No SQL execution switch exists.");
var root=Path.GetFullPath(args[1]);var candidate=Path.GetFullPath(args[2]);var checks=new List<string>();var factories=0;var h=new string('A',64);
void Check(string id,Action test){test();checks.Add(id);Console.WriteLine("NOSQL_PASS "+id);}
void Deny(Action action,string? reason=null){try{action();}catch(InvalidOperationException error)when(reason is null||error.Message.Contains(reason,StringComparison.Ordinal)){return;}throw new Exception("Expected fail-closed rejection "+reason);}
string Message(string index="UX_n01_operation_event_v1",string table="dbo.notification_publications")=>$"Cannot insert duplicate key row in object '{table}' with unique index '{index}'. The duplicate key value is (17, operation, event).";
NamedIndexContract Contract(string id="n01-duplicate-event")=>OfflineEvidence.Contract(id);
NamedIndexOutcome Outcome(NamedIndexContract c,string? message=null,int number=2601)
{var m=message??Message(c.ExpectedIndexName);return OfflineEvidence.Outcome(c,m,number); }
NamedIndexOutcome WithMessage(NamedIndexOutcome e,string message,int number=2601)=>OfflineEvidence.WithProvider(e with{Capture=e.Capture with{ErrorNumber=number,ErrorMessage=message}},[new(number,message)]);
void Require(NamedIndexContract c,NamedIndexOutcome e)=>NamedIndexEvidence.Require(c,e,OfflineEvidence.Trust);
var expected=Contract();var observed=Outcome(expected);
Check("T01_exact_index_table_error_rollback_all_three",()=>{foreach(var id in new[]{"n01-duplicate-event","legacy-duplicate-assignment","legacy-cross-family-duplicate"}){var c=Contract(id);Require(c,Outcome(c));}});
Check("T02_2601_wrong_index",()=>Deny(()=>Require(expected,WithMessage(observed,Message("UX_fs015_occurrence")))));
Check("T03_2627_wrong_constraint",()=>Deny(()=>Require(expected,WithMessage(observed,"Violation of UNIQUE KEY constraint 'PK_publication'. Cannot insert duplicate key in object 'dbo.notification_publications'. The duplicate key value is (17).",2627))));
Check("T04_2601_missing_identity",()=>Deny(()=>Require(expected,WithMessage(observed,"Duplicate key row"))));
Check("T05_2627_missing_identity",()=>Deny(()=>Require(expected,WithMessage(observed,"Duplicate key",2627))));
Check("T06_similar_prefix_suffix",()=>{foreach(var name in new[]{"XUX_n01_operation_event_v1","UX_n01_operation_event_v1_extra"})Deny(()=>Require(expected,WithMessage(observed,Message(name))));});
Check("T07_case_quoting_policy",()=>
{
    foreach(var m in new[]{Message("[UX_n01_operation_event_v1]","[dbo].[notification_publications]"),"Cannot insert duplicate key row in object [dbo].[notification_publications] with unique index [UX_n01_operation_event_v1]. The duplicate key value is (17).","Cannot insert duplicate key row in object \"dbo\".\"notification_publications\" with unique index \"UX_n01_operation_event_v1\". The duplicate key value is (17)."})Require(expected,WithMessage(observed,m));
    Deny(()=>Require(expected,WithMessage(observed,Message("ux_n01_operation_event_v1"))));
    Deny(()=>Require(expected,WithMessage(observed,Message(table:"DBO.notification_publications"))));
});
Check("T08_unexpected_SQL_error",()=>Deny(()=>Require(expected,WithMessage(observed,Message(),547))));
Check("T09_SQL1205_always_rejected",()=>Deny(()=>Require(expected,observed with{ProviderErrors=[observed.ProviderErrors[0],new(1205,"deadlock")]})));
Check("T10_no_duplicate_error",()=>Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ExecutionResult="NO_DUPLICATE_ERROR",ErrorNumber=null,ErrorMessage=null},ProviderErrors=[]})));
Check("T11_malformed_incomplete_evidence",()=>
{
    Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ErrorLine=null}}));
    Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ErrorMessage=""}}));
    Deny(()=>Require(expected,null!));
    Deny(()=>Require(expected,observed with{ProviderErrors=null!}));
});
Check("T12_SQL09_legacy_signature_and_known_valid_messages",()=>
{
    NamedIndexEvidence.Require(expected.ExpectedIndexName,[new(2601,"duplicate unique index 'UX_n01_operation_event_v1'")]);
    NamedIndexEvidence.Require(expected.ExpectedIndexName,[new(2601,Message())]);
    NamedIndexEvidence.Require("UX_fs009_publication_operation",[new(2627,"Violation of UNIQUE KEY constraint 'UX_fs009_publication_operation'. Cannot insert duplicate key in object 'dbo.notification_publications'. The duplicate key value is (17).")]);
});
var schemaSql=Path.Combine(root,".fe04schema002/worktree/scripts/database/notifications/Fe04Schema002");
Check("T13_exact_three_authoritative_probe_index_definitions",()=>
{
    using var plan=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(schemaSql,"13_SchemaProbePlan.json")));using var catalog=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(schemaSql,"07_ExpectedCatalog.json")));
    var forward=File.ReadAllText(Path.Combine(schemaSql,"02_ForwardCandidate.sql"));
    foreach(var id in new[]{"n01-duplicate-event","legacy-duplicate-assignment","legacy-cross-family-duplicate"})
    {
        if(plan.RootElement.GetProperty("cases").EnumerateArray().Count(c=>c.GetProperty("id").GetString()==id)!=1)throw new Exception("Case drift");
        var index=NamedIndexEvidence.ExpectedIndex(id);var row=catalog.RootElement.GetProperty("indexes").EnumerateArray().Single(i=>i.GetProperty("name").GetString()==index);
        var keys=row.GetProperty("columns").EnumerateArray().Select(e=>e.GetString()).ToArray();
        if(!keys.SequenceEqual(id=="n01-duplicate-event"?new[]{"actor_user_id","operation_key","event_type"}:new[]{"actor_user_id","operation_key"})||!forward.Contains("CREATE UNIQUE INDEX "+index+" ON dbo.notification_publications"))throw new Exception("Index drift");
        if(row.GetProperty("filter").GetString()!=(id=="n01-duplicate-event"?"inspection_id IS NOT NULL":"event_type<>'inspection.failed' AND event_type<>'cleaning.rework.requested'"))throw new Exception("Filter drift");
    }
});
void VerifyManifest(string dir,string manifest,string identity)
{
    using var m=JsonDocument.Parse(File.ReadAllBytes(manifest));var canonical=new System.Text.StringBuilder();
    foreach(var f in m.RootElement.GetProperty("files").EnumerateArray()){var p=f.GetProperty("path").GetString()!;var hash=f.GetProperty("sha256").GetString()!;if(!Hashes.Bytes(File.ReadAllBytes(Path.Combine(dir,p))).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new Exception("Frozen source drift "+p);canonical.Append(p).Append('\0').Append(hash).Append('\n');}
    if(Hashes.Utf8(canonical.ToString()).ToLowerInvariant()!=identity||m.RootElement.GetProperty("sourceIdentity").GetString()!=identity)throw new Exception("Identity drift");
}
Check("T14_original_SCHEMA002_identity_preserved",()=>VerifyManifest(Path.Combine(root,".fe04schema002/worktree"),Path.Combine(root,"docs/integration-notes/DEV2-NOTIF-FE04-SCHEMA-002/SCHEMA002_SOURCE_MANIFEST_SHA256.json"),"bdfbf02f3700f6a28abd18c19f7c368ce876f593fbe18d27f240586951ee2a9a"));
Check("T15_frozen_DECISION08_identity_preserved",()=>VerifyManifest(Path.Combine(root,".n01decision08/source"),Path.Combine(root,"docs/integration-notes/DEV2-FE05-N01-DECISION-08/DECISION08_SOURCE_MANIFEST_SHA256.json"),"8b241249b8a6b99f601ff35ee048528d0319eaf0f4d63565d758715021ab67e5"));
Check("T16_wrong_target_table",()=>Deny(()=>Require(expected,WithMessage(observed,Message(table:"dbo.unrelated")))));
Check("T17_correct_name_in_key_value_does_not_match",()=>Deny(()=>Require(expected,WithMessage(observed,"Cannot insert duplicate key row in object 'dbo.notification_publications' with unique index 'UX_fs015_occurrence'. The duplicate key value is ('UX_n01_operation_event_v1')."))));
Check("T18_unrelated_provider_constraint_error",()=>Deny(()=>Require(expected,observed with{ProviderErrors=[observed.ProviderErrors[0],new(547,"foreign key failure")]})));
Check("T19_multiple_duplicate_roots_are_ambiguous",()=>Deny(()=>Require(expected,observed with{ProviderErrors=[observed.ProviderErrors[0],new(2601,Message("UX_fs015_occurrence"))]})));
Check("T20_standard_statement_termination_is_allowed",()=>Require(expected,OfflineEvidence.WithProvider(observed,[observed.ProviderErrors[0],new(3621,"The statement has been terminated.")])));
Check("T21_unrelated_statement_termination_is_not_allowed",()=>Deny(()=>Require(expected,observed with{ProviderErrors=[observed.ProviderErrors[0],new(3621,"different error")]})));
Check("T22_error_class_must_be_index2601_even_if_name_matches2627",()=>Deny(()=>Require(expected,WithMessage(observed,"Violation of UNIQUE KEY constraint 'UX_n01_operation_event_v1'. Cannot insert duplicate key in object 'dbo.notification_publications'. The duplicate key value is (17).",2627))));
Check("T23_localized_or_truncated_text_fails_closed",()=>{Deny(()=>Require(expected,WithMessage(observed,"Không thể chèn khóa trùng 'UX_n01_operation_event_v1'")));Deny(()=>Require(expected,WithMessage(observed,Message()[..60])));});
Check("T24_wrong_probe_or_echo_labels",()=>{Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ProbeId="other"}}));Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ExpectedIndexName="other"}}));});
Check("T25_wrong_error_procedure_or_line",()=>{Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ErrorProcedure="unrelated_trigger"}}));Deny(()=>Require(expected,observed with{Capture=observed.Capture with{ErrorLine=0}}));});
Check("T26_provider_vs_SQL_capture_disagreement",()=>Deny(()=>Require(expected,observed with{ProviderErrors=[new(2601,Message("UX_fs009_publication_operation"))]})));
Check("T27_rollback_missing_or_transactions_open",()=>{Deny(()=>Require(expected,observed with{RolledBack=false}));Deny(()=>Require(expected,observed with{TransactionsAfter=1}));});
Check("T28_protected_digest_drift_or_missing",()=>{Deny(()=>Require(expected,observed with{AfterProtectedHash=new string('B',64)}));Deny(()=>Require(expected,observed with{BeforeProtectedHash=""}));});
Check("T29_fixture_ownership_receipt_mismatch",()=>Deny(()=>Require(expected,observed with{FixtureOwnershipReceiptSha256=new string('B',64)})));
Check("T30_SQL_state_policy",()=>{Require(expected,OfflineEvidence.WithProvider(observed with{Capture=observed.Capture with{XactStateAtCatch=1}},observed.ProviderErrors));Deny(()=>Require(expected,observed with{Capture=observed.Capture with{XactStateAtCatch=0}}));Deny(()=>Require(expected,observed with{Capture=observed.Capture with{TransactionsAtCatch=2}}));});
Check("T31_unknown_index_or_probe_contract",()=>{Deny(()=>NamedIndexEvidence.RequireContract(expected with{ExpectedIndexName="unknown"}));Deny(()=>NamedIndexEvidence.RequireContract(expected with{ProbeId="unknown"}));Deny(()=>NamedIndexEvidence.RequireContract(expected with{ExpectedErrorNumber=2627}));});
Dictionary<string,object?> Row(string type,int ordinal)
{
    var n01=type is "inspection.failed" or "cleaning.rework.requested";var rework=type is "REWORK_SUBMITTED" or "cleaning.rework.requested";
    return new(){["publication_id"]=Guid.Parse("00000000-0000-0000-0000-"+ordinal.ToString("D12")),["actor_user_id"]=17L,["operation_key"]=Guid.Parse("00000000-0000-0000-0000-000000000999"),["event_type"]=type,["schema_version"]=1,["assignment_id"]=n01||rework?null:100L+ordinal,["rework_id"]=rework?200L+ordinal:null,["inspection_id"]=n01?300L+ordinal:null,["payload_hash_hex"]=n01?h:null,["revision_hex"]="0000000000000001",["payload_json"]="{}",["occurrence_key"]=type=="CLEANING_TASK_ASSIGNED"?null:"OFFLINE_ONLY_"+ordinal};
}
string Rows(Dictionary<string,object?> a,Dictionary<string,object?> b)=>JsonSerializer.Serialize(new[]{a,b});
var rowsJson=Rows(Row("inspection.failed",1),Row("inspection.failed",2));
Check("T32_isolated_scenarios_all_three",()=>{NamedIndexEvidence.RequireFixture("n01-duplicate-event",rowsJson);NamedIndexEvidence.RequireFixture("n01-duplicate-event",Rows(Row("cleaning.rework.requested",1),Row("cleaning.rework.requested",2)));NamedIndexEvidence.RequireFixture("legacy-duplicate-assignment",Rows(Row("CLEANING_TASK_ASSIGNED",1),Row("CLEANING_TASK_ASSIGNED",2)));NamedIndexEvidence.RequireFixture("legacy-cross-family-duplicate",Rows(Row("CLEANING_TASK_COMPLETED",1),Row("REWORK_SUBMITTED",2)));});
Check("T33_unrelated_PK_or_occurrence_collision",()=>{var a=Row("inspection.failed",1);var b=Row("inspection.failed",2);b["publication_id"]=a["publication_id"];Deny(()=>NamedIndexEvidence.RequireFixture("n01-duplicate-event",Rows(a,b)));b=Row("inspection.failed",2);b["occurrence_key"]=a["occurrence_key"];Deny(()=>NamedIndexEvidence.RequireFixture("n01-duplicate-event",Rows(a,b)));});
Check("T34_unrelated_assignment_unique_collision",()=>{var a=Row("CLEANING_TASK_ASSIGNED",1);var b=Row("CLEANING_TASK_ASSIGNED",2);b["assignment_id"]=a["assignment_id"];Deny(()=>NamedIndexEvidence.RequireFixture("legacy-duplicate-assignment",Rows(a,b)));});
Check("T35_wrong_operation_or_event_scenario",()=>{var a=Row("inspection.failed",1);var b=Row("inspection.failed",2);b["operation_key"]=Guid.NewGuid();Deny(()=>NamedIndexEvidence.RequireFixture("n01-duplicate-event",Rows(a,b)));Deny(()=>NamedIndexEvidence.RequireFixture("legacy-cross-family-duplicate",Rows(Row("CLEANING_TASK_ASSIGNED",1),Row("CLEANING_TASK_ASSIGNED",2))));});
Check("T36_same_N01_source_would_collide_on_occurrence",()=>{var a=Row("inspection.failed",1);var b=Row("inspection.failed",2);b["inspection_id"]=a["inspection_id"];Deny(()=>NamedIndexEvidence.RequireFixture("n01-duplicate-event",Rows(a,b)));});
Check("T37_malformed_fixture_refused",()=>{Deny(()=>NamedIndexEvidence.RequireFixture("n01-duplicate-event","[]"));Deny(()=>NamedIndexEvidence.RequireFixture("n01-duplicate-event","bad json"));});
var script=Path.Combine(candidate,"14_NamedIndexProbe.sql");var sqlText=File.ReadAllText(script);
Check("T38_SQL_capture_ScriptDom_parse",()=>SqlReview.Parse(sqlText));
Check("T39_SQL_capture_has_all_fields_no_commit_or_nested_transaction",()=>
{
    foreach(var name in new[]{"ERROR_NUMBER()","ERROR_MESSAGE()","ERROR_PROCEDURE()","ERROR_LINE()","XACT_STATE()","@@TRANCOUNT","BEGIN TRY","BEGIN CATCH"})if(!sqlText.Contains(name))throw new Exception("Missing capture "+name);
    foreach(var word in new[]{"COMMIT;","BEGIN TRANSACTION","DELETE dbo.","CREATE UNIQUE INDEX","ALTER TABLE"})if(sqlText.Contains(word))throw new Exception("Unsafe scope "+word);
});
var scope=new ExecutionScope("OFFLINE_ONLY","OFFLINE_ONLY","OFFLINE_ONLY",Guid.NewGuid(),h,h,"unapproved","unapproved","unregistered","unleased",[expected.FixtureId]);
var executor=new Schema002Executor(schemaSql,()=>{factories++;throw new Exception("SQL factory must never be invoked");},new ClosedSchema002Authorization());
var bound=expected with{RowsSha256=Hashes.Utf8(rowsJson),CaptureSqlSha256=Hashes.Bytes(File.ReadAllBytes(script))};
Check("T40_closed_authority_before_connection",()=>Deny(()=>executor.ProbeNamedIndexAsync(scope,new(expected.ProbeId,expected.FixtureId,rowsJson,2601),bound,script,"not-read",h,"not-read",h,default).GetAwaiter().GetResult(),"SCHEMA002_RUNTIME_SCHEMA_LANE_LEASE_FIXTURES_NOT_ACCEPTED"));
Check("T41_legacy_numeric_only_duplicate_path_disabled",()=>{foreach(var id in new[]{expected.ProbeId,"alias-cannot-bypass-named-contract"})Deny(()=>executor.ProbeAsync(scope,new(id,expected.FixtureId,rowsJson,2601),"not-read",h,"not-read",h,default).GetAwaiter().GetResult(),"EXPLICIT_NAMED_INDEX_CONTRACT_REQUIRED");});
Check("T42_capture_hash_drift_before_connection",()=>Deny(()=>executor.ProbeNamedIndexAsync(scope,new(expected.ProbeId,expected.FixtureId,rowsJson,2601),bound with{CaptureSqlSha256=h},script,"not-read",h,"not-read",h,default).GetAwaiter().GetResult(),"CAPTURE_SCRIPT_HASH_MISMATCH"));
Check("T43_fixture_hash_drift_before_connection",()=>Deny(()=>executor.ProbeNamedIndexAsync(scope,new(expected.ProbeId,expected.FixtureId,rowsJson,2601),bound with{RowsSha256=h},script,"not-read",h,"not-read",h,default).GetAwaiter().GetResult(),"EXACT_NAMED_INDEX_FIXTURE_BINDING_REQUIRED"));
Check("T44_no_SQL_connection_factory_calls",()=>{if(factories!=0)throw new Exception("SQL factory invoked");});

Check("R01_missing_actual_receipt",()=>
{
    Deny(()=>Require(expected,observed with{FixtureReceipt=null}),"NOT_VERIFIED");
    try{ActualEvidence.RequireFixture(expected,null,OfflineEvidence.Trust);throw new Exception("Missing receipt accepted");}
    catch(InvalidOperationException error){using var report=JsonDocument.Parse((string)error.Data["FixtureReceiptVerificationJson"]!);if(report.RootElement.GetProperty("Status").GetString()!="NOT_VERIFIED"||report.RootElement.GetProperty("ActualSha256").ValueKind!=JsonValueKind.Null)throw new Exception("Unverified evidence mislabeled");}
    var verified=ActualEvidence.RequireFixture(expected,observed.FixtureReceipt,OfflineEvidence.Trust);
    if(verified.Status!="VERIFIED"||verified.ExpectedSha256!=expected.FixtureOwnershipReceiptSha256||verified.ActualSha256!=ActualEvidence.Hash(observed.FixtureReceipt!.PayloadJson)||verified.VerifiedIssuer!="OFFLINE_TEST_ISSUER")throw new Exception("Verification/provenance missing");
});
Check("R02_wrong_actual_receipt",()=>Deny(()=>Require(expected,observed with{FixtureReceipt=OfflineEvidence.Fixture(Contract("legacy-duplicate-assignment"))})));
Check("R03_expected_hash_alone_not_evidence",()=>Deny(()=>Require(expected,observed with{FixtureReceipt=null,FixtureOwnershipReceiptSha256=expected.FixtureOwnershipReceiptSha256})));
Check("R04_altered_actual_receipt",()=>{var altered=observed.FixtureReceipt! with{PayloadJson=observed.FixtureReceipt!.PayloadJson+" "};Deny(()=>Require(expected,observed with{FixtureReceipt=altered}));var hashMatched=expected with{FixtureOwnershipReceiptSha256=ActualEvidence.Hash(altered.PayloadJson)};Deny(()=>Require(hashMatched,observed with{FixtureReceipt=altered,FixtureOwnershipReceiptSha256=hashMatched.FixtureOwnershipReceiptSha256}));});
Check("R05_trusted_provenance_and_required_fields",()=>{Require(expected,observed);Deny(()=>NamedIndexEvidence.Require(expected,observed));Deny(()=>Require(expected,observed with{FixtureReceipt=observed.FixtureReceipt! with{KeyId="untrusted"}}));var receipt=JsonSerializer.Deserialize<OwnershipReceipt>(observed.FixtureReceipt!.PayloadJson)!;foreach(var bad in new[]{receipt with{FixtureId="other"},receipt with{Issuer=""},receipt with{ReceiptId=""},receipt with{ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(-1)},receipt with{CaptureSqlSha256=new string('B',64)}}){var actual=OfflineEvidence.Sign(bad,"SCHEMA002_FIXTURE_OWNERSHIP_V1");var c=expected with{FixtureOwnershipReceiptSha256=ActualEvidence.Hash(actual.PayloadJson)};Deny(()=>Require(c,observed with{FixtureReceipt=actual,FixtureOwnershipReceiptSha256=c.FixtureOwnershipReceiptSha256}));}});
Check("R06_multiple_and_unrelated_provider_errors",()=>{foreach(var errors in new ProviderError[][]{[observed.ProviderErrors[0],new(547,"FK")],[observed.ProviderErrors[0],new(2601,Message("other"))]})Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed,errors)));});
Check("R07_missing_provider_collection",()=>{Deny(()=>Require(expected,observed with{ProviderEvidence=null}),"NOT_AVAILABLE");Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed,[])));});
Check("R08_inconsistent_provider_number",()=>Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed,[new(2627,Message())]))));
Check("R09_wrong_provider_index",()=>Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed,[new(2601,Message("UX_fs009_publication_operation"))]))));
Check("R10_ambiguous_and_wrong_execution_provider_evidence",()=>{Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed,[observed.ProviderErrors[0],observed.ProviderErrors[0]])));Deny(()=>Require(expected,observed with{ExecutionId=Guid.NewGuid()}));var r=JsonSerializer.Deserialize<ProviderCollectionReceipt>(observed.ProviderEvidence!.PayloadJson)!;Deny(()=>Require(expected,observed with{ProviderEvidence=OfflineEvidence.Sign(r with{Source="SQL_TRY_CATCH_RECONSTRUCTION"},"SCHEMA002_PROVIDER_ERRORS_V1")}));});
Check("R11_provider_1205",()=>Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed,[observed.ProviderErrors[0],new(1205,"deadlock")]))));
Check("R12_expected_failure_not_raised",()=>Deny(()=>Require(expected,OfflineEvidence.WithProvider(observed with{Capture=observed.Capture with{ExecutionResult="NO_DUPLICATE_ERROR",ErrorNumber=null,ErrorMessage=null}},[]))));
Check("R13_exact_table_index_required",()=>{Deny(()=>Require(expected,WithMessage(observed,Message("UX_n01_operation_event_v1_suffix"))));Deny(()=>Require(expected,WithMessage(observed,Message(table:"dbo.other"))));});
Check("R14_three_mappings",()=>{if(NamedIndexEvidence.ExpectedIndex("n01-duplicate-event")!="UX_n01_operation_event_v1"||NamedIndexEvidence.ExpectedIndex("legacy-duplicate-assignment")!="UX_fs009_publication_operation"||NamedIndexEvidence.ExpectedIndex("legacy-cross-family-duplicate")!="UX_fs009_publication_operation")throw new Exception("Mapping drift");});
Check("R15_source_preservation",()=>{VerifyManifest(Path.Combine(root,".fe04schema002/worktree"),Path.Combine(root,"docs/integration-notes/DEV2-NOTIF-FE04-SCHEMA-002/SCHEMA002_SOURCE_MANIFEST_SHA256.json"),"bdfbf02f3700f6a28abd18c19f7c368ce876f593fbe18d27f240586951ee2a9a");VerifyManifest(Path.Combine(root,".n01decision08/source"),Path.Combine(root,"docs/integration-notes/DEV2-FE05-N01-DECISION-08/DECISION08_SOURCE_MANIFEST_SHA256.json"),"8b241249b8a6b99f601ff35ee048528d0319eaf0f4d63565d758715021ab67e5");});
Check("R16_legacy_API_compatibility_smoke_actual31_separate",()=>NamedIndexEvidence.Require("UX_n01_operation_event_v1",[new(2601,"duplicate unique index 'UX_n01_operation_event_v1'")]));

Console.WriteLine(JsonSerializer.Serialize(new{Status="NOSQL_PASS",Checks=checks.Count,SqlConnections=factories,StaticSql="STATIC_PASS",SQL="SQL_NOT_RUN",SchemaAcceptance=false,OwnerApproval="PENDING",PassedTestIds=checks}));
