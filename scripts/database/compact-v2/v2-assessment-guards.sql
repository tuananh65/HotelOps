SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRANSACTION;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_supply_reservation_scope ON dbo.supply_reservations AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted r JOIN dbo.staff_supply_balances b ON b.balance_id=r.balance_id
 JOIN dbo.staff_supply_allocations a ON a.allocation_id=b.allocation_id JOIN dbo.work_items w ON w.work_id=r.work_id
 JOIN dbo.rooms room ON room.room_id=r.room_id
 WHERE w.kind<>''CLEANING'' OR w.branch_id<>a.branch_id OR room.branch_id<>a.branch_id OR w.room_id<>r.room_id
 OR (r.per_guest_quantity<>b.per_guest_quantity AND NOT EXISTS(
 SELECT 1 FROM dbo.supply_reservations prior JOIN dbo.staff_supply_balances old ON old.balance_id=prior.balance_id
 WHERE prior.work_id=r.work_id AND old.item_id=b.item_id AND prior.status=''RELEASED''
 AND prior.capacity=r.capacity AND prior.per_guest_quantity=r.per_guest_quantity AND prior.created_at<=r.created_at)))
 THROW 51505,''Reservation requires the task room, branch, and an original or inherited work snapshot.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.reservation_id=d.reservation_id
 WHERE d.status<>''RESERVED'' OR i.status NOT IN(''RESERVED'',''CONSUMED'',''RELEASED'')
 OR i.balance_id<>d.balance_id OR i.work_id<>d.work_id OR i.room_id<>d.room_id
 OR i.capacity<>d.capacity OR i.per_guest_quantity<>d.per_guest_quantity OR i.created_at<>d.created_at)
 THROW 51505,''Resolved reservations and snapshot identity are immutable.'',1;
 IF EXISTS(SELECT r.work_id,b.item_id FROM dbo.supply_reservations r JOIN dbo.staff_supply_balances b ON b.balance_id=r.balance_id
 WHERE r.status IN(''RESERVED'',''CONSUMED'') AND r.work_id IN(SELECT work_id FROM inserted)
 GROUP BY r.work_id,b.item_id HAVING COUNT(*)>1)
 THROW 51505,''A cleaning item can be reserved or consumed only once.'',1;
END;');
IF NOT EXISTS(SELECT 1 FROM dbo.audit_logs WHERE entity_type='V2_ASSESSMENT_GUARD' AND entity_key='261125-v1')
 INSERT dbo.audit_logs(entity_type,entity_key,action,operation_key,occurred_at,after_json)
 VALUES('V2_ASSESSMENT_GUARD','261125-v1','APPLIED',NEWID(),SYSUTCDATETIME(),'{"allowsAssessment":true,"preservesResolvedReservations":true,"inheritsWorkSnapshotOnReassignment":true}');
COMMIT;
