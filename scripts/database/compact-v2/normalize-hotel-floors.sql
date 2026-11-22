-- Approved target: eight floors, six rooms per floor, preserving existing room IDs.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HotelOpsCompactDB' THROW 51500,'Unexpected database.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @operation uniqueidentifier=NEWID();
 SELECT * INTO #branches FROM dbo.branches WITH(UPDLOCK,HOLDLOCK);
 SELECT * INTO #locations FROM dbo.locations WITH(TABLOCKX,HOLDLOCK);
 SELECT * INTO #rooms FROM dbo.rooms WITH(TABLOCKX,HOLDLOCK);
 IF NOT EXISTS(SELECT 1 FROM #branches) OR EXISTS(SELECT 1 FROM #branches WHERE is_active=0)
  THROW 51501,'Expected active hotel branches.',1;
 IF EXISTS(SELECT 1 FROM #locations WHERE kind='FLOOR' AND (floor_number IS NULL OR floor_number NOT BETWEEN 1 AND 8 OR is_active=0))
  OR EXISTS(SELECT 1 FROM #locations WHERE kind='FLOOR' GROUP BY branch_id,floor_number HAVING COUNT(*)>1)
  THROW 51502,'Existing floors require manual review.',1;

 SELECT b.branch_id,b.code,b.name,t.room_type_id,d.department_id INTO #defaults
 FROM #branches b
 JOIN dbo.room_types t WITH(UPDLOCK,HOLDLOCK) ON t.branch_id=b.branch_id AND t.name=N'Standard' AND t.is_active=1
 JOIN dbo.departments d WITH(UPDLOCK,HOLDLOCK) ON d.branch_id=b.branch_id AND d.code='HK' AND d.is_active=1;
 IF (SELECT COUNT(*) FROM #defaults)<>(SELECT COUNT(*) FROM #branches)
  OR EXISTS(SELECT 1 FROM #defaults GROUP BY branch_id HAVING COUNT(*)<>1)
  THROW 51503,'Each branch needs one active Standard type and HK department.',1;

 SELECT b.branch_id,n.n floor_number,CONCAT('F',n.n) code,CONCAT(N'Tầng ',n.n) name
 INTO #floors FROM #branches b CROSS JOIN(VALUES(1),(2),(3),(4),(5),(6),(7),(8))n(n);
 IF EXISTS(SELECT 1 FROM #floors f JOIN #locations l ON l.branch_id=f.branch_id AND l.code=f.code
   WHERE l.kind<>'FLOOR' OR l.floor_number IS NULL OR l.floor_number<>f.floor_number)
  THROW 51504,'Floor code collision.',1;

 -- Only the existing first-floor overflow 107..112 is moved to 201..206.
 SELECT r.room_id,r.branch_id,r.code old_code,r.location_id old_location_id,
  CASE WHEN l.kind='FLOOR' AND l.floor_number=1 AND TRY_CONVERT(int,r.code) BETWEEN 107 AND 112
    THEN CONVERT(varchar(30),TRY_CONVERT(int,r.code)+94) ELSE r.code END new_code,
  CASE WHEN l.kind='FLOOR' AND l.floor_number=1 AND TRY_CONVERT(int,r.code) BETWEEN 107 AND 112
    THEN 2 ELSE l.floor_number END new_floor
 INTO #map FROM #rooms r JOIN #locations l ON l.location_id=r.location_id AND l.branch_id=r.branch_id;
 IF (SELECT COUNT(*) FROM #map)<>(SELECT COUNT(*) FROM #rooms)
  OR EXISTS(SELECT 1 FROM #map m JOIN #rooms r ON r.room_id=m.room_id
    WHERE r.is_active=0 OR m.new_floor IS NULL OR m.new_floor NOT BETWEEN 1 AND 8
    OR TRY_CONVERT(int,m.new_code) IS NULL
    OR TRY_CONVERT(int,m.new_code)/100<>m.new_floor
    OR TRY_CONVERT(int,m.new_code)%100 NOT BETWEEN 1 AND 6)
  OR EXISTS(SELECT 1 FROM #map GROUP BY branch_id,new_code HAVING COUNT(*)>1)
  THROW 51505,'Existing room numbering cannot be safely normalized.',1;
 IF EXISTS(SELECT 1 FROM #map m JOIN dbo.assets a ON a.room_id=m.room_id WHERE m.old_code<>m.new_code AND a.location_id IS NOT NULL)
  OR EXISTS(SELECT 1 FROM #map m JOIN dbo.work_items w ON w.room_id=m.room_id WHERE m.old_code<>m.new_code AND w.location_id IS NOT NULL)
  THROW 51506,'Moved rooms have explicit location dependencies; review required.',1;
 SELECT f.branch_id,f.floor_number,CONVERT(varchar(30),f.floor_number*100+n.n) code
 INTO #desired FROM #floors f CROSS JOIN(VALUES(1),(2),(3),(4),(5),(6))n(n);
 IF EXISTS(SELECT 1 FROM #map m LEFT JOIN #desired d ON d.branch_id=m.branch_id AND d.code=m.new_code WHERE d.code IS NULL)
  THROW 51507,'Unexpected room code format.',1;

 SELECT b.code branch,b.name,
  (SELECT COUNT(*) FROM #floors f WHERE f.branch_id=b.branch_id AND NOT EXISTS(SELECT 1 FROM #locations l WHERE l.branch_id=f.branch_id AND l.kind='FLOOR' AND l.floor_number=f.floor_number)) floors_to_add,
  (SELECT COUNT(*) FROM #desired d WHERE d.branch_id=b.branch_id AND NOT EXISTS(SELECT 1 FROM #map m WHERE m.branch_id=d.branch_id AND m.new_code=d.code)) rooms_to_add,
  (SELECT COUNT(*) FROM #map m WHERE m.branch_id=b.branch_id AND m.old_code<>m.new_code) rooms_to_rearrange,
  8 target_floors,48 target_rooms FROM #branches b;
 SELECT b.code branch,m.room_id,m.old_code,m.new_code,m.new_floor FROM #map m JOIN #branches b ON b.branch_id=m.branch_id WHERE m.old_code<>m.new_code;
 IF @apply=0 BEGIN ROLLBACK TRANSACTION; RETURN; END;

 INSERT dbo.locations(branch_id,kind,code,name,floor_number,is_active)
 SELECT f.branch_id,'FLOOR',f.code,f.name,f.floor_number,1 FROM #floors f
 WHERE NOT EXISTS(SELECT 1 FROM #locations l WHERE l.branch_id=f.branch_id AND l.kind='FLOOR' AND l.floor_number=f.floor_number);
 UPDATE r SET code=m.new_code,location_id=l.location_id
 FROM dbo.rooms r JOIN #map m ON m.room_id=r.room_id
 JOIN dbo.locations l ON l.branch_id=m.branch_id AND l.kind='FLOOR' AND l.floor_number=m.new_floor
 WHERE r.code<>m.new_code OR r.location_id<>l.location_id;
 INSERT dbo.rooms(branch_id,location_id,room_type_id,department_id,code,operational_status,is_active)
 SELECT d.branch_id,l.location_id,t.room_type_id,t.department_id,d.code,'DIRTY',1
 FROM #desired d JOIN #defaults t ON t.branch_id=d.branch_id
 JOIN dbo.locations l ON l.branch_id=d.branch_id AND l.kind='FLOOR' AND l.floor_number=d.floor_number
 WHERE NOT EXISTS(SELECT 1 FROM dbo.rooms r WHERE r.branch_id=d.branch_id AND r.code=d.code);

 IF EXISTS(SELECT 1 FROM #floors f LEFT JOIN dbo.locations l ON l.branch_id=f.branch_id AND l.kind='FLOOR' AND l.floor_number=f.floor_number
   LEFT JOIN dbo.rooms r ON r.location_id=l.location_id AND r.branch_id=l.branch_id
   GROUP BY f.branch_id,f.floor_number HAVING COUNT(r.room_id)<>6)
  OR (SELECT COUNT(*) FROM dbo.rooms)<>(SELECT COUNT(*)*48 FROM #branches)
  OR (SELECT COUNT(*) FROM dbo.locations WHERE kind='FLOOR')<>(SELECT COUNT(*)*8 FROM #branches)
  THROW 51508,'Target counts do not match.',1;
 IF EXISTS(SELECT room_id,branch_id,room_type_id,department_id,operational_status,turnover_no,current_cleaning_work_id,is_active,created_at FROM #rooms
   EXCEPT SELECT room_id,branch_id,room_type_id,department_id,operational_status,turnover_no,current_cleaning_work_id,is_active,created_at FROM dbo.rooms)
  THROW 51509,'Existing room identity or operational data changed.',1;
 IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>40 THROW 51510,'Expected 40 tables.',1;

 INSERT dbo.audit_logs(branch_id,actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json)
 SELECT l.branch_id,NULL,@operation,'Location',CONVERT(nvarchar(150),l.location_id),'CREATE',NULL,
  (SELECT l.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.locations l WHERE NOT EXISTS(SELECT 1 FROM #locations o WHERE o.location_id=l.location_id);
 INSERT dbo.audit_logs(branch_id,actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json)
 SELECT r.branch_id,NULL,@operation,'Room',CONVERT(nvarchar(150),r.room_id),CASE WHEN o.room_id IS NULL THEN 'CREATE' ELSE 'UPDATE' END,
  CASE WHEN o.room_id IS NOT NULL THEN (SELECT o.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END,
  (SELECT r.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.rooms r LEFT JOIN #rooms o ON o.room_id=r.room_id
 WHERE o.room_id IS NULL OR o.code<>r.code OR o.location_id<>r.location_id;
 SELECT @operation operation_key,COUNT(*) audit_rows FROM dbo.audit_logs WHERE operation_key=@operation;
 COMMIT TRANSACTION;
 SELECT b.code branch,l.floor_number,COUNT(*) rooms,MIN(r.code) first_room,MAX(r.code) last_room
 FROM dbo.branches b JOIN dbo.locations l ON l.branch_id=b.branch_id AND l.kind='FLOOR'
 JOIN dbo.rooms r ON r.location_id=l.location_id AND r.branch_id=b.branch_id
 GROUP BY b.code,l.floor_number ORDER BY b.code,l.floor_number;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
