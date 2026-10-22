-- Approved DEV2 Inventory schema delta. Database First; no EF migrations.
-- Run with sqlcmd -b against the verified target after taking a database backup.
-- Does not seed data, change legacy rows, or relax existing constraints.
SET NOCOUNT ON;
SET XACT_ABORT ON;

SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'HotelOpsDB'
    THROW 51000, 'Inventory patch requires HotelOpsDB as the current database.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'HotelOps:Inventory:Schema',
        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
    IF @lockResult < 0
        THROW 51001, 'Could not acquire Inventory schema lock.', 1;

    IF OBJECT_ID(N'dbo.stock_transfer_receipts', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.stock_transfer_receipts
        (
            transfer_receipt_id bigint IDENTITY(1,1) NOT NULL,
            transfer_document_id bigint NOT NULL,
            received_by bigint NOT NULL,
            received_at datetime2(7) NOT NULL,
            note nvarchar(max) NULL,
            CONSTRAINT PK_stock_transfer_receipts PRIMARY KEY (transfer_receipt_id),
            CONSTRAINT FK_stock_transfer_receipts_document FOREIGN KEY (transfer_document_id)
                REFERENCES dbo.stock_documents(document_id),
            CONSTRAINT FK_stock_transfer_receipts_user FOREIGN KEY (received_by)
                REFERENCES dbo.users(user_id)
        );
    END;

    IF OBJECT_ID(N'dbo.stock_transfer_receipt_lines', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.stock_transfer_receipt_lines
        (
            transfer_receipt_line_id bigint IDENTITY(1,1) NOT NULL,
            transfer_receipt_id bigint NOT NULL,
            transfer_line_id bigint NOT NULL,
            received_qty decimal(18,3) NOT NULL,
            damaged_qty decimal(18,3) NOT NULL,
            missing_qty decimal(18,3) NOT NULL,
            note nvarchar(max) NULL,
            CONSTRAINT PK_stock_transfer_receipt_lines PRIMARY KEY (transfer_receipt_line_id),
            CONSTRAINT FK_stock_transfer_receipt_lines_receipt FOREIGN KEY (transfer_receipt_id)
                REFERENCES dbo.stock_transfer_receipts(transfer_receipt_id),
            CONSTRAINT FK_stock_transfer_receipt_lines_line FOREIGN KEY (transfer_line_id)
                REFERENCES dbo.stock_document_lines(line_id)
        );
    END;

    IF OBJECT_ID(N'dbo.stock_receipt_line_details', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.stock_receipt_line_details
        (
            stock_document_line_id bigint NOT NULL,
            rejected_qty decimal(18,3) NOT NULL,
            damaged_qty decimal(18,3) NOT NULL,
            discrepancy_reason nvarchar(max) NULL,
            CONSTRAINT PK_stock_receipt_line_details PRIMARY KEY (stock_document_line_id),
            CONSTRAINT FK_stock_receipt_line_details_line FOREIGN KEY (stock_document_line_id)
                REFERENCES dbo.stock_document_lines(line_id)
        );
    END;

    IF OBJECT_ID(N'dbo.stock_return_line_details', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.stock_return_line_details
        (
            stock_document_line_id bigint NOT NULL,
            original_issue_line_id bigint NOT NULL,
            damaged_qty decimal(18,3) NOT NULL,
            note nvarchar(max) NULL,
            CONSTRAINT PK_stock_return_line_details PRIMARY KEY (stock_document_line_id),
            CONSTRAINT FK_stock_return_line_details_line FOREIGN KEY (stock_document_line_id)
                REFERENCES dbo.stock_document_lines(line_id),
            CONSTRAINT FK_stock_return_line_details_original FOREIGN KEY (original_issue_line_id)
                REFERENCES dbo.stock_document_lines(line_id)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.stock_transfer_receipts')
        AND name = N'IX_stock_transfer_receipts_document')
        CREATE INDEX IX_stock_transfer_receipts_document
            ON dbo.stock_transfer_receipts(transfer_document_id, transfer_receipt_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.stock_transfer_receipts')
        AND name = N'IX_stock_transfer_receipts_received_by')
        CREATE INDEX IX_stock_transfer_receipts_received_by ON dbo.stock_transfer_receipts(received_by);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.stock_transfer_receipt_lines')
        AND name = N'IX_stock_transfer_receipt_lines_receipt')
        CREATE INDEX IX_stock_transfer_receipt_lines_receipt
            ON dbo.stock_transfer_receipt_lines(transfer_receipt_id);

    -- Intentionally NON-UNIQUE: one transfer line may have multiple receiving batches.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.stock_transfer_receipt_lines')
        AND name = N'IX_stock_transfer_receipt_lines_transfer_line')
        CREATE INDEX IX_stock_transfer_receipt_lines_transfer_line
            ON dbo.stock_transfer_receipt_lines(transfer_line_id)
            INCLUDE (received_qty, damaged_qty, missing_qty);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.stock_return_line_details')
        AND name = N'IX_stock_return_line_details_original')
        CREATE INDEX IX_stock_return_line_details_original
            ON dbo.stock_return_line_details(original_issue_line_id);

    DECLARE @tableName sysname, @sql nvarchar(max);
    DECLARE inventoryRoots CURSOR LOCAL FAST_FORWARD FOR
        SELECT name FROM (VALUES
            (N'inventory_items'), (N'units_of_measure'), (N'uom_conversions'),
            (N'warehouses'), (N'stock_thresholds'), (N'stock_documents'), (N'stocktakes')
        ) AS roots(name);
    OPEN inventoryRoots;
    FETCH NEXT FROM inventoryRoots INTO @tableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @tableName, N'U') IS NULL
            THROW 51002, 'Required Inventory aggregate table is missing.', 1;

        IF COL_LENGTH(N'dbo.' + @tableName, N'row_version') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tableName)
                + N' ADD row_version rowversion NOT NULL;';
            EXEC sys.sp_executesql @sql;
        END
        ELSE IF NOT EXISTS
        (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.' + @tableName)
                AND name = N'row_version' AND system_type_id = 189 AND is_nullable = 0
        )
            THROW 51003, 'Existing row_version does not match the approved rowversion contract.', 1;

        FETCH NEXT FROM inventoryRoots INTO @tableName;
    END;
    CLOSE inventoryRoots;
    DEALLOCATE inventoryRoots;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
