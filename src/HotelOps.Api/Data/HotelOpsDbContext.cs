using System;
using System.Collections.Generic;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public partial class HotelOpsDbContext : DbContext
{
    public HotelOpsDbContext(DbContextOptions<HotelOpsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<AssetConditionHistory> AssetConditionHistories { get; set; }

    public virtual DbSet<AssetHandover> AssetHandovers { get; set; }

    public virtual DbSet<AssetLocationHistory> AssetLocationHistories { get; set; }

    public virtual DbSet<AssetType> AssetTypes { get; set; }

    public virtual DbSet<Attachment> Attachments { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<CleaningAssignment> CleaningAssignments { get; set; }

    public virtual DbSet<CleaningProgressLog> CleaningProgressLogs { get; set; }

    public virtual DbSet<CleaningRework> CleaningReworks { get; set; }

    public virtual DbSet<CleaningTask> CleaningTasks { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Floor> Floors { get; set; }

    public virtual DbSet<Incident> Incidents { get; set; }

    public virtual DbSet<IncidentEvent> IncidentEvents { get; set; }

    public virtual DbSet<InspectionChecklist> InspectionChecklists { get; set; }

    public virtual DbSet<InspectionChecklistItem> InspectionChecklistItems { get; set; }

    public virtual DbSet<InspectionChecklistVersion> InspectionChecklistVersions { get; set; }

    public virtual DbSet<InspectionDefect> InspectionDefects { get; set; }

    public virtual DbSet<InspectionResult> InspectionResults { get; set; }

    public virtual DbSet<InternalRequest> InternalRequests { get; set; }

    public virtual DbSet<InternalRequestAssignment> InternalRequestAssignments { get; set; }

    public virtual DbSet<InternalRequestEvent> InternalRequestEvents { get; set; }

    public virtual DbSet<InternalRequestFeedback> InternalRequestFeedbacks { get; set; }

    public virtual DbSet<InternalRequestType> InternalRequestTypes { get; set; }

    public virtual DbSet<InventoryItem> InventoryItems { get; set; }

    public virtual DbSet<ItemCategory> ItemCategories { get; set; }

    public virtual DbSet<MaintenanceWork> MaintenanceWorks { get; set; }

    public virtual DbSet<MaintenanceWorkLog> MaintenanceWorkLogs { get; set; }

    public virtual DbSet<MaterialFulfillment> MaterialFulfillments { get; set; }

    public virtual DbSet<MaterialFulfillmentLine> MaterialFulfillmentLines { get; set; }

    public virtual DbSet<MaterialRequest> MaterialRequests { get; set; }

    public virtual DbSet<MaterialRequestApproval> MaterialRequestApprovals { get; set; }

    public virtual DbSet<MaterialRequestLine> MaterialRequestLines { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<NotificationRecipient> NotificationRecipients { get; set; }

    public virtual DbSet<OperationalTask> OperationalTasks { get; set; }

    public virtual DbSet<OperationalTaskAssignment> OperationalTaskAssignments { get; set; }

    public virtual DbSet<OperationalTaskDependency> OperationalTaskDependencies { get; set; }

    public virtual DbSet<OperationalTaskProgress> OperationalTaskProgresses { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<PreventiveMaintenanceRun> PreventiveMaintenanceRuns { get; set; }

    public virtual DbSet<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules { get; set; }

    public virtual DbSet<ReferenceValue> ReferenceValues { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomBlock> RoomBlocks { get; set; }

    public virtual DbSet<RoomInspection> RoomInspections { get; set; }

    public virtual DbSet<RoomStatusHistory> RoomStatusHistories { get; set; }

    public virtual DbSet<RoomType> RoomTypes { get; set; }

    public virtual DbSet<ShiftAssignment> ShiftAssignments { get; set; }

    public virtual DbSet<ShiftAttendance> ShiftAttendances { get; set; }

    public virtual DbSet<ShiftTemplate> ShiftTemplates { get; set; }

    public virtual DbSet<StockDocument> StockDocuments { get; set; }

    public virtual DbSet<StockDocumentLine> StockDocumentLines { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    public virtual DbSet<StockThreshold> StockThresholds { get; set; }

    public virtual DbSet<Stocktake> Stocktakes { get; set; }

    public virtual DbSet<StocktakeLine> StocktakeLines { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<UnitsOfMeasure> UnitsOfMeasures { get; set; }

    public virtual DbSet<UomConversion> UomConversions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBranchAccess> UserBranchAccesses { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<WorkCategory> WorkCategories { get; set; }

    public virtual DbSet<WorkShift> WorkShifts { get; set; }

    public virtual DbSet<StockTransferReceipt> StockTransferReceipts { get; set; }

    public virtual DbSet<StockTransferReceiptLine> StockTransferReceiptLines { get; set; }

    public virtual DbSet<StockReceiptLineDetail> StockReceiptLineDetails { get; set; }

    public virtual DbSet<StockReturnLineDetail> StockReturnLineDetails { get; set; }

    public virtual DbSet<MaterialRequestApprovalCycle> MaterialRequestApprovalCycles { get; set; }

    public virtual DbSet<MaterialRequestApprovalLineSnapshot> MaterialRequestApprovalLineSnapshots { get; set; }

    public virtual DbSet<MaterialReplenishment> MaterialReplenishments { get; set; }

    public virtual DbSet<MaterialReplenishmentLine> MaterialReplenishmentLines { get; set; }

    public virtual DbSet<MaterialReplenishmentReceiptLine> MaterialReplenishmentReceiptLines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.AreaId).HasName("PK__areas__985D6D6B56C7E2CB");

            entity.ToTable("areas");

            entity.HasIndex(e => new { e.BranchId, e.AreaCode }, "areas_index_1").IsUnique();

            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.AreaCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("area_code");
            entity.Property(e => e.AreaName)
                .HasMaxLength(150)
                .HasColumnName("area_name");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FloorId).HasColumnName("floor_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.Branch).WithMany(p => p.Areas)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_areas_branch_id");

            entity.HasOne(d => d.Floor).WithMany(p => p.Areas)
                .HasForeignKey(d => d.FloorId)
                .HasConstraintName("FK_areas_floor_id");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.AssetId).HasName("PK__assets__D28B561D387209AB");

            entity.ToTable("assets");

            entity.HasIndex(e => e.AssetCode, "UQ__assets__89F020EB7838CE06").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.LifecycleStatus }, "assets_index_17");

            entity.HasIndex(e => e.SerialNumber, "assets_index_18");

            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AcquisitionCost)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("acquisition_cost");
            entity.Property(e => e.AssetCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("asset_code");
            entity.Property(e => e.AssetName)
                .HasMaxLength(200)
                .HasColumnName("asset_name");
            entity.Property(e => e.AssetTypeId).HasColumnName("asset_type_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Brand)
                .HasMaxLength(100)
                .HasColumnName("brand");
            entity.Property(e => e.ConditionStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("GOOD")
                .HasColumnName("condition_status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CurrentAreaId).HasColumnName("current_area_id");
            entity.Property(e => e.CurrentDepartmentId).HasColumnName("current_department_id");
            entity.Property(e => e.CurrentRoomId).HasColumnName("current_room_id");
            entity.Property(e => e.CustodianUserId).HasColumnName("custodian_user_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.LifecycleStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("IN_USE")
                .HasColumnName("lifecycle_status");
            entity.Property(e => e.Model)
                .HasMaxLength(100)
                .HasColumnName("model");
            entity.Property(e => e.PurchaseDate).HasColumnName("purchase_date");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("serial_number");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.WarrantyExpiryDate).HasColumnName("warranty_expiry_date");

            entity.HasOne(d => d.AssetType).WithMany(p => p.Assets)
                .HasForeignKey(d => d.AssetTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_assets_asset_type_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.Assets)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_assets_branch_id");

            entity.HasOne(d => d.CurrentArea).WithMany(p => p.Assets)
                .HasForeignKey(d => d.CurrentAreaId)
                .HasConstraintName("FK_assets_current_area_id");

            entity.HasOne(d => d.CurrentDepartment).WithMany(p => p.Assets)
                .HasForeignKey(d => d.CurrentDepartmentId)
                .HasConstraintName("FK_assets_current_department_id");

            entity.HasOne(d => d.CurrentRoom).WithMany(p => p.Assets)
                .HasForeignKey(d => d.CurrentRoomId)
                .HasConstraintName("FK_assets_current_room_id");

            entity.HasOne(d => d.CustodianUser).WithMany(p => p.Assets)
                .HasForeignKey(d => d.CustodianUserId)
                .HasConstraintName("FK_assets_custodian_user_id");
        });

        modelBuilder.Entity<AssetConditionHistory>(entity =>
        {
            entity.HasKey(e => e.ConditionHistoryId).HasName("PK__asset_co__323B4C70CB901C05");

            entity.ToTable("asset_condition_history");

            entity.Property(e => e.ConditionHistoryId).HasColumnName("condition_history_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.NewCondition)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("new_condition");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.OldCondition)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("old_condition");
            entity.Property(e => e.RelatedIncidentId).HasColumnName("related_incident_id");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetConditionHistories)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_asset_condition_history_asset_id");

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.AssetConditionHistories)
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_asset_condition_history_changed_by");

            entity.HasOne(d => d.RelatedIncident).WithMany(p => p.AssetConditionHistories)
                .HasForeignKey(d => d.RelatedIncidentId)
                .HasConstraintName("FK_asset_condition_history_related_incident_id");
        });

        modelBuilder.Entity<AssetHandover>(entity =>
        {
            entity.HasKey(e => e.HandoverId).HasName("PK__asset_ha__67DDAEB220E57CA7");

            entity.ToTable("asset_handovers");

            entity.HasIndex(e => e.HandoverNo, "UQ__asset_ha__67DDC72F6A589925").IsUnique();

            entity.Property(e => e.HandoverId).HasColumnName("handover_id");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.ConditionNote).HasColumnName("condition_note");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FromDepartmentId).HasColumnName("from_department_id");
            entity.Property(e => e.FromUserId).HasColumnName("from_user_id");
            entity.Property(e => e.HandedOverAt).HasColumnName("handed_over_at");
            entity.Property(e => e.HandoverNo)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("handover_no");
            entity.Property(e => e.HandoverStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("handover_status");
            entity.Property(e => e.ToDepartmentId).HasColumnName("to_department_id");
            entity.Property(e => e.ToUserId).HasColumnName("to_user_id");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetHandovers)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_asset_handovers_asset_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetHandoverCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_asset_handovers_created_by");

            entity.HasOne(d => d.FromDepartment).WithMany(p => p.AssetHandoverFromDepartments)
                .HasForeignKey(d => d.FromDepartmentId)
                .HasConstraintName("FK_asset_handovers_from_department_id");

            entity.HasOne(d => d.FromUser).WithMany(p => p.AssetHandoverFromUsers)
                .HasForeignKey(d => d.FromUserId)
                .HasConstraintName("FK_asset_handovers_from_user_id");

            entity.HasOne(d => d.ToDepartment).WithMany(p => p.AssetHandoverToDepartments)
                .HasForeignKey(d => d.ToDepartmentId)
                .HasConstraintName("FK_asset_handovers_to_department_id");

            entity.HasOne(d => d.ToUser).WithMany(p => p.AssetHandoverToUsers)
                .HasForeignKey(d => d.ToUserId)
                .HasConstraintName("FK_asset_handovers_to_user_id");
        });

        modelBuilder.Entity<AssetLocationHistory>(entity =>
        {
            entity.HasKey(e => e.LocationHistoryId).HasName("PK__asset_lo__BAC098A6C895733A");

            entity.ToTable("asset_location_history");

            entity.Property(e => e.LocationHistoryId).HasColumnName("location_history_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.EffectiveAt).HasColumnName("effective_at");
            entity.Property(e => e.FromLocationId).HasColumnName("from_location_id");
            entity.Property(e => e.FromLocationType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("from_location_type");
            entity.Property(e => e.MovedBy).HasColumnName("moved_by");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ToLocationId).HasColumnName("to_location_id");
            entity.Property(e => e.ToLocationType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("to_location_type");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetLocationHistories)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_asset_location_history_asset_id");

            entity.HasOne(d => d.MovedByNavigation).WithMany(p => p.AssetLocationHistories)
                .HasForeignKey(d => d.MovedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_asset_location_history_moved_by");
        });

        modelBuilder.Entity<AssetType>(entity =>
        {
            entity.HasKey(e => e.AssetTypeId).HasName("PK__asset_ty__95A1E2BC28853AF7");

            entity.ToTable("asset_types");

            entity.HasIndex(e => e.TypeCode, "UQ__asset_ty__2CB4DBF5DAD316F6").IsUnique();

            entity.Property(e => e.AssetTypeId).HasColumnName("asset_type_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.TypeCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("type_code");
            entity.Property(e => e.TypeName)
                .HasMaxLength(150)
                .HasColumnName("type_name");
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId).HasName("PK__attachme__B74DF4E293C29C82");

            entity.ToTable("attachments");

            entity.HasIndex(e => new { e.EntityType, e.EntityId }, "attachments_index_44");

            entity.Property(e => e.AttachmentId).HasColumnName("attachment_id");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("entity_type");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.FileUrl).HasColumnName("file_url");
            entity.Property(e => e.MimeType)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("mime_type");
            entity.Property(e => e.OriginalFileName)
                .HasMaxLength(255)
                .HasColumnName("original_file_name");
            entity.Property(e => e.StorageKey)
                .HasMaxLength(500)
                .HasColumnName("storage_key");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.Attachments)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_attachments_uploaded_by");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__audit_lo__5AF33E333EAC675C");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt }, "audit_logs_index_45");

            entity.HasIndex(e => new { e.ActorUserId, e.OccurredAt }, "audit_logs_index_46");

            entity.Property(e => e.AuditId).HasColumnName("audit_id");
            entity.Property(e => e.Action)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("action");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ChangedFields).HasColumnName("changed_fields");
            entity.Property(e => e.EntityId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entity_type");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("ip_address");
            entity.Property(e => e.NewValues).HasColumnName("new_values");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.OldValues).HasColumnName("old_values");
            entity.Property(e => e.RequestId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("request_id");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK_audit_logs_actor_user_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_audit_logs_branch_id");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("PK__branches__E55E37DEE9AE3269");

            entity.ToTable("branches");

            entity.HasIndex(e => e.BranchCode, "UQ__branches__A9F83E3BF9DD6797").IsUnique();

            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.BranchCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("branch_code");
            entity.Property(e => e.BranchName)
                .HasMaxLength(200)
                .HasColumnName("branch_name");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.Timezone)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasDefaultValue("Asia/Ho_Chi_Minh")
                .HasColumnName("timezone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<CleaningAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__cleaning__DA89181490CF02ED");

            entity.ToTable("cleaning_assignments");

            entity.HasIndex(e => new { e.CleaningTaskId, e.AssignedAt }, "cleaning_assignments_index_8");

            entity.HasIndex(e => new { e.AssigneeUserId, e.AssignmentStatus }, "cleaning_assignments_index_9");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssigneeUserId).HasColumnName("assignee_user_id");
            entity.Property(e => e.AssignmentStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("assignment_status");
            entity.Property(e => e.CleaningTaskId).HasColumnName("cleaning_task_id");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.Reason).HasColumnName("reason");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.CleaningAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_assignments_assigned_by");

            entity.HasOne(d => d.AssigneeUser).WithMany(p => p.CleaningAssignmentAssigneeUsers)
                .HasForeignKey(d => d.AssigneeUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_assignments_assignee_user_id");

            entity.HasOne(d => d.CleaningTask).WithMany(p => p.CleaningAssignments)
                .HasForeignKey(d => d.CleaningTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_assignments_cleaning_task_id");
        });

        modelBuilder.Entity<CleaningProgressLog>(entity =>
        {
            entity.HasKey(e => e.ProgressId).HasName("PK__cleaning__49B3D8C18015562F");

            entity.ToTable("cleaning_progress_logs");

            entity.Property(e => e.ProgressId).HasColumnName("progress_id");
            entity.Property(e => e.BlockerTypeId).HasColumnName("blocker_type_id");
            entity.Property(e => e.CleaningTaskId).HasColumnName("cleaning_task_id");
            entity.Property(e => e.LoggedAt).HasColumnName("logged_at");
            entity.Property(e => e.LoggedBy).HasColumnName("logged_by");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");

            entity.HasOne(d => d.BlockerType).WithMany(p => p.CleaningProgressLogs)
                .HasForeignKey(d => d.BlockerTypeId)
                .HasConstraintName("FK_cleaning_progress_logs_blocker_type_id");

            entity.HasOne(d => d.CleaningTask).WithMany(p => p.CleaningProgressLogs)
                .HasForeignKey(d => d.CleaningTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_progress_logs_cleaning_task_id");

            entity.HasOne(d => d.LoggedByNavigation).WithMany(p => p.CleaningProgressLogs)
                .HasForeignKey(d => d.LoggedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_progress_logs_logged_by");
        });

        modelBuilder.Entity<CleaningRework>(entity =>
        {
            entity.HasKey(e => e.ReworkId).HasName("PK__cleaning__E97779ADA0ACCDC3");

            entity.ToTable("cleaning_reworks");

            entity.Property(e => e.ReworkId).HasColumnName("rework_id");
            entity.Property(e => e.AssigneeUserId).HasColumnName("assignee_user_id");
            entity.Property(e => e.CleaningTaskId).HasColumnName("cleaning_task_id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.RequestedAt).HasColumnName("requested_at");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");

            entity.HasOne(d => d.AssigneeUser).WithMany(p => p.CleaningReworks)
                .HasForeignKey(d => d.AssigneeUserId)
                .HasConstraintName("FK_cleaning_reworks_assignee_user_id");

            entity.HasOne(d => d.CleaningTask).WithMany(p => p.CleaningReworks)
                .HasForeignKey(d => d.CleaningTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_reworks_cleaning_task_id");

            entity.HasOne(d => d.Inspection).WithMany(p => p.CleaningReworks)
                .HasForeignKey(d => d.InspectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_reworks_inspection_id");
        });

        modelBuilder.Entity<CleaningTask>(entity =>
        {
            entity.HasKey(e => e.CleaningTaskId).HasName("PK__cleaning__F39385631C58DB4E");

            entity.ToTable("cleaning_tasks");

            entity.HasIndex(e => e.TaskNo, "UQ__cleaning__04924C1399B9F5DA").IsUnique();

            entity.HasIndex(e => new { e.RoomId, e.Status }, "cleaning_tasks_index_6");

            entity.HasIndex(e => e.DueAt, "cleaning_tasks_index_7");

            entity.Property(e => e.CleaningTaskId).HasColumnName("cleaning_task_id");
            entity.Property(e => e.ActualEndAt).HasColumnName("actual_end_at");
            entity.Property(e => e.ActualStartAt).HasColumnName("actual_start_at");
            entity.Property(e => e.CleaningTypeId).HasColumnName("cleaning_type_id");
            entity.Property(e => e.CompletionNote).HasColumnName("completion_note");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DueAt).HasColumnName("due_at");
            entity.Property(e => e.Instruction).HasColumnName("instruction");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.ScheduledStartAt).HasColumnName("scheduled_start_at");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.TaskNo)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("task_no");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.CleaningType).WithMany(p => p.CleaningTaskCleaningTypes)
                .HasForeignKey(d => d.CleaningTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_tasks_cleaning_type_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CleaningTasks)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_tasks_created_by");

            entity.HasOne(d => d.Priority).WithMany(p => p.CleaningTaskPriorities)
                .HasForeignKey(d => d.PriorityId)
                .HasConstraintName("FK_cleaning_tasks_priority_id");

            entity.HasOne(d => d.Room).WithMany(p => p.CleaningTasks)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cleaning_tasks_room_id");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__departme__C22324222C79F96D");

            entity.ToTable("departments");

            entity.HasIndex(e => new { e.BranchId, e.DepartmentCode }, "departments_index_4").IsUnique();

            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.DepartmentCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("department_code");
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(150)
                .HasColumnName("department_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(e => e.ScopeDescription).HasColumnName("scope_description");

            entity.HasOne(d => d.Branch).WithMany(p => p.Departments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_departments_branch_id");

            entity.HasOne(d => d.ManagerUser).WithMany(p => p.Departments)
                .HasForeignKey(d => d.ManagerUserId)
                .HasConstraintName("FK_departments_manager_user_id");
        });

        modelBuilder.Entity<Floor>(entity =>
        {
            entity.HasKey(e => e.FloorId).HasName("PK__floors__76040CCC6D1AD281");

            entity.ToTable("floors");

            entity.HasIndex(e => new { e.BranchId, e.FloorCode }, "floors_index_0").IsUnique();

            entity.Property(e => e.FloorId).HasColumnName("floor_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FloorCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("floor_code");
            entity.Property(e => e.FloorName)
                .HasMaxLength(100)
                .HasColumnName("floor_name");
            entity.Property(e => e.FloorNumber).HasColumnName("floor_number");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.Branch).WithMany(p => p.Floors)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_floors_branch_id");
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.IncidentId).HasName("PK__incident__E6C40DA303F8965B");

            entity.ToTable("incidents");

            entity.HasIndex(e => e.IncidentNo, "UQ__incident__E6C4CAB931AE8E72").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.Status }, "incidents_index_14");

            entity.HasIndex(e => new { e.RoomId, e.ReportedAt }, "incidents_index_15");

            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.ClosureNote).HasColumnName("closure_note");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DiscoveredAt).HasColumnName("discovered_at");
            entity.Property(e => e.IncidentNo)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("incident_no");
            entity.Property(e => e.IncidentTitle)
                .HasMaxLength(200)
                .HasColumnName("incident_title");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.ReportedAt).HasColumnName("reported_at");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.SeverityId).HasColumnName("severity_id");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("OPEN")
                .HasColumnName("status");

            entity.HasOne(d => d.Area).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK_incidents_area_id");

            entity.HasOne(d => d.Asset).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.AssetId)
                .HasConstraintName("FK_incidents_asset_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_incidents_branch_id");

            entity.HasOne(d => d.Priority).WithMany(p => p.IncidentPriorities)
                .HasForeignKey(d => d.PriorityId)
                .HasConstraintName("FK_incidents_priority_id");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_incidents_reported_by");

            entity.HasOne(d => d.Room).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK_incidents_room_id");

            entity.HasOne(d => d.Severity).WithMany(p => p.IncidentSeverities)
                .HasForeignKey(d => d.SeverityId)
                .HasConstraintName("FK_incidents_severity_id");
        });

        modelBuilder.Entity<IncidentEvent>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("PK__incident__2370F7271C2FD394");

            entity.ToTable("incident_events");

            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("event_type");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("from_status");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.ToStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("to_status");

            entity.HasOne(d => d.Actor).WithMany(p => p.IncidentEvents)
                .HasForeignKey(d => d.ActorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_incident_events_actor_id");

            entity.HasOne(d => d.Incident).WithMany(p => p.IncidentEvents)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_incident_events_incident_id");
        });

        modelBuilder.Entity<InspectionChecklist>(entity =>
        {
            entity.HasKey(e => e.ChecklistId).HasName("PK__inspecti__23C471C51E5CCF3E");

            entity.ToTable("inspection_checklists");

            entity.HasIndex(e => e.ChecklistCode, "UQ__inspecti__9D02AD0D94E8AC49").IsUnique();

            entity.Property(e => e.ChecklistId).HasColumnName("checklist_id");
            entity.Property(e => e.ChecklistCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("checklist_code");
            entity.Property(e => e.ChecklistName)
                .HasMaxLength(200)
                .HasColumnName("checklist_name");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.RoomTypeId).HasColumnName("room_type_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InspectionChecklists)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_inspection_checklists_created_by");

            entity.HasOne(d => d.RoomType).WithMany(p => p.InspectionChecklists)
                .HasForeignKey(d => d.RoomTypeId)
                .HasConstraintName("FK_inspection_checklists_room_type_id");
        });

        modelBuilder.Entity<InspectionChecklistItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__inspecti__52020FDD7BA0BD49");

            entity.ToTable("inspection_checklist_items");

            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Criterion)
                .HasMaxLength(250)
                .HasColumnName("criterion");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true)
                .HasColumnName("is_required");
            entity.Property(e => e.PassCondition).HasColumnName("pass_condition");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.VersionId).HasColumnName("version_id");

            entity.HasOne(d => d.Version).WithMany(p => p.InspectionChecklistItems)
                .HasForeignKey(d => d.VersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inspection_checklist_items_version_id");
        });

        modelBuilder.Entity<InspectionChecklistVersion>(entity =>
        {
            entity.HasKey(e => e.VersionId).HasName("PK__inspecti__07A58869B7892627");

            entity.ToTable("inspection_checklist_versions");

            entity.HasIndex(e => new { e.ChecklistId, e.VersionNo }, "inspection_checklist_versions_index_11").IsUnique();

            entity.Property(e => e.VersionId).HasColumnName("version_id");
            entity.Property(e => e.ChecklistId).HasColumnName("checklist_id");
            entity.Property(e => e.EffectiveAt).HasColumnName("effective_at");
            entity.Property(e => e.PublishedBy).HasColumnName("published_by");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.VersionNo).HasColumnName("version_no");

            entity.HasOne(d => d.Checklist).WithMany(p => p.InspectionChecklistVersions)
                .HasForeignKey(d => d.ChecklistId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inspection_checklist_versions_checklist_id");

            entity.HasOne(d => d.PublishedByNavigation).WithMany(p => p.InspectionChecklistVersions)
                .HasForeignKey(d => d.PublishedBy)
                .HasConstraintName("FK_inspection_checklist_versions_published_by");
        });

        modelBuilder.Entity<InspectionDefect>(entity =>
        {
            entity.HasKey(e => e.DefectId).HasName("PK__inspecti__A64C624BCA677D9F");

            entity.ToTable("inspection_defects");

            entity.Property(e => e.DefectId).HasColumnName("defect_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.ChecklistItemId).HasColumnName("checklist_item_id");
            entity.Property(e => e.CorrectiveAction).HasColumnName("corrective_action");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.ResolutionNote).HasColumnName("resolution_note");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.SeverityId).HasColumnName("severity_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("OPEN")
                .HasColumnName("status");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.InspectionDefects)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("FK_inspection_defects_assigned_to");

            entity.HasOne(d => d.ChecklistItem).WithMany(p => p.InspectionDefects)
                .HasForeignKey(d => d.ChecklistItemId)
                .HasConstraintName("FK_inspection_defects_checklist_item_id");

            entity.HasOne(d => d.Inspection).WithMany(p => p.InspectionDefects)
                .HasForeignKey(d => d.InspectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inspection_defects_inspection_id");

            entity.HasOne(d => d.Severity).WithMany(p => p.InspectionDefects)
                .HasForeignKey(d => d.SeverityId)
                .HasConstraintName("FK_inspection_defects_severity_id");
        });

        modelBuilder.Entity<InspectionResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("PK__inspecti__AFB3C3163662A858");

            entity.ToTable("inspection_results");

            entity.HasIndex(e => new { e.InspectionId, e.ChecklistItemId }, "inspection_results_index_13").IsUnique();

            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.ChecklistItemId).HasColumnName("checklist_item_id");
            entity.Property(e => e.InspectedAt).HasColumnName("inspected_at");
            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Outcome)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("outcome");

            entity.HasOne(d => d.ChecklistItem).WithMany(p => p.InspectionResults)
                .HasForeignKey(d => d.ChecklistItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inspection_results_checklist_item_id");

            entity.HasOne(d => d.Inspection).WithMany(p => p.InspectionResults)
                .HasForeignKey(d => d.InspectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inspection_results_inspection_id");
        });

        modelBuilder.Entity<InternalRequest>(entity =>
        {
            entity.HasKey(e => e.InternalRequestId).HasName("PK__internal__90426592DE26A97D");

            entity.ToTable("internal_requests");

            entity.HasIndex(e => e.RequestNo, "UQ__internal__18D3F2FD6C64CCEB").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.Status }, "internal_requests_index_30");

            entity.HasIndex(e => new { e.ReceivingDepartmentId, e.DueAt }, "internal_requests_index_31");

            entity.Property(e => e.InternalRequestId).HasColumnName("internal_request_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CancellationReason).HasColumnName("cancellation_reason");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DueAt).HasColumnName("due_at");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.ReceivingDepartmentId).HasColumnName("receiving_department_id");
            entity.Property(e => e.RelatedIncidentId).HasColumnName("related_incident_id");
            entity.Property(e => e.RequestNo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("request_no");
            entity.Property(e => e.RequestTypeId).HasColumnName("request_type_id");
            entity.Property(e => e.RequesterDepartmentId).HasColumnName("requester_department_id");
            entity.Property(e => e.RequesterId).HasColumnName("requester_id");
            entity.Property(e => e.RequiredAt).HasColumnName("required_at");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.Subject)
                .HasMaxLength(200)
                .HasColumnName("subject");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Area).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK_internal_requests_area_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_requests_branch_id");

            entity.HasOne(d => d.Priority).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.PriorityId)
                .HasConstraintName("FK_internal_requests_priority_id");

            entity.HasOne(d => d.ReceivingDepartment).WithMany(p => p.InternalRequestReceivingDepartments)
                .HasForeignKey(d => d.ReceivingDepartmentId)
                .HasConstraintName("FK_internal_requests_receiving_department_id");

            entity.HasOne(d => d.RelatedIncident).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.RelatedIncidentId)
                .HasConstraintName("FK_internal_requests_related_incident_id");

            entity.HasOne(d => d.RequestType).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.RequestTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_requests_request_type_id");

            entity.HasOne(d => d.RequesterDepartment).WithMany(p => p.InternalRequestRequesterDepartments)
                .HasForeignKey(d => d.RequesterDepartmentId)
                .HasConstraintName("FK_internal_requests_requester_department_id");

            entity.HasOne(d => d.Requester).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_requests_requester_id");

            entity.HasOne(d => d.Room).WithMany(p => p.InternalRequests)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK_internal_requests_room_id");
        });

        modelBuilder.Entity<InternalRequestAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__internal__DA8918148588E79A");

            entity.ToTable("internal_request_assignments");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssigneeId).HasColumnName("assignee_id");
            entity.Property(e => e.AssignmentNote).HasColumnName("assignment_note");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.InternalRequestId).HasColumnName("internal_request_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("status");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.InternalRequestAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_assignments_assigned_by");

            entity.HasOne(d => d.Assignee).WithMany(p => p.InternalRequestAssignmentAssignees)
                .HasForeignKey(d => d.AssigneeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_assignments_assignee_id");

            entity.HasOne(d => d.InternalRequest).WithMany(p => p.InternalRequestAssignments)
                .HasForeignKey(d => d.InternalRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_assignments_internal_request_id");
        });

        modelBuilder.Entity<InternalRequestEvent>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("PK__internal__2370F727D3FBF309");

            entity.ToTable("internal_request_events");

            entity.HasIndex(e => new { e.InternalRequestId, e.OccurredAt }, "internal_request_events_index_32");

            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.EventNote).HasColumnName("event_note");
            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("event_type");
            entity.Property(e => e.InternalRequestId).HasColumnName("internal_request_id");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("new_status");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("old_status");

            entity.HasOne(d => d.Actor).WithMany(p => p.InternalRequestEvents)
                .HasForeignKey(d => d.ActorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_events_actor_id");

            entity.HasOne(d => d.InternalRequest).WithMany(p => p.InternalRequestEvents)
                .HasForeignKey(d => d.InternalRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_events_internal_request_id");
        });

        modelBuilder.Entity<InternalRequestFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__internal__7A6B2B8C2BE718BE");

            entity.ToTable("internal_request_feedback");

            entity.HasIndex(e => e.InternalRequestId, "UQ__internal__904265932A5DB880").IsUnique();

            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.InternalRequestId).HasColumnName("internal_request_id");
            entity.Property(e => e.IsSatisfied).HasColumnName("is_satisfied");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.SubmittedBy).HasColumnName("submitted_by");

            entity.HasOne(d => d.InternalRequest).WithOne(p => p.InternalRequestFeedback)
                .HasForeignKey<InternalRequestFeedback>(d => d.InternalRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_feedback_internal_request_id");

            entity.HasOne(d => d.SubmittedByNavigation).WithMany(p => p.InternalRequestFeedbacks)
                .HasForeignKey(d => d.SubmittedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_internal_request_feedback_submitted_by");
        });

        modelBuilder.Entity<InternalRequestType>(entity =>
        {
            entity.HasKey(e => e.RequestTypeId).HasName("PK__internal__C24DD950755EBFD6");

            entity.ToTable("internal_request_types");

            entity.HasIndex(e => e.TypeCode, "UQ__internal__2CB4DBF53EDB381B").IsUnique();

            entity.Property(e => e.RequestTypeId).HasColumnName("request_type_id");
            entity.Property(e => e.DefaultDepartmentId).HasColumnName("default_department_id");
            entity.Property(e => e.DefaultPriorityId).HasColumnName("default_priority_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SlaMinutes).HasColumnName("sla_minutes");
            entity.Property(e => e.TypeCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("type_code");
            entity.Property(e => e.TypeName)
                .HasMaxLength(150)
                .HasColumnName("type_name");

            entity.HasOne(d => d.DefaultDepartment).WithMany(p => p.InternalRequestTypes)
                .HasForeignKey(d => d.DefaultDepartmentId)
                .HasConstraintName("FK_internal_request_types_default_department_id");

            entity.HasOne(d => d.DefaultPriority).WithMany(p => p.InternalRequestTypes)
                .HasForeignKey(d => d.DefaultPriorityId)
                .HasConstraintName("FK_internal_request_types_default_priority_id");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__inventor__52020FDD14FA6F3D");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("inventory_items");

            entity.HasIndex(e => e.ItemCode, "UQ__inventor__4A67201E314B119B").IsUnique();

            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.BaseUomId).HasColumnName("base_uom_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsConsumable)
                .HasDefaultValue(true)
                .HasColumnName("is_consumable");
            entity.Property(e => e.ItemCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("item_code");
            entity.Property(e => e.ItemName)
                .HasMaxLength(200)
                .HasColumnName("item_name");
            entity.Property(e => e.Specification).HasColumnName("specification");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.BaseUom).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.BaseUomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inventory_items_base_uom_id");

            entity.HasOne(d => d.Category).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inventory_items_category_id");
        });

        modelBuilder.Entity<ItemCategory>(entity =>
        {
            entity.HasKey(e => e.ItemCategoryId).HasName("PK__item_cat__34A91ACABB9080B1");

            entity.ToTable("item_categories");

            entity.HasIndex(e => e.CategoryCode, "UQ__item_cat__BC9D1E7CF1786C61").IsUnique();

            entity.Property(e => e.ItemCategoryId).HasColumnName("item_category_id");
            entity.Property(e => e.CategoryCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("category_code");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(150)
                .HasColumnName("category_name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<MaintenanceWork>(entity =>
        {
            entity.HasKey(e => e.MaintenanceWorkId).HasName("PK__maintena__84CE6E3DB00E3DB9");

            entity.ToTable("maintenance_works");

            entity.HasIndex(e => e.WorkNo, "UQ__maintena__110E7BE92B5AEEB7").IsUnique();

            entity.Property(e => e.MaintenanceWorkId).HasColumnName("maintenance_work_id");
            entity.Property(e => e.ActualStartAt).HasColumnName("actual_start_at");
            entity.Property(e => e.AssigneeId).HasColumnName("assignee_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.Instruction).HasColumnName("instruction");
            entity.Property(e => e.PlannedEndAt).HasColumnName("planned_end_at");
            entity.Property(e => e.PlannedStartAt).HasColumnName("planned_start_at");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.ResolutionNote).HasColumnName("resolution_note");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.WorkNo)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("work_no");

            entity.HasOne(d => d.Assignee).WithMany(p => p.MaintenanceWorkAssignees)
                .HasForeignKey(d => d.AssigneeId)
                .HasConstraintName("FK_maintenance_works_assignee_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaintenanceWorkCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_maintenance_works_created_by");

            entity.HasOne(d => d.Department).WithMany(p => p.MaintenanceWorks)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_maintenance_works_department_id");

            entity.HasOne(d => d.Incident).WithMany(p => p.MaintenanceWorks)
                .HasForeignKey(d => d.IncidentId)
                .HasConstraintName("FK_maintenance_works_incident_id");

            entity.HasOne(d => d.Priority).WithMany(p => p.MaintenanceWorks)
                .HasForeignKey(d => d.PriorityId)
                .HasConstraintName("FK_maintenance_works_priority_id");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.MaintenanceWorkReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_maintenance_works_reviewed_by");
        });

        modelBuilder.Entity<MaintenanceWorkLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__maintena__9E2397E041AC2DA9");

            entity.ToTable("maintenance_work_logs");

            entity.Property(e => e.LogId).HasColumnName("log_id");
            entity.Property(e => e.LoggedAt).HasColumnName("logged_at");
            entity.Property(e => e.LoggedBy).HasColumnName("logged_by");
            entity.Property(e => e.MaintenanceWorkId).HasColumnName("maintenance_work_id");
            entity.Property(e => e.MaterialIssueId).HasColumnName("material_issue_id");
            entity.Property(e => e.SpentMinutes).HasColumnName("spent_minutes");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.WorkNote).HasColumnName("work_note");

            entity.HasOne(d => d.LoggedByNavigation).WithMany(p => p.MaintenanceWorkLogs)
                .HasForeignKey(d => d.LoggedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_maintenance_work_logs_logged_by");

            entity.HasOne(d => d.MaintenanceWork).WithMany(p => p.MaintenanceWorkLogs)
                .HasForeignKey(d => d.MaintenanceWorkId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_maintenance_work_logs_maintenance_work_id");

            entity.HasOne(d => d.MaterialIssue).WithMany(p => p.MaintenanceWorkLogs)
                .HasForeignKey(d => d.MaterialIssueId)
                .HasConstraintName("FK_maintenance_work_logs_material_issue_id");
        });

        modelBuilder.Entity<MaterialFulfillment>(entity =>
        {
            entity.HasKey(e => e.FulfillmentId).HasName("PK__material__34E680DBD40CE9AA");

            entity.ToTable("material_fulfillments");

            entity.HasIndex(e => new { e.RequestId, e.FulfillmentId }, "IX_mr_fulfillments_request");

            entity.HasIndex(e => e.FulfillmentNo, "UQ__material__34E7AD3928AC09D5").IsUnique();

            entity.HasIndex(e => e.StockDocumentId, "UX_mr_fulfillment_document")
                .IsUnique()
                .HasFilter("([stock_document_id] IS NOT NULL)");

            entity.Property(e => e.FulfillmentId).HasColumnName("fulfillment_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.FulfilledAt).HasColumnName("fulfilled_at");
            entity.Property(e => e.FulfilledBy).HasColumnName("fulfilled_by");
            entity.Property(e => e.FulfillmentNo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("fulfillment_no");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReceivedAt).HasColumnName("received_at");
            entity.Property(e => e.ReceivedBy).HasColumnName("received_by");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PREPARING")
                .HasColumnName("status");
            entity.Property(e => e.StockDocumentId).HasColumnName("stock_document_id");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");

            entity.HasOne(d => d.FulfilledByNavigation).WithMany(p => p.MaterialFulfillmentFulfilledByNavigations)
                .HasForeignKey(d => d.FulfilledBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillments_fulfilled_by");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.MaterialFulfillmentReceivedByNavigations)
                .HasForeignKey(d => d.ReceivedBy)
                .HasConstraintName("FK_material_fulfillments_received_by");

            entity.HasOne(d => d.Request).WithMany(p => p.MaterialFulfillments)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillments_request_id");

            entity.HasOne(d => d.StockDocument).WithOne(p => p.MaterialFulfillment)
                .HasForeignKey<MaterialFulfillment>(d => d.StockDocumentId)
                .HasConstraintName("FK_material_fulfillments_stock_document_id");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MaterialFulfillments)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillments_warehouse_id");
        });

        modelBuilder.Entity<MaterialFulfillmentLine>(entity =>
        {
            entity.HasKey(e => e.FulfillmentLineId).HasName("PK__material__891A96CDE399BA24");

            entity.ToTable("material_fulfillment_lines");

            entity.HasIndex(e => e.RequestLineId, "IX_mr_fulfillment_request_line");

            entity.HasIndex(e => e.StockDocumentLineId, "UX_mr_fulfillment_issue_line")
                .IsUnique()
                .HasFilter("([stock_document_line_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.FulfillmentId, e.RequestLineId }, "material_fulfillment_lines_index_29").IsUnique();

            entity.Property(e => e.FulfillmentLineId).HasColumnName("fulfillment_line_id");
            entity.Property(e => e.FulfillmentId).HasColumnName("fulfillment_id");
            entity.Property(e => e.IssuedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("issued_qty");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReceivedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("received_qty");
            entity.Property(e => e.RequestLineId).HasColumnName("request_line_id");
            entity.Property(e => e.StockDocumentLineId).HasColumnName("stock_document_line_id");
            entity.Property(e => e.UomId).HasColumnName("uom_id");

            entity.HasOne(d => d.Fulfillment).WithMany(p => p.MaterialFulfillmentLines)
                .HasForeignKey(d => d.FulfillmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillment_lines_fulfillment_id");

            entity.HasOne(d => d.Item).WithMany(p => p.MaterialFulfillmentLines)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillment_lines_item_id");

            entity.HasOne(d => d.RequestLine).WithMany(p => p.MaterialFulfillmentLines)
                .HasForeignKey(d => d.RequestLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillment_lines_request_line_id");

            entity.HasOne(d => d.StockDocumentLine).WithOne(p => p.MaterialFulfillmentLine)
                .HasForeignKey<MaterialFulfillmentLine>(d => d.StockDocumentLineId)
                .HasConstraintName("FK_mr_fulfillment_issue_line");

            entity.HasOne(d => d.Uom).WithMany(p => p.MaterialFulfillmentLines)
                .HasForeignKey(d => d.UomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_fulfillment_lines_uom_id");
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__material__18D3B90F59856EC9");

            entity.ToTable("material_requests");

            entity.HasIndex(e => e.RequestNo, "UQ__material__18D3F2FDBAF5D1E8").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.DepartmentId, e.Status }, "material_requests_index_27");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CancellationReason).HasColumnName("cancellation_reason");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.NeededAt).HasColumnName("needed_at");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.Purpose).HasColumnName("purpose");
            entity.Property(e => e.ReceivingAreaId).HasColumnName("receiving_area_id");
            entity.Property(e => e.ReceivingRoomId).HasColumnName("receiving_room_id");
            entity.Property(e => e.RemainderCloseReason).HasColumnName("remainder_close_reason");
            entity.Property(e => e.RemainderClosedAt).HasColumnName("remainder_closed_at");
            entity.Property(e => e.RemainderClosedBy).HasColumnName("remainder_closed_by");
            entity.Property(e => e.RequestNo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("request_no");
            entity.Property(e => e.RequesterId).HasColumnName("requester_id");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.SourceWarehouseId).HasColumnName("source_warehouse_id");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Branch).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_requests_branch_id");

            entity.HasOne(d => d.Department).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_requests_department_id");

            entity.HasOne(d => d.Incident).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.IncidentId)
                .HasConstraintName("FK_material_requests_incident");

            entity.HasOne(d => d.Priority).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.PriorityId)
                .HasConstraintName("FK_material_requests_priority_id");

            entity.HasOne(d => d.ReceivingArea).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.ReceivingAreaId)
                .HasConstraintName("FK_material_requests_receiving_area");

            entity.HasOne(d => d.ReceivingRoom).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.ReceivingRoomId)
                .HasConstraintName("FK_material_requests_receiving_room");

            entity.HasOne(d => d.RemainderClosedByNavigation).WithMany(p => p.MaterialRequestRemainderClosedByNavigations)
                .HasForeignKey(d => d.RemainderClosedBy)
                .HasConstraintName("FK_material_requests_remainder_closed_by");

            entity.HasOne(d => d.Requester).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_requests_requester_id");

            entity.HasOne(d => d.SourceWarehouse).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.SourceWarehouseId)
                .HasConstraintName("FK_material_requests_source_warehouse_id");
        });

        modelBuilder.Entity<MaterialRequestApproval>(entity =>
        {
            entity.HasKey(e => e.ApprovalId).HasName("PK__material__C94AE61AFF645BA6");

            entity.ToTable("material_request_approvals");

            entity.HasIndex(e => new { e.RequestId, e.DecidedAt, e.ApprovalId }, "IX_mr_approval_timeline");

            entity.HasIndex(e => e.ApprovalCycleId, "UX_mr_decision_cycle")
                .IsUnique()
                .HasFilter("([approval_cycle_id] IS NOT NULL)");

            entity.Property(e => e.ApprovalId).HasColumnName("approval_id");
            entity.Property(e => e.ApprovalCycleId).HasColumnName("approval_cycle_id");
            entity.Property(e => e.ApprovalLevel)
                .HasDefaultValue(1)
                .HasColumnName("approval_level");
            entity.Property(e => e.ApproverId).HasColumnName("approver_id");
            entity.Property(e => e.DecidedAt).HasColumnName("decided_at");
            entity.Property(e => e.Decision)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("decision");
            entity.Property(e => e.DecisionNote).HasColumnName("decision_note");
            entity.Property(e => e.RequestId).HasColumnName("request_id");

            entity.HasOne(d => d.ApprovalCycle).WithOne(p => p.MaterialRequestApproval)
                .HasForeignKey<MaterialRequestApproval>(d => d.ApprovalCycleId)
                .HasConstraintName("FK_mr_approvals_cycle");

            entity.HasOne(d => d.Approver).WithMany(p => p.MaterialRequestApprovals)
                .HasForeignKey(d => d.ApproverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_request_approvals_approver_id");

            entity.HasOne(d => d.Request).WithMany(p => p.MaterialRequestApprovals)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_request_approvals_request_id");
        });

        modelBuilder.Entity<MaterialRequestLine>(entity =>
        {
            entity.HasKey(e => e.LineId).HasName("PK__material__F5AE5F62052870C2");

            entity.ToTable("material_request_lines");

            entity.HasIndex(e => new { e.RequestId, e.LineNo }, "material_request_lines_index_28").IsUnique();

            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.ApprovedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("approved_qty");
            entity.Property(e => e.FulfilledQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("fulfilled_qty");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "DF_material_request_lines_active")
                .HasColumnName("is_active");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.RequestedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("requested_qty");
            entity.Property(e => e.UomId).HasColumnName("uom_id");

            entity.HasOne(d => d.Item).WithMany(p => p.MaterialRequestLines)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_request_lines_item_id");

            entity.HasOne(d => d.Request).WithMany(p => p.MaterialRequestLines)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_request_lines_request_id");

            entity.HasOne(d => d.Uom).WithMany(p => p.MaterialRequestLines)
                .HasForeignKey(d => d.UomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_material_request_lines_uom_id");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__notifica__E059842F0A16E308");

            entity.ToTable("notifications");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.ActionUrl).HasColumnName("action_url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.NotificationType)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("notification_type");
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("NORMAL")
                .HasColumnName("priority");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.SourceType)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("source_type");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_notifications_created_by");
        });

        modelBuilder.Entity<NotificationRecipient>(entity =>
        {
            entity.HasKey(e => e.RecipientId).HasName("PK__notifica__FA0A4027B0E0EB85");

            entity.ToTable("notification_recipients");

            entity.HasIndex(e => new { e.NotificationId, e.UserId, e.DeliveryChannel }, "notification_recipients_index_42").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.ReadAt }, "notification_recipients_index_43");

            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(e => e.DeliveryChannel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("IN_APP")
                .HasColumnName("delivery_channel");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("delivery_status");
            entity.Property(e => e.DismissedAt).HasColumnName("dismissed_at");
            entity.Property(e => e.FailureReason).HasColumnName("failure_reason");
            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Notification).WithMany(p => p.NotificationRecipients)
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_notification_recipients_notification_id");

            entity.HasOne(d => d.User).WithMany(p => p.NotificationRecipients)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_notification_recipients_user_id");
        });

        modelBuilder.Entity<OperationalTask>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__operatio__0492148D3F873A8D");

            entity.ToTable("operational_tasks");

            entity.HasIndex(e => e.TaskCode, "UQ__operatio__4ED291560B90DEA7").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.DepartmentId, e.Status }, "operational_tasks_index_33");

            entity.HasIndex(e => e.DueAt, "operational_tasks_index_34");

            entity.Property(e => e.TaskId).HasColumnName("task_id");
            entity.Property(e => e.ActualStartAt).HasColumnName("actual_start_at");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CompletionNote).HasColumnName("completion_note");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DueAt).HasColumnName("due_at");
            entity.Property(e => e.ParentTaskId).HasColumnName("parent_task_id");
            entity.Property(e => e.PlannedStartAt).HasColumnName("planned_start_at");
            entity.Property(e => e.PriorityId).HasColumnName("priority_id");
            entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.SourceType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("source_type");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("TODO")
                .HasColumnName("status");
            entity.Property(e => e.TaskCode)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("task_code");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Area).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK_operational_tasks_area_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_tasks_branch_id");

            entity.HasOne(d => d.Category).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_operational_tasks_category_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_tasks_created_by");

            entity.HasOne(d => d.Department).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_tasks_department_id");

            entity.HasOne(d => d.ParentTask).WithMany(p => p.InverseParentTask)
                .HasForeignKey(d => d.ParentTaskId)
                .HasConstraintName("FK_operational_tasks_parent_task_id");

            entity.HasOne(d => d.Priority).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.PriorityId)
                .HasConstraintName("FK_operational_tasks_priority_id");

            entity.HasOne(d => d.Room).WithMany(p => p.OperationalTasks)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK_operational_tasks_room_id");
        });

        modelBuilder.Entity<OperationalTaskAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__operatio__DA8918144DFA48E6");

            entity.ToTable("operational_task_assignments");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssigneeId).HasColumnName("assignee_id");
            entity.Property(e => e.AssignmentStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("assignment_status");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.TaskId).HasColumnName("task_id");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.OperationalTaskAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_assignments_assigned_by");

            entity.HasOne(d => d.Assignee).WithMany(p => p.OperationalTaskAssignmentAssignees)
                .HasForeignKey(d => d.AssigneeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_assignments_assignee_id");

            entity.HasOne(d => d.Task).WithMany(p => p.OperationalTaskAssignments)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_assignments_task_id");
        });

        modelBuilder.Entity<OperationalTaskDependency>(entity =>
        {
            entity.HasKey(e => e.DependencyId).HasName("PK__operatio__952A18724FE8B43C");

            entity.ToTable("operational_task_dependencies");

            entity.HasIndex(e => new { e.PredecessorTaskId, e.SuccessorTaskId }, "operational_task_dependencies_index_35").IsUnique();

            entity.Property(e => e.DependencyId).HasColumnName("dependency_id");
            entity.Property(e => e.DependencyType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("FINISH_TO_START")
                .HasColumnName("dependency_type");
            entity.Property(e => e.LagMinutes)
                .HasDefaultValue(0)
                .HasColumnName("lag_minutes");
            entity.Property(e => e.PredecessorTaskId).HasColumnName("predecessor_task_id");
            entity.Property(e => e.SuccessorTaskId).HasColumnName("successor_task_id");

            entity.HasOne(d => d.PredecessorTask).WithMany(p => p.OperationalTaskDependencyPredecessorTasks)
                .HasForeignKey(d => d.PredecessorTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_dependencies_predecessor_task_id");

            entity.HasOne(d => d.SuccessorTask).WithMany(p => p.OperationalTaskDependencySuccessorTasks)
                .HasForeignKey(d => d.SuccessorTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_dependencies_successor_task_id");
        });

        modelBuilder.Entity<OperationalTaskProgress>(entity =>
        {
            entity.HasKey(e => e.ProgressId).HasName("PK__operatio__49B3D8C1F8378F9A");

            entity.ToTable("operational_task_progress");

            entity.Property(e => e.ProgressId).HasColumnName("progress_id");
            entity.Property(e => e.BlockerReason).HasColumnName("blocker_reason");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("new_status");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("old_status");
            entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
            entity.Property(e => e.ReportedAt).HasColumnName("reported_at");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.TaskId).HasColumnName("task_id");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.OperationalTaskProgresses)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_progress_reported_by");

            entity.HasOne(d => d.Task).WithMany(p => p.OperationalTaskProgresses)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_operational_task_progress_task_id");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__permissi__E5331AFA6BFD8720");

            entity.ToTable("permissions");

            entity.HasIndex(e => e.PermissionCode, "UQ__permissi__A98A808E2C8B304E").IsUnique();

            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.ActionCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("action_code");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ModuleCode)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("module_code");
            entity.Property(e => e.PermissionCode)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("permission_code");
            entity.Property(e => e.PermissionName)
                .HasMaxLength(150)
                .HasColumnName("permission_name");
            entity.Property(e => e.ResourceCode)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("resource_code");
        });

        modelBuilder.Entity<PreventiveMaintenanceRun>(entity =>
        {
            entity.HasKey(e => e.RunId).HasName("PK__preventi__7D3D901BB789D833");

            entity.ToTable("preventive_maintenance_runs");

            entity.HasIndex(e => new { e.ScheduleId, e.PlannedAt }, "preventive_maintenance_runs_index_16").IsUnique();

            entity.Property(e => e.RunId).HasColumnName("run_id");
            entity.Property(e => e.MaintenanceWorkId).HasColumnName("maintenance_work_id");
            entity.Property(e => e.NextDueAt).HasColumnName("next_due_at");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.PerformedAt).HasColumnName("performed_at");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
            entity.Property(e => e.PlannedAt).HasColumnName("planned_at");
            entity.Property(e => e.Result)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("result");
            entity.Property(e => e.ScheduleId).HasColumnName("schedule_id");

            entity.HasOne(d => d.MaintenanceWork).WithMany(p => p.PreventiveMaintenanceRuns)
                .HasForeignKey(d => d.MaintenanceWorkId)
                .HasConstraintName("FK_preventive_maintenance_runs_maintenance_work_id");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.PreventiveMaintenanceRuns)
                .HasForeignKey(d => d.PerformedBy)
                .HasConstraintName("FK_preventive_maintenance_runs_performed_by");

            entity.HasOne(d => d.Schedule).WithMany(p => p.PreventiveMaintenanceRuns)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_preventive_maintenance_runs_schedule_id");
        });

        modelBuilder.Entity<PreventiveMaintenanceSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("PK__preventi__C46A8A6F61C85A0D");

            entity.ToTable("preventive_maintenance_schedules");

            entity.HasIndex(e => e.ScheduleCode, "UQ__preventi__C0670CCF84E8AF87").IsUnique();

            entity.Property(e => e.ScheduleId).HasColumnName("schedule_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AssignedDepartmentId).HasColumnName("assigned_department_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Frequency)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("frequency");
            entity.Property(e => e.FrequencyInterval)
                .HasDefaultValue(1)
                .HasColumnName("frequency_interval");
            entity.Property(e => e.Instruction).HasColumnName("instruction");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastPerformedAt).HasColumnName("last_performed_at");
            entity.Property(e => e.NextDueAt).HasColumnName("next_due_at");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.ScheduleCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("schedule_code");

            entity.HasOne(d => d.Area).WithMany(p => p.PreventiveMaintenanceSchedules)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK_preventive_maintenance_schedules_area_id");

            entity.HasOne(d => d.Asset).WithMany(p => p.PreventiveMaintenanceSchedules)
                .HasForeignKey(d => d.AssetId)
                .HasConstraintName("FK_preventive_maintenance_schedules_asset_id");

            entity.HasOne(d => d.AssignedDepartment).WithMany(p => p.PreventiveMaintenanceSchedules)
                .HasForeignKey(d => d.AssignedDepartmentId)
                .HasConstraintName("FK_preventive_maintenance_schedules_assigned_department_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PreventiveMaintenanceSchedules)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_preventive_maintenance_schedules_created_by");

            entity.HasOne(d => d.Room).WithMany(p => p.PreventiveMaintenanceSchedules)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK_preventive_maintenance_schedules_room_id");
        });

        modelBuilder.Entity<ReferenceValue>(entity =>
        {
            entity.HasKey(e => e.ReferenceId).HasName("PK__referenc__8E860B28090EF0D1");

            entity.ToTable("reference_values");

            entity.HasIndex(e => new { e.GroupCode, e.ValueCode }, "reference_values_index_5").IsUnique();

            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(150)
                .HasColumnName("display_name");
            entity.Property(e => e.GroupCode)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("group_code");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
            entity.Property(e => e.ValueCode)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("value_code");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__roles__760965CC49349513");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleCode, "UQ__roles__BAE630754150AC14").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsSystemRole).HasColumnName("is_system_role");
            entity.Property(e => e.RoleCode)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("role_code");
            entity.Property(e => e.RoleName)
                .HasMaxLength(150)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.RolePermissionId).HasName("PK__role_per__B1E85A10C926A139");

            entity.ToTable("role_permissions");

            entity.HasIndex(e => new { e.RoleId, e.PermissionId }, "role_permissions_index_40").IsUnique();

            entity.Property(e => e.RolePermissionId).HasColumnName("role_permission_id");
            entity.Property(e => e.GrantedAt).HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");

            entity.HasOne(d => d.GrantedByNavigation).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.GrantedBy)
                .HasConstraintName("FK_role_permissions_granted_by");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_role_permissions_permission_id");

            entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_role_permissions_role_id");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__rooms__19675A8A2A0F0C24");

            entity.ToTable("rooms");

            entity.HasIndex(e => new { e.BranchId, e.RoomNumber }, "rooms_index_2").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.OperationalStatus }, "rooms_index_3");

            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FloorId).HasColumnName("floor_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.OperationalStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasDefaultValue("AVAILABLE")
                .HasColumnName("operational_status");
            entity.Property(e => e.RoomNumber)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("room_number");
            entity.Property(e => e.RoomTypeId).HasColumnName("room_type_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Area).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.AreaId)
                .HasConstraintName("FK_rooms_area_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_rooms_branch_id");

            entity.HasOne(d => d.Floor).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.FloorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_rooms_floor_id");

            entity.HasOne(d => d.RoomType).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.RoomTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_rooms_room_type_id");
        });

        modelBuilder.Entity<RoomBlock>(entity =>
        {
            entity.HasKey(e => e.BlockId).HasName("PK__room_blo__A67E647D4FE07846");

            entity.ToTable("room_blocks");

            entity.Property(e => e.BlockId).HasColumnName("block_id");
            entity.Property(e => e.BlockedAt).HasColumnName("blocked_at");
            entity.Property(e => e.BlockedBy).HasColumnName("blocked_by");
            entity.Property(e => e.ExpectedReleaseAt).HasColumnName("expected_release_at");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReleaseNote).HasColumnName("release_note");
            entity.Property(e => e.ReleasedAt).HasColumnName("released_at");
            entity.Property(e => e.ReleasedBy).HasColumnName("released_by");
            entity.Property(e => e.RoomId).HasColumnName("room_id");

            entity.HasOne(d => d.BlockedByNavigation).WithMany(p => p.RoomBlockBlockedByNavigations)
                .HasForeignKey(d => d.BlockedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_room_blocks_blocked_by");

            entity.HasOne(d => d.Incident).WithMany(p => p.RoomBlocks)
                .HasForeignKey(d => d.IncidentId)
                .HasConstraintName("FK_room_blocks_incident_id");

            entity.HasOne(d => d.ReleasedByNavigation).WithMany(p => p.RoomBlockReleasedByNavigations)
                .HasForeignKey(d => d.ReleasedBy)
                .HasConstraintName("FK_room_blocks_released_by");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomBlocks)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_room_blocks_room_id");
        });

        modelBuilder.Entity<RoomInspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId).HasName("PK__room_ins__C3C4E743175BCB02");

            entity.ToTable("room_inspections");

            entity.HasIndex(e => e.InspectionNo, "UQ__room_ins__C3E020912494CB5B").IsUnique();

            entity.HasIndex(e => new { e.RoomId, e.InspectedAt }, "room_inspections_index_12");

            entity.Property(e => e.InspectionId).HasColumnName("inspection_id");
            entity.Property(e => e.CleaningTaskId).HasColumnName("cleaning_task_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.InspectedAt).HasColumnName("inspected_at");
            entity.Property(e => e.InspectionNo)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("inspection_no");
            entity.Property(e => e.InspectorUserId).HasColumnName("inspector_user_id");
            entity.Property(e => e.Result)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("result");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.ScheduledAt).HasColumnName("scheduled_at");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.SummaryNote).HasColumnName("summary_note");
            entity.Property(e => e.VersionId).HasColumnName("version_id");

            entity.HasOne(d => d.CleaningTask).WithMany(p => p.RoomInspections)
                .HasForeignKey(d => d.CleaningTaskId)
                .HasConstraintName("FK_room_inspections_cleaning_task_id");

            entity.HasOne(d => d.InspectorUser).WithMany(p => p.RoomInspections)
                .HasForeignKey(d => d.InspectorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_room_inspections_inspector_user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomInspections)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_room_inspections_room_id");

            entity.HasOne(d => d.Version).WithMany(p => p.RoomInspections)
                .HasForeignKey(d => d.VersionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_room_inspections_version_id");
        });

        modelBuilder.Entity<RoomStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__room_sta__096AA2E9A21225A5");

            entity.ToTable("room_status_history");

            entity.HasIndex(e => new { e.RoomId, e.ChangedAt }, "room_status_history_index_10");

            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("from_status");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.ToStatus)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("to_status");
            entity.Property(e => e.TriggerId).HasColumnName("trigger_id");
            entity.Property(e => e.TriggerType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("trigger_type");

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.RoomStatusHistories)
                .HasForeignKey(d => d.ChangedBy)
                .HasConstraintName("FK_room_status_history_changed_by");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomStatusHistories)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_room_status_history_room_id");
        });

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.HasKey(e => e.RoomTypeId).HasName("PK__room_typ__42395E848D05FB98");

            entity.ToTable("room_types");

            entity.HasIndex(e => e.TypeCode, "UQ__room_typ__2CB4DBF5CD9B49E6").IsUnique();

            entity.Property(e => e.RoomTypeId).HasColumnName("room_type_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.StandardCapacity).HasColumnName("standard_capacity");
            entity.Property(e => e.TypeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("type_code");
            entity.Property(e => e.TypeName)
                .HasMaxLength(150)
                .HasColumnName("type_name");
        });

        modelBuilder.Entity<ShiftAssignment>(entity =>
        {
            entity.HasKey(e => e.ShiftAssignmentId).HasName("PK__shift_as__E418729A7EAF8801");

            entity.ToTable("shift_assignments");

            entity.HasIndex(e => new { e.ShiftId, e.UserId }, "shift_assignments_index_37").IsUnique();

            entity.Property(e => e.ShiftAssignmentId).HasColumnName("shift_assignment_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.AssignmentStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("ASSIGNED")
                .HasColumnName("assignment_status");
            entity.Property(e => e.CancellationReason).HasColumnName("cancellation_reason");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.ShiftAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shift_assignments_assigned_by");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftAssignments)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shift_assignments_shift_id");

            entity.HasOne(d => d.User).WithMany(p => p.ShiftAssignmentUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shift_assignments_user_id");
        });

        modelBuilder.Entity<ShiftAttendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__shift_at__20D6A96870E9AB63");

            entity.ToTable("shift_attendance");

            entity.HasIndex(e => e.ShiftAssignmentId, "UQ__shift_at__E418729B0FB1931B").IsUnique();

            entity.Property(e => e.AttendanceId).HasColumnName("attendance_id");
            entity.Property(e => e.ActualCheckInAt).HasColumnName("actual_check_in_at");
            entity.Property(e => e.ActualCheckOutAt).HasColumnName("actual_check_out_at");
            entity.Property(e => e.AttendanceStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("NOT_STARTED")
                .HasColumnName("attendance_status");
            entity.Property(e => e.LateMinutes)
                .HasDefaultValue(0)
                .HasColumnName("late_minutes");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.OvertimeMinutes)
                .HasDefaultValue(0)
                .HasColumnName("overtime_minutes");
            entity.Property(e => e.ShiftAssignmentId).HasColumnName("shift_assignment_id");
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
            entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");

            entity.HasOne(d => d.ShiftAssignment).WithOne(p => p.ShiftAttendance)
                .HasForeignKey<ShiftAttendance>(d => d.ShiftAssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shift_attendance_shift_assignment_id");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.ShiftAttendances)
                .HasForeignKey(d => d.VerifiedBy)
                .HasConstraintName("FK_shift_attendance_verified_by");
        });

        modelBuilder.Entity<ShiftTemplate>(entity =>
        {
            entity.HasKey(e => e.ShiftTemplateId).HasName("PK__shift_te__4A2DE082B43B11AE");

            entity.ToTable("shift_templates");

            entity.HasIndex(e => new { e.BranchId, e.ShiftCode }, "shift_templates_index_36").IsUnique();

            entity.Property(e => e.ShiftTemplateId).HasColumnName("shift_template_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.BreakMinutes).HasColumnName("break_minutes");
            entity.Property(e => e.CrossesMidnight).HasColumnName("crosses_midnight");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ShiftCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("shift_code");
            entity.Property(e => e.ShiftName)
                .HasMaxLength(100)
                .HasColumnName("shift_name");
            entity.Property(e => e.StartTime).HasColumnName("start_time");

            entity.HasOne(d => d.Branch).WithMany(p => p.ShiftTemplates)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shift_templates_branch_id");
        });

        modelBuilder.Entity<StockDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PK__stock_do__9666E8AC2AC8BA18");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("stock_documents");

            entity.HasIndex(e => e.DocumentNo, "UQ__stock_do__967931BE36726CCA").IsUnique();

            entity.HasIndex(e => new { e.DocumentType, e.Status }, "stock_documents_index_22");

            entity.HasIndex(e => e.DocumentDate, "stock_documents_index_23");

            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DestinationWarehouseId).HasColumnName("destination_warehouse_id");
            entity.Property(e => e.DocumentDate).HasColumnName("document_date");
            entity.Property(e => e.DocumentNo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("document_no");
            entity.Property(e => e.DocumentType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("document_type");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.PostedAt).HasColumnName("posted_at");
            entity.Property(e => e.PostedBy).HasColumnName("posted_by");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.ReferenceType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("reference_type");
            entity.Property(e => e.ReversalOfDocumentId).HasColumnName("reversal_of_document_id");
            entity.Property(e => e.SourceWarehouseId).HasColumnName("source_warehouse_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.StockDocumentApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_stock_documents_approved_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StockDocumentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_documents_created_by");

            entity.HasOne(d => d.DestinationWarehouse).WithMany(p => p.StockDocumentDestinationWarehouses)
                .HasForeignKey(d => d.DestinationWarehouseId)
                .HasConstraintName("FK_stock_documents_destination_warehouse_id");

            entity.HasOne(d => d.PostedByNavigation).WithMany(p => p.StockDocumentPostedByNavigations)
                .HasForeignKey(d => d.PostedBy)
                .HasConstraintName("FK_stock_documents_posted_by");

            entity.HasOne(d => d.ReversalOfDocument).WithMany(p => p.InverseReversalOfDocument)
                .HasForeignKey(d => d.ReversalOfDocumentId)
                .HasConstraintName("FK_stock_documents_reversal_of_document_id");

            entity.HasOne(d => d.SourceWarehouse).WithMany(p => p.StockDocumentSourceWarehouses)
                .HasForeignKey(d => d.SourceWarehouseId)
                .HasConstraintName("FK_stock_documents_source_warehouse_id");
        });

        modelBuilder.Entity<StockDocumentLine>(entity =>
        {
            entity.HasKey(e => e.LineId).HasName("PK__stock_do__F5AE5F62162FCEDA");

            entity.ToTable("stock_document_lines");

            entity.HasIndex(e => new { e.DocumentId, e.LineNo }, "stock_document_lines_index_24").IsUnique();

            entity.Property(e => e.LineId).HasColumnName("line_id");
            entity.Property(e => e.ActualQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("actual_qty");
            entity.Property(e => e.BaseQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("base_qty");
            entity.Property(e => e.ConversionFactor)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(18, 6)")
                .HasColumnName("conversion_factor");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.RequestedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("requested_qty");
            entity.Property(e => e.UnitCost)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("unit_cost");
            entity.Property(e => e.UomId).HasColumnName("uom_id");

            entity.HasOne(d => d.Document).WithMany(p => p.StockDocumentLines)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_document_lines_document_id");

            entity.HasOne(d => d.Item).WithMany(p => p.StockDocumentLines)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_document_lines_item_id");

            entity.HasOne(d => d.Uom).WithMany(p => p.StockDocumentLines)
                .HasForeignKey(d => d.UomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_document_lines_uom_id");
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId).HasName("PK__stock_mo__AB1D102219A07711");

            entity.ToTable("stock_movements");

            entity.HasIndex(e => new { e.WarehouseId, e.ItemId, e.OccurredAt }, "stock_movements_index_25");

            entity.Property(e => e.MovementId).HasColumnName("movement_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DocumentLineId).HasColumnName("document_line_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.SignedBaseQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("signed_base_qty");
            entity.Property(e => e.UnitCost)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("unit_cost");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_movements_created_by");

            entity.HasOne(d => d.DocumentLine).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.DocumentLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_movements_document_line_id");

            entity.HasOne(d => d.Item).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_movements_item_id");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_movements_warehouse_id");
        });

        modelBuilder.Entity<StockThreshold>(entity =>
        {
            entity.HasKey(e => e.ThresholdId).HasName("PK__stock_th__A2E239DED8B42A00");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("stock_thresholds");

            entity.HasIndex(e => new { e.WarehouseId, e.ItemId }, "stock_thresholds_index_21").IsUnique();

            entity.Property(e => e.ThresholdId).HasColumnName("threshold_id");
            entity.Property(e => e.AlertEnabled)
                .HasDefaultValue(true)
                .HasColumnName("alert_enabled");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.MaximumQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("maximum_qty");
            entity.Property(e => e.MinimumQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("minimum_qty");
            entity.Property(e => e.ReorderQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("reorder_qty");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");

            entity.HasOne(d => d.Item).WithMany(p => p.StockThresholds)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_thresholds_item_id");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockThresholds)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_thresholds_warehouse_id");
        });

        modelBuilder.Entity<Stocktake>(entity =>
        {
            entity.HasKey(e => e.StocktakeId).HasName("PK__stocktak__988280EDCD3908BB");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("stocktakes");

            entity.HasIndex(e => e.StocktakeNo, "UQ__stocktak__9882A83F9B36B25E").IsUnique();

            entity.Property(e => e.StocktakeId).HasColumnName("stocktake_id");
            entity.Property(e => e.AdjustmentDocumentId).HasColumnName("adjustment_document_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CountedBy).HasColumnName("counted_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.SnapshotAt).HasColumnName("snapshot_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.StocktakeNo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("stocktake_no");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");

            entity.HasOne(d => d.AdjustmentDocument).WithMany(p => p.Stocktakes)
                .HasForeignKey(d => d.AdjustmentDocumentId)
                .HasConstraintName("FK_stocktakes_adjustment_document_id");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.StocktakeApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_stocktakes_approved_by");

            entity.HasOne(d => d.CountedByNavigation).WithMany(p => p.StocktakeCountedByNavigations)
                .HasForeignKey(d => d.CountedBy)
                .HasConstraintName("FK_stocktakes_counted_by");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.Stocktakes)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stocktakes_warehouse_id");
        });

        modelBuilder.Entity<StocktakeLine>(entity =>
        {
            entity.HasKey(e => e.StocktakeLineId).HasName("PK__stocktak__65202FD583442BD4");

            entity.ToTable("stocktake_lines");

            entity.HasIndex(e => new { e.StocktakeId, e.ItemId }, "stocktake_lines_index_26").IsUnique();

            entity.Property(e => e.StocktakeLineId).HasColumnName("stocktake_line_id");
            entity.Property(e => e.CountedAt).HasColumnName("counted_at");
            entity.Property(e => e.CountedBy).HasColumnName("counted_by");
            entity.Property(e => e.CountedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("counted_qty");
            entity.Property(e => e.DiscrepancyReason).HasColumnName("discrepancy_reason");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.StocktakeId).HasColumnName("stocktake_id");
            entity.Property(e => e.SystemQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("system_qty");
            entity.Property(e => e.VarianceQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("variance_qty");

            entity.HasOne(d => d.CountedByNavigation).WithMany(p => p.StocktakeLines)
                .HasForeignKey(d => d.CountedBy)
                .HasConstraintName("FK_stocktake_lines_counted_by");

            entity.HasOne(d => d.Item).WithMany(p => p.StocktakeLines)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stocktake_lines_item_id");

            entity.HasOne(d => d.Stocktake).WithMany(p => p.StocktakeLines)
                .HasForeignKey(d => d.StocktakeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stocktake_lines_stocktake_id");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PK__system_s__256E1E32A0E730E4");

            entity.ToTable("system_settings");

            entity.HasIndex(e => new { e.BranchId, e.SettingGroup, e.SettingKey }, "system_settings_index_47").IsUnique();

            entity.Property(e => e.SettingId).HasColumnName("setting_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.DataType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("STRING")
                .HasColumnName("data_type");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsSensitive).HasColumnName("is_sensitive");
            entity.Property(e => e.SettingGroup)
                .HasMaxLength(60)
                .IsUnicode(false)
                .HasColumnName("setting_group");
            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue).HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Branch).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_system_settings_branch_id");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_system_settings_updated_by");
        });

        modelBuilder.Entity<UnitsOfMeasure>(entity =>
        {
            entity.HasKey(e => e.UomId).HasName("PK__units_of__48EF04DF7562B367");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("units_of_measure");

            entity.HasIndex(e => e.UomCode, "UQ__units_of__31E2176B7AC6F419").IsUnique();

            entity.Property(e => e.UomId).HasColumnName("uom_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.UomCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("uom_code");
            entity.Property(e => e.UomName)
                .HasMaxLength(100)
                .HasColumnName("uom_name");
        });

        modelBuilder.Entity<UomConversion>(entity =>
        {
            entity.HasKey(e => e.ConversionId).HasName("PK__uom_conv__E4E07B3FD3F36B3B");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("uom_conversions");

            entity.HasIndex(e => new { e.ItemId, e.FromUomId, e.ToUomId }, "uom_conversions_index_20").IsUnique();

            entity.Property(e => e.ConversionId).HasColumnName("conversion_id");
            entity.Property(e => e.ConversionFactor)
                .HasColumnType("decimal(18, 6)")
                .HasColumnName("conversion_factor");
            entity.Property(e => e.FromUomId).HasColumnName("from_uom_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ToUomId).HasColumnName("to_uom_id");

            entity.HasOne(d => d.FromUom).WithMany(p => p.UomConversionFromUoms)
                .HasForeignKey(d => d.FromUomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_uom_conversions_from_uom_id");

            entity.HasOne(d => d.Item).WithMany(p => p.UomConversions)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("FK_uom_conversions_item_id");

            entity.HasOne(d => d.ToUom).WithMany(p => p.UomConversionToUoms)
                .HasForeignKey(d => d.ToUomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_uom_conversions_to_uom_id");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370FD4563718");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E61641B43B5D5").IsUnique();

            entity.HasIndex(e => e.EmployeeCode, "UQ__users__B0AA73458F47F1B4").IsUnique();

            entity.HasIndex(e => e.Username, "UQ__users__F3DBC57220477F7A").IsUnique();

            entity.HasIndex(e => new { e.PrimaryBranchId, e.DepartmentId }, "users_index_38");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AccountStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("account_status");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("employee_code");
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("employment_status");
            entity.Property(e => e.FullName)
                .HasMaxLength(200)
                .HasColumnName("full_name");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.PrimaryBranchId).HasColumnName("primary_branch_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("username");

            entity.HasOne(d => d.Department).WithMany(p => p.Users)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_users_department_id");

            entity.HasOne(d => d.ManagerUser).WithMany(p => p.InverseManagerUser)
                .HasForeignKey(d => d.ManagerUserId)
                .HasConstraintName("FK_users_manager_user_id");

            entity.HasOne(d => d.PrimaryBranch).WithMany(p => p.Users)
                .HasForeignKey(d => d.PrimaryBranchId)
                .HasConstraintName("FK_users_primary_branch_id");
        });

        modelBuilder.Entity<UserBranchAccess>(entity =>
        {
            entity.HasKey(e => e.AccessId).HasName("PK__user_bra__10FA1E20F44C43BC");

            entity.ToTable("user_branch_access");

            entity.HasIndex(e => new { e.UserId, e.BranchId }, "user_branch_access_index_41").IsUnique();

            entity.Property(e => e.AccessId).HasColumnName("access_id");
            entity.Property(e => e.AccessLevel)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("STANDARD")
                .HasColumnName("access_level");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.GrantedAt).HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Branch).WithMany(p => p.UserBranchAccesses)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_user_branch_access_branch_id");

            entity.HasOne(d => d.GrantedByNavigation).WithMany(p => p.UserBranchAccessGrantedByNavigations)
                .HasForeignKey(d => d.GrantedBy)
                .HasConstraintName("FK_user_branch_access_granted_by");

            entity.HasOne(d => d.User).WithMany(p => p.UserBranchAccessUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_user_branch_access_user_id");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.UserRoleId).HasName("PK__user_rol__B8D9ABA2A37D4098");

            entity.ToTable("user_roles");

            entity.HasIndex(e => new { e.UserId, e.RoleId, e.BranchId }, "user_roles_index_39").IsUnique();

            entity.Property(e => e.UserRoleId).HasColumnName("user_role_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.UserRoleAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK_user_roles_assigned_by");

            entity.HasOne(d => d.Branch).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_user_roles_branch_id");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_user_roles_role_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_user_roles_user_id");
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__user_ses__69B13FDCB96F3735");

            entity.ToTable("user_sessions");

            entity.HasIndex(e => e.RefreshTokenHash, "UQ__user_ses__63546C90BE1D5BBB").IsUnique();

            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DeviceInfo).HasColumnName("device_info");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("ip_address");
            entity.Property(e => e.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(e => e.RefreshTokenHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("refresh_token_hash");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserSessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_user_sessions_user_id");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.WarehouseId).HasName("PK__warehous__734FE6BF3D0071C3");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");

            entity.ToTable("warehouses");

            entity.HasIndex(e => new { e.BranchId, e.WarehouseCode }, "warehouses_index_19").IsUnique();

            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LocationDescription).HasColumnName("location_description");
            entity.Property(e => e.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(e => e.WarehouseCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("warehouse_code");
            entity.Property(e => e.WarehouseName)
                .HasMaxLength(150)
                .HasColumnName("warehouse_name");

            entity.HasOne(d => d.Branch).WithMany(p => p.Warehouses)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_warehouses_branch_id");

            entity.HasOne(d => d.Department).WithMany(p => p.Warehouses)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_warehouses_department_id");

            entity.HasOne(d => d.ManagerUser).WithMany(p => p.Warehouses)
                .HasForeignKey(d => d.ManagerUserId)
                .HasConstraintName("FK_warehouses_manager_user_id");
        });

        modelBuilder.Entity<WorkCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__work_cat__D54EE9B4ED346103");

            entity.ToTable("work_categories");

            entity.HasIndex(e => e.CategoryCode, "UQ__work_cat__BC9D1E7C9BA35E22").IsUnique();

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryCode)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("category_code");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(150)
                .HasColumnName("category_name");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.Department).WithMany(p => p.WorkCategories)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_work_categories_department_id");
        });

        modelBuilder.Entity<WorkShift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("PK__work_shi__7B267220C8C2BEE1");

            entity.ToTable("work_shifts");

            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.RequiredStaffCount)
                .HasDefaultValue(1)
                .HasColumnName("required_staff_count");
            entity.Property(e => e.ScheduledEndAt).HasColumnName("scheduled_end_at");
            entity.Property(e => e.ScheduledStartAt).HasColumnName("scheduled_start_at");
            entity.Property(e => e.ShiftDate).HasColumnName("shift_date");
            entity.Property(e => e.ShiftTemplateId).HasColumnName("shift_template_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PLANNED")
                .HasColumnName("status");

            entity.HasOne(d => d.Branch).WithMany(p => p.WorkShifts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_work_shifts_branch_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.WorkShifts)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_work_shifts_created_by");

            entity.HasOne(d => d.Department).WithMany(p => p.WorkShifts)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_work_shifts_department_id");

            entity.HasOne(d => d.ShiftTemplate).WithMany(p => p.WorkShifts)
                .HasForeignKey(d => d.ShiftTemplateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_work_shifts_shift_template_id");
        });

        modelBuilder.Entity<StockTransferReceipt>(entity =>
        {
            entity.HasKey(e => e.TransferReceiptId);

            entity.ToTable("stock_transfer_receipts");

            entity.HasIndex(e => new { e.TransferDocumentId, e.TransferReceiptId }, "IX_stock_transfer_receipts_document");

            entity.HasIndex(e => e.ReceivedBy, "IX_stock_transfer_receipts_received_by");

            entity.Property(e => e.TransferReceiptId).HasColumnName("transfer_receipt_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReceivedAt).HasColumnName("received_at");
            entity.Property(e => e.ReceivedBy).HasColumnName("received_by");
            entity.Property(e => e.TransferDocumentId).HasColumnName("transfer_document_id");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.StockTransferReceipts)
                .HasForeignKey(d => d.ReceivedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_transfer_receipts_user");

            entity.HasOne(d => d.TransferDocument).WithMany(p => p.StockTransferReceipts)
                .HasForeignKey(d => d.TransferDocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_transfer_receipts_document");
        });

        modelBuilder.Entity<StockTransferReceiptLine>(entity =>
        {
            entity.HasKey(e => e.TransferReceiptLineId);

            entity.ToTable("stock_transfer_receipt_lines");

            entity.HasIndex(e => e.TransferReceiptId, "IX_stock_transfer_receipt_lines_receipt");

            entity.HasIndex(e => e.TransferLineId, "IX_stock_transfer_receipt_lines_transfer_line");

            entity.Property(e => e.TransferReceiptLineId).HasColumnName("transfer_receipt_line_id");
            entity.Property(e => e.DamagedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("damaged_qty");
            entity.Property(e => e.MissingQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("missing_qty");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReceivedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("received_qty");
            entity.Property(e => e.TransferLineId).HasColumnName("transfer_line_id");
            entity.Property(e => e.TransferReceiptId).HasColumnName("transfer_receipt_id");

            entity.HasOne(d => d.TransferLine).WithMany(p => p.StockTransferReceiptLines)
                .HasForeignKey(d => d.TransferLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_transfer_receipt_lines_line");

            entity.HasOne(d => d.TransferReceipt).WithMany(p => p.StockTransferReceiptLines)
                .HasForeignKey(d => d.TransferReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_transfer_receipt_lines_receipt");
        });

        modelBuilder.Entity<StockReceiptLineDetail>(entity =>
        {
            entity.HasKey(e => e.StockDocumentLineId);

            entity.ToTable("stock_receipt_line_details");

            entity.Property(e => e.StockDocumentLineId)
                .ValueGeneratedNever()
                .HasColumnName("stock_document_line_id");
            entity.Property(e => e.DamagedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("damaged_qty");
            entity.Property(e => e.DiscrepancyReason).HasColumnName("discrepancy_reason");
            entity.Property(e => e.RejectedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("rejected_qty");

            entity.HasOne(d => d.StockDocumentLine).WithOne(p => p.StockReceiptLineDetail)
                .HasForeignKey<StockReceiptLineDetail>(d => d.StockDocumentLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_receipt_line_details_line");
        });

        modelBuilder.Entity<StockReturnLineDetail>(entity =>
        {
            entity.HasKey(e => e.StockDocumentLineId);

            entity.ToTable("stock_return_line_details");

            entity.HasIndex(e => e.OriginalIssueLineId, "IX_stock_return_line_details_original");

            entity.Property(e => e.StockDocumentLineId)
                .ValueGeneratedNever()
                .HasColumnName("stock_document_line_id");
            entity.Property(e => e.DamagedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("damaged_qty");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.OriginalIssueLineId).HasColumnName("original_issue_line_id");

            entity.HasOne(d => d.OriginalIssueLine).WithMany(p => p.StockReturnLineDetailOriginalIssueLines)
                .HasForeignKey(d => d.OriginalIssueLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_return_line_details_original");

            entity.HasOne(d => d.StockDocumentLine).WithOne(p => p.StockReturnLineDetailStockDocumentLine)
                .HasForeignKey<StockReturnLineDetail>(d => d.StockDocumentLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_stock_return_line_details_line");
        });

        modelBuilder.Entity<MaterialRequestApprovalCycle>(entity =>
        {
            entity.HasKey(e => e.ApprovalCycleId).HasName("PK__material__FBD97943D0FA7855");

            entity.ToTable("material_request_approval_cycles");

            entity.HasIndex(e => new { e.RequestId, e.RevisionNo }, "UQ_mr_cycles_revision").IsUnique();

            entity.Property(e => e.ApprovalCycleId).HasColumnName("approval_cycle_id");
            entity.Property(e => e.AssignedApproverId).HasColumnName("assigned_approver_id");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.RevisionNo).HasColumnName("revision_no");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.SubmittedBy).HasColumnName("submitted_by");

            entity.HasOne(d => d.AssignedApprover).WithMany(p => p.MaterialRequestApprovalCycleAssignedApprovers)
                .HasForeignKey(d => d.AssignedApproverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_cycles_approver");

            entity.HasOne(d => d.Request).WithMany(p => p.MaterialRequestApprovalCycles)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_cycles_request");

            entity.HasOne(d => d.SubmittedByNavigation).WithMany(p => p.MaterialRequestApprovalCycleSubmittedByNavigations)
                .HasForeignKey(d => d.SubmittedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_cycles_submitter");
        });

        modelBuilder.Entity<MaterialRequestApprovalLineSnapshot>(entity =>
        {
            entity.HasKey(e => e.SnapshotId).HasName("PK__material__C27CFBF76273C8DC");

            entity.ToTable("material_request_approval_line_snapshots");

            entity.HasIndex(e => new { e.ApprovalCycleId, e.RequestLineId }, "UQ_mr_snapshots_line").IsUnique();

            entity.Property(e => e.SnapshotId).HasColumnName("snapshot_id");
            entity.Property(e => e.ApprovalCycleId).HasColumnName("approval_cycle_id");
            entity.Property(e => e.ApprovedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("approved_qty");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.RequestLineId).HasColumnName("request_line_id");
            entity.Property(e => e.RequestedQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("requested_qty");
            entity.Property(e => e.UomId).HasColumnName("uom_id");

            entity.HasOne(d => d.ApprovalCycle).WithMany(p => p.MaterialRequestApprovalLineSnapshots)
                .HasForeignKey(d => d.ApprovalCycleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_snapshots_cycle");

            entity.HasOne(d => d.Item).WithMany(p => p.MaterialRequestApprovalLineSnapshots)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_snapshots_item");

            entity.HasOne(d => d.RequestLine).WithMany(p => p.MaterialRequestApprovalLineSnapshots)
                .HasForeignKey(d => d.RequestLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_snapshots_line");

            entity.HasOne(d => d.Uom).WithMany(p => p.MaterialRequestApprovalLineSnapshots)
                .HasForeignKey(d => d.UomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_mr_snapshots_uom");
        });

        modelBuilder.Entity<MaterialReplenishment>(entity =>
        {
            entity.HasKey(e => e.ReplenishmentId);

            entity.ToTable("material_replenishments");

            entity.HasIndex(e => e.ClosedBy, "IX_material_replenishments_closed_by");

            entity.HasIndex(e => e.RequestedBy, "IX_material_replenishments_requested_by");

            entity.HasIndex(e => new { e.WarehouseId, e.Status, e.ExpectedOn, e.ReplenishmentId }, "IX_replenishments_tracking");

            entity.HasIndex(e => e.ReplenishmentNo, "UQ_replenishment_no").IsUnique();

            entity.HasIndex(e => new { e.ReplenishmentId, e.WarehouseId }, "UQ_replenishments_warehouse").IsUnique();

            entity.Property(e => e.ReplenishmentId).HasColumnName("replenishment_id");
            entity.Property(e => e.CloseReason).HasColumnName("close_reason");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.ClosedBy).HasColumnName("closed_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpectedOn).HasColumnName("expected_on");
            entity.Property(e => e.ReplenishmentNo)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("replenishment_no");
            entity.Property(e => e.RequestedBy).HasColumnName("requested_by");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("row_version");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.SupplierName)
                .HasMaxLength(200)
                .HasColumnName("supplier_name");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");

            entity.HasOne(d => d.ClosedByNavigation).WithMany(p => p.MaterialReplenishmentClosedByNavigations)
                .HasForeignKey(d => d.ClosedBy)
                .HasConstraintName("FK_replenishments_closer");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.MaterialReplenishmentRequestedByNavigations)
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishments_requester");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MaterialReplenishments)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishments_warehouse");
        });

        modelBuilder.Entity<MaterialReplenishmentLine>(entity =>
        {
            entity.HasKey(e => e.ReplenishmentLineId);

            entity.ToTable("material_replenishment_lines");

            entity.HasIndex(e => e.BaseUomId, "IX_material_replenishment_lines_base_uom_id");

            entity.HasIndex(e => e.ItemId, "IX_material_replenishment_lines_item_id");

            entity.HasIndex(e => new { e.ReplenishmentId, e.WarehouseId }, "IX_material_replenishment_lines_replenishment_id_warehouse_id");

            entity.HasIndex(e => e.SourceMaterialRequestLineId, "IX_replenishment_lines_request_line");

            entity.HasIndex(e => e.SourceThresholdId, "IX_replenishment_lines_threshold");

            entity.HasIndex(e => new { e.ReplenishmentId, e.LineNo }, "UQ_replenishment_lines_number").IsUnique();

            entity.HasIndex(e => new { e.WarehouseId, e.ItemId }, "UX_replenishment_active_coverage")
                .IsUnique()
                .HasFilter("([is_open_coverage]=(1))");

            entity.Property(e => e.ReplenishmentLineId).HasColumnName("replenishment_line_id");
            entity.Property(e => e.BaseUomId).HasColumnName("base_uom_id");
            entity.Property(e => e.IsOpenCoverage).HasColumnName("is_open_coverage");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.LineNo).HasColumnName("line_no");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReceivedBaseQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("received_base_qty");
            entity.Property(e => e.ReplenishmentId).HasColumnName("replenishment_id");
            entity.Property(e => e.RequestedBaseQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("requested_base_qty");
            entity.Property(e => e.SourceMaterialRequestLineId).HasColumnName("source_material_request_line_id");
            entity.Property(e => e.SourceThresholdId).HasColumnName("source_threshold_id");
            entity.Property(e => e.SourceType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("source_type");
            entity.Property(e => e.WarehouseId).HasColumnName("warehouse_id");

            entity.HasOne(d => d.BaseUom).WithMany(p => p.MaterialReplenishmentLines)
                .HasForeignKey(d => d.BaseUomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishment_lines_uom");

            entity.HasOne(d => d.Item).WithMany(p => p.MaterialReplenishmentLines)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishment_lines_item");

            entity.HasOne(d => d.SourceMaterialRequestLine).WithMany(p => p.MaterialReplenishmentLines)
                .HasForeignKey(d => d.SourceMaterialRequestLineId)
                .HasConstraintName("FK_replenishment_lines_request_line");

            entity.HasOne(d => d.SourceThreshold).WithMany(p => p.MaterialReplenishmentLines)
                .HasForeignKey(d => d.SourceThresholdId)
                .HasConstraintName("FK_replenishment_lines_threshold");

            entity.HasOne(d => d.MaterialReplenishment).WithMany(p => p.MaterialReplenishmentLines)
                .HasPrincipalKey(p => new { p.ReplenishmentId, p.WarehouseId })
                .HasForeignKey(d => new { d.ReplenishmentId, d.WarehouseId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishment_lines_owner");
        });

        modelBuilder.Entity<MaterialReplenishmentReceiptLine>(entity =>
        {
            entity.HasKey(e => e.ReplenishmentReceiptLineId);

            entity.ToTable("material_replenishment_receipt_lines");

            entity.HasIndex(e => e.LinkedBy, "IX_material_replenishment_receipt_lines_linked_by");

            entity.HasIndex(e => e.ReplenishmentLineId, "IX_replenishment_receipts_owner");

            entity.HasIndex(e => e.StockDocumentLineId, "UQ_replenishment_receipt_stock_line").IsUnique();

            entity.Property(e => e.ReplenishmentReceiptLineId).HasColumnName("replenishment_receipt_line_id");
            entity.Property(e => e.CreditedBaseQty)
                .HasColumnType("decimal(18, 3)")
                .HasColumnName("credited_base_qty");
            entity.Property(e => e.LinkedAt).HasColumnName("linked_at");
            entity.Property(e => e.LinkedBy).HasColumnName("linked_by");
            entity.Property(e => e.ReplenishmentLineId).HasColumnName("replenishment_line_id");
            entity.Property(e => e.StockDocumentLineId).HasColumnName("stock_document_line_id");

            entity.HasOne(d => d.LinkedByNavigation).WithMany(p => p.MaterialReplenishmentReceiptLines)
                .HasForeignKey(d => d.LinkedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishment_receipts_actor");

            entity.HasOne(d => d.ReplenishmentLine).WithMany(p => p.MaterialReplenishmentReceiptLines)
                .HasForeignKey(d => d.ReplenishmentLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishment_receipts_owner");

            entity.HasOne(d => d.StockDocumentLine).WithOne(p => p.MaterialReplenishmentReceiptLine)
                .HasForeignKey<MaterialReplenishmentReceiptLine>(d => d.StockDocumentLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_replenishment_receipts_stock_line");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
