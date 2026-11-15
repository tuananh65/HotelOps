SET NOCOUNT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita' OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51999,'Exact disposable source required',1;
IF EXISTS(SELECT 1 FROM dbo.operational_tasks) OR EXISTS(SELECT 1 FROM dbo.task_progress_versions)
 OR EXISTS(SELECT 1 FROM dbo.task_completion_criteria) OR EXISTS(SELECT 1 FROM dbo.operational_task_assignments)
 OR EXISTS(SELECT 1 FROM dbo.operational_task_progress) OR EXISTS(SELECT 1 FROM dbo.workdays)
 OR EXISTS(SELECT 1 FROM dbo.work_intervals) OR EXISTS(SELECT 1 FROM dbo.audit_logs)
 THROW 51999,'Task trial rows remain',1;
IF OBJECT_ID('dbo.task_submission_events') IS NOT NULL OR OBJECT_ID('dbo.task_verification_events') IS NOT NULL
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name IN('HotelOps.TaskEntry.V1','HotelOps.TaskSubmission.V1','HotelOps.TaskVerification.V1'))
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE name LIKE 'TR_DEV3_Task%Fault')
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_entry_v1')
 THROW 51999,'Task trial schema remains',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_task_progress_state_v1' AND is_disabled=0 AND is_not_trusted=0)
 THROW 51999,'Original workflow guard not restored',1;
IF DB_ID('HotelOps_DEV3_142A_CriteriaTrial_e484ba02') IS NOT NULL OR DB_ID('HotelOps_DEV3_142A_CriteriaTrial_8b785130') IS NOT NULL OR DB_ID('HotelOps_DEV3_142A_CriteriaTrial_16d9eabc') IS NOT NULL
 THROW 51999,'Owned clone remains',1;
SELECT 'THREE_CLONES_ABSENT_SOURCE_EMPTY_ORIGINAL_TASK_GUARDS_RESTORED' AS result;
SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN('CK_operational_tasks_source_type','CK_operational_tasks_status','CK_task_progress_state_v1');

IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskDecline.V1') THROW 51867,'Decline capability remains',1;
IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_c48e3757') IS NOT NULL THROW 51867,'Owned C67 clone remains',1;
IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_dc801184') IS NOT NULL THROW 51867,'Owned C67 clone remains',1;
IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_412e53c7') IS NOT NULL THROW 51867,'Owned C67 clone remains',1;
IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_4df587fe') IS NOT NULL THROW 51867,'Owned C67 clone remains',1;
IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_4c319dc6') IS NOT NULL THROW 51867,'Owned C67 clone remains',1;
SELECT 'C67_FIVE_CLONES_ABSENT_SOURCE_RESTORED' AS result;

IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_f5da7eeb') IS NOT NULL THROW 51868,'Owned C68 clone remains',1;
IF EXISTS(SELECT 1 FROM dbo.internal_requests) THROW 51868,'Source request fixture remains',1;
SELECT 'C68_SOURCE_RESTORED_AND_CLONE_ABSENT' AS result;
IF DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_e58f6991') IS NOT NULL OR DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_56157cdf') IS NOT NULL OR DB_ID(N'HotelOps_DEV3_142A_CriteriaTrial_027c5281') IS NOT NULL THROW 51869,'C69 owned clone remains',1;
SELECT 'C69_THREE_CLONES_ABSENT_SOURCE_RESTORED' AS result;