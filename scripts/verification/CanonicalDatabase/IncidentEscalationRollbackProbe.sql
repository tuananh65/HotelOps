-- Run from repository root with the same exact disposable identity variables as IncidentEscalation.sql.
-- Installer's nested COMMIT never commits this owning transaction. No fixtures survive.
:on error exit
:setvar ApplyChanges "1"
SET NOCOUNT ON;
BEGIN TRANSACTION;
:r scripts/verification/CanonicalDatabase/IncidentEscalation.sql
GO
IF @@TRANCOUNT<>1 THROW 51846,'Probe must own exactly one uncommitted transaction.',1;
-- Expected constraint errors must remain statement-scoped so we can inspect and rollback the fixture.
-- FK/CHECK/unique constraints and triggers stay enabled throughout.
SET XACT_ABORT OFF;
BEGIN TRY
 DECLARE @prefix varchar(30)='D3ESC'+LEFT(REPLACE(CONVERT(varchar(36),NEWID()),'-',''),16),@now datetime2(7)=SYSUTCDATETIME();
 DECLARE @branch bigint,@user bigint,@incident bigint,@foreign bigint,@opened bigint,@otherOpen bigint,@closed bigint,@foreignClose bigint;
 INSERT dbo.branches(branch_code,branch_name,is_active) VALUES(@prefix,N'Synthetic escalation rollback',1);
 SET @branch=SCOPE_IDENTITY();
 INSERT dbo.users(employee_code,username,email,full_name,created_at,password_hash)
 VALUES(@prefix,@prefix,@prefix+'@example.invalid',N'Synthetic escalation actor',@now,'Disabled synthetic account');
 SET @user=SCOPE_IDENTITY();
 INSERT dbo.incidents(incident_no,branch_id,incident_title,description,status,reported_by,discovered_at,reported_at)
 VALUES(@prefix+'A',@branch,N'Synthetic A',N'Rollback fixture','RESOLVED',@user,@now,@now);
 SET @incident=SCOPE_IDENTITY();
 INSERT dbo.incidents(incident_no,branch_id,incident_title,description,status,reported_by,discovered_at,reported_at)
 VALUES(@prefix+'B',@branch,N'Synthetic B',N'Rollback fixture','RESOLVED',@user,@now,@now);
 SET @foreign=SCOPE_IDENTITY();
 INSERT dbo.incident_events(incident_id,event_type,note,actor_id,occurred_at) VALUES(@incident,'ESCALATED',N'Synthetic opening',@user,@now);
 SET @opened=SCOPE_IDENTITY();
 INSERT dbo.incident_events(incident_id,event_type,note,actor_id,occurred_at) VALUES(@incident,'ESCALATED',N'Synthetic competing opening',@user,@now);
 SET @otherOpen=SCOPE_IDENTITY();
 INSERT dbo.incident_events(incident_id,event_type,from_status,to_status,note,actor_id,occurred_at)
 VALUES(@foreign,'STATUS_CHANGED','RESOLVED','CLOSED',N'Synthetic foreign closure',@user,@now);
 SET @foreignClose=SCOPE_IDENTITY();
 INSERT dbo.incident_escalations(incident_id,opened_event_id,state,opened_at,reason,requested_support)
 VALUES(@incident,@opened,'OPEN',@now,N'Synthetic critical incident',N'Synthetic resource request');
 DECLARE @duplicateRejected bit=0,@foreignRejected bit=0;
 BEGIN TRY
  INSERT dbo.incident_escalations(incident_id,opened_event_id,state,opened_at,reason,requested_support)
  VALUES(@incident,@otherOpen,'OPEN',@now,N'Synthetic second open',N'Synthetic resource request');
 END TRY BEGIN CATCH
  IF ERROR_NUMBER() NOT IN(2601,2627) THROW;
  SET @duplicateRejected=1;
 END CATCH;
 IF @duplicateRejected<>1 THROW 51846,'Duplicate Open escalation was accepted.',1;
 BEGIN TRY
  UPDATE dbo.incident_escalations SET state='CLOSED',closed_at=@now,closed_event_id=@foreignClose WHERE incident_id=@incident;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>547 THROW;
  SET @foreignRejected=1;
 END CATCH;
 IF @foreignRejected<>1 THROW 51846,'Foreign Incident closure event was accepted.',1;
 UPDATE dbo.incidents SET status='CLOSED',closed_at=@now WHERE incident_id=@incident;
 INSERT dbo.incident_events(incident_id,event_type,from_status,to_status,note,actor_id,occurred_at)
 VALUES(@incident,'STATUS_CHANGED','RESOLVED','CLOSED',N'Synthetic valid closure',@user,@now);
 SET @closed=SCOPE_IDENTITY();
 DECLARE @output TABLE(EscalationId bigint,OpenedEventId bigint,OpenedAt datetime2(7));
 UPDATE dbo.incident_escalations WITH(UPDLOCK,HOLDLOCK) SET state='CLOSED',closed_at=@now,closed_event_id=@closed
 OUTPUT inserted.escalation_id,inserted.opened_event_id,inserted.opened_at INTO @output
 WHERE incident_id=@incident AND state='OPEN';
 IF (SELECT COUNT(*) FROM @output)<>1 OR NOT EXISTS(SELECT 1 FROM @output WHERE OpenedEventId=@opened AND OpenedAt=@now)
  THROW 51846,'Closure output does not preserve original opening evidence.',1;
 UPDATE dbo.incident_escalations SET state='CLOSED',closed_at=@now,closed_event_id=@closed WHERE incident_id=@incident AND state='OPEN';
 IF @@ROWCOUNT<>0 THROW 51846,'Closure retry updated an already closed escalation.',1;
 ROLLBACK;
 IF EXISTS(SELECT 1 FROM dbo.users WHERE username=@prefix) OR EXISTS(SELECT 1 FROM dbo.branches WHERE branch_code=@prefix)
  OR EXISTS(SELECT 1 FROM dbo.incidents WHERE incident_no IN(@prefix+'A',@prefix+'B'))
  OR OBJECT_ID('dbo.incident_escalations','U') IS NOT NULL
  OR EXISTS(SELECT 1 FROM sys.key_constraints WHERE name='UQ_incident_event_source')
  OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE name='HotelOps.IncidentEscalation.V1')
  THROW 51846,'Rollback cleanup failed.',1;
 SELECT 'PASS' AS outcome,'duplicate-open; foreign-event FK; closure output; repeat closure; schema and fixture rollback' AS checks;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
