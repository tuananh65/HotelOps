IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'TEST required.',1;
SELECT name,OBJECT_DEFINITION(object_id) definition FROM sys.triggers WHERE name IN('TR_supply_reservation_scope','TR_supply_ledger_scope');
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID('dbo.staff_supply_ledger'),OBJECT_ID('dbo.supply_reservations'));
