SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'HotelOpsCompactDB' THROW 51400,'Unexpected database.',1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @levels TABLE(kind varchar(40),code varchar(60),name nvarchar(150),sort_order int,PRIMARY KEY(kind,code));
    INSERT @levels VALUES
      ('PRIORITY','LOW',N'Thấp',1),('PRIORITY','NORMAL',N'Bình thường',2),
      ('PRIORITY','HIGH',N'Cao',3),('PRIORITY','CRITICAL',N'Khẩn cấp',4),
      ('SEVERITY','LOW',N'Nhẹ',1),('SEVERITY','NORMAL',N'Trung bình',2),
      ('SEVERITY','HIGH',N'Nặng',3),('SEVERITY','CRITICAL',N'Nghiêm trọng',4);

    -- Hold the catalog while normalizing. Existing reference IDs and foreign keys stay unchanged.
    DECLARE @count int=(SELECT COUNT(*) FROM dbo.reference_values WITH(TABLOCKX,HOLDLOCK));
    DECLARE @map TABLE(reference_id bigint PRIMARY KEY,kind varchar(40),code varchar(60));
    INSERT @map
    SELECT reference_id,kind,
      CASE WHEN code IN('LOW','NORMAL','HIGH','CRITICAL') THEN code
           WHEN kind='PRIORITY' AND code='DEMO-HIGH' THEN 'HIGH'
           WHEN kind='PRIORITY' AND code LIKE 'CFG-%' AND name=N'Bình thường' THEN 'NORMAL'
           WHEN kind='SEVERITY' AND code='DEMO-MEDIUM' THEN 'NORMAL'
      END
    FROM dbo.reference_values WHERE kind IN('PRIORITY','SEVERITY');
    IF EXISTS(SELECT 1 FROM @map WHERE code IS NULL)
        THROW 51401,'Unrecognized operational level; review instead of deleting data.',1;
    IF EXISTS(SELECT 1 FROM @map GROUP BY kind,code HAVING COUNT(*)>1)
        THROW 51402,'Ambiguous duplicate level; merge requires review.',1;

    UPDATE r SET code=l.code,name=l.name,sort_order=l.sort_order,is_active=1
    FROM dbo.reference_values r JOIN @map m ON m.reference_id=r.reference_id
    JOIN @levels l ON l.kind=m.kind AND l.code=m.code
    WHERE r.code<>l.code OR r.name<>l.name OR r.sort_order<>l.sort_order OR r.is_active=0;

    INSERT dbo.reference_values(kind,code,name,sort_order,is_active)
    SELECT l.kind,l.code,l.name,l.sort_order,1 FROM @levels l
    WHERE NOT EXISTS(SELECT 1 FROM dbo.reference_values r WHERE r.kind=l.kind AND r.code=l.code);

    IF (SELECT COUNT(*) FROM dbo.reference_values WHERE kind IN('PRIORITY','SEVERITY'))<>8
        THROW 51403,'Expected exactly four values per operational group.',1;
    IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>40
        THROW 51404,'Expected 40 tables.',1;
    SELECT reference_id,kind,code,name,sort_order,is_active FROM dbo.reference_values
    WHERE kind IN('PRIORITY','SEVERITY') ORDER BY kind,sort_order;
    IF @apply=1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
