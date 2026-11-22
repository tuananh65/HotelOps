SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME() <> 'HotelOpsCompactDB' THROW 51500,'Unexpected database.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @operation uniqueidentifier=NEWID(), @before nvarchar(max), @after nvarchar(max);
 SELECT role_id INTO #targets FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE is_active=0;
 IF (SELECT COUNT(*) FROM #targets)<>2 OR EXISTS(SELECT 1 FROM dbo.roles r JOIN #targets t ON t.role_id=r.role_id WHERE r.code NOT IN('DEMO_MANAGER','DEMO_AUDITOR'))
  THROW 51501,'Inactive role set changed; inspect before applying.',1;
 IF EXISTS(SELECT 1 FROM dbo.user_roles u JOIN #targets t ON t.role_id=u.role_id WHERE u.revoked_at IS NULL)
  THROW 51502,'An inactive role still has an unrevoked assignment.',1;
 IF EXISTS(SELECT 1 FROM dbo.workday_shifts s JOIN #targets t ON t.role_id=s.role_id)
  THROW 51503,'An inactive role is referenced by workday shifts.',1;
 IF (SELECT COUNT(*) FROM dbo.user_roles u JOIN #targets t ON t.role_id=u.role_id)<>3 OR
    (SELECT COUNT(*) FROM dbo.role_permissions p JOIN #targets t ON t.role_id=p.role_id)<>47
  THROW 51504,'Dependent records changed.',1;
 SELECT u.* INTO #preservedAssignments FROM dbo.user_roles u WHERE NOT EXISTS(SELECT 1 FROM #targets t WHERE t.role_id=u.role_id);
 SELECT p.* INTO #preservedGrants FROM dbo.role_permissions p WHERE NOT EXISTS(SELECT 1 FROM #targets t WHERE t.role_id=p.role_id);
 DECLARE @users int=(SELECT COUNT(*) FROM dbo.users),@tables int=(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0);
 SET @before=(SELECT JSON_QUERY((SELECT role_id,code,name,description,is_active FROM dbo.roles ORDER BY role_id FOR JSON PATH)) roles,
 JSON_QUERY((SELECT u.* FROM dbo.user_roles u JOIN #targets t ON t.role_id=u.role_id FOR JSON PATH)) removed_assignments,
 JSON_QUERY((SELECT p.* FROM dbo.role_permissions p JOIN #targets t ON t.role_id=p.role_id FOR JSON PATH)) removed_grants FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 DELETE u FROM dbo.user_roles u JOIN #targets t ON t.role_id=u.role_id;
 DELETE p FROM dbo.role_permissions p JOIN #targets t ON t.role_id=p.role_id;
 DELETE r FROM dbo.roles r JOIN #targets t ON t.role_id=r.role_id;
 DECLARE @copy TABLE(code varchar(60) PRIMARY KEY,name nvarchar(150),description nvarchar(1000));
 INSERT @copy VALUES
 ('SYS_ADMIN',N'Quản trị hệ thống',N'Quản lý tài khoản, vai trò và cấu hình chung của hệ thống.'),
 ('RECEPTION',N'Lễ tân',N'Theo dõi tình trạng phòng và thực hiện các thao tác lễ tân được cấp quyền tại chi nhánh.'),
 ('HK_STAFF',N'Nhân viên buồng phòng',N'Thực hiện công việc dọn phòng được giao và cập nhật kết quả công việc.'),
 ('MAINTENANCE_STAFF',N'Nhân viên bảo trì',N'Tiếp nhận công việc bảo trì được giao và cập nhật tiến độ xử lý sự cố.'),
 ('STOREKEEPER',N'Thủ kho',N'Quản lý vật tư, nghiệp vụ kho và các yêu cầu vật tư trong phạm vi được cấp quyền.'),
 ('DEPT_MANAGER',N'Trưởng bộ phận',N'Theo dõi và điều phối công việc các bộ phận trong chi nhánh được giao.'),
 ('OPERATION_MANAGER',N'Quản lý vận hành',N'Điều phối hoạt động vận hành khách sạn và theo dõi công việc trong phạm vi được giao.'),
 ('HOUSEKEEPING_SUPERVISOR',N'Giám sát buồng phòng',N'Phân công dọn phòng, kiểm tra chất lượng và theo dõi công việc buồng phòng.'),
 ('MAINTENANCE_LEAD',N'Trưởng nhóm bảo trì',N'Điều phối nhóm bảo trì, phân công và theo dõi tiến độ xử lý sự cố.'),
 ('OWNER',N'Chủ sở hữu',N'Xem tổng quan, báo cáo và tình hình hoạt động của các chi nhánh được cấp quyền.');
 IF EXISTS(SELECT code FROM dbo.roles EXCEPT SELECT code FROM @copy) OR EXISTS(SELECT code FROM @copy EXCEPT SELECT code FROM dbo.roles)
  THROW 51505,'Active role set changed.',1;
 UPDATE r SET name=c.name,description=c.description FROM dbo.roles r JOIN @copy c ON c.code=r.code;
 IF EXISTS(SELECT * FROM #preservedAssignments EXCEPT SELECT * FROM dbo.user_roles) OR EXISTS(SELECT * FROM dbo.user_roles EXCEPT SELECT * FROM #preservedAssignments)
  THROW 51506,'Preserved assignments changed.',1;
 IF EXISTS(SELECT * FROM #preservedGrants EXCEPT SELECT * FROM dbo.role_permissions) OR EXISTS(SELECT * FROM dbo.role_permissions EXCEPT SELECT * FROM #preservedGrants)
  THROW 51507,'Preserved grants changed.',1;
 IF @users<>(SELECT COUNT(*) FROM dbo.users) OR @tables<>40 OR @tables<>(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)
  THROW 51508,'Database invariant changed.',1;
 SET @after=(SELECT JSON_QUERY((SELECT role_id,code,name,description,is_active FROM dbo.roles ORDER BY role_id FOR JSON PATH)) roles FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.audit_logs(operation_key,entity_type,entity_key,action,before_json,after_json)
 VALUES(@operation,'roles','inactive-role-cleanup-20261122','ROLE_CLEANUP',@before,@after);
 SELECT CONVERT(varchar(36),@operation) operation_key,2 deleted_roles,3 deleted_revoked_assignments,47 deleted_inactive_role_grants,
 (SELECT COUNT(*) FROM dbo.roles) remaining_roles,(SELECT COUNT(*) FROM dbo.role_permissions) remaining_grants,
 (SELECT COUNT(*) FROM dbo.user_roles) remaining_assignments,@users users,@tables tables,@apply applied;
 IF @apply=1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
