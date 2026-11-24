SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompactWorkdayTestP0%' THROW 51980,'Isolated P0 TEST required.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>49
 OR (SELECT COUNT(*) FROM sys.foreign_keys)<>205
 OR (SELECT COUNT(*) FROM sys.check_constraints)<>97
 OR (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1)<>38
 THROW 51981,'P0 schema contract mismatch.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1)
 THROW 51982,'P0 requires trusted enabled database guards.',1;
EXEC sys.sp_updateextendedproperty @name=N'HotelOpsCompactSchema',@value=N'3.0.0';
