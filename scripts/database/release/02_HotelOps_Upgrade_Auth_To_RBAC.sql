/* HotelOps - Upgrade Authentication -> RBAC (2026-10-21)
  SHARED TEMPLATE - NOT EXECUTABLE UNTIL THE TARGET IS REVIEWED.
  Replace __REVIEWED_TARGET_DATABASE__ at exactly three executable locations:
    USE, pre-step DB_NAME guard, transactional DB_NAME guard.
  Start with a disposable AUTH-READY backup clone, not the original HotelOpsDB.
  The expected target is deliberately repeated to fail closed across GO batches.
  No hard-coded employee IDs; manual review of branch DH/OM and legacy depts required.
  This script DOES NOT create branches/users or drop business tables.
  Data scopes and role catalog are normalized. Adding data_scope is a separate additive step; catalog changes are transactional.
  Run only after SQL Server backup; test on an isolated copy; requires downtime.
  The original 001 Authentication schema update MUST have run already.
  Not runtime-tested on your instance: static validation only.
  This file is a template, not a one-click universal migration. Different user/role
  assignments may require reviewed @SelectedDh choices and a separate conflict resolution.
  To upgrade a real client/production DB, require a separate migration review.
*/
USE [__REVIEWED_TARGET_DATABASE__];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
GO

/* Additive schema pre-step. SQL Server compiles column references per batch;
   scope column must exist before the next batch is compiled. On later errors,
   this column may remain even though the authorization-catalog transaction rolls back. */
IF DB_NAME() <> N'__REVIEWED_TARGET_DATABASE__'
    THROW 51700, 'Stop: selected database differs from reviewed target.', 1;
IF OBJECT_ID(N'dbo.role_permissions', N'U') IS NULL
    THROW 51720, 'Missing role_permissions table.', 1;
IF COL_LENGTH(N'dbo.role_permissions', N'data_scope') IS NULL
BEGIN
    ALTER TABLE dbo.role_permissions ADD data_scope VARCHAR(20) NOT NULL
      CONSTRAINT DF_role_permissions_data_scope DEFAULT ('BRANCH') WITH VALUES;
END;
GO

/* OPTIONAL human-reviewed DH selection if original DB has multiple DH per branch.
   Leave empty if there is exactly one active DH per branch.
   Examples for TEST SEED ONLY (do not paste as universal assignments):
     INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);
*/
BEGIN TRY
    IF DB_NAME()<>N'__REVIEWED_TARGET_DATABASE__' THROW 51701, 'Safety guard: incorrect reviewed target database.', 1;
    IF @@TRANCOUNT<>0 THROW 51702, 'Execute outside an existing transaction.', 1;
    IF OBJECT_ID(N'dbo.users',N'U') IS NULL OR OBJECT_ID(N'dbo.user_roles',N'U') IS NULL
       OR OBJECT_ID(N'dbo.roles',N'U') IS NULL OR OBJECT_ID(N'dbo.permissions',N'U') IS NULL
       OR OBJECT_ID(N'dbo.user_branch_access',N'U') IS NULL OR OBJECT_ID(N'dbo.role_permissions',N'U') IS NULL
        THROW 51703, 'Expected baseline RBAC/Auth tables are missing.', 1;
    IF COL_LENGTH(N'dbo.users',N'failed_login_count') IS NULL
       OR COL_LENGTH(N'dbo.users',N'locked_until') IS NULL
       OR COL_LENGTH(N'dbo.users',N'temporary_password_expires_at') IS NULL
       OR COL_LENGTH(N'dbo.user_sessions',N'session_key_hash') IS NULL
        THROW 51704, 'Authentication schema prerequisite missing. Apply approved Authentication update first.', 1;

    -- Choices are by real user ID, explicitly reviewed for EACH branch.
    DECLARE @SelectedDh TABLE(branch_id BIGINT PRIMARY KEY,user_id BIGINT NOT NULL);
    -- Optional reviewed mappings, example for disposable sample only:
    -- INSERT INTO @SelectedDh(branch_id,user_id) VALUES (1,4),(2,19);

    BEGIN TRANSACTION;

    -- Rename role codes without changing IDs, so historic user_roles FKs survive.
    DECLARE @RoleAliases TABLE(old_code VARCHAR(60) PRIMARY KEY,new_code VARCHAR(60) UNIQUE);
    INSERT @RoleAliases VALUES
      ('GENERAL_MANAGER','OPERATIONS_MANAGER'),
      ('HK_MANAGER','HK_SUPERVISOR'),
      ('MAINT_MANAGER','MAINTENANCE_LEAD'),
      ('TECHNICIAN','MAINTENANCE_STAFF'),
      ('WAREHOUSE_KEEPER','STOREKEEPER');

    IF EXISTS (
        SELECT 1 FROM @RoleAliases a JOIN dbo.roles r_old ON r_old.role_code=a.old_code
        JOIN dbo.roles r_new ON r_new.role_code=a.new_code
    ) THROW 51705, 'Both old and new role codes are present; alias merge requires manual review.', 1;

    UPDATE r SET r.role_code=a.new_code
    FROM dbo.roles r JOIN @RoleAliases a ON a.old_code=r.role_code;

    IF OBJECT_ID(N'dbo.user_roles',N'U') IS NULL
       OR (SELECT COUNT(*) FROM dbo.roles WHERE role_code='HK_SUPERVISOR')<>1
       OR (SELECT COUNT(*) FROM dbo.roles WHERE role_code='HK_INSPECTOR')<>1
        THROW 51706, 'Legacy HK roles do not match expected baseline.', 1;

    DECLARE @SupervisorId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='HK_SUPERVISOR');
    DECLARE @InspectorId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='HK_INSPECTOR');
    -- Avoid unique index conflicts; reviewer must resolve duplicate role assignments first.
    IF EXISTS (
      SELECT 1 FROM dbo.user_roles oldrole JOIN dbo.user_roles newer
        ON newer.user_id=oldrole.user_id AND newer.role_id=@SupervisorId
           AND (newer.branch_id=oldrole.branch_id OR (newer.branch_id IS NULL AND oldrole.branch_id IS NULL))
      WHERE oldrole.role_id=@InspectorId AND oldrole.is_active=1
    ) THROW 51707, 'Some Inspector users already have Supervisor grants in that branch; review duplicates.', 1;

    INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_by,assigned_at,expires_at,is_active)
    SELECT user_id,@SupervisorId,branch_id,assigned_by,SYSUTCDATETIME(),expires_at,1
    FROM dbo.user_roles WHERE role_id=@InspectorId AND is_active=1;
    UPDATE dbo.user_roles SET is_active=0 WHERE role_id=@InspectorId AND is_active=1;

    -- Preflight all current Department Head duplicates. Explicit choices required.
    DECLARE @DepartmentHeadRoleId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='DEPT_MANAGER');
    IF @DepartmentHeadRoleId IS NULL THROW 51708, 'DEPT_MANAGER baseline role missing.', 1;

    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur
       WHERE ur.role_id=@DepartmentHeadRoleId AND ur.is_active=1
       AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
       GROUP BY ur.branch_id HAVING COUNT(*)>1
         AND NOT EXISTS (SELECT 1 FROM @SelectedDh s WHERE s.branch_id=ur.branch_id)
    ) THROW 51709, 'Multiple DH per branch. Add reviewed branch/user choices in @SelectedDh; no automatic selection.', 1;

    IF EXISTS (
       SELECT 1 FROM @SelectedDh s WHERE NOT EXISTS (
          SELECT 1 FROM dbo.user_roles ur JOIN dbo.users u ON u.user_id=ur.user_id
          WHERE ur.role_id=@DepartmentHeadRoleId AND ur.user_id=s.user_id
            AND ur.branch_id=s.branch_id AND ur.is_active=1
            AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME())
            AND u.primary_branch_id=s.branch_id AND u.account_status='ACTIVE'
       )
    ) THROW 51710, 'Chosen DH must be active and assigned to the corresponding branch.', 1;

    UPDATE ur SET is_active=0 FROM dbo.user_roles ur JOIN @SelectedDh s ON s.branch_id=ur.branch_id
    WHERE ur.role_id=@DepartmentHeadRoleId AND ur.is_active=1 AND ur.user_id<>s.user_id;

    -- DH has branch-level oversight; detach head identity from line department.
    UPDATE u SET department_id=NULL FROM dbo.users u JOIN @SelectedDh s ON s.user_id=u.user_id
    WHERE u.primary_branch_id=s.branch_id AND u.department_id IS NOT NULL;

    -- Do not permit active users to possess DH and Storekeeper roles at once.
    IF EXISTS (
       SELECT 1 FROM dbo.user_roles dh JOIN dbo.user_roles wh
        ON wh.user_id=dh.user_id AND wh.branch_id=dh.branch_id
       JOIN dbo.roles wr ON wr.role_id=wh.role_id
       WHERE dh.role_id=@DepartmentHeadRoleId AND dh.is_active=1 AND wh.is_active=1
         AND wr.role_code='STOREKEEPER'
         AND (dh.expires_at IS NULL OR dh.expires_at>SYSUTCDATETIME())
         AND (wh.expires_at IS NULL OR wh.expires_at>SYSUTCDATETIME())
    ) THROW 51711, 'User is both DH and Storekeeper. Disable warehouse assignment after review.', 1;

    -- One OM may manage only one branch. Do not infer or revoke the wrong branch.
    DECLARE @OperationsId BIGINT=(SELECT role_id FROM dbo.roles WHERE role_code='OPERATIONS_MANAGER');
    IF @OperationsId IS NULL THROW 51712, 'Operations Manager role missing.', 1;
    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur
       WHERE ur.role_id=@OperationsId AND ur.is_active=1 AND ur.branch_id IS NULL
         AND NOT EXISTS (
            SELECT 1 FROM dbo.users u WHERE u.user_id=ur.user_id
            AND u.primary_branch_id IS NOT NULL
         )
    ) THROW 51713, 'OM role with NULL branch and unknown primary branch.', 1;
    UPDATE ur SET branch_id=u.primary_branch_id FROM dbo.user_roles ur
    JOIN dbo.users u ON ur.user_id=u.user_id
    WHERE ur.role_id=@OperationsId AND ur.branch_id IS NULL AND ur.is_active=1;

    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur JOIN dbo.user_branch_access uba ON uba.user_id=ur.user_id
       WHERE ur.role_id=@OperationsId AND ur.is_active=1 AND uba.revoked_at IS NULL
         AND uba.branch_id<>ur.branch_id AND uba.access_level='MANAGER'
    ) THROW 51714, 'OM has MANAGER access in another branch. Revoke after explicit review.', 1;

    IF EXISTS (
       SELECT 1 FROM dbo.user_roles ur
       WHERE ur.role_id IN (@OperationsId,@DepartmentHeadRoleId) AND ur.is_active=1
       GROUP BY ur.role_id,ur.branch_id HAVING COUNT(*)>1
    ) THROW 51715, 'More than one active OM or DH assignment in a branch.', 1;

    -- Existing departments outside HK, MT, WH, FO are NOT automatically removed.
    -- An account may still reference them; review + retire separately.

DECLARE @SeedRoles TABLE (role_code VARCHAR(60) NOT NULL PRIMARY KEY, role_name NVARCHAR(150) NOT NULL, description NVARCHAR(1000) NULL, is_system_role BIT NOT NULL, is_active BIT NOT NULL);

INSERT INTO @SeedRoles (role_code,role_name,description,is_system_role,is_active) VALUES
('SYS_ADMIN',N'Quản trị hệ thống',N'Quản trị user, role, danh mục, cấu hình và audit; không được mặc định vượt qua quyền tác nghiệp.',1,1),
('OPERATIONS_MANAGER',N'Quản lý vận hành',N'Quản lý vận hành một chi nhánh; xử lý escalation, tài sản, điều chỉnh hoặc đảo giao dịch kho theo phân nhiệm và audit.',1,1),
('HK_SUPERVISOR',N'Giám sát Buồng phòng',N'Phân công buồng phòng theo chi nhánh và ca; kiểm tra độc lập, không tự nghiệm thu công việc mình thực hiện.',0,1),
('HK_STAFF',N'Nhân viên buồng phòng',N'Dọn phòng, sửa lỗi và xử lý các nhiệm vụ được giao; quyền thao tác cá nhân theo OWN.',0,1),
('HK_INSPECTOR',N'Vai trò kiểm tra phòng cũ',N'Vai trò cũ ngừng hoạt động; người dùng được chuyển sang HK_SUPERVISOR có phân nhiệm độc lập.',0,0),
('MAINTENANCE_LEAD',N'Trưởng nhóm Bảo trì',N'Điều phối bảo trì theo ca, phân loại sự cố, nghiệm thu độc lập; không tự nghiệm thu phần đã thực hiện.',0,1),
('MAINTENANCE_STAFF',N'Nhân viên bảo trì',N'Thực hiện sửa chữa và nhật ký theo phân công; không có quyền tự nghiệm thu.',0,1),
('STOREKEEPER',N'Nhân viên kho',N'Ghi sổ phiếu nhập, xuất, trả, chuyển và kiểm kê theo phạm vi; không tự duyệt chênh lệch hoặc điều chỉnh/đảo kho.',0,1),
('DEPT_MANAGER',N'Department Head',N'Quản lý điều phối cả bốn bộ phận nghiệp vụ trong một cơ sở; ngang cấp Operations Manager, có quyền theo BRANCH hoặc OWN.',0,1),
('STAFF',N'Vai trò nhân viên cũ',N'Vai trò generic cũ ngừng hoạt động; không sử dụng cho tài khoản mới.',0,0),
('RECEPTION',N'Lễ tân',N'Ghi nhận check-in, check-out và đổi phòng; không trực tiếp ghi đè tình trạng phòng hoặc phát hành READY.',0,1),
('OWNER',N'Chủ sở hữu',N'Chỉ xem dashboard và báo cáo toàn hệ thống; không có quyền tác nghiệp.',1,1);

DECLARE @SeedPermissions TABLE (permission_code VARCHAR(100) NOT NULL PRIMARY KEY, permission_name NVARCHAR(150) NOT NULL, module_code VARCHAR(60) NOT NULL, resource_code VARCHAR(60) NOT NULL, action_code VARCHAR(40) NOT NULL, description NVARCHAR(MAX) NULL);

INSERT INTO @SeedPermissions (permission_code,permission_name,module_code,resource_code,action_code,description) VALUES
('USER_VIEW',N'Xem người dùng','ADMIN','user','view',N'Xem người dùng'),
('USER_MANAGE',N'Quản lý người dùng','ADMIN','user','manage',N'Quản lý người dùng'),
('ROLE_MANAGE',N'Quản lý vai trò và phân quyền','ADMIN','role','manage',N'Quản lý vai trò và phân quyền'),
('MASTER_DATA_MANAGE',N'Quản lý danh mục nền (chi nhánh, tầng, khu vực, phòng, bộ phận)','ADMIN','master_data','manage',N'Quản lý danh mục nền (chi nhánh, tầng, khu vực, phòng, bộ phận)'),
('SETTINGS_MANAGE',N'Quản lý cấu hình hệ thống','ADMIN','setting','manage',N'Quản lý cấu hình hệ thống'),
('AUDIT_VIEW',N'Xem nhật ký kiểm toán','ADMIN','audit_log','view',N'Xem nhật ký kiểm toán'),
('ROOM_VIEW',N'Xem phòng','ROOM','room','view',N'Xem phòng'),
('ROOM_STATUS_UPDATE',N'Điều chỉnh trạng thái phòng thủ công (ngoại lệ, sửa sai)','ROOM','room','update_status',N'Điều chỉnh trạng thái phòng thủ công (ngoại lệ, sửa sai)'),
('ROOM_CHECKOUT',N'Ghi nhận trả phòng (OCCUPIED → DIRTY)','ROOM','room','checkout',N'Lễ tân ghi nhận khách trả phòng; phòng chuyển OCCUPIED → DIRTY'),
('ROOM_BLOCK_MANAGE',N'Khóa / mở khóa phòng','ROOM','room_block','manage',N'Khóa / mở khóa phòng'),
('CLEANING_VIEW',N'Xem công việc dọn phòng','HOUSEKEEPING','cleaning_task','view',N'Xem công việc dọn phòng'),
('CLEANING_ASSIGN',N'Tạo, giao, giao lại, hủy công việc dọn phòng','HOUSEKEEPING','cleaning_task','assign',N'Tạo, giao, giao lại, hủy công việc dọn phòng'),
('CLEANING_EXECUTE',N'Thực hiện dọn phòng (bắt đầu, tiến độ, hoàn tất)','HOUSEKEEPING','cleaning_task','execute',N'Thực hiện dọn phòng (bắt đầu, tiến độ, hoàn tất)'),
('INSPECTION_VIEW',N'Xem kiểm tra phòng','INSPECTION','room_inspection','view',N'Xem kiểm tra phòng'),
('INSPECTION_PERFORM',N'Thực hiện kiểm tra phòng, tạo lỗi và yêu cầu làm lại','INSPECTION','room_inspection','perform',N'Thực hiện kiểm tra phòng, tạo lỗi và yêu cầu làm lại'),
('CHECKLIST_MANAGE',N'Quản lý checklist kiểm tra','INSPECTION','checklist','manage',N'Quản lý checklist kiểm tra'),
('DEFECT_RESOLVE',N'Xử lý lỗi kiểm tra / làm lại','INSPECTION','defect','resolve',N'Xử lý lỗi kiểm tra / làm lại'),
('INCIDENT_VIEW',N'Xem sự cố','INCIDENT','incident','view',N'Xem sự cố'),
('INCIDENT_CREATE',N'Báo cáo sự cố','INCIDENT','incident','create',N'Báo cáo sự cố'),
('INCIDENT_MANAGE',N'Phân công, đổi trạng thái, đóng sự cố','INCIDENT','incident','manage',N'Phân công, đổi trạng thái, đóng sự cố'),
('MAINTENANCE_VIEW',N'Xem công việc bảo trì','MAINTENANCE','maintenance_work','view',N'Xem công việc bảo trì'),
('MAINTENANCE_EXECUTE',N'Thực hiện bảo trì, ghi nhật ký','MAINTENANCE','maintenance_work','execute',N'Thực hiện bảo trì, ghi nhật ký'),
('MAINTENANCE_REVIEW',N'Nghiệm thu / đóng công việc bảo trì','MAINTENANCE','maintenance_work','review',N'Nghiệm thu / đóng công việc bảo trì'),
('PM_MANAGE',N'Quản lý bảo trì định kỳ','MAINTENANCE','pm_schedule','manage',N'Quản lý bảo trì định kỳ'),
('ASSET_VIEW',N'Xem tài sản','ASSET','asset','view',N'Xem tài sản'),
('ASSET_MANAGE',N'Quản lý tài sản (điều chuyển, bàn giao, tình trạng)','ASSET','asset','manage',N'Quản lý tài sản (điều chuyển, bàn giao, tình trạng)'),
('INVENTORY_VIEW',N'Xem tồn kho và phiếu kho','INVENTORY','stock','view',N'Xem tồn kho và phiếu kho'),
('INVENTORY_POST',N'Lập và ghi sổ phiếu kho','INVENTORY','stock_document','post',N'Lập và ghi sổ phiếu kho'),
('STOCK_APPROVE',N'Duyệt phiếu kho và kết quả kiểm kê','INVENTORY','stock_document','approve',N'Duyệt phiếu kho và kết quả kiểm kê'),
('STOCKTAKE_MANAGE',N'Thực hiện kiểm kê','INVENTORY','stocktake','manage',N'Thực hiện kiểm kê'),
('ITEM_MASTER_MANAGE',N'Quản lý danh mục vật tư, đơn vị, ngưỡng tồn','INVENTORY','item','manage',N'Quản lý danh mục vật tư, đơn vị, ngưỡng tồn'),
('MR_CREATE',N'Tạo yêu cầu vật tư','INVENTORY','material_request','create',N'Tạo yêu cầu vật tư'),
('MR_APPROVE_L1',N'Duyệt yêu cầu vật tư cấp 1','INVENTORY','material_request','approve_l1',N'Duyệt yêu cầu vật tư cấp 1'),
('MR_APPROVE_L2',N'Duyệt yêu cầu vật tư cấp 2','INVENTORY','material_request','approve_l2',N'Duyệt yêu cầu vật tư cấp 2'),
('MR_FULFILL',N'Cấp phát vật tư','INVENTORY','material_fulfillment','fulfill',N'Cấp phát vật tư'),
('IR_CREATE',N'Tạo yêu cầu nội bộ','REQUEST','internal_request','create',N'Tạo yêu cầu nội bộ'),
('IR_PROCESS',N'Tiếp nhận, giao và xử lý yêu cầu nội bộ','REQUEST','internal_request','process',N'Tiếp nhận, giao và xử lý yêu cầu nội bộ'),
('TASK_VIEW',N'Xem công việc vận hành','TASK','operational_task','view',N'Xem công việc vận hành'),
('TASK_MANAGE',N'Tạo, giao, hủy công việc vận hành','TASK','operational_task','manage',N'Tạo, giao, hủy công việc vận hành'),
('TASK_EXECUTE',N'Cập nhật tiến độ công việc được giao','TASK','operational_task','execute',N'Cập nhật tiến độ công việc được giao'),
('SHIFT_VIEW',N'Xem ca làm việc','SHIFT','work_shift','view',N'Xem ca làm việc'),
('SHIFT_MANAGE',N'Xếp ca, phân công ca, xác nhận chấm công','SHIFT','work_shift','manage',N'Xếp ca, phân công ca, xác nhận chấm công'),
('ATTENDANCE_RECORD',N'Chấm công vào/ra','SHIFT','attendance','record',N'Chấm công vào/ra'),
('DASHBOARD_VIEW',N'Xem dashboard','MONITOR','dashboard','view',N'Xem dashboard'),
('REPORT_VIEW',N'Xem báo cáo','MONITOR','report','view',N'Xem báo cáo'),
('ROOM_CHECKIN',N'Ghi nhận khách nhận phòng (AVAILABLE → OCCUPIED)','ROOM','room','checkin',N'Lễ tân ghi nhận khách nhận phòng; phòng chuyển AVAILABLE → OCCUPIED'),
('ROOM_RELEASE',N'Phát hành phòng sẵn sàng (READY → AVAILABLE)','ROOM','room','release',N'Lễ tân xác nhận phòng READY đủ điều kiện đưa vào sử dụng; chuyển READY → AVAILABLE'),
('ASSET_RETIRE',N'Thanh lý hoặc ngừng quản lý tài sản','ASSET','asset','retire',N'OM chỉ thanh lý khi kiểm tra đủ điều kiện và không còn phụ thuộc mở.'),
('ASSET_TRANSFER',N'Bàn giao và điều chuyển tài sản','ASSET','asset','transfer',N'OM điều chuyển tài sản trong phạm vi cho phép, ghi biên bản và audit.'),
('INCIDENT_ESCALATION_HANDLE',N'Xử lý sự cố được leo thang','INCIDENT','incident','handle_escalation',N'OM xử lý escalation trong chi nhánh, không tự xác minh sửa chữa.'),
('IR_OVERDUE_HANDLE',N'Xử lý yêu cầu nội bộ quá hạn','REQUEST','internal_request','handle_overdue',N'OM xử lý quá hạn trong chi nhánh theo trạng thái và quy tắc nghiệp vụ.'),
('MR_APPROVE_FALLBACK',N'Duyệt yêu cầu vật tư dự phòng','INVENTORY','material_request','approve_fallback',N'Chỉ OM được duyệt khi DH là requester; người duyệt phải khác requester.'),
('STOCK_ADJUST_POST',N'Đăng điều chỉnh tồn kho','INVENTORY','stock_adjustment','post',N'OM chỉ đăng điều chỉnh đã có đề nghị, đúng chi nhánh, khác người lập.'),
('STOCK_REVERSE',N'Đảo giao dịch kho đã đăng','INVENTORY','stock_transaction','reverse',N'Chỉ đảo chứng từ hợp lệ, đúng chi nhánh, có lý do và kiểm tra tồn.'),
('INCIDENT_TRIAGE',N'Phân loại và điều phối sự cố kỹ thuật','INCIDENT','incident','triage',N'Lead phân loại/điều phối sự cố kỹ thuật trong chi nhánh; sự cố nghiêm trọng chuyển OM.'),
('MAINTENANCE_ASSIGN',N'Phân công công việc bảo trì','MAINTENANCE','maintenance_work','assign',N'Lead phân công hoặc điều phối công việc MT đúng chi nhánh và ca; kiểm tra người được giao.'),
('STOCK_ISSUE_POST',N'Ghi sổ phiếu xuất kho','INVENTORY','stock_document','post_issue',N'Storekeeper ghi sổ xuất kho theo yêu cầu đã duyệt, trong giới hạn tồn.'),
('STOCK_RECEIPT_POST',N'Ghi sổ phiếu nhập kho','INVENTORY','stock_document','post_receipt',N'Storekeeper ghi sổ phiếu nhập hợp lệ trong kho thuộc chi nhánh được cấp.'),
('STOCK_RETURN_POST',N'Ghi sổ phiếu trả kho','INVENTORY','stock_document','post_return',N'Storekeeper ghi sổ phiếu trả kho hợp lệ theo chứng từ nguồn.'),
('STOCK_TRANSFER_POST',N'Ghi sổ phiếu chuyển kho','INVENTORY','stock_document','post_transfer',N'Storekeeper chuyển kho theo quy trình, không vượt phạm vi được phép.'),
('STOCKTAKE_PERFORM',N'Thực hiện kiểm kê kho','INVENTORY','stocktake','perform',N'Chuẩn bị, ghi số đếm và gửi kết quả kiểm kê; không tự duyệt chênh lệch.'),
('ROOM_OCCUPANCY_MOVE',N'Ghi nhận chuyển phòng','ROOM','room_occupancy','move',N'Reception ghi nhận chuyển occupancy từ phòng nguồn sang phòng đích trong cùng chi nhánh; không ghi đè room readiness. Backend phải kiểm tra điều kiện và thực hiện giao dịch nguyên tử.');

DECLARE @SeedGrants TABLE (role_code VARCHAR(60) NOT NULL,permission_code VARCHAR(100) NOT NULL,data_scope VARCHAR(20) NOT NULL,PRIMARY KEY(role_code,permission_code));

INSERT INTO @SeedGrants (role_code,permission_code,data_scope) VALUES
('SYS_ADMIN','USER_VIEW','ALL'),
('SYS_ADMIN','USER_MANAGE','ALL'),
('SYS_ADMIN','ROLE_MANAGE','ALL'),
('SYS_ADMIN','MASTER_DATA_MANAGE','ALL'),
('SYS_ADMIN','SETTINGS_MANAGE','ALL'),
('SYS_ADMIN','AUDIT_VIEW','ALL'),
('OPERATIONS_MANAGER','ROOM_VIEW','BRANCH'),
('OPERATIONS_MANAGER','CLEANING_VIEW','BRANCH'),
('OPERATIONS_MANAGER','INSPECTION_VIEW','BRANCH'),
('OPERATIONS_MANAGER','INCIDENT_VIEW','BRANCH'),
('OPERATIONS_MANAGER','MAINTENANCE_VIEW','BRANCH'),
('OPERATIONS_MANAGER','ASSET_VIEW','BRANCH'),
('OPERATIONS_MANAGER','INVENTORY_VIEW','BRANCH'),
('OPERATIONS_MANAGER','IR_CREATE','BRANCH'),
('OPERATIONS_MANAGER','TASK_VIEW','BRANCH'),
('OPERATIONS_MANAGER','SHIFT_VIEW','BRANCH'),
('OPERATIONS_MANAGER','DASHBOARD_VIEW','BRANCH'),
('OPERATIONS_MANAGER','REPORT_VIEW','BRANCH'),
('HK_SUPERVISOR','ROOM_VIEW','BRANCH'),
('HK_SUPERVISOR','CLEANING_VIEW','BRANCH'),
('HK_SUPERVISOR','CLEANING_ASSIGN','BRANCH'),
('HK_SUPERVISOR','INSPECTION_VIEW','BRANCH'),
('HK_SUPERVISOR','INSPECTION_PERFORM','BRANCH'),
('HK_SUPERVISOR','INCIDENT_VIEW','BRANCH'),
('HK_SUPERVISOR','INCIDENT_CREATE','BRANCH'),
('HK_SUPERVISOR','MR_CREATE','OWN'),
('HK_SUPERVISOR','IR_CREATE','OWN'),
('HK_SUPERVISOR','TASK_VIEW','BRANCH'),
('HK_SUPERVISOR','TASK_EXECUTE','OWN'),
('HK_SUPERVISOR','SHIFT_VIEW','BRANCH'),
('HK_SUPERVISOR','ATTENDANCE_RECORD','OWN'),
('HK_STAFF','ROOM_VIEW','BRANCH'),
('HK_STAFF','CLEANING_VIEW','OWN'),
('HK_STAFF','CLEANING_EXECUTE','OWN'),
('HK_STAFF','DEFECT_RESOLVE','OWN'),
('HK_STAFF','INCIDENT_VIEW','OWN'),
('HK_STAFF','INCIDENT_CREATE','BRANCH'),
('HK_STAFF','MR_CREATE','OWN'),
('HK_STAFF','IR_CREATE','OWN'),
('HK_STAFF','IR_PROCESS','OWN'),
('HK_STAFF','TASK_VIEW','OWN'),
('HK_STAFF','TASK_EXECUTE','OWN'),
('HK_STAFF','SHIFT_VIEW','OWN'),
('HK_STAFF','ATTENDANCE_RECORD','OWN'),
('MAINTENANCE_LEAD','ROOM_VIEW','BRANCH'),
('MAINTENANCE_LEAD','ROOM_BLOCK_MANAGE','BRANCH'),
('MAINTENANCE_LEAD','INCIDENT_VIEW','BRANCH'),
('MAINTENANCE_LEAD','INCIDENT_CREATE','BRANCH'),
('MAINTENANCE_LEAD','MAINTENANCE_VIEW','BRANCH'),
('MAINTENANCE_LEAD','MAINTENANCE_EXECUTE','OWN'),
('MAINTENANCE_LEAD','MAINTENANCE_REVIEW','BRANCH'),
('MAINTENANCE_LEAD','PM_MANAGE','BRANCH'),
('MAINTENANCE_LEAD','ASSET_VIEW','BRANCH'),
('MAINTENANCE_LEAD','MR_CREATE','OWN'),
('MAINTENANCE_LEAD','IR_CREATE','OWN'),
('MAINTENANCE_LEAD','IR_PROCESS','BRANCH'),
('MAINTENANCE_LEAD','TASK_VIEW','BRANCH'),
('MAINTENANCE_LEAD','TASK_EXECUTE','OWN'),
('MAINTENANCE_LEAD','SHIFT_VIEW','BRANCH'),
('MAINTENANCE_LEAD','ATTENDANCE_RECORD','OWN'),
('MAINTENANCE_STAFF','ROOM_VIEW','BRANCH'),
('MAINTENANCE_STAFF','INCIDENT_VIEW','BRANCH'),
('MAINTENANCE_STAFF','INCIDENT_CREATE','BRANCH'),
('MAINTENANCE_STAFF','MAINTENANCE_VIEW','OWN'),
('MAINTENANCE_STAFF','MAINTENANCE_EXECUTE','OWN'),
('MAINTENANCE_STAFF','ASSET_VIEW','BRANCH'),
('MAINTENANCE_STAFF','MR_CREATE','OWN'),
('MAINTENANCE_STAFF','IR_CREATE','OWN'),
('MAINTENANCE_STAFF','IR_PROCESS','OWN'),
('MAINTENANCE_STAFF','TASK_VIEW','OWN'),
('MAINTENANCE_STAFF','TASK_EXECUTE','OWN'),
('MAINTENANCE_STAFF','SHIFT_VIEW','OWN'),
('MAINTENANCE_STAFF','ATTENDANCE_RECORD','OWN'),
('STOREKEEPER','ASSET_VIEW','BRANCH'),
('STOREKEEPER','INVENTORY_VIEW','BRANCH'),
('STOREKEEPER','MR_CREATE','OWN'),
('STOREKEEPER','MR_FULFILL','BRANCH'),
('STOREKEEPER','IR_CREATE','OWN'),
('STOREKEEPER','IR_PROCESS','OWN'),
('STOREKEEPER','TASK_VIEW','OWN'),
('STOREKEEPER','TASK_EXECUTE','OWN'),
('STOREKEEPER','SHIFT_VIEW','OWN'),
('STOREKEEPER','ATTENDANCE_RECORD','OWN'),
('DEPT_MANAGER','USER_VIEW','BRANCH'),
('DEPT_MANAGER','ROOM_VIEW','BRANCH'),
('DEPT_MANAGER','INCIDENT_VIEW','BRANCH'),
('DEPT_MANAGER','INCIDENT_CREATE','BRANCH'),
('DEPT_MANAGER','ASSET_VIEW','BRANCH'),
('DEPT_MANAGER','INVENTORY_VIEW','BRANCH'),
('DEPT_MANAGER','MR_CREATE','BRANCH'),
('DEPT_MANAGER','MR_APPROVE_L1','BRANCH'),
('DEPT_MANAGER','IR_CREATE','BRANCH'),
('DEPT_MANAGER','IR_PROCESS','BRANCH'),
('DEPT_MANAGER','TASK_VIEW','BRANCH'),
('DEPT_MANAGER','TASK_MANAGE','BRANCH'),
('DEPT_MANAGER','TASK_EXECUTE','OWN'),
('DEPT_MANAGER','SHIFT_VIEW','BRANCH'),
('DEPT_MANAGER','SHIFT_MANAGE','BRANCH'),
('DEPT_MANAGER','ATTENDANCE_RECORD','OWN'),
('DEPT_MANAGER','DASHBOARD_VIEW','BRANCH'),
('DEPT_MANAGER','REPORT_VIEW','BRANCH'),
('STAFF','ROOM_VIEW','BRANCH'),
('STAFF','INCIDENT_VIEW','OWN'),
('STAFF','INCIDENT_CREATE','BRANCH'),
('STAFF','MR_CREATE','OWN'),
('STAFF','IR_CREATE','OWN'),
('STAFF','IR_PROCESS','OWN'),
('STAFF','TASK_VIEW','OWN'),
('STAFF','TASK_EXECUTE','OWN'),
('STAFF','SHIFT_VIEW','OWN'),
('STAFF','ATTENDANCE_RECORD','OWN'),
('RECEPTION','ROOM_VIEW','BRANCH'),
('RECEPTION','ROOM_CHECKOUT','BRANCH'),
('RECEPTION','CLEANING_VIEW','BRANCH'),
('RECEPTION','INCIDENT_VIEW','BRANCH'),
('RECEPTION','INCIDENT_CREATE','BRANCH'),
('RECEPTION','IR_CREATE','OWN'),
('RECEPTION','TASK_VIEW','OWN'),
('RECEPTION','TASK_EXECUTE','OWN'),
('RECEPTION','SHIFT_VIEW','OWN'),
('RECEPTION','ATTENDANCE_RECORD','OWN'),
('RECEPTION','ROOM_CHECKIN','BRANCH'),
('OPERATIONS_MANAGER','ASSET_MANAGE','BRANCH'),
('OPERATIONS_MANAGER','ASSET_RETIRE','BRANCH'),
('OPERATIONS_MANAGER','ASSET_TRANSFER','BRANCH'),
('OPERATIONS_MANAGER','INCIDENT_ESCALATION_HANDLE','BRANCH'),
('OPERATIONS_MANAGER','IR_OVERDUE_HANDLE','BRANCH'),
('OPERATIONS_MANAGER','MR_APPROVE_FALLBACK','BRANCH'),
('OPERATIONS_MANAGER','STOCK_ADJUST_POST','BRANCH'),
('OPERATIONS_MANAGER','STOCK_REVERSE','BRANCH'),
('MAINTENANCE_LEAD','INCIDENT_TRIAGE','BRANCH'),
('MAINTENANCE_LEAD','MAINTENANCE_ASSIGN','BRANCH'),
('STOREKEEPER','STOCK_ISSUE_POST','BRANCH'),
('STOREKEEPER','STOCK_RECEIPT_POST','BRANCH'),
('STOREKEEPER','STOCK_RETURN_POST','BRANCH'),
('STOREKEEPER','STOCK_TRANSFER_POST','BRANCH'),
('STOREKEEPER','STOCKTAKE_PERFORM','BRANCH'),
('RECEPTION','ROOM_OCCUPANCY_MOVE','BRANCH'),
('OWNER','DASHBOARD_VIEW','ALL'),
('OWNER','REPORT_VIEW','ALL');

    IF (SELECT COUNT(*) FROM @SeedRoles)<>12 OR (SELECT COUNT(*) FROM @SeedRoles WHERE is_active=1)<>10
       OR (SELECT COUNT(*) FROM @SeedPermissions)<>62 OR (SELECT COUNT(*) FROM @SeedGrants)<>140
        THROW 51620, 'Catalog generator count mismatch.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.permissions p JOIN @SeedPermissions d ON d.permission_code=p.permission_code
        WHERE p.module_code<>d.module_code OR p.resource_code<>d.resource_code OR p.action_code<>d.action_code
    ) THROW 51621, 'Existing permission code conflicts with resource/action; manual review required.', 1;

    -- Existing unknown roles are not silently deactivated.
    IF EXISTS (SELECT 1 FROM dbo.roles r LEFT JOIN @SeedRoles d ON d.role_code=r.role_code
               WHERE d.role_code IS NULL AND r.is_active=1)
        THROW 51622, 'Unknown active role. Stop and review before replacing grants.', 1;

    UPDATE r SET r.role_name=d.role_name, r.description=d.description,
                 r.is_system_role=d.is_system_role, r.is_active=d.is_active
    FROM dbo.roles r JOIN @SeedRoles d ON d.role_code=r.role_code;

    INSERT INTO dbo.roles (role_code,role_name,description,is_system_role,is_active,created_at)
    SELECT d.role_code,d.role_name,d.description,d.is_system_role,d.is_active,SYSUTCDATETIME()
    FROM @SeedRoles d WHERE NOT EXISTS (SELECT 1 FROM dbo.roles r WHERE r.role_code=d.role_code);

    UPDATE p SET p.permission_name=d.permission_name, p.description=d.description
    FROM dbo.permissions p JOIN @SeedPermissions d ON d.permission_code=p.permission_code;

    INSERT INTO dbo.permissions(permission_code,permission_name,module_code,resource_code,action_code,description)
    SELECT d.permission_code,d.permission_name,d.module_code,d.resource_code,d.action_code,d.description
    FROM @SeedPermissions d
    WHERE NOT EXISTS (SELECT 1 FROM dbo.permissions p WHERE p.permission_code=d.permission_code);

    -- The snapshot is the source of truth for catalog grants of these 12 roles.
    -- Removing a grant is deliberate. This does NOT delete business records.
    DELETE rp FROM dbo.role_permissions rp
    JOIN dbo.roles r ON r.role_id=rp.role_id
    JOIN @SeedRoles sr ON sr.role_code=r.role_code
    JOIN dbo.permissions p ON p.permission_id=rp.permission_id
    WHERE NOT EXISTS (
       SELECT 1 FROM @SeedGrants g
       WHERE g.role_code=r.role_code AND g.permission_code=p.permission_code
    );

    UPDATE rp SET rp.data_scope=g.data_scope
    FROM dbo.role_permissions rp
    JOIN dbo.roles r ON r.role_id=rp.role_id
    JOIN dbo.permissions p ON p.permission_id=rp.permission_id
    JOIN @SeedGrants g ON g.role_code=r.role_code AND g.permission_code=p.permission_code
    WHERE rp.data_scope<>g.data_scope;

    INSERT INTO dbo.role_permissions (role_id,permission_id,data_scope,granted_at,granted_by)
    SELECT r.role_id,p.permission_id,g.data_scope,SYSUTCDATETIME(),NULL
    FROM @SeedGrants g
    JOIN dbo.roles r ON r.role_code=g.role_code
    JOIN dbo.permissions p ON p.permission_code=g.permission_code
    WHERE NOT EXISTS (SELECT 1 FROM dbo.role_permissions rp
                      WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);

    IF (SELECT COUNT(*) FROM dbo.roles WHERE is_active=1)<>10
       OR (SELECT COUNT(*) FROM dbo.roles WHERE is_active=0)<>2
       OR (SELECT COUNT(*) FROM dbo.role_permissions rp
           JOIN dbo.roles r ON r.role_id=rp.role_id
           JOIN @SeedRoles sr ON sr.role_code=r.role_code)<>140
       OR EXISTS (
         SELECT r.role_code,p.permission_code,rp.data_scope
         FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
              JOIN dbo.permissions p ON p.permission_id=rp.permission_id
              JOIN @SeedRoles sr ON sr.role_code=r.role_code
         EXCEPT SELECT role_code,permission_code,data_scope FROM @SeedGrants
       ) OR EXISTS (
         SELECT role_code,permission_code,data_scope FROM @SeedGrants
         EXCEPT
         SELECT r.role_code,p.permission_code,rp.data_scope
         FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
              JOIN dbo.permissions p ON p.permission_id=rp.permission_id
              JOIN @SeedRoles sr ON sr.role_code=r.role_code
       )
        THROW 51623, 'Catalog postconditions failed; transaction rolled back.', 1;

    IF (SELECT COUNT(*) FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
        WHERE r.role_code='OWNER')<>2
       OR EXISTS (SELECT 1 FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id
                  WHERE r.role_code='OWNER' AND rp.data_scope<>'ALL')
        THROW 51624, 'OWNER must have only two ALL grants.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.role_permissions') AND name=N'CK_role_permissions_data_scope')
    BEGIN
        ALTER TABLE dbo.role_permissions WITH CHECK ADD CONSTRAINT CK_role_permissions_data_scope
        CHECK (data_scope IN ('ALL','BRANCH','DEPARTMENT','OWN'));
    END;

    -- Ensure old legacy roles cannot accidentally grant authorization via backend.
    IF EXISTS (SELECT 1 FROM dbo.roles WHERE role_code IN ('GENERAL_MANAGER','HK_MANAGER','MAINT_MANAGER','TECHNICIAN','WAREHOUSE_KEEPER') AND is_active=1)
        THROW 51717, 'One or more legacy role aliases remained enabled.', 1;

    COMMIT TRANSACTION;
    PRINT N'RBAC UPGRADE COMMITTED. REVIEW DH/OM BRANCH, retired departments and sample accounts before API testing.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
