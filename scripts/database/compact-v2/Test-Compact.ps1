param(
    [Parameter(Mandatory = $true)][string]$Server,
    [ValidatePattern('^HotelOpsCompact[A-Za-z0-9_]{0,90}$')][string]$Database = 'HotelOpsCompactDB',
    [string]$OutputPath = 'artifacts/compact-v2/verification.json'
)
$ErrorActionPreference = 'Stop'
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$Server; $builder['Initial Catalog']=$Database
$builder['Integrated Security']=$true; $builder['Encrypt']=$true
$builder['TrustServerCertificate']=$true; $builder['Connect Timeout']=10
$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$results = New-Object 'System.Collections.Generic.List[object]'
$fixture = @'
SET NOCOUNT ON; SET XACT_ABORT ON;
DECLARE @b bigint,@b2 bigint,@u bigint,@u2 bigint,@dep bigint,@loc bigint,@loc2 bigint,
 @rt bigint,@room bigint,@room2 bigint,@clean bigint,@inspect bigint,@rework bigint,
 @ct bigint,@ci bigint,@wi bigint,@wh bigint,@wh2 bigint,@unit bigint,@item bigint,
 @doc bigint,@line bigint,@day bigint,@version binary(8);
INSERT dbo.branches(code,name) VALUES('QA_A',N'Isolated test A'); SET @b=SCOPE_IDENTITY();
INSERT dbo.branches(code,name) VALUES('QA_B',N'Isolated test B'); SET @b2=SCOPE_IDENTITY();
INSERT dbo.users(employee_code,username,full_name,employment_status,account_status)
 VALUES('QA_U1',N'qa_compact_u1',N'Test user 1','ACTIVE','INACTIVE'); SET @u=SCOPE_IDENTITY();
INSERT dbo.users(employee_code,username,full_name,employment_status,account_status)
 VALUES('QA_U2',N'qa_compact_u2',N'Test user 2','ACTIVE','INACTIVE'); SET @u2=SCOPE_IDENTITY();
INSERT dbo.departments(branch_id,code,name) VALUES(@b,'HK',N'Housekeeping'); SET @dep=SCOPE_IDENTITY();
INSERT dbo.locations(branch_id,kind,code,name) VALUES(@b,'FLOOR','F1',N'Floor 1'); SET @loc=SCOPE_IDENTITY();
INSERT dbo.locations(branch_id,kind,code,name) VALUES(@b2,'FLOOR','F1',N'Floor 1'); SET @loc2=SCOPE_IDENTITY();
INSERT dbo.room_types(branch_id,code,name,capacity) VALUES(@b,'STD',N'Standard',2); SET @rt=SCOPE_IDENTITY();
INSERT dbo.rooms(branch_id,location_id,room_type_id,code) VALUES(@b,@loc,@rt,'101'); SET @room=SCOPE_IDENTITY();
INSERT dbo.rooms(branch_id,location_id,room_type_id,code) VALUES(@b,@loc,@rt,'102'); SET @room2=SCOPE_IDENTITY();
INSERT dbo.checklist_templates(branch_id,code,version_no,name,kind,created_by)
 VALUES(@b,'INSPECT',1,N'Inspection','INSPECTION',@u); SET @ct=SCOPE_IDENTITY();
INSERT dbo.checklist_items(checklist_template_id,line_no,description) VALUES(@ct,1,N'Floor clean'); SET @ci=SCOPE_IDENTITY();
INSERT dbo.work_items(branch_id,work_no,kind,status,title,room_id,turnover_no,created_by)
 VALUES(@b,'C1','CLEANING','PENDING_ASSIGNMENT',N'Clean room',@room,0,@u); SET @clean=SCOPE_IDENTITY();
INSERT dbo.work_items(branch_id,work_no,kind,status,title,room_id,turnover_no,parent_work_id,checklist_template_id,created_by)
 VALUES(@b,'I1','INSPECTION','ASSIGNED',N'Inspect room',@room,0,@clean,@ct,@u); SET @inspect=SCOPE_IDENTITY();
INSERT dbo.work_items(branch_id,work_no,kind,status,title,room_id,turnover_no,parent_work_id,created_by)
 VALUES(@b,'R1','REWORK','PENDING_ACCEPTANCE',N'Rework room',@room,0,@clean,@u); SET @rework=SCOPE_IDENTITY();
INSERT dbo.work_checklist_items(branch_id,work_id,checklist_template_id,checklist_item_id,description_snapshot,required_snapshot,weight_snapshot)
 VALUES(@b,@inspect,@ct,@ci,N'Floor clean',1,1); SET @wi=SCOPE_IDENTITY();
INSERT dbo.warehouses(branch_id,code,name) VALUES(@b,'WH',N'Warehouse'); SET @wh=SCOPE_IDENTITY();
INSERT dbo.warehouses(branch_id,code,name) VALUES(@b2,'WH',N'Warehouse'); SET @wh2=SCOPE_IDENTITY();
INSERT dbo.reference_values(kind,code,name) VALUES('UOM','EACH',N'Each'); SET @unit=SCOPE_IDENTITY();
INSERT dbo.inventory_items(code,name,base_unit_id) VALUES('QA_ITEM',N'Test item',@unit); SET @item=SCOPE_IDENTITY();
INSERT dbo.inventory_documents(branch_id,document_no,kind,status,destination_warehouse_id,destination_branch_id,created_by)
 VALUES(@b,'REC1','RECEIPT','DRAFT',@wh,@b,@u); SET @doc=SCOPE_IDENTITY();
INSERT dbo.inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,actual_qty)
 VALUES(@b,@doc,1,@item,@unit,1,10); SET @line=SCOPE_IDENTITY();
INSERT dbo.work_assignments(branch_id,work_id,assignee_user_id,assigned_by) VALUES(@b,@clean,@u,@u2); SET @day=SCOPE_IDENTITY();
'@
$post = @'
UPDATE dbo.inventory_documents SET status='POSTED',posted_by=@u2,posted_at=SYSUTCDATETIME() WHERE document_id=@doc;
INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by)
 VALUES(@b,@line,@wh,@item,10,@u2);
'@
$cases = @(
 @{name='valid cleaning, inspection, checklist and rework';sql=@'
UPDATE dbo.rooms SET current_cleaning_work_id=@clean WHERE room_id=@room;
INSERT dbo.inspection_findings(branch_id,inspection_work_id,work_checklist_item_id,rework_work_id,description)
 VALUES(@b,@inspect,@wi,@rework,N'Dust on floor');
UPDATE dbo.work_checklist_items SET outcome='FAIL',checked_by=@u2,checked_at=SYSUTCDATETIME() WHERE work_checklist_item_id=@wi;
'@;error=0},
 @{name='cross-branch room location rejected';sql='UPDATE dbo.rooms SET location_id=@loc2 WHERE room_id=@room;';error=547},
 @{name='cross-branch employment rejected';sql="INSERT dbo.memberships(branch_id,user_id,department_id,kind) VALUES(@b2,@u,@dep,'EMPLOYMENT');";error=547},
 @{name='duplicate primary branch access rejected';sql="INSERT dbo.memberships(branch_id,user_id,kind,is_primary) VALUES(@b,@u,'BRANCH_ACCESS',1),(@b2,@u,'BRANCH_ACCESS',1);";error=2601},
 @{name='invalid cleaning workflow status rejected';sql="UPDATE dbo.work_items SET status='PASSED' WHERE work_id=@clean;";error=547},
 @{name='passed inspection without result rejected';sql="UPDATE dbo.work_items SET status='PASSED' WHERE work_id=@inspect;";error=547},
 @{name='inspection in different room rejected';sql='UPDATE dbo.work_items SET room_id=@room2 WHERE work_id=@inspect;';error=51013},
 @{name='changing cleaning cycle with children rejected';sql='UPDATE dbo.work_items SET turnover_no=1 WHERE work_id=@clean;';error=51013},
 @{name='room pointer cannot refer to inspection';sql='UPDATE dbo.rooms SET current_cleaning_work_id=@inspect WHERE room_id=@room;';error=51011},
 @{name='work kind cannot change';sql="UPDATE dbo.work_items SET kind='INCIDENT',status='OPEN' WHERE work_id=@clean;";error=51012},
 @{name='finding cannot refer to cleaning as inspection';sql="INSERT dbo.inspection_findings(branch_id,inspection_work_id,description) VALUES(@b,@clean,N'Invalid');";error=51014},
 @{name='duplicate active assignments rejected in a multi-row statement';sql="INSERT dbo.work_assignments(branch_id,work_id,assignee_user_id,assigned_by) VALUES(@b,@clean,@u,@u2),(@b,@clean,@u2,@u);";error=2601},
 @{name='duplicate open room blocks rejected';sql="INSERT dbo.room_blocks(branch_id,room_id,reason,blocked_by) VALUES(@b,@room,N'Block A',@u),(@b,@room,N'Block B',@u);";error=2601},
 @{name='event operation idempotency enforced';sql="DECLARE @key uniqueidentifier=NEWID(); INSERT dbo.work_events(branch_id,work_id,event_type,actor_user_id,operation_key) VALUES(@b,@clean,'START',@u,@key),(@b,@clean,'START',@u,@key);";error=2601},
 @{name='work history immutable';sql="INSERT dbo.work_events(branch_id,work_id,event_type,actor_user_id,operation_key) VALUES(@b,@clean,'START',@u,NEWID()); UPDATE dbo.work_events SET event_type='FAKE';";error=51021},
 @{name='audit history immutable';sql="INSERT dbo.audit_logs(actor_user_id,operation_key,entity_type,entity_key,action) VALUES(@u,NEWID(),'WORK','QA','CREATE'); DELETE dbo.audit_logs;";error=51021},
 @{name='published checklist items frozen';sql="UPDATE dbo.checklist_templates SET published_at=SYSUTCDATETIME() WHERE checklist_template_id=@ct; UPDATE dbo.checklist_items SET description=N'Changed' WHERE checklist_item_id=@ci;";error=51015},
 @{name='lookup category cannot replace a unit';sql="INSERT dbo.reference_values(kind,code,name) VALUES('ASSET_TYPE','QA_ASSET',N'Asset'); DECLARE @bad bigint=SCOPE_IDENTITY(); UPDATE dbo.inventory_items SET base_unit_id=@bad WHERE item_id=@item;";error=547},
 @{name='positive posted receipt and balance';sql=($post+"IF (SELECT base_qty FROM dbo.v_stock_balances WHERE warehouse_id=@wh AND item_id=@item)<>10 THROW 51990,'Incorrect stock balance.',1;");error=0},
 @{name='unposted ledger entry rejected';sql='INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@line,@wh,@item,10,@u);';error=51018},
 @{name='stock ledger update rejected';sql=($post+'UPDATE dbo.stock_movements SET signed_base_qty=999 WHERE line_id=@line;');error=51017},
 @{name='posted document line update rejected';sql=($post+'UPDATE dbo.inventory_document_lines SET actual_qty=999 WHERE line_id=@line;');error=51020},
 @{name='stock movement item identity checked';sql="INSERT dbo.inventory_items(code,name,base_unit_id) VALUES('QA_ITEM2',N'Other',@unit); DECLARE @other bigint=SCOPE_IDENTITY(); INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@line,@wh,@other,10,@u);";error=547},
 @{name='negative document quantity rejected';sql='UPDATE dbo.inventory_document_lines SET actual_qty=-1 WHERE line_id=@line;';error=547},
 @{name='zero conversion rejected';sql='UPDATE dbo.inventory_document_lines SET conversion_factor=0 WHERE line_id=@line;';error=547},
 @{name='self approval rejected';sql='UPDATE dbo.inventory_documents SET approved_by=@u,approved_at=SYSUTCDATETIME() WHERE document_id=@doc;';error=547},
 @{name='attachment requires exactly one typed owner';sql="INSERT dbo.attachments(branch_id,work_id,document_id,storage_key,original_name,mime_type,byte_length,sha256,uploaded_by) VALUES(@b,@clean,@doc,N'qa-key',N'qa.png','image/png',1,HASHBYTES('SHA2_256','qa'),@u);";error=547},
 @{name='notification recipient deduplication';sql="DECLARE @event uniqueidentifier=NEWID(); INSERT dbo.notifications(branch_id,recipient_user_id,event_key,notification_type,title,message) VALUES(@b,@u,@event,'ASSIGNMENT',N'Assigned',N'Test'),(@b,@u,@event,'ASSIGNMENT',N'Assigned',N'Test');";error=2601},
 @{name='stale rowversion cannot update room';sql="SELECT @version=row_version FROM dbo.rooms WHERE room_id=@room; UPDATE dbo.rooms SET code='101A' WHERE room_id=@room; UPDATE dbo.rooms SET code='101B' WHERE room_id=@room AND row_version=@version; IF @@ROWCOUNT<>0 THROW 51990,'Stale update succeeded.',1;";error=0},
 @{name='invalid material request status rejected';sql="UPDATE dbo.inventory_documents SET kind='MATERIAL_REQUEST',status='IN_TRANSIT' WHERE document_id=@doc;";error=547},
 @{name='posting marker requires posted status';sql="UPDATE dbo.inventory_documents SET posted_by=@u2,posted_at=SYSUTCDATETIME() WHERE document_id=@doc;";error=547},
 @{name='receipt quantity must match line';sql="UPDATE dbo.inventory_documents SET status='POSTED',posted_by=@u2,posted_at=SYSUTCDATETIME() WHERE document_id=@doc; INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@line,@wh,@item,999,@u2);";error=51022},
 @{name='insufficient stock issue rejected';sql="UPDATE dbo.inventory_documents SET kind='ISSUE',source_warehouse_id=@wh,destination_warehouse_id=NULL,destination_branch_id=NULL,status='POSTED',posted_by=@u2,posted_at=SYSUTCDATETIME() WHERE document_id=@doc; INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@line,@wh,@item,-10,@u2);";error=51023},
 @{name='exact reversal restores stock to zero';sql=($post+@'
DECLARE @original_movement bigint=SCOPE_IDENTITY(),@reversal_doc bigint,@reversal_line bigint;
INSERT dbo.inventory_documents(branch_id,document_no,kind,status,source_warehouse_id,source_document_id,created_by)
 VALUES(@b,'REV1','REVERSAL','DRAFT',@wh,@doc,@u2); SET @reversal_doc=SCOPE_IDENTITY();
INSERT dbo.inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,actual_qty,source_line_id)
 VALUES(@b,@reversal_doc,1,@item,@unit,1,10,@line); SET @reversal_line=SCOPE_IDENTITY();
UPDATE dbo.inventory_documents SET status='POSTED',posted_by=@u,posted_at=SYSUTCDATETIME() WHERE document_id=@reversal_doc;
INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,reversal_of_movement_id,created_by)
 VALUES(@b,@reversal_line,@wh,@item,-10,@original_movement,@u);
IF (SELECT base_qty FROM dbo.v_stock_balances WHERE warehouse_id=@wh AND item_id=@item)<>0 THROW 51990,'Reversal balance incorrect.',1;
'@);error=0}
)
$cases += . (Join-Path $PSScriptRoot 'Level2-Cases.ps1')
try {
    $connection.Open()
    $cmd=$connection.CreateCommand(); $cmd.CommandTimeout=30
    $cmd.CommandText="SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;"
    if ([int]$cmd.ExecuteScalar() -ne 37) { throw 'Expected exactly 37 tables.' }
    $cmd.CommandText="SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;"
    if ([int]$cmd.ExecuteScalar() -ne 0) { throw 'Disabled or untrusted foreign keys.' }
    $cmd.CommandText="SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1;"
    if ([int]$cmd.ExecuteScalar() -ne 0) { throw 'Disabled or untrusted checks.' }
    $cmd.CommandText="SELECT CONVERT(varchar(20),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema';"
    if ($cmd.ExecuteScalar() -ne '2.0.0') { throw 'Unexpected schema marker.' }
    $cmd.CommandText="SELECT COALESCE(SUM(p.rows),0) FROM sys.tables t JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0;"
    if ([long]$cmd.ExecuteScalar() -ne 0) { throw 'Tests require the empty independent database; refusing to touch existing data.' }
    foreach ($case in $cases) {
        $transaction=$connection.BeginTransaction()
        $cmd.Transaction=$transaction
        $passed=$false; $observed=0; $message=$null
        try {
            $cmd.CommandText=$fixture + "`n" + $case.sql
            [void]$cmd.ExecuteNonQuery()
            $passed=($case.error -eq 0)
        } catch [System.Data.SqlClient.SqlException] {
            $observed=$_.Exception.Number
            $passed=($observed -eq $case.error)
            if (-not $passed) { $message=$_.Exception.Message }
        } finally {
            try { $transaction.Rollback() } catch {
                # THROW under XACT_ABORT may already have rolled back the server transaction.
                $cmd.Transaction=$null; $cmd.CommandText='IF @@TRANCOUNT>0 ROLLBACK;'
                [void]$cmd.ExecuteNonQuery()
            }
            $transaction.Dispose(); $cmd.Transaction=$null
        }
        $results.Add([pscustomobject]@{name=$case.name;passed=$passed;expected_error=$case.error;observed_error=$observed;detail=$message})
        Write-Output ("{0}: {1}" -f $(if($passed){'PASS'}else{'FAIL'}),$case.name)
    }
    $cmd.CommandText="SELECT COALESCE(SUM(p.rows),0) FROM sys.tables t JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0;"
    $remainingRows=[long]$cmd.ExecuteScalar()
    if ($remainingRows -ne 0) { throw "Test rollback leaked $remainingRows rows." }
    $report=[ordered]@{database=$Database;verified_at_utc=[DateTime]::UtcNow.ToString('o');table_count=37;remaining_rows=$remainingRows;passed=@($results | Where-Object passed).Count;failed=@($results | Where-Object {-not $_.passed}).Count;tests=$results}
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),($report | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    Write-Output ("Result: {0} passed; {1} failed; {2} persisted fixture rows." -f $report.passed,$report.failed,$remainingRows)
    if ($report.failed -gt 0) { throw 'Compact database verification failed; see JSON report.' }
} finally { $connection.Dispose() }
