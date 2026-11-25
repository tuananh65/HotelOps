-- Owner 25/11/2026: remove proven room-supply fixture AND MR440/ISSUE441.
-- Run only inside the reviewed outer upgrade transaction after a verified backup.
-- Archive remains in append-only audit; no account/catalog/work history is removed.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 THROW 51990,'Exactly one caller-owned cleanup transaction required.',1;
IF DB_NAME()<>@ExpectedV2Database OR
 (DB_NAME()<>'HotelOpsCompactDB' AND DB_NAME() NOT LIKE 'HotelOpsCompactWorkdayTestOfficial[0-9]%')
 THROW 51990,'Exact authorized cleanup database required.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>49
 THROW 51990,'Cleanup must precede V2 opening balance migration.',1;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1)
 THROW 51990,'Baseline contains disabled triggers.',1;
DECLARE @cleanupLock int,@lockRows bigint;
EXEC @cleanupLock=sys.sp_getapplock @Resource=N'HotelOps.OfficialV2Upgrade',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @cleanupLock<0 THROW 51990,'Cleanup lock unavailable.',1;
-- Physical table locks prevent application writers bypassing the migration applock.
SELECT @lockRows=COUNT_BIG(*) FROM dbo.inventory_documents WITH(TABLOCKX,HOLDLOCK);
SELECT @lockRows=COUNT_BIG(*) FROM dbo.inventory_document_lines WITH(TABLOCKX,HOLDLOCK);
SELECT @lockRows=COUNT_BIG(*) FROM dbo.stock_movements WITH(TABLOCKX,HOLDLOCK);
SELECT @lockRows=COUNT_BIG(*) FROM dbo.document_events WITH(TABLOCKX,HOLDLOCK);
SELECT @lockRows=COUNT_BIG(*) FROM dbo.notifications WITH(TABLOCKX,HOLDLOCK);
SELECT @lockRows=COUNT_BIG(*) FROM dbo.request_workflows WITH(TABLOCKX,HOLDLOCK);
SELECT @lockRows=COUNT_BIG(*) FROM dbo.request_workflow_events WITH(TABLOCKX,HOLDLOCK);
IF EXISTS(SELECT 1 FROM dbo.audit_logs WHERE operation_key='be99df91-a554-482a-b90c-ce71b5de6066')
 THROW 51990,'Cleanup already recorded; do not run twice.',1;

CREATE TABLE #CleanupDocuments(document_id bigint NOT NULL PRIMARY KEY);
INSERT #CleanupDocuments(document_id) SELECT d.document_id FROM dbo.inventory_documents d
WHERE d.document_no LIKE 'TEST-ROOM-261124-%'
 AND d.reason LIKE N'DỮ LIỆU KIỂM THỬ được người dùng duyệt 24/11/2026:%'
 AND EXISTS(SELECT 1 FROM dbo.audit_logs a WHERE a.entity_type='InventoryDocument'
  AND a.entity_key=CONVERT(nvarchar(150),d.document_id) AND a.action='AUTHORIZED_ROOM_TEST_DATA'
  AND JSON_VALUE(a.after_json,'$.synthetic')='true'
  AND JSON_VALUE(a.after_json,'$.authorization')='user-approved-20261124-room-supplies');
IF (SELECT COUNT(*) FROM #CleanupDocuments)<>194 THROW 51990,'Expected exactly 194 proven fixture documents.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.inventory_documents WHERE document_id=440 AND document_no='5ac4e9973fbf4548a8adabd14a4b938f' AND kind='MATERIAL_REQUEST' AND source_work_id=665)
 OR NOT EXISTS(SELECT 1 FROM dbo.inventory_documents WHERE document_id=441 AND document_no='bdb02db1f2fd4646b38b0a12c2b7f375' AND kind='ISSUE' AND source_document_id=440 AND source_work_id=665)
 THROW 51990,'Owner-confirmed room103 documents do not match.',1;
INSERT #CleanupDocuments VALUES(440),(441);
SELECT l.line_id INTO #CleanupLines FROM dbo.inventory_document_lines l JOIN #CleanupDocuments d ON d.document_id=l.document_id;
SELECT m.movement_id INTO #CleanupMovements FROM dbo.stock_movements m JOIN #CleanupLines l ON l.line_id=m.line_id;
IF EXISTS(SELECT 1 FROM dbo.inventory_documents d JOIN #CleanupDocuments s ON s.document_id=d.source_document_id WHERE NOT EXISTS(SELECT 1 FROM #CleanupDocuments x WHERE x.document_id=d.document_id))
 OR EXISTS(SELECT 1 FROM dbo.inventory_document_lines l JOIN #CleanupLines s ON s.line_id=l.source_line_id WHERE NOT EXISTS(SELECT 1 FROM #CleanupLines x WHERE x.line_id=l.line_id))
 OR EXISTS(SELECT 1 FROM dbo.stock_movements m JOIN #CleanupMovements s ON s.movement_id=m.reversal_of_movement_id WHERE NOT EXISTS(SELECT 1 FROM #CleanupMovements x WHERE x.movement_id=m.movement_id))
 THROW 51990,'Outside documents/lines/reversals depend on the selected fixture.',1;
IF EXISTS(SELECT 1 FROM dbo.attachments a JOIN #CleanupDocuments d ON d.document_id=a.document_id)
 OR EXISTS(SELECT 1 FROM dbo.supply_stock_documents a JOIN #CleanupDocuments d ON d.document_id=a.document_id)
 OR EXISTS(SELECT 1 FROM dbo.staff_supply_ledger a JOIN #CleanupLines l ON l.line_id=a.document_line_id)
 OR EXISTS(SELECT 1 FROM dbo.request_workflow_lines a JOIN #CleanupLines l ON l.line_id=a.document_line_id)
 THROW 51990,'Unexpected attachment/custody/request-line dependency; cleanup refused.',1;
IF (SELECT COUNT(*) FROM dbo.request_workflows w JOIN #CleanupDocuments d ON d.document_id=w.document_id)<>1
 OR NOT EXISTS(SELECT 1 FROM dbo.request_workflows WHERE work_id=666 AND document_id=440 AND status='COMPLETED')
 OR (SELECT COUNT(*) FROM dbo.request_workflow_events e JOIN #CleanupDocuments d ON d.document_id=e.related_document_id)<>4
 OR EXISTS(SELECT 1 FROM dbo.request_workflow_events e JOIN #CleanupDocuments d ON d.document_id=e.related_document_id
   WHERE e.related_document_id<>440 OR e.request_event_id NOT IN(7,8,33,36) OR e.work_id NOT IN(665,666))
 THROW 51990,'Request reference set changed; repeat cleanup review.',1;
IF EXISTS(SELECT 1 FROM dbo.stock_movements m WHERE NOT EXISTS(SELECT 1 FROM #CleanupMovements x WHERE x.movement_id=m.movement_id)
 GROUP BY m.warehouse_id,m.item_id HAVING SUM(m.signed_base_qty)<0)
 THROW 51990,'Removing fixture would make retained inventory negative.',1;

SELECT * INTO #CleanupKeepDocuments FROM dbo.inventory_documents d WHERE NOT EXISTS(SELECT 1 FROM #CleanupDocuments x WHERE x.document_id=d.document_id);
SELECT * INTO #CleanupKeepLines FROM dbo.inventory_document_lines l WHERE NOT EXISTS(SELECT 1 FROM #CleanupLines x WHERE x.line_id=l.line_id);
SELECT * INTO #CleanupKeepStock FROM dbo.stock_movements m WHERE NOT EXISTS(SELECT 1 FROM #CleanupMovements x WHERE x.movement_id=m.movement_id);
SELECT * INTO #CleanupKeepEvents FROM dbo.document_events e WHERE NOT EXISTS(SELECT 1 FROM #CleanupDocuments x WHERE x.document_id=e.document_id);
SELECT * INTO #CleanupKeepNotifications FROM dbo.notifications n WHERE NOT EXISTS(SELECT 1 FROM #CleanupDocuments x WHERE x.document_id=n.document_id);
SELECT * INTO #CleanupOriginalRequests FROM dbo.request_workflows;
SELECT * INTO #CleanupOriginalRequestEvents FROM dbo.request_workflow_events;
SELECT name,OBJECT_DEFINITION(object_id) definition INTO #CleanupTriggerDefinitions FROM sys.triggers WHERE parent_class=1;

DECLARE @cleanupArchive nvarchar(max),@cleanupManifest nvarchar(max);
SELECT @cleanupArchive=(SELECT
 JSON_QUERY((SELECT d.* FROM dbo.inventory_documents d JOIN #CleanupDocuments x ON x.document_id=d.document_id ORDER BY d.document_id FOR JSON PATH,INCLUDE_NULL_VALUES)) documents,
 JSON_QUERY((SELECT l.* FROM dbo.inventory_document_lines l JOIN #CleanupLines x ON x.line_id=l.line_id ORDER BY l.line_id FOR JSON PATH,INCLUDE_NULL_VALUES)) lines,
 JSON_QUERY((SELECT m.* FROM dbo.stock_movements m JOIN #CleanupMovements x ON x.movement_id=m.movement_id ORDER BY m.movement_id FOR JSON PATH,INCLUDE_NULL_VALUES)) movements,
 JSON_QUERY((SELECT e.* FROM dbo.document_events e JOIN #CleanupDocuments x ON x.document_id=e.document_id ORDER BY e.document_event_id FOR JSON PATH,INCLUDE_NULL_VALUES)) documentEvents,
 JSON_QUERY((SELECT n.* FROM dbo.notifications n JOIN #CleanupDocuments x ON x.document_id=n.document_id ORDER BY n.notification_id FOR JSON PATH,INCLUDE_NULL_VALUES)) notifications,
 JSON_QUERY((SELECT * FROM dbo.request_workflows WHERE work_id=666 FOR JSON PATH,INCLUDE_NULL_VALUES)) requestReferences,
 JSON_QUERY((SELECT * FROM dbo.request_workflow_events WHERE request_event_id IN(7,8,33,36) FOR JSON PATH,INCLUDE_NULL_VALUES)) requestEventReferences
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
IF ISJSON(@cleanupArchive)<>1 OR LEN(@cleanupArchive)<1000 THROW 51990,'Cleanup archive missing.',1;
SELECT @cleanupManifest=(SELECT 'OWNER_20261125_213950_REMOVE_PROVEN_TEST_AND_440_441' [authorization],
 JSON_QUERY((SELECT document_id FROM #CleanupDocuments ORDER BY document_id FOR JSON PATH)) removedDocuments,
 (SELECT COUNT(*) FROM #CleanupLines) removedLines,(SELECT COUNT(*) FROM #CleanupMovements) removedMovements,
 'Preserve original audits and work/request history; unlink removed test documents only' historyPolicy
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
INSERT dbo.audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json,occurred_at)
VALUES(NULL,'be99df91-a554-482a-b90c-ce71b5de6066','AUTHORIZED_TEST_CLEANUP','room-fixture-261124-and-440-441','ARCHIVED_AND_REMOVED',@cleanupArchive,@cleanupManifest,SYSUTCDATETIME());

-- Explicit one-off maintenance exception: only these four append-only/provenance
-- triggers need suspension. FK and all other business guards remain enabled.
IF (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_stock_movements_immutable','TR_document_events_immutable','TR_request_events_immutable','TR_request_workflow_provenance') AND is_disabled=0)<>4
 THROW 51990,'Expected cleanup guards missing.',1;
DISABLE TRIGGER dbo.TR_stock_movements_immutable ON dbo.stock_movements;
DISABLE TRIGGER dbo.TR_document_events_immutable ON dbo.document_events;
DISABLE TRIGGER dbo.TR_request_events_immutable ON dbo.request_workflow_events;
DISABLE TRIGGER dbo.TR_request_workflow_provenance ON dbo.request_workflows;
UPDATE dbo.request_workflows SET document_id=NULL WHERE work_id=666 AND document_id=440;
UPDATE dbo.request_workflow_events SET related_document_id=NULL WHERE request_event_id IN(7,8,33,36) AND related_document_id=440;
DELETE n FROM dbo.notifications n JOIN #CleanupDocuments d ON d.document_id=n.document_id;
DELETE e FROM dbo.document_events e JOIN #CleanupDocuments d ON d.document_id=e.document_id;
DELETE m FROM dbo.stock_movements m JOIN #CleanupMovements x ON x.movement_id=m.movement_id;
DELETE l FROM dbo.inventory_document_lines l JOIN #CleanupLines x ON x.line_id=l.line_id;
DELETE d FROM dbo.inventory_documents d JOIN #CleanupDocuments x ON x.document_id=d.document_id;
ENABLE TRIGGER dbo.TR_stock_movements_immutable ON dbo.stock_movements;
ENABLE TRIGGER dbo.TR_document_events_immutable ON dbo.document_events;
ENABLE TRIGGER dbo.TR_request_events_immutable ON dbo.request_workflow_events;
ENABLE TRIGGER dbo.TR_request_workflow_provenance ON dbo.request_workflows;

IF EXISTS(SELECT * FROM #CleanupKeepDocuments EXCEPT SELECT * FROM dbo.inventory_documents)
 OR EXISTS(SELECT * FROM dbo.inventory_documents EXCEPT SELECT * FROM #CleanupKeepDocuments)
 OR EXISTS(SELECT * FROM #CleanupKeepLines EXCEPT SELECT * FROM dbo.inventory_document_lines)
 OR EXISTS(SELECT * FROM dbo.inventory_document_lines EXCEPT SELECT * FROM #CleanupKeepLines)
 OR EXISTS(SELECT * FROM #CleanupKeepStock EXCEPT SELECT * FROM dbo.stock_movements)
 OR EXISTS(SELECT * FROM dbo.stock_movements EXCEPT SELECT * FROM #CleanupKeepStock)
 OR EXISTS(SELECT * FROM #CleanupKeepEvents EXCEPT SELECT * FROM dbo.document_events)
 OR EXISTS(SELECT * FROM dbo.document_events EXCEPT SELECT * FROM #CleanupKeepEvents)
 OR EXISTS(SELECT * FROM #CleanupKeepNotifications EXCEPT SELECT * FROM dbo.notifications)
 OR EXISTS(SELECT * FROM dbo.notifications EXCEPT SELECT * FROM #CleanupKeepNotifications)
 THROW 51990,'Cleanup changed an unrelated inventory/notification record.',1;
-- Compare all request columns except the specifically cleared nullable reference
-- and SQL rowversion; all transition content and original work statuses survive.
DECLARE @cleanupColumns nvarchar(max),@cleanupSql nvarchar(max);
SELECT @cleanupColumns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id)
 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.request_workflows') AND name NOT IN('document_id','row_version');
SET @cleanupSql=N'IF EXISTS(SELECT '+@cleanupColumns+N' FROM #CleanupOriginalRequests EXCEPT SELECT '+@cleanupColumns+N' FROM dbo.request_workflows) OR EXISTS(SELECT '+@cleanupColumns+N' FROM dbo.request_workflows EXCEPT SELECT '+@cleanupColumns+N' FROM #CleanupOriginalRequests) THROW 51990,''Unrelated request state changed.'',1;';
EXEC sys.sp_executesql @cleanupSql;
IF EXISTS(SELECT 1 FROM #CleanupOriginalRequests o JOIN dbo.request_workflows n ON n.work_id=o.work_id WHERE
 (o.work_id=666 AND n.document_id IS NOT NULL) OR (o.work_id<>666 AND ISNULL(n.document_id,-1)<>ISNULL(o.document_id,-1)))
 THROW 51990,'Unexpected request reference change.',1;
SELECT @cleanupColumns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id)
 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.request_workflow_events') AND name<>'related_document_id';
SET @cleanupSql=N'IF EXISTS(SELECT '+@cleanupColumns+N' FROM #CleanupOriginalRequestEvents EXCEPT SELECT '+@cleanupColumns+N' FROM dbo.request_workflow_events) OR EXISTS(SELECT '+@cleanupColumns+N' FROM dbo.request_workflow_events EXCEPT SELECT '+@cleanupColumns+N' FROM #CleanupOriginalRequestEvents) THROW 51990,''Request history changed.'',1;';
EXEC sys.sp_executesql @cleanupSql;
IF EXISTS(SELECT 1 FROM #CleanupOriginalRequestEvents o JOIN dbo.request_workflow_events n ON n.request_event_id=o.request_event_id WHERE
 (o.request_event_id IN(7,8,33,36) AND n.related_document_id IS NOT NULL) OR
 (o.request_event_id NOT IN(7,8,33,36) AND ISNULL(n.related_document_id,-1)<>ISNULL(o.related_document_id,-1)))
 THROW 51990,'Unexpected request event reference change.',1;
IF EXISTS(SELECT name,definition FROM #CleanupTriggerDefinitions EXCEPT SELECT name,OBJECT_DEFINITION(object_id) FROM sys.triggers WHERE parent_class=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1)
 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51990,'Database guards changed during cleanup.',1;
IF EXISTS(SELECT 1 FROM dbo.stock_movements GROUP BY warehouse_id,item_id HAVING SUM(signed_base_qty)<0)
 OR EXISTS(SELECT 1 FROM dbo.stock_movements WHERE item_id=222)
 THROW 51990,'Retained stock reconciliation failed.',1;
SELECT 'CLEANUP_VALIDATED_PENDING_OUTER_COMMIT' result,196 removed_documents,
 (SELECT COUNT(*) FROM #CleanupLines) removed_lines,(SELECT COUNT(*) FROM #CleanupMovements) removed_movements;
