SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
SELECT i.item_id,i.code,i.name,i.base_unit_id,c.branch_id,c.supply_group,c.initial_quantity,c.per_guest_quantity,c.is_active FROM inventory_items i LEFT JOIN supply_configurations c ON c.item_id=i.item_id AND c.is_current=1 ORDER BY i.item_id,c.branch_id;
SELECT SUM(CASE WHEN role.code='MAINTENANCE_STAFF' THEN 1 ELSE 0 END) maintenance_staff,SUM(CASE WHEN role.code='HK_STAFF' THEN 1 ELSE 0 END) housekeeping_staff FROM user_roles ur JOIN roles role ON role.role_id=ur.role_id WHERE ur.revoked_at IS NULL AND role.is_active=1;
SELECT COUNT(*) changed_credentials FROM users t JOIN HotelOpsCompactDB.dbo.users p ON p.user_id=t.user_id WHERE t.password_hash<>p.password_hash OR t.security_stamp<>p.security_stamp;
SELECT COUNT(*) bad_fk FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
SELECT COUNT(*) bad_check FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1;
