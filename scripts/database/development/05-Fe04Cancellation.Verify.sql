IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications') AND name='CK_dev3_cancellation_event' AND is_disabled=0 AND is_not_trusted=0) THROW 51990,'Cancellation event constraint missing',1;
IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_dev3_closure_publication_source') AND is_disabled=0) THROW 51990,'Publication guard missing',1;
IF OBJECT_ID('dbo.CK_fs015_publication_event') IS NOT NULL THROW 51990,'Predecessor constraint unexpectedly retained',1;
