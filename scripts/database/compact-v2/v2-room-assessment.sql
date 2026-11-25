SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() <> 'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.supply_reservations','usable_quantity') IS NULL
BEGIN
 ALTER TABLE dbo.supply_reservations ADD usable_quantity decimal(18,4) NULL,assessed_at datetime2 NULL,assessed_by bigint NULL;
 ALTER TABLE dbo.supply_reservations ADD CONSTRAINT FK_supply_assessment_user FOREIGN KEY(assessed_by) REFERENCES dbo.users(user_id);
END;
ALTER TABLE dbo.supply_reservations DROP CONSTRAINT CK_supply_reservation_qty;
ALTER TABLE dbo.supply_reservations WITH CHECK ADD CONSTRAINT CK_supply_reservation_qty CHECK(capacity>0 AND per_guest_quantity>0 AND quantity>=0 AND quantity<=capacity*per_guest_quantity);
EXEC(N'IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=''CK_supply_reservation_assessment'')
ALTER TABLE dbo.supply_reservations WITH CHECK ADD CONSTRAINT CK_supply_reservation_assessment CHECK(
 (usable_quantity IS NULL AND assessed_at IS NULL AND assessed_by IS NULL AND quantity=capacity*per_guest_quantity)
 OR (usable_quantity IS NOT NULL AND usable_quantity>=0 AND assessed_at IS NOT NULL AND assessed_at>=created_at AND assessed_by IS NOT NULL AND quantity=CASE WHEN usable_quantity>=capacity*per_guest_quantity THEN 0 ELSE capacity*per_guest_quantity-usable_quantity END));');
COMMIT;
SELECT 'room-assessment' feature,'APPLIED' status;
