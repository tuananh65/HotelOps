using System;
using System.Collections.Generic;
using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Data;

public partial class HotelOpsCompactDbContext : DbContext
{
    public HotelOpsCompactDbContext(DbContextOptions<HotelOpsCompactDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<AssetEvent> AssetEvents { get; set; }

    public virtual DbSet<Attachment> Attachments { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<ChecklistItem> ChecklistItems { get; set; }

    public virtual DbSet<ChecklistTemplate> ChecklistTemplates { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<DocumentEvent> DocumentEvents { get; set; }

    public virtual DbSet<InspectionFinding> InspectionFindings { get; set; }

    public virtual DbSet<InventoryDocument> InventoryDocuments { get; set; }

    public virtual DbSet<InventoryDocumentLine> InventoryDocumentLines { get; set; }

    public virtual DbSet<InventoryItem> InventoryItems { get; set; }

    public virtual DbSet<InventoryPolicy> InventoryPolicies { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Membership> Memberships { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<ReferenceValue> ReferenceValues { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomBlock> RoomBlocks { get; set; }

    public virtual DbSet<RoomEvent> RoomEvents { get; set; }

    public virtual DbSet<RoomType> RoomTypes { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    public virtual DbSet<VBacklogHandover> VBacklogHandovers { get; set; }

    public virtual DbSet<VInventoryDocumentTotal> VInventoryDocumentTotals { get; set; }

    public virtual DbSet<VMaterialFulfillment> VMaterialFulfillments { get; set; }

    public virtual DbSet<VRoomReadiness> VRoomReadinesses { get; set; }

    public virtual DbSet<VStockBalance> VStockBalances { get; set; }

    public virtual DbSet<VWorkDuration> VWorkDurations { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<WorkAssignment> WorkAssignments { get; set; }

    public virtual DbSet<WorkChecklistItem> WorkChecklistItems { get; set; }

    public virtual DbSet<WorkEvent> WorkEvents { get; set; }

    public virtual DbSet<WorkInterval> WorkIntervals { get; set; }

    public virtual DbSet<WorkItem> WorkItems { get; set; }

    public virtual DbSet<WorkLink> WorkLinks { get; set; }

    public virtual DbSet<WorkTemplate> WorkTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");

            entity.HasIndex(e => e.AssetCode, "UX_assets_asset_code").IsUnique();

            entity.HasIndex(e => new { e.AssetId, e.BranchId }, "UX_assets_asset_id_branch_id").IsUnique();

            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AssetCode)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("asset_code");
            entity.Property(e => e.AssetTypeId).HasColumnName("asset_type_id");
            entity.Property(e => e.AssetTypeKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("ASSET_TYPE")
                .HasColumnName("asset_type_kind");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ConditionCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("condition_code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CustodianUserId).HasColumnName("custodian_user_id");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.PurchasedOn).HasColumnName("purchased_on");
            entity.Property(e => e.RetiredAt).HasColumnName("retired_at");
            entity.Property(e => e.RetirementReason)
                .HasMaxLength(2000)
                .HasColumnName("retirement_reason");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(100)
                .HasColumnName("serial_number");
            entity.Property(e => e.WarrantyUntil).HasColumnName("warranty_until");






        });

        modelBuilder.Entity<AssetEvent>(entity =>
        {
            entity.ToTable("asset_events", tb => tb.HasTrigger("TR_asset_events_immutable"));

            entity.HasIndex(e => new { e.AssetEventId, e.BranchId }, "UX_asset_events_asset_event_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.AssetId, e.ActorUserId, e.OperationKey }, "UX_asset_events_asset_id_actor_user_id_operation_key").IsUnique();

            entity.Property(e => e.AssetEventId).HasColumnName("asset_event_id");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("event_type");
            entity.Property(e => e.FromBranchId).HasColumnName("from_branch_id");
            entity.Property(e => e.FromLocationId).HasColumnName("from_location_id");
            entity.Property(e => e.FromRoomId).HasColumnName("from_room_id");
            entity.Property(e => e.FromUserId).HasColumnName("from_user_id");
            entity.Property(e => e.HandoverStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("handover_status");
            entity.Property(e => e.NewCondition)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("new_condition");
            entity.Property(e => e.Note)
                .HasMaxLength(2000)
                .HasColumnName("note");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OldCondition)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("old_condition");
            entity.Property(e => e.OperationKey).HasColumnName("operation_key");
            entity.Property(e => e.SourceWorkId).HasColumnName("source_work_id");
            entity.Property(e => e.ToLocationId).HasColumnName("to_location_id");
            entity.Property(e => e.ToRoomId).HasColumnName("to_room_id");
            entity.Property(e => e.ToUserId).HasColumnName("to_user_id");











        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("attachments");

            entity.HasIndex(e => new { e.AttachmentId, e.BranchId }, "UX_attachments_attachment_id_branch_id").IsUnique();

            entity.HasIndex(e => e.StorageKey, "UX_attachments_storage_key").IsUnique();

            entity.Property(e => e.AttachmentId).HasColumnName("attachment_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ByteLength).HasColumnName("byte_length");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.FindingId).HasColumnName("finding_id");
            entity.Property(e => e.InvalidatedAt).HasColumnName("invalidated_at");
            entity.Property(e => e.InvalidationReason)
                .HasMaxLength(2000)
                .HasColumnName("invalidation_reason");
            entity.Property(e => e.MimeType)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("mime_type");
            entity.Property(e => e.OriginalName)
                .HasMaxLength(255)
                .HasColumnName("original_name");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.Sha256)
                .HasMaxLength(32)
                .IsFixedLength()
                .HasColumnName("sha256");
            entity.Property(e => e.StorageKey).HasColumnName("storage_key");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(e => e.WorkId).HasColumnName("work_id");






        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId);

            entity.ToTable("audit_logs", tb => tb.HasTrigger("TR_audit_logs_immutable"));

            entity.HasIndex(e => new { e.BranchId, e.OccurredAt }, "IX_audit_logs_branch_id_occurred_at");

            entity.Property(e => e.AuditId).HasColumnName("audit_id");
            entity.Property(e => e.Action)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("action");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.AfterJson).HasColumnName("after_json");
            entity.Property(e => e.BeforeJson).HasColumnName("before_json");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.EntityKey)
                .HasMaxLength(150)
                .HasColumnName("entity_key");
            entity.Property(e => e.EntityType)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("entity_type");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OperationKey).HasColumnName("operation_key");


        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");

            entity.HasIndex(e => e.Code, "UX_branches_code").IsUnique();

            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.Timezone)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasDefaultValue("Asia/Ho_Chi_Minh")
                .HasColumnName("timezone");
        });

        modelBuilder.Entity<ChecklistItem>(entity =>
        {
            entity.ToTable("checklist_items", tb => tb.HasTrigger("TR_checklist_items_immutable"));

            entity.HasIndex(e => new { e.ChecklistItemId, e.ChecklistTemplateId }, "UX_checklist_items_checklist_item_id_checklist_template_id").IsUnique();

            entity.HasIndex(e => new { e.ChecklistTemplateId, e.LineNo }, "UX_checklist_items_checklist_template_id_line_no").IsUnique();

            entity.Property(e => e.ChecklistItemId).HasColumnName("checklist_item_id");
            entity.Property(e => e.ChecklistTemplateId).HasColumnName("checklist_template_id");
            entity.Property(e => e.Description)
                .HasMaxLength(2000)
                .HasColumnName("description");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.Weight)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(9, 3)")
                .HasColumnName("weight");

        });

        modelBuilder.Entity<ChecklistTemplate>(entity =>
        {
            entity.ToTable("checklist_templates", tb => tb.HasTrigger("TR_checklist_template_immutable"));

            entity.HasIndex(e => new { e.BranchId, e.Code, e.VersionNo }, "UX_checklist_templates_branch_id_code_version_no").IsUnique();

            entity.HasIndex(e => new { e.ChecklistTemplateId, e.BranchId }, "UX_checklist_templates_checklist_template_id_branch_id").IsUnique();

            entity.Property(e => e.ChecklistTemplateId).HasColumnName("checklist_template_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Code)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Kind)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.VersionNo).HasColumnName("version_no");


        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("departments");

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UX_departments_branch_id_code").IsUnique();

            entity.HasIndex(e => new { e.DepartmentId, e.BranchId }, "UX_departments_department_id_branch_id").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");


        });

        modelBuilder.Entity<DocumentEvent>(entity =>
        {
            entity.ToTable("document_events", tb => tb.HasTrigger("TR_document_events_immutable"));

            entity.HasIndex(e => new { e.DocumentEventId, e.BranchId }, "UX_document_events_document_event_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.DocumentId, e.ActorUserId, e.OperationKey, e.LineId }, "UX_document_events_document_id_actor_user_id_operation_key_line_id").IsUnique();

            entity.Property(e => e.DocumentEventId).HasColumnName("document_event_id");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("event_type");
            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OperationKey).HasColumnName("operation_key");
            entity.Property(e => e.QuantitySnapshot)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("quantity_snapshot");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.RevisionNo)
                .HasDefaultValue(1)
                .HasColumnName("revision_no");
            entity.Property(e => e.SnapshotJson).HasColumnName("snapshot_json");




        });

        modelBuilder.Entity<InspectionFinding>(entity =>
        {
            entity.HasKey(e => e.FindingId);

            entity.ToTable("inspection_findings", tb =>
                {
                    tb.HasTrigger("TR_findings_kind");
                    tb.HasTrigger("TR_findings_passed");
                });

            entity.HasIndex(e => new { e.FindingId, e.BranchId }, "UX_inspection_findings_finding_id_branch_id").IsUnique();

            entity.Property(e => e.FindingId).HasColumnName("finding_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Description)
                .HasMaxLength(2000)
                .HasColumnName("description");
            entity.Property(e => e.InspectionWorkId).HasColumnName("inspection_work_id");
            entity.Property(e => e.ResolutionNote)
                .HasMaxLength(2000)
                .HasColumnName("resolution_note");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ReworkWorkId).HasColumnName("rework_work_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SeverityId).HasColumnName("severity_id");
            entity.Property(e => e.SeverityKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("SEVERITY")
                .HasColumnName("severity_kind");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("OPEN")
                .HasColumnName("status");
            entity.Property(e => e.WorkChecklistItemId).HasColumnName("work_checklist_item_id");






        });

        modelBuilder.Entity<InventoryDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId);

            entity.ToTable("inventory_documents", tb => tb.HasTrigger("TR_inventory_documents_posted"));

            entity.HasIndex(e => new { e.BranchId, e.Kind, e.Status }, "IX_inventory_documents_branch_id_kind_status");

            entity.HasIndex(e => new { e.BranchId, e.DocumentNo }, "UX_inventory_documents_branch_id_document_no").IsUnique();

            entity.HasIndex(e => new { e.DocumentId, e.BranchId }, "UX_inventory_documents_document_id_branch_id").IsUnique();

            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.DestinationBranchId).HasColumnName("destination_branch_id");
            entity.Property(e => e.DestinationWarehouseId).HasColumnName("destination_warehouse_id");
            entity.Property(e => e.DocumentNo)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("document_no");
            entity.Property(e => e.ExpectedOn).HasColumnName("expected_on");
            entity.Property(e => e.Kind)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.PostedAt).HasColumnName("posted_at");
            entity.Property(e => e.PostedBy).HasColumnName("posted_by");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.ReceivingRoomId).HasColumnName("receiving_room_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SourceDocumentId).HasColumnName("source_document_id");
            entity.Property(e => e.SourceWarehouseId).HasColumnName("source_warehouse_id");
            entity.Property(e => e.SourceWorkId).HasColumnName("source_work_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(200)
                .HasColumnName("supplier_name");











        });

        modelBuilder.Entity<InventoryDocumentLine>(entity =>
        {
            entity.HasKey(e => e.LineId);

            entity.ToTable("inventory_document_lines", tb =>
                {
                    tb.HasTrigger("TR_inventory_lines_posted");
                    tb.HasTrigger("TR_material_approved_lines");
                });

            entity.HasIndex(e => new { e.DocumentId, e.LineNo }, "UX_inventory_document_lines_document_id_line_no").IsUnique();

            entity.HasIndex(e => new { e.LineId, e.BranchId }, "UX_inventory_document_lines_line_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.LineId, e.BranchId, e.ItemId }, "UX_inventory_document_lines_line_id_branch_id_item_id").IsUnique();

            entity.HasIndex(e => new { e.LineId, e.DocumentId }, "UX_inventory_document_lines_line_id_document_id").IsUnique();

            entity.HasIndex(e => new { e.LineId, e.ItemId }, "UX_inventory_document_lines_line_id_item_id").IsUnique();

            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.ActualQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("actual_qty");
            entity.Property(e => e.ApprovedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("approved_qty");
            entity.Property(e => e.BaseQty)
                .HasComputedColumnSql("(CONVERT([decimal](19,6),[actual_qty]*[conversion_factor]))", true)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("base_qty");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ConversionFactor)
                .HasColumnType("decimal(19, 8)")
                .HasColumnName("conversion_factor");
            entity.Property(e => e.CountedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("counted_qty");
            entity.Property(e => e.DamagedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("damaged_qty");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ExpectedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("expected_qty");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.MissingQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("missing_qty");
            entity.Property(e => e.Note)
                .HasMaxLength(2000)
                .HasColumnName("note");
            entity.Property(e => e.RequestedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("requested_qty");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SourceLineId).HasColumnName("source_line_id");
            entity.Property(e => e.UnitCost)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("unit_cost");
            entity.Property(e => e.UnitId).HasColumnName("unit_id");
            entity.Property(e => e.UnitKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("UOM")
                .HasColumnName("unit_kind");





        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.ItemId);

            entity.ToTable("inventory_items");

            entity.HasIndex(e => e.Code, "UX_inventory_items_code").IsUnique();

            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.BaseUnitId).HasColumnName("base_unit_id");
            entity.Property(e => e.BaseUnitKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("UOM")
                .HasColumnName("base_unit_kind");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("ITEM_CATEGORY")
                .HasColumnName("category_kind");
            entity.Property(e => e.Code)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasMaxLength(2000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");


        });

        modelBuilder.Entity<InventoryPolicy>(entity =>
        {
            entity.ToTable("inventory_policies");

            entity.HasIndex(e => new { e.InventoryPolicyId, e.BranchId }, "UX_inventory_policies_inventory_policy_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.WarehouseId, e.ItemId }, "UX_inventory_policies_warehouse_id_item_id").IsUnique();

            entity.Property(e => e.InventoryPolicyId).HasColumnName("inventory_policy_id");
            entity.Property(e => e.AlertedAt).HasColumnName("alerted_at");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LowStockEpisode).HasColumnName("low_stock_episode");
            entity.Property(e => e.MinimumQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("minimum_qty");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.TargetQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("target_qty");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");



        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations", tb => tb.HasTrigger("TR_locations_hierarchy"));

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UX_locations_branch_id_code").IsUnique();

            entity.HasIndex(e => new { e.LocationId, e.BranchId }, "UX_locations_location_id_branch_id").IsUnique();

            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.FloorNumber).HasColumnName("floor_number");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Kind)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.ParentLocationId).HasColumnName("parent_location_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");


        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");

            entity.HasIndex(e => new { e.MembershipId, e.BranchId }, "UX_memberships_membership_id_branch_id").IsUnique();

            entity.HasIndex(e => e.UserId, "UX_memberships_user_id")
                .IsUnique()
                .HasFilter("([is_primary]=(1) AND [valid_to] IS NULL)");

            entity.HasIndex(e => new { e.UserId, e.BranchId, e.Kind, e.DepartmentId }, "UX_memberships_user_id_branch_id_kind_department_id")
                .IsUnique()
                .HasFilter("([valid_to] IS NULL)");

            entity.Property(e => e.MembershipId).HasColumnName("membership_id");
            entity.Property(e => e.AccessLevel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("access_level");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");
            entity.Property(e => e.Kind)
                .HasMaxLength(24)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.Provenance)
                .HasMaxLength(500)
                .HasColumnName("provenance");
            entity.Property(e => e.RecordedBy).HasColumnName("recorded_by");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ValidFrom)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("valid_from");
            entity.Property(e => e.ValidTo).HasColumnName("valid_to");




        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.DeliveredAt, e.AvailableAt }, "IX_notifications_delivered_at_available_at");

            entity.HasIndex(e => new { e.RecipientUserId, e.CreatedAt }, "IX_notifications_recipient_user_id_created_at");

            entity.HasIndex(e => new { e.NotificationId, e.BranchId }, "UX_notifications_notification_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.RecipientUserId, e.EventKey }, "UX_notifications_recipient_user_id_event_key").IsUnique();

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.AcknowledgedAt).HasColumnName("acknowledged_at");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count");
            entity.Property(e => e.AvailableAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("available_at");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.EventKey).HasColumnName("event_key");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.LastError)
                .HasMaxLength(2000)
                .HasColumnName("last_error");
            entity.Property(e => e.LeaseId).HasColumnName("lease_id");
            entity.Property(e => e.LeaseUntil).HasColumnName("lease_until");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.NotificationType)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("notification_type");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(e => e.RequiresAcknowledgment).HasColumnName("requires_acknowledgment");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.Title)
                .HasMaxLength(250)
                .HasColumnName("title");
            entity.Property(e => e.WorkId).HasColumnName("work_id");





        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");

            entity.HasIndex(e => e.Code, "UX_permissions_code").IsUnique();

            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ModuleCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("module_code");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
        });

        modelBuilder.Entity<ReferenceValue>(entity =>
        {
            entity.HasKey(e => e.ReferenceId);

            entity.ToTable("reference_values");

            entity.HasIndex(e => new { e.Kind, e.Code }, "UX_reference_values_kind_code").IsUnique();

            entity.HasIndex(e => new { e.ReferenceId, e.Kind }, "UX_reference_values_reference_id_kind").IsUnique();

            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.BaseUnitId).HasColumnName("base_unit_id");
            entity.Property(e => e.Code)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.ConversionFactor)
                .HasColumnType("decimal(19, 8)")
                .HasColumnName("conversion_factor");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Kind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SettingsJson).HasColumnName("settings_json");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");


        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");

            entity.HasIndex(e => e.Code, "UX_roles_code").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Code)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.GrantId);

            entity.ToTable("role_permissions");

            entity.HasIndex(e => new { e.RoleId, e.PermissionId }, "UX_role_permissions_role_id_permission_id").IsUnique();

            entity.Property(e => e.GrantId).HasColumnName("grant_id");
            entity.Property(e => e.DataScope)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("data_scope");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");


        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms", tb =>
                {
                    tb.HasTrigger("TR_rooms_cycle");
                    tb.HasTrigger("TR_rooms_ready_admission");
                });

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UX_rooms_branch_id_code").IsUnique();

            entity.HasIndex(e => new { e.RoomId, e.BranchId }, "UX_rooms_room_id_branch_id").IsUnique();

            entity.Property(e => e.RoomId)
                .ValueGeneratedOnAdd()
                .HasColumnName("room_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CurrentCleaningWorkId).HasColumnName("current_cleaning_work_id");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.OperationalStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DIRTY")
                .HasColumnName("operational_status");
            entity.Property(e => e.RoomTypeId).HasColumnName("room_type_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.TurnoverNo).HasColumnName("turnover_no");






        });

        modelBuilder.Entity<RoomBlock>(entity =>
        {
            entity.HasKey(e => e.BlockId);

            entity.ToTable("room_blocks");

            entity.HasIndex(e => new { e.BlockId, e.BranchId }, "UX_room_blocks_block_id_branch_id").IsUnique();

            entity.HasIndex(e => e.RoomId, "UX_room_blocks_room_id")
                .IsUnique()
                .HasFilter("([released_at] IS NULL)");

            entity.Property(e => e.BlockId).HasColumnName("block_id");
            entity.Property(e => e.BlockedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("blocked_at");
            entity.Property(e => e.BlockedBy).HasColumnName("blocked_by");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ExpectedReleaseAt).HasColumnName("expected_release_at");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.ReleasedAt).HasColumnName("released_at");
            entity.Property(e => e.ReleasedBy).HasColumnName("released_by");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SourceWorkId).HasColumnName("source_work_id");





        });

        modelBuilder.Entity<RoomEvent>(entity =>
        {
            entity.ToTable("room_events", tb => tb.HasTrigger("TR_room_events_immutable"));

            entity.HasIndex(e => new { e.RoomId, e.OccurredAt }, "IX_room_events_room_id_occurred_at");

            entity.HasIndex(e => new { e.RoomEventId, e.BranchId }, "UX_room_events_room_event_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.RoomId, e.ActorUserId, e.OperationKey }, "UX_room_events_room_id_actor_user_id_operation_key").IsUnique();

            entity.Property(e => e.RoomEventId).HasColumnName("room_event_id");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("event_type");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("from_status");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OperationKey).HasColumnName("operation_key");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.SourceWorkId).HasColumnName("source_work_id");
            entity.Property(e => e.ToStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("to_status");
            entity.Property(e => e.TurnoverNo).HasColumnName("turnover_no");




        });

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.ToTable("room_types");

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UX_room_types_branch_id_code").IsUnique();

            entity.HasIndex(e => new { e.RoomTypeId, e.BranchId }, "UX_room_types_room_type_id_branch_id").IsUnique();

            entity.Property(e => e.RoomTypeId).HasColumnName("room_type_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasMaxLength(2000)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);

            entity.ToTable("stock_movements", tb =>
                {
                    tb.HasTrigger("TR_material_issue_approval");
                    tb.HasTrigger("TR_stock_movements_immutable");
                    tb.HasTrigger("TR_stock_movements_posted");
                    tb.HasTrigger("TR_stock_movements_quantity");
                });

            entity.HasIndex(e => new { e.WarehouseId, e.ItemId, e.OccurredAt }, "IX_stock_movements_warehouse_id_item_id_occurred_at");

            entity.HasIndex(e => new { e.LineId, e.WarehouseId }, "UX_stock_movements_line_id_warehouse_id").IsUnique();

            entity.HasIndex(e => new { e.MovementId, e.BranchId }, "UX_stock_movements_movement_id_branch_id").IsUnique();

            entity.HasIndex(e => e.ReversalOfMovementId, "UX_stock_movements_reversal_of_movement_id")
                .IsUnique()
                .HasFilter("([reversal_of_movement_id] IS NOT NULL)");

            entity.Property(e => e.MovementId).HasColumnName("movement_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.ReversalOfMovementId).HasColumnName("reversal_of_movement_id");
            entity.Property(e => e.SignedBaseQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("signed_base_qty");
            entity.Property(e => e.UnitCost)
                .HasColumnType("decimal(19, 4)")
                .HasColumnName("unit_cost");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");





        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UX_users_email")
                .IsUnique()
                .HasFilter("([email] IS NOT NULL)");

            entity.HasIndex(e => e.EmployeeCode, "UX_users_employee_code").IsUnique();

            entity.HasIndex(e => e.Username, "UX_users_username").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AccountStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("account_status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("employee_code");
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("employment_status");
            entity.Property(e => e.FailedLoginCount).HasColumnName("failed_login_count");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
            entity.Property(e => e.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(500)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SecurityStamp)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("security_stamp");
            entity.Property(e => e.TemporaryPasswordExpiresAt).HasColumnName("temporary_password_expires_at");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .HasColumnName("username");

        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");

            entity.HasIndex(e => new { e.UserId, e.RoleId, e.BranchId }, "UX_user_roles_user_id_role_id_branch_id")
                .IsUnique()
                .HasFilter("([revoked_at] IS NULL)");

            entity.Property(e => e.UserRoleId).HasColumnName("user_role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.UserId).HasColumnName("user_id");




        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            entity.ToTable("user_sessions");

            entity.HasIndex(e => e.TokenHash, "UX_user_sessions_token_hash").IsUnique();

            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(32)
                .IsFixedLength()
                .HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");

        });

        modelBuilder.Entity<VBacklogHandover>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_backlog_handovers");

            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.DecidedAt).HasColumnName("decided_at");
            entity.Property(e => e.Decision)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("decision");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(e => e.SenderUserId).HasColumnName("sender_user_id");
            entity.Property(e => e.WorkEventId).HasColumnName("work_event_id");
            entity.Property(e => e.WorkId).HasColumnName("work_id");
        });

        modelBuilder.Entity<VInventoryDocumentTotal>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_inventory_document_totals");

            entity.Property(e => e.BaseQty)
                .HasColumnType("decimal(38, 6)")
                .HasColumnName("base_qty");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.Kind)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.LineCount).HasColumnName("line_count");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.TotalCost)
                .HasColumnType("decimal(38, 9)")
                .HasColumnName("total_cost");
        });

        modelBuilder.Entity<VMaterialFulfillment>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_material_fulfillment");

            entity.Property(e => e.ApprovedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("approved_qty");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ConversionFactor)
                .HasColumnType("decimal(19, 8)")
                .HasColumnName("conversion_factor");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.IssuedBaseQty)
                .HasColumnType("decimal(38, 6)")
                .HasColumnName("issued_base_qty");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.MissingBaseQty)
                .HasColumnType("decimal(38, 6)")
                .HasColumnName("missing_base_qty");
            entity.Property(e => e.RequestedQty)
                .HasColumnType("decimal(19, 6)")
                .HasColumnName("requested_qty");
        });

        modelBuilder.Entity<VRoomReadiness>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_room_readiness");

            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.IsOccupied).HasColumnName("is_occupied");
            entity.Property(e => e.OperationalStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("operational_status");
            entity.Property(e => e.Readiness)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("readiness");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.TurnoverNo).HasColumnName("turnover_no");
        });

        modelBuilder.Entity<VStockBalance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_stock_balances");

            entity.Property(e => e.BaseQty)
                .HasColumnType("decimal(38, 6)")
                .HasColumnName("base_qty");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        });

        modelBuilder.Entity<VWorkDuration>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_work_duration");

            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CompletedSeconds).HasColumnName("completed_seconds");
            entity.Property(e => e.OpenIntervals).HasColumnName("open_intervals");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WorkId).HasColumnName("work_id");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses");

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UX_warehouses_branch_id_code").IsUnique();

            entity.HasIndex(e => new { e.WarehouseId, e.BranchId }, "UX_warehouses_warehouse_id_branch_id").IsUnique();

            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");



        });

        modelBuilder.Entity<WorkAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);

            entity.ToTable("work_assignments", tb => tb.HasTrigger("TR_work_assignments_open_time"));

            entity.HasIndex(e => new { e.AssignmentId, e.BranchId }, "UX_work_assignments_assignment_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.AssignmentId, e.WorkId, e.BranchId, e.AssigneeUserId }, "UX_work_assignments_assignment_id_work_id_branch_id_assignee_user_id").IsUnique();

            entity.HasIndex(e => e.WorkId, "UX_work_assignments_work_id")
                .IsUnique()
                .HasFilter("([assignment_status]='ACTIVE')");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssigneeUserId).HasColumnName("assignee_user_id");
            entity.Property(e => e.AssignmentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("assignment_status");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .HasColumnName("reason");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.WorkId).HasColumnName("work_id");




        });

        modelBuilder.Entity<WorkChecklistItem>(entity =>
        {
            entity.ToTable("work_checklist_items", tb => tb.HasTrigger("TR_inspection_results_final"));

            entity.HasIndex(e => new { e.WorkChecklistItemId, e.BranchId }, "UX_work_checklist_items_work_checklist_item_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.WorkChecklistItemId, e.WorkId }, "UX_work_checklist_items_work_checklist_item_id_work_id").IsUnique();

            entity.HasIndex(e => new { e.WorkId, e.ChecklistItemId }, "UX_work_checklist_items_work_id_checklist_item_id").IsUnique();

            entity.Property(e => e.WorkChecklistItemId).HasColumnName("work_checklist_item_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CheckedAt).HasColumnName("checked_at");
            entity.Property(e => e.CheckedBy).HasColumnName("checked_by");
            entity.Property(e => e.ChecklistItemId).HasColumnName("checklist_item_id");
            entity.Property(e => e.ChecklistTemplateId).HasColumnName("checklist_template_id");
            entity.Property(e => e.DescriptionSnapshot)
                .HasMaxLength(2000)
                .HasColumnName("description_snapshot");
            entity.Property(e => e.Note)
                .HasMaxLength(2000)
                .HasColumnName("note");
            entity.Property(e => e.Outcome)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("outcome");
            entity.Property(e => e.RequiredSnapshot).HasColumnName("required_snapshot");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.WeightSnapshot)
                .HasColumnType("decimal(9, 3)")
                .HasColumnName("weight_snapshot");
            entity.Property(e => e.WorkId).HasColumnName("work_id");





        });

        modelBuilder.Entity<WorkEvent>(entity =>
        {
            entity.ToTable("work_events", tb =>
                {
                    tb.HasTrigger("TR_work_events_handover");
                    tb.HasTrigger("TR_work_events_immutable");
                });

            entity.HasIndex(e => new { e.WorkId, e.OccurredAt }, "IX_work_events_work_id_occurred_at");

            entity.HasIndex(e => new { e.WorkEventId, e.BranchId }, "UX_work_events_work_event_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.WorkEventId, e.WorkId, e.BranchId }, "UX_work_events_work_event_id_work_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.WorkId, e.ActorUserId, e.OperationKey }, "UX_work_events_work_id_actor_user_id_operation_key").IsUnique();

            entity.Property(e => e.WorkEventId).HasColumnName("work_event_id");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("event_type");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("from_status");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OperationKey).HasColumnName("operation_key");
            entity.Property(e => e.ProgressPercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("progress_percent");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(e => e.RelatedEventId).HasColumnName("related_event_id");
            entity.Property(e => e.SnapshotJson).HasColumnName("snapshot_json");
            entity.Property(e => e.ToStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("to_status");
            entity.Property(e => e.WorkId).HasColumnName("work_id");





        });

        modelBuilder.Entity<WorkInterval>(entity =>
        {
            entity.HasKey(e => e.IntervalId);

            entity.ToTable("work_intervals", tb =>
                {
                    tb.HasTrigger("TR_work_intervals_assignment");
                    tb.HasTrigger("TR_work_intervals_no_delete");
                });

            entity.HasIndex(e => new { e.WorkId, e.UserId, e.EffectiveStartedAt }, "IX_work_intervals_work_id_user_id_effective_started_at");

            entity.HasIndex(e => new { e.IntervalId, e.BranchId }, "UX_work_intervals_interval_id_branch_id").IsUnique();

            entity.HasIndex(e => e.UserId, "UX_work_intervals_user_id")
                .IsUnique()
                .HasFilter("([closed_at] IS NULL)");

            entity.Property(e => e.IntervalId).HasColumnName("interval_id");
            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CloseReason)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("close_reason");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.ClosedBy).HasColumnName("closed_by");
            entity.Property(e => e.EffectiveEndedAt).HasColumnName("effective_ended_at");
            entity.Property(e => e.EffectiveStartedAt).HasColumnName("effective_started_at");
            entity.Property(e => e.OpenedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("opened_at");
            entity.Property(e => e.OpenedBy).HasColumnName("opened_by");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WorkId).HasColumnName("work_id");






        });

        modelBuilder.Entity<WorkItem>(entity =>
        {
            entity.HasKey(e => e.WorkId);

            entity.ToTable("work_items", tb =>
                {
                    tb.HasTrigger("TR_work_items_relations");
                    tb.HasTrigger("TR_work_items_review");
                });

            entity.HasIndex(e => new { e.BranchId, e.Kind, e.Status, e.DueAt }, "IX_work_items_branch_id_kind_status_due_at");

            entity.HasIndex(e => new { e.RoomId, e.TurnoverNo, e.Kind }, "IX_work_items_room_id_turnover_no_kind");

            entity.HasIndex(e => new { e.BranchId, e.WorkNo }, "UX_work_items_branch_id_work_no").IsUnique();

            entity.HasIndex(e => new { e.WorkId, e.BranchId }, "UX_work_items_work_id_branch_id").IsUnique();

            entity.HasIndex(e => new { e.WorkId, e.BranchId, e.RoomId, e.TurnoverNo }, "UX_work_items_work_id_branch_id_room_id_turnover_no").IsUnique();

            entity.HasIndex(e => new { e.WorkId, e.ChecklistTemplateId }, "UX_work_items_work_id_checklist_template_id").IsUnique();

            entity.HasIndex(e => new { e.WorkTemplateId, e.OccurrenceAt }, "UX_work_items_work_template_id_occurrence_at")
                .IsUnique()
                .HasFilter("([work_template_id] IS NOT NULL AND [occurrence_at] IS NOT NULL)");

            entity.Property(e => e.WorkId).HasColumnName("work_id");
            entity.Property(e => e.ActualStartAt).HasColumnName("actual_start_at");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("WORK_CATEGORY")
                .HasColumnName("category_kind");
            entity.Property(e => e.ChecklistTemplateId)
                .HasColumnName("checklist_template_id");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CompletionNote).HasColumnName("completion_note");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DueAt).HasColumnName("due_at");
            entity.Property(e => e.Kind)
                .HasMaxLength(24)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.OccurrenceAt).HasColumnName("occurrence_at");
            entity.Property(e => e.ParentWorkId).HasColumnName("parent_work_id");
            entity.Property(e => e.PlannedStartAt).HasColumnName("planned_start_at");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.PriorityKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("PRIORITY")
                .HasColumnName("priority_kind");
            entity.Property(e => e.ProgressPercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("progress_percent");
            entity.Property(e => e.RequesterDepartmentId).HasColumnName("requester_department_id");
            entity.Property(e => e.ResponseDeadlineAt).HasColumnName("response_deadline_at");
            entity.Property(e => e.Result)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("result");
            entity.Property(e => e.RoomId)
                .HasColumnName("room_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SeverityId).HasColumnName("severity_id");
            entity.Property(e => e.SeverityKind)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("SEVERITY")
                .HasColumnName("severity_kind");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(250)
                .HasColumnName("title");
            entity.Property(e => e.TurnoverNo)
                .HasColumnName("turnover_no");
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
            entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");
            entity.Property(e => e.WorkNo)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("work_no");
            entity.Property(e => e.WorkTemplateId).HasColumnName("work_template_id");














        });

        modelBuilder.Entity<WorkLink>(entity =>
        {
            entity.ToTable("work_links");

            entity.HasIndex(e => new { e.SourceWorkId, e.TargetWorkId, e.Relation }, "UX_work_links_source_work_id_target_work_id_relation").IsUnique();

            entity.HasIndex(e => new { e.WorkLinkId, e.BranchId }, "UX_work_links_work_link_id_branch_id").IsUnique();

            entity.Property(e => e.WorkLinkId).HasColumnName("work_link_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Relation)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("relation");
            entity.Property(e => e.SourceWorkId).HasColumnName("source_work_id");
            entity.Property(e => e.TargetWorkId).HasColumnName("target_work_id");




        });

        modelBuilder.Entity<WorkTemplate>(entity =>
        {
            entity.ToTable("work_templates");

            entity.HasIndex(e => new { e.BranchId, e.Code }, "UX_work_templates_branch_id_code").IsUnique();

            entity.HasIndex(e => new { e.WorkTemplateId, e.BranchId }, "UX_work_templates_work_template_id_branch_id").IsUnique();

            entity.Property(e => e.WorkTemplateId).HasColumnName("work_template_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ChecklistTemplateId).HasColumnName("checklist_template_id");
            entity.Property(e => e.Code)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("code");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Frequency)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("frequency");
            entity.Property(e => e.FrequencyInterval).HasColumnName("frequency_interval");
            entity.Property(e => e.Instruction).HasColumnName("instruction");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Kind)
                .HasMaxLength(24)
                .IsUnicode(false)
                .HasColumnName("kind");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.NextDueAt).HasColumnName("next_due_at");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.RoomTypeId).HasColumnName("room_type_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.ScheduleJson).HasColumnName("schedule_json");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");








        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
