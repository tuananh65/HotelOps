SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST required.',1;
SELECT entity_type,entity_key,action,occurred_at,after_json FROM dbo.audit_logs
WHERE entity_type IN('V2_REUSABLE_MIGRATION','V2_REUSABLE_SETTLEMENT') OR action='RECLASSIFIED_TO_REUSABLE';
SELECT p.position_id,p.branch_id,p.item_id,p.location_key,p.status,p.quantity,
 COALESCE((SELECT SUM(m.quantity) FROM dbo.reusable_movements m WHERE m.destination_position_id=p.position_id),0)
 -COALESCE((SELECT SUM(m.quantity) FROM dbo.reusable_movements m WHERE m.source_position_id=p.position_id),0) net_movement
FROM dbo.reusable_positions p ORDER BY p.position_id;
