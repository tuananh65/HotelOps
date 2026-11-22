param([ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV3202611221630')][string]$Database='HotelOpsCompactDB',[switch]$Apply,[switch]$RestoreTarget)
$ErrorActionPreference='Stop'
if($RestoreTarget -and $Database -eq 'HotelOpsCompactDB'){throw 'RestoreTarget is a fixture-only recovery rehearsal.'}
if($Apply){& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database
$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandTimeout=60
 $cmd.CommandText=@'
SET NOCOUNT ON;SET XACT_ABORT ON;
IF DB_NAME() NOT IN ('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV3202611221630') THROW 51650,'Wrong database.',1;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @before nvarchar(max),@after nvarchar(max),@batch uniqueidentifier,@version binary(8),@hash nvarchar(500),@stamp uniqueidentifier;
SELECT @before=before_json,@after=after_json,@batch=operation_key FROM audit_logs WHERE entity_type='StaffingV3' AND action='APPLY';
IF @before IS NULL THROW 51650,'Staffing audit missing.',1;
SELECT @version=row_version,@hash=password_hash,@stamp=security_stamp FROM users WITH(UPDLOCK,HOLDLOCK) WHERE user_id=424;
DECLARE @source nvarchar(max)=CASE WHEN @Restore=1 THEN @before ELSE @after END,@target nvarchar(max)=CASE WHEN @Restore=1 THEN @after ELSE @before END;
IF NOT EXISTS(SELECT 1 FROM users u JOIN OPENJSON(@source,'$.users') WITH(user_id bigint,employee_code varchar(30),username nvarchar(100),full_name nvarchar(200),email nvarchar(254)) x ON u.user_id=x.user_id WHERE u.user_id=424 AND u.employee_code=x.employee_code AND u.username=x.username AND u.full_name=x.full_name AND ISNULL(u.email,N'')=ISNULL(x.email,N'')) THROW 51651,'Administrator changed after expected state; recovery refused.',1;
IF @Apply=0 BEGIN ROLLBACK;SELECT 'RECOVERY_READY' AS result;RETURN;END;
DECLARE @old nvarchar(max)=(SELECT user_id,employee_code,username,full_name,email FROM users WHERE user_id=424 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
UPDATE u SET employee_code=x.employee_code,username=x.username,full_name=x.full_name,email=x.email FROM users u JOIN OPENJSON(@target,'$.users') WITH(user_id bigint,employee_code varchar(30),username nvarchar(100),full_name nvarchar(200),email nvarchar(254)) x ON x.user_id=u.user_id WHERE u.user_id=424 AND u.row_version=@version;
IF @@ROWCOUNT<>1 THROW 51652,'Recovery concurrency failure.',1;
IF NOT EXISTS(SELECT 1 FROM users WHERE user_id=424 AND password_hash=@hash AND security_stamp=@stamp AND account_status='ACTIVE') THROW 51652,'Recovery changed protected credentials.',1;
DECLARE @new nvarchar(max)=(SELECT user_id,employee_code,username,full_name,email,CONVERT(varchar(36),@batch) AS staffing_batch FROM users WHERE user_id=424 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
INSERT audit_logs(actor_user_id,operation_key,entity_type,entity_key,action,before_json,after_json) VALUES(424,NEWID(),'StaffingV3AdminRecovery','424',CASE WHEN @Restore=1 THEN 'REHEARSAL_RESTORE_TARGET' ELSE 'RESTORE_OLD_IDENTITY' END,@old,@new);
COMMIT;SELECT 'RECOVERED' AS result;
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
'@
 [void]$cmd.Parameters.AddWithValue('@Apply',[bool]$Apply);[void]$cmd.Parameters.AddWithValue('@Restore',[bool]$RestoreTarget)
 Write-Output $cmd.ExecuteScalarAsync().GetAwaiter().GetResult()
}finally{$c.Dispose()}
