SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST required.',1;
SELECT t.name,OBJECT_NAME(t.parent_id) table_name,t.is_disabled,OBJECT_DEFINITION(t.object_id) definition
FROM sys.triggers t WHERE t.parent_id IN(OBJECT_ID('dbo.user_roles'),OBJECT_ID('dbo.role_permissions'),OBJECT_ID('dbo.memberships'));
