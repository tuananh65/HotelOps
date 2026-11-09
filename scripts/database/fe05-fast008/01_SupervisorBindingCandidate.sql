-- REVIEW / OWNED DISPOSABLE VERIFICATION ONLY. No shared deployment or legacy backfill.
IF @@TRANCOUNT=0 OR DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')
 THROW 51997,'Owned FE05 trial and caller transaction required',1;
IF OBJECT_ID('dbo.inspection_supervisor_bindings','U') IS NOT NULL
 THROW 51997,'Refusing overwrite of supervisor candidate',1;
CREATE TABLE dbo.inspection_supervisor_bindings(
 inspection_id bigint NOT NULL CONSTRAINT PK_fe05_supervisor_candidate PRIMARY KEY REFERENCES dbo.room_inspections(inspection_id),
 cleaning_task_id bigint NOT NULL REFERENCES dbo.cleaning_tasks(cleaning_task_id),
 room_id bigint NOT NULL REFERENCES dbo.rooms(room_id),
 branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 responsible_supervisor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 cycle_revision binary(8) NOT NULL,
 inspection_revision_at_binding binary(8) NOT NULL,
 bound_by_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 bound_at datetime2(7) NOT NULL CONSTRAINT DF_fe05_supervisor_bound_at DEFAULT SYSUTCDATETIME(),
 reason nvarchar(500) NOT NULL,
 row_version rowversion NOT NULL,
 CONSTRAINT CK_fe05_supervisor_reason CHECK(LEN(LTRIM(RTRIM(reason)))>0)
);
GO
CREATE TRIGGER dbo.TR_fe05_supervisor_binding_candidate
ON dbo.inspection_supervisor_bindings AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d WHERE NOT EXISTS(SELECT 1 FROM inserted i WHERE i.inspection_id=d.inspection_id))
  THROW 51997,'Supervisor removal authority is not approved; delete/rebind forbidden',1;
 -- Each new binding is explicit. No inspector/assigner/creator/role inference.
 IF EXISTS(SELECT 1 FROM inserted b
  LEFT JOIN dbo.room_inspections i WITH(HOLDLOCK) ON i.inspection_id=b.inspection_id
  LEFT JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=b.cleaning_task_id
  LEFT JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=b.room_id
  LEFT JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=b.room_id
  WHERE i.inspection_id IS NULL OR i.cleaning_task_id IS NULL OR i.cleaning_task_id<>b.cleaning_task_id
   OR i.room_id<>b.room_id OR t.room_id<>b.room_id OR r.branch_id<>b.branch_id
   OR c.room_id IS NULL OR c.cleaning_task_id<>b.cleaning_task_id OR c.row_version<>b.cycle_revision
   OR i.row_version<>b.inspection_revision_at_binding)
  THROW 51997,'Supervisor binding source/cycle/revision mismatch',1;
 INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,occurred_at)
 SELECT COALESCE(n.branch_id,o.branch_id),COALESCE(n.bound_by_user_id,o.bound_by_user_id),
  'inspection_supervisor_bindings',CONVERT(nvarchar(100),COALESCE(n.inspection_id,o.inspection_id)),
  CASE WHEN o.inspection_id IS NULL THEN 'CREATE' WHEN n.inspection_id IS NULL THEN 'DELETE' ELSE 'UPDATE' END,
  CASE WHEN o.inspection_id IS NOT NULL THEN (SELECT o.responsible_supervisor_user_id AS supervisorUserId,
    CONVERT(varchar(16),o.cycle_revision,2) AS cycleRevision FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END,
  CASE WHEN n.inspection_id IS NOT NULL THEN (SELECT n.responsible_supervisor_user_id AS supervisorUserId,
    CONVERT(varchar(16),n.cycle_revision,2) AS cycleRevision,n.reason AS reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END,
  SYSUTCDATETIME()
 FROM inserted n FULL OUTER JOIN deleted o ON o.inspection_id=n.inspection_id;
 ;WITH parents AS(SELECT inspection_id FROM inserted UNION SELECT inspection_id FROM deleted)
 UPDATE i SET summary_note=summary_note FROM dbo.room_inspections i JOIN parents p ON p.inspection_id=i.inspection_id;
END;
GO
-- No INSERT/backfill, production registration, grants or caller COMMIT in this candidate.
