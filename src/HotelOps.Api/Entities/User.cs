using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class User
{
    public long UserId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public long? PrimaryBranchId { get; set; }

    public long? DepartmentId { get; set; }

    public long? ManagerUserId { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public string AccountStatus { get; set; } = null!;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<AssetConditionHistory> AssetConditionHistories { get; set; } = new List<AssetConditionHistory>();

    public virtual ICollection<AssetHandover> AssetHandoverCreatedByNavigations { get; set; } = new List<AssetHandover>();

    public virtual ICollection<AssetHandover> AssetHandoverFromUsers { get; set; } = new List<AssetHandover>();

    public virtual ICollection<AssetHandover> AssetHandoverToUsers { get; set; } = new List<AssetHandover>();

    public virtual ICollection<AssetLocationHistory> AssetLocationHistories { get; set; } = new List<AssetLocationHistory>();

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<CleaningAssignment> CleaningAssignmentAssignedByNavigations { get; set; } = new List<CleaningAssignment>();

    public virtual ICollection<CleaningAssignment> CleaningAssignmentAssigneeUsers { get; set; } = new List<CleaningAssignment>();

    public virtual ICollection<CleaningProgressLog> CleaningProgressLogs { get; set; } = new List<CleaningProgressLog>();

    public virtual ICollection<CleaningRework> CleaningReworks { get; set; } = new List<CleaningRework>();

    public virtual ICollection<CleaningTask> CleaningTasks { get; set; } = new List<CleaningTask>();

    public virtual Department? Department { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<IncidentEvent> IncidentEvents { get; set; } = new List<IncidentEvent>();

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<InspectionChecklistVersion> InspectionChecklistVersions { get; set; } = new List<InspectionChecklistVersion>();

    public virtual ICollection<InspectionChecklist> InspectionChecklists { get; set; } = new List<InspectionChecklist>();

    public virtual ICollection<InspectionDefect> InspectionDefects { get; set; } = new List<InspectionDefect>();

    public virtual ICollection<InternalRequestAssignment> InternalRequestAssignmentAssignedByNavigations { get; set; } = new List<InternalRequestAssignment>();

    public virtual ICollection<InternalRequestAssignment> InternalRequestAssignmentAssignees { get; set; } = new List<InternalRequestAssignment>();

    public virtual ICollection<InternalRequestEvent> InternalRequestEvents { get; set; } = new List<InternalRequestEvent>();

    public virtual ICollection<InternalRequestFeedback> InternalRequestFeedbacks { get; set; } = new List<InternalRequestFeedback>();

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();

    public virtual ICollection<User> InverseManagerUser { get; set; } = new List<User>();

    public virtual ICollection<MaintenanceWork> MaintenanceWorkAssignees { get; set; } = new List<MaintenanceWork>();

    public virtual ICollection<MaintenanceWork> MaintenanceWorkCreatedByNavigations { get; set; } = new List<MaintenanceWork>();

    public virtual ICollection<MaintenanceWorkLog> MaintenanceWorkLogs { get; set; } = new List<MaintenanceWorkLog>();

    public virtual ICollection<MaintenanceWork> MaintenanceWorkReviewedByNavigations { get; set; } = new List<MaintenanceWork>();

    public virtual User? ManagerUser { get; set; }

    public virtual ICollection<MaterialFulfillment> MaterialFulfillmentFulfilledByNavigations { get; set; } = new List<MaterialFulfillment>();

    public virtual ICollection<MaterialFulfillment> MaterialFulfillmentReceivedByNavigations { get; set; } = new List<MaterialFulfillment>();

    public virtual ICollection<MaterialRequestApproval> MaterialRequestApprovals { get; set; } = new List<MaterialRequestApproval>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<NotificationRecipient> NotificationRecipients { get; set; } = new List<NotificationRecipient>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OperationalTaskAssignment> OperationalTaskAssignmentAssignedByNavigations { get; set; } = new List<OperationalTaskAssignment>();

    public virtual ICollection<OperationalTaskAssignment> OperationalTaskAssignmentAssignees { get; set; } = new List<OperationalTaskAssignment>();

    public virtual ICollection<OperationalTaskProgress> OperationalTaskProgresses { get; set; } = new List<OperationalTaskProgress>();

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();

    public virtual ICollection<PreventiveMaintenanceRun> PreventiveMaintenanceRuns { get; set; } = new List<PreventiveMaintenanceRun>();

    public virtual ICollection<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules { get; set; } = new List<PreventiveMaintenanceSchedule>();

    public virtual Branch? PrimaryBranch { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<RoomBlock> RoomBlockBlockedByNavigations { get; set; } = new List<RoomBlock>();

    public virtual ICollection<RoomBlock> RoomBlockReleasedByNavigations { get; set; } = new List<RoomBlock>();

    public virtual ICollection<RoomInspection> RoomInspections { get; set; } = new List<RoomInspection>();

    public virtual ICollection<RoomStatusHistory> RoomStatusHistories { get; set; } = new List<RoomStatusHistory>();

    public virtual ICollection<ShiftAssignment> ShiftAssignmentAssignedByNavigations { get; set; } = new List<ShiftAssignment>();

    public virtual ICollection<ShiftAssignment> ShiftAssignmentUsers { get; set; } = new List<ShiftAssignment>();

    public virtual ICollection<ShiftAttendance> ShiftAttendances { get; set; } = new List<ShiftAttendance>();

    public virtual ICollection<StockDocument> StockDocumentApprovedByNavigations { get; set; } = new List<StockDocument>();

    public virtual ICollection<StockDocument> StockDocumentCreatedByNavigations { get; set; } = new List<StockDocument>();

    public virtual ICollection<StockDocument> StockDocumentPostedByNavigations { get; set; } = new List<StockDocument>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual ICollection<StockTransferReceipt> StockTransferReceipts { get; set; } = new List<StockTransferReceipt>();

    public virtual ICollection<Stocktake> StocktakeApprovedByNavigations { get; set; } = new List<Stocktake>();

    public virtual ICollection<Stocktake> StocktakeCountedByNavigations { get; set; } = new List<Stocktake>();

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();

    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    public virtual ICollection<UserBranchAccess> UserBranchAccessGrantedByNavigations { get; set; } = new List<UserBranchAccess>();

    public virtual ICollection<UserBranchAccess> UserBranchAccessUsers { get; set; } = new List<UserBranchAccess>();

    public virtual ICollection<UserRole> UserRoleAssignedByNavigations { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRole> UserRoleUsers { get; set; } = new List<UserRole>();

    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();

    public virtual ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();

    public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();

    public virtual ICollection<MaterialRequestApprovalCycle> MaterialRequestApprovalCycleAssignedApprovers { get; set; } = new List<MaterialRequestApprovalCycle>();

    public virtual ICollection<MaterialRequestApprovalCycle> MaterialRequestApprovalCycleSubmittedByNavigations { get; set; } = new List<MaterialRequestApprovalCycle>();

    public virtual ICollection<MaterialRequest> MaterialRequestRemainderClosedByNavigations { get; set; } = new List<MaterialRequest>();
}
