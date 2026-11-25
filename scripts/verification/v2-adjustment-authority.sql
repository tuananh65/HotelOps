SELECT r.code role,p.code permission,rp.data_scope FROM role_permissions rp JOIN roles r ON r.role_id=rp.role_id JOIN permissions p ON p.permission_id=rp.permission_id WHERE p.code IN('STOCK_ADJUST_POST','STOCKTAKE_PERFORM','STOCK_REVERSE');
SELECT u.user_id,r.code,ur.branch_id FROM users u JOIN user_roles ur ON ur.user_id=u.user_id JOIN roles r ON r.role_id=ur.role_id WHERE r.code='STOREKEEPER' AND ur.revoked_at IS NULL AND u.account_status='ACTIVE';
SELECT condition_code,COUNT(*) n FROM assets GROUP BY condition_code;
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.reference_values');
