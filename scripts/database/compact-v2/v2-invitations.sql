SET XACT_ABORT ON;SET NOCOUNT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51990,'Isolated V2 TEST database required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.account_invitations') IS NULL
CREATE TABLE dbo.account_invitations(
 invitation_id bigint IDENTITY PRIMARY KEY,user_id bigint NOT NULL,created_by bigint NOT NULL,recipient nvarchar(254) NOT NULL,
 token_hash varbinary(32) NOT NULL,password_fingerprint varbinary(32) NOT NULL,protected_token nvarchar(2000) NULL,
 created_at datetime2 NOT NULL,expires_at datetime2 NOT NULL,used_at datetime2 NULL,revoked_at datetime2 NULL,
 delivery_status varchar(20) NOT NULL,attempts int NOT NULL,next_attempt_at datetime2 NOT NULL,lease_until datetime2 NULL,lease_id uniqueidentifier NULL,
 sent_at datetime2 NULL,last_error varchar(100) NULL,row_version rowversion NOT NULL,
 CONSTRAINT FK_invitation_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
 CONSTRAINT FK_invitation_creator FOREIGN KEY(created_by) REFERENCES dbo.users(user_id),
 CONSTRAINT CK_invitation_delivery CHECK(delivery_status IN('PENDING','SENT','FAILED') AND attempts>=0),
 CONSTRAINT CK_invitation_token CHECK(expires_at>created_at AND DATALENGTH(token_hash)=32 AND DATALENGTH(password_fingerprint)=32),
 CONSTRAINT UQ_invitation_token UNIQUE(token_hash));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.account_invitations') AND name='IX_invitation_delivery')
CREATE INDEX IX_invitation_delivery ON dbo.account_invitations(delivery_status,next_attempt_at) INCLUDE(user_id,expires_at,revoked_at,used_at,lease_until);
COMMIT;
