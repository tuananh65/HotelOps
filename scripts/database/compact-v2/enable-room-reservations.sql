SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompact%' THROW 51420,'Compact database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(varchar(20),value)='2.1.0')
 THROW 51420,'Workday schema 2.1.0 required.',1;
DECLARE @view nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.v_room_readiness')),
        @guard nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_rooms_ready_admission'));
IF @view IS NULL OR @guard IS NULL THROW 51420,'Room readiness contract is missing.',1;
IF CHARINDEX('RESERVED',@view)=0 AND CHARINDEX('IN (''READY'',''AVAILABLE'',''OCCUPIED'')',@view)=0
 THROW 51420,'Unexpected readiness definition. Manual review required.',1;
IF CHARINDEX('RESERVED',@guard)=0 AND CHARINDEX('IN (''READY'',''AVAILABLE'',''OCCUPIED'')',@guard)=0
 THROW 51420,'Unexpected room guard definition. Manual review required.',1;
ALTER TABLE dbo.rooms DROP CONSTRAINT CK_rooms_status;
ALTER TABLE dbo.rooms WITH CHECK ADD CONSTRAINT CK_rooms_status CHECK
 (operational_status IN ('AVAILABLE','OCCUPIED','DIRTY','CLEANING','INSPECTION','READY','OUT_OF_SERVICE','RESERVED'));
SET @view=REPLACE(REPLACE(@view,'CREATE OR ALTER VIEW','ALTER VIEW'),'CREATE VIEW','ALTER VIEW');
SET @view=REPLACE(@view,'IN (''READY'',''AVAILABLE'',''OCCUPIED'')','IN (''READY'',''AVAILABLE'',''OCCUPIED'',''RESERVED'')');
EXEC sys.sp_executesql @view;
SET @guard=REPLACE(REPLACE(@guard,'CREATE OR ALTER TRIGGER','ALTER TRIGGER'),'CREATE TRIGGER','ALTER TRIGGER');
SET @guard=REPLACE(@guard,'IN (''READY'',''AVAILABLE'',''OCCUPIED'')','IN (''READY'',''AVAILABLE'',''OCCUPIED'',''RESERVED'')');
EXEC sys.sp_executesql @guard;
