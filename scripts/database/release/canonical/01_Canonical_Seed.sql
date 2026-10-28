-- Executed only by Run-Canonical.ps1 after its target/instance guards.
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'
    THROW 51900, 'Canonical seed requires an isolated canonical database.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:Canonical:Seed',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51901,'Canonical seed lock unavailable.',1;
 -- Fail on conflicting existing data rather than silently renaming branches.
 IF EXISTS(SELECT 1 FROM dbo.branches WHERE branch_code NOT IN('BR001','BR002'))
    THROW 51902,'Existing branch data needs a separately reviewed conversion.',1;
 IF EXISTS(SELECT 1 FROM dbo.branches WHERE is_active=0
    OR (branch_code='BR001' AND branch_name<>N'HotelOps Branch 1')
    OR (branch_code='BR002' AND branch_name<>N'HotelOps Branch 2'))
    THROW 51903,'Canonical branch attributes conflict.',1;
 INSERT dbo.branches(branch_code,branch_name,timezone,is_active,created_at)
 SELECT s.code,s.name,N'Asia/Ho_Chi_Minh',1,SYSUTCDATETIME()
 FROM (VALUES('BR001',N'HotelOps Branch 1'),('BR002',N'HotelOps Branch 2')) s(code,name)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.branches b WHERE b.branch_code=s.code);
 IF EXISTS(SELECT 1 FROM dbo.departments WHERE department_code NOT IN('HK','MT','WH','FO') OR is_active=0)
    THROW 51904,'Existing department data needs separate review.',1;
 INSERT dbo.departments(branch_id,department_code,department_name,is_active)
 SELECT b.branch_id,d.code,d.name,1 FROM dbo.branches b CROSS JOIN
 (VALUES('HK',N'Housekeeping'),('MT',N'Maintenance'),('WH',N'Warehouse'),('FO',N'Front Office')) d(code,name)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.departments e WHERE e.branch_id=b.branch_id AND e.department_code=d.code);
 -- Codes observed in existing reference data and consumed by current services.
 DECLARE @refs TABLE(g varchar(60),v varchar(60),n nvarchar(150),s int);
 INSERT @refs VALUES
 ('PRIORITY','LOW',N'Thấp',1),('PRIORITY','NORMAL',N'Bình thường',2),('PRIORITY','HIGH',N'Cao',3),('PRIORITY','CRITICAL',N'Khẩn cấp',4),
 ('SEVERITY','MINOR',N'Nhẹ',1),('SEVERITY','MAJOR',N'Nghiêm trọng',2),('SEVERITY','CRITICAL',N'Rất nghiêm trọng',3),
 ('CLEANING_TYPE','STAYOVER',N'Dọn phòng lưu trú',2),('CLEANING_TYPE','CHECKOUT',N'Dọn phòng trả',1),
 ('CLEANING_TYPE','DEEP_CLEAN',N'Tổng vệ sinh',3),('CLEANING_TYPE','TURNDOWN',N'Dọn buổi tối',4),
 ('BLOCKER','GUEST_IN_ROOM',N'Khách còn trong phòng',1),('BLOCKER','MISSING_SUPPLIES',N'Thiếu vật tư',2),
 ('BLOCKER','EQUIPMENT_FAULT',N'Thiết bị hư hỏng',3),('BLOCKER','DND',N'Khách yêu cầu không làm phiền',4);
 IF EXISTS(SELECT 1 FROM dbo.reference_values e JOIN @refs s ON e.group_code=s.g AND e.value_code=s.v
    WHERE e.is_active=0 OR e.display_name<>s.n OR ISNULL(e.sort_order,-1)<>s.s)
    THROW 51905,'Existing reference attributes conflict.',1;
 INSERT dbo.reference_values(group_code,value_code,display_name,sort_order,is_active)
 SELECT g,v,n,s,1 FROM @refs r WHERE NOT EXISTS
 (SELECT 1 FROM dbo.reference_values e WHERE e.group_code=r.g AND e.value_code=r.v);
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
