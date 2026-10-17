> **Navigation:** [SRS overview](../SRS_README.md) · [Full converted SRS](../HotelOps_SRS_DRAFT.md) · [Pending revisions](../SRS_CHANGE_NOTES.md)

> **DRAFT DOCUMENT — CONVERSION NOTE (not part of the original SRS)**
>
> This Markdown is a structural conversion of the uploaded *Report 3 – Software Requirement Specification*. The original SRS descriptions, permissions, use cases, and business rules have **not** been silently revised. Some complex source tables remain as HTML tables; diagrams and figures are linked in `assets/media/`.
>
> **Pending requirements (not approved yet):** A new **Reception** role will be split from **Operations Manager** to handle designated room lifecycle/status operations. **Department Head** means a manager of departments within one hotel branch/facility, **not** the head of a single department. The **Permission Matrix** must be reworked against the final Use Case list. Exact permissions and room status transitions have **not** been finalized.
>
> **Known source-versus-change conflicts to resolve:** The existing SRS refers to a Department Head associated with an individual department (for example, **GBR-041**) and says room operational status is system-calculated, not manually entered (**GBR-043/GBR-044**). These are **existing source statements, not approved interpretations** of the new Reception requirement. Preserve them as historical draft content; obtain a revised approved rule before implementing affected RBAC/status logic.
>
> **For Codex:** Read `SRS_CHANGE_NOTES.md` alongside this draft. Do not implement unresolved roles, permissions, scope rules, or state transitions by assumption. Treat an absent or unrevised Permission Matrix as pending approval.

---

<img src="../assets/media/image47.png" style="width:2.96778in;height:0.81186in" />

**<span class="smallcaps">CAPSTONE PROJECT REPORT</span>**

**Report 3 – Software Requirement Specification**

> – Hanoi, Aug 2026 –

**Table of Contents**

[Change Log 9](#change-log)

[Definition and Acronyms 10](#definition-and-acronyms)

[I. Overall Requirements 10](#i.-overall-requirements)

> [1. Context Diagram 10](#context-diagram)
>
> [2. Main Business Flow 11](#main-business-flow)
>
> [2.1 Room Cleaning, Inspection and Readiness (BF-01) 11](#room-cleaning-inspection-and-readiness-bf-01)
>
> [2.2 Incident, Maintenance and Asset Resolution (BF-02) 12](#incident-maintenance-and-asset-resolution-bf-02)
>
> [2.3 Material Request, Stock Issue and Return (BF-03) 13](#material-request-stock-issue-and-return-bf-03)
>
> [2.4 Replenishment and Supplier Delivery (BF-04) 13](#replenishment-and-supplier-delivery-bf-04)
>
> [2.5 Operational Task, Recurring Work and Work-Time (BF-05) 15](#operational-task-recurring-work-and-work-time-bf-05)
>
> [2.6 Cross-Department Internal Request (BF-06) 16](#cross-department-internal-request-bf-06)
>
> [3. Conceptual Data Model 17](#conceptual-data-model)
>
> [3.1 Entity Relationship Diagram 17](#entity-relationship-diagram)
>
> [3.2 Entity Descriptions 17](#entity-descriptions)
>
> [3.3 Data Business Rules 24](#data-business-rules)
>
> [4. User Requirements 25](#user-requirements)
>
> [4.1 Actors 25](#actors)
>
> [4.2 Use Cases (UC) 26](#use-cases-uc)
>
> [4.3 Use Case Diagrams 34](#use-case-diagrams)
>
> [4.4 Permission Matrix 45](#permission-matrix)
>
> [5. System Functionalities 48](#system-functionalities)
>
> [5.1 Screen Inventory 48](#screen-inventory)
>
> [5.2 External API Inventory 56](#external-api-inventory)
>
> [5.3 Background Job Inventory 56](#background-job-inventory)

[II. Use Case Specifications 56](#ii.-use-case-specifications)

> [1. Authentication & Role Management (FE-01) 56](#authentication-role-management-fe-01)
>
> [1.1 UC-001 Login 56](#uc-001-login)
>
> [1.2 UC-002 Logout 58](#uc-002-logout)
>
> [1.3 UC-003 Change Password 58](#uc-003-change-password)
>
> [1.4 UC-004 Create User Account 59](#uc-004-create-user-account)
>
> [1.5 UC-005 Update User Account 60](#uc-005-update-user-account)
>
> [1.6 UC-006 Activate/Deactivate User Account 61](#uc-006-activatedeactivate-user-account)
>
> [1.7 UC-007 Assign User to Branch/Department 62](#uc-007-assign-user-to-branchdepartment)
>
> [1.8 UC-008 Create/Update User Role 62](#uc-008-createupdate-user-role)
>
> [1.9 UC-009 Configure Role Permissions 63](#uc-009-configure-role-permissions)
>
> [1.10 UC-010 Assign/Revoke User Roles 64](#uc-010-assignrevoke-user-roles)
>
> [1.11 UC-011 Review User Access Permissions 64](#uc-011-review-user-access-permissions)
>
> [1.12 UC-141 View User List 65](#uc-141-view-user-list)
>
> [2. Hotel Structure Management (FE-02) 65](#hotel-structure-management-fe-02)
>
> [2.1 UC-012 Update Branch Information 65](#uc-012-update-branch-information)
>
> [2.2 UC-013 View Branch Structure 66](#uc-013-view-branch-structure)
>
> [2.3 UC-014 Create Floor/Area 67](#uc-014-create-floorarea)
>
> [2.4 UC-015 Update Floor/Area 67](#uc-015-update-floorarea)
>
> [2.5 UC-016 Deactivate Floor/Area 68](#uc-016-deactivate-floorarea)
>
> [2.6 UC-017 Create Room Type 68](#uc-017-create-room-type)
>
> [2.7 UC-018 Update Room Type 69](#uc-018-update-room-type)
>
> [2.8 UC-019 Deactivate Room Type 69](#uc-019-deactivate-room-type)
>
> [2.9 UC-020 Create Room 70](#uc-020-create-room)
>
> [2.10 UC-021 Update Room Information 71](#uc-021-update-room-information)
>
> [2.11 UC-022 Deactivate Room 71](#uc-022-deactivate-room)
>
> [2.12 UC-023 View Room Directory 72](#uc-023-view-room-directory)
>
> [2.13 UC-024 Create Department 73](#uc-024-create-department)
>
> [2.14 UC-025 Update Department 73](#uc-025-update-department)
>
> [2.15 UC-026 Deactivate Department 74](#uc-026-deactivate-department)
>
> [2.16 UC-142 View Room Types List 74](#uc-142-view-room-types-list)
>
> [3. Room Operations (FE-03) 75](#room-operations-fe-03)
>
> [3.1 UC-027 Manage Operational Room Status 75](#uc-027-manage-operational-room-status)
>
> [4. Housekeeping Management (FE-04) 76](#housekeeping-management-fe-04)
>
> [4.1 UC-028 Create Cleaning Task 76](#uc-028-create-cleaning-task)
>
> [4.2 UC-029 Assign Cleaning Task 77](#uc-029-assign-cleaning-task)
>
> [4.3 UC-030 Reassign Cleaning Task 77](#uc-030-reassign-cleaning-task)
>
> [4.4 UC-031 Start Cleaning Task 78](#uc-031-start-cleaning-task)
>
> [4.5 UC-032 Update Cleaning Progress 79](#uc-032-update-cleaning-progress)
>
> [4.6 UC-033 Report Cleaning Blocker 80](#uc-033-report-cleaning-blocker)
>
> [4.7 UC-034 Complete Cleaning Task 80](#uc-034-complete-cleaning-task)
>
> [4.8 UC-035 Receive Rework Task 81](#uc-035-receive-rework-task)
>
> [4.9 UC-036 Complete Cleaning Rework 82](#uc-036-complete-cleaning-rework)
>
> [4.10 UC-037 Monitor Cleaning Tasks 83](#uc-037-monitor-cleaning-tasks)
>
> [4.11 UC-038 View Room Turnover History 84](#uc-038-view-room-turnover-history)
>
> [5. Room Quality Inspection (FE-05) 84](#room-quality-inspection-fe-05)
>
> [5.1 UC-039 Create Room Inspection Checklist 84](#uc-039-create-room-inspection-checklist)
>
> [5.2 UC-040 Update Room Inspection Checklist 85](#uc-040-update-room-inspection-checklist)
>
> [5.3 UC-041 Assign Room Inspection 85](#uc-041-assign-room-inspection)
>
> [5.4 UC-042 Perform Room Inspection 86](#uc-042-perform-room-inspection)
>
> [5.5 UC-043 Record Inspection Defects 87](#uc-043-record-inspection-defects)
>
> [5.6 UC-044 Approve Room Inspection 88](#uc-044-approve-room-inspection)
>
> [5.7 UC-045 Reject Room Inspection 88](#uc-045-reject-room-inspection)
>
> [5.8 UC-046 Request Cleaning Rework 89](#uc-046-request-cleaning-rework)
>
> [5.9 UC-047 Reinspect Room 90](#uc-047-reinspect-room)
>
> [5.10 UC-048 View Room Inspection History 91](#uc-048-view-room-inspection-history)
>
> [6. Incident & Maintenance Management (FE-06) 91](#incident-maintenance-management-fe-06)
>
> [6.1 UC-049 Report Incident 91](#uc-049-report-incident)
>
> [6.2 UC-050 Classify Incident 92](#uc-050-classify-incident)
>
> [6.3 UC-051 Attach Incident Evidence 93](#uc-051-attach-incident-evidence)
>
> [6.4 UC-052 Triage Incident 94](#uc-052-triage-incident)
>
> [6.5 UC-053 Assign Maintenance Work 95](#uc-053-assign-maintenance-work)
>
> [6.6 UC-054 Accept and Start Maintenance 96](#uc-054-accept-and-start-maintenance)
>
> [6.7 UC-055 Update Maintenance Progress 96](#uc-055-update-maintenance-progress)
>
> [6.8 UC-056 Block Room Due to Incident 97](#uc-056-block-room-due-to-incident)
>
> [6.9 UC-057 Escalate Critical Incident 98](#uc-057-escalate-critical-incident)
>
> [6.10 UC-058 Resolve Maintenance Incident 99](#uc-058-resolve-maintenance-incident)
>
> [6.11 UC-059 Verify and Close Incident 99](#uc-059-verify-and-close-incident)
>
> [6.12 UC-060 Reopen Incident 100](#uc-060-reopen-incident)
>
> [6.13 UC-061 Create Preventive Maintenance Schedule 101](#uc-061-create-preventive-maintenance-schedule)
>
> [6.14 UC-062 Execute Scheduled Maintenance 102](#uc-062-execute-scheduled-maintenance)
>
> [6.15 UC-063 Manage Work Categories 103](#uc-063-manage-work-categories)
>
> [6.16 UC-064 Manage Operational Reference Data 103](#uc-064-manage-operational-reference-data)
>
> [6.17 UC-143 View Incidents List 104](#uc-143-view-incidents-list)
>
> [6.18 UC-144 View Maintenance List 105](#uc-144-view-maintenance-list)
>
> [7. Asset & Equipment Management (FE-07) 105](#asset-equipment-management-fe-07)
>
> [7.1 UC-065 Register Asset/Equipment 105](#uc-065-register-assetequipment)
>
> [7.2 UC-066 Update Asset Information 106](#uc-066-update-asset-information)
>
> [7.3 UC-067 Assign Asset to Location 106](#uc-067-assign-asset-to-location)
>
> [7.4 UC-068 Transfer Asset Location 107](#uc-068-transfer-asset-location)
>
> [7.5 UC-069 Record Asset Handover 108](#uc-069-record-asset-handover)
>
> [7.6 UC-070 Update Asset Condition 109](#uc-070-update-asset-condition)
>
> [7.7 UC-071 View Asset Profile 110](#uc-071-view-asset-profile)
>
> [7.8 UC-072 Review Asset Maintenance History 110](#uc-072-review-asset-maintenance-history)
>
> [7.9 UC-073 Review Assets by Location 111](#uc-073-review-assets-by-location)
>
> [7.10 UC-074 Retire/Deactivate Asset 111](#uc-074-retiredeactivate-asset)
>
> [8. Inventory Management (FE-08) 112](#inventory-management-fe-08)
>
> [8.1 UC-075 Manage Warehouses 112](#uc-075-manage-warehouses)
>
> [8.2 UC-076 Manage Inventory Items 112](#uc-076-manage-inventory-items)
>
> [8.3 UC-077 Manage Units of Measurement 113](#uc-077-manage-units-of-measurement)
>
> [8.4 UC-078 Configure Minimum Stock Levels 114](#uc-078-configure-minimum-stock-levels)
>
> [8.5 UC-079 Create Stock Receipt 114](#uc-079-create-stock-receipt)
>
> [8.6 UC-080 Verify and Post Stock Receipt 115](#uc-080-verify-and-post-stock-receipt)
>
> [8.7 UC-081 Issue Stock 116](#uc-081-issue-stock)
>
> [8.8 UC-082 Return Materials to Stock 117](#uc-082-return-materials-to-stock)
>
> [8.9 UC-083 Create Stock Transfer 117](#uc-083-create-stock-transfer)
>
> [8.10 UC-084 Confirm Stock Transfer Receipt 118](#uc-084-confirm-stock-transfer-receipt)
>
> [8.11 UC-085 Conduct Stocktake 119](#uc-085-conduct-stocktake)
>
> [8.12 UC-086 Reconcile Stocktake Discrepancies 120](#uc-086-reconcile-stocktake-discrepancies)
>
> [8.13 UC-087 Adjust Stock Balance 121](#uc-087-adjust-stock-balance)
>
> [8.14 UC-088 Reverse Stock Transaction 121](#uc-088-reverse-stock-transaction)
>
> [8.15 UC-089 View Stock Balance and Movement Ledger 122](#uc-089-view-stock-balance-and-movement-ledger)
>
> [8.16 UC-090 Return Unused Materials 123](#uc-090-return-unused-materials)
>
> [9. Material Request Management (FE-09) 123](#material-request-management-fe-09)
>
> [9.1 UC-091 Create Material Request 123](#uc-091-create-material-request)
>
> [9.2 UC-092 Link Material Request to Work 124](#uc-092-link-material-request-to-work)
>
> [9.3 UC-093 Update/Cancel Material Request 125](#uc-093-updatecancel-material-request)
>
> [9.4 UC-094 Review Material Request 126](#uc-094-review-material-request)
>
> [9.5 UC-095 Approve/Reject Material Request 127](#uc-095-approvereject-material-request)
>
> [9.6 UC-096 Check Material Availability 128](#uc-096-check-material-availability)
>
> [9.7 UC-097 Issue Approved Materials 128](#uc-097-issue-approved-materials)
>
> [9.8 UC-098 Manage Partial Material Fulfillment 129](#uc-098-manage-partial-material-fulfillment)
>
> [9.9 UC-099 Track Material Request History 129](#uc-099-track-material-request-history)
>
> [10. Material Replenishment Management (FE-10) 130](#material-replenishment-management-fe-10)
>
> [10.1 UC-100 Create Replenishment Request 130](#uc-100-create-replenishment-request)
>
> [10.2 UC-101 Track Material Replenishment 131](#uc-101-track-material-replenishment)
>
> [11. Task Assignment Management (FE-11) 132](#task-assignment-management-fe-11)
>
> [11.1 UC-102 Create Operational Task 132](#uc-102-create-operational-task)
>
> [11.2 UC-103 Set Task Priority and Deadline 132](#uc-103-set-task-priority-and-deadline)
>
> [11.3 UC-104 Assign/Reassign Task 133](#uc-104-assignreassign-task)
>
> [11.4 UC-105 Review and Verify Task Completion 134](#uc-105-review-and-verify-task-completion)
>
> [11.5 UC-106 Reopen Assigned Task 135](#uc-106-reopen-assigned-task)
>
> [11.6 UC-107 Accept Assigned Task 135](#uc-107-accept-assigned-task)
>
> [11.7 UC-108 Start Assigned Task 136](#uc-108-start-assigned-task)
>
> [11.8 UC-109 Update Task Progress 137](#uc-109-update-task-progress)
>
> [11.9 UC-110 Pause/Resume Task 137](#uc-110-pauseresume-task)
>
> [11.10 UC-112 Complete Assigned Task 138](#uc-112-complete-assigned-task)
>
> [11.11 UC-113 Review Task Workload and Time Logs 138](#uc-113-review-task-workload-and-time-logs)
>
> [12. Work-Time & Workday Tracking (FE-12) 139](#work-time-workday-tracking-fe-12)
>
> [12.1 UC-111 Record Actual Working Time 139](#uc-111-record-actual-working-time)
>
> [12.2 UC-138 Manage Operational Workday and Shift Records 140](#uc-138-manage-operational-workday-and-shift-records)
>
> [12.3 UC-139 Record Workday Start and End 141](#uc-139-record-workday-start-and-end)
>
> [12.4 UC-140 Manage Operational Shift Handover 142](#uc-140-manage-operational-shift-handover)
>
> [13. Internal Request Management (FE-13) 143](#internal-request-management-fe-13)
>
> [13.1 UC-114 Create Internal Request 143](#uc-114-create-internal-request)
>
> [13.2 UC-115 Route and Assign Internal Request 144](#uc-115-route-and-assign-internal-request)
>
> [13.3 UC-116 Accept/Reject Internal Request 145](#uc-116-acceptreject-internal-request)
>
> [13.4 UC-117 Update Internal Request Progress 145](#uc-117-update-internal-request-progress)
>
> [13.5 UC-118 Create Linked Operational Work 146](#uc-118-create-linked-operational-work)
>
> [13.6 UC-119 Record Request Handover 147](#uc-119-record-request-handover)
>
> [13.7 UC-120 Complete Internal Request 148](#uc-120-complete-internal-request)
>
> [13.8 UC-121 Reopen Internal Request 148](#uc-121-reopen-internal-request)
>
> [13.9 UC-122 Escalate Overdue Internal Request 149](#uc-122-escalate-overdue-internal-request)
>
> [13.10 UC-123 View Internal Request History 150](#uc-123-view-internal-request-history)
>
> [14. Notification & Operational Dashboard (FE-14) 151](#notification-operational-dashboard-fe-14)
>
> [14.1 UC-124 Receive Assignment Notifications 151](#uc-124-receive-assignment-notifications)
>
> [14.2 UC-125 Receive Approval Notifications 151](#uc-125-receive-approval-notifications)
>
> [14.3 UC-126 Receive Inspection and Incident Alerts 152](#uc-126-receive-inspection-and-incident-alerts)
>
> [14.4 UC-127 Receive Overdue Work Alerts 153](#uc-127-receive-overdue-work-alerts)
>
> [14.5 UC-128 Receive Low Stock Alerts 154](#uc-128-receive-low-stock-alerts)
>
> [14.6 UC-129 View Notification Center 154](#uc-129-view-notification-center)
>
> [14.7 UC-130 Acknowledge Notifications 155](#uc-130-acknowledge-notifications)
>
> [14.8 UC-131 View Room Readiness Dashboard 155](#uc-131-view-room-readiness-dashboard)
>
> [14.9 UC-132 View Task Workload Dashboard 156](#uc-132-view-task-workload-dashboard)
>
> [14.10 UC-133 View Incident and Maintenance Dashboard 157](#uc-133-view-incident-and-maintenance-dashboard)
>
> [14.11 UC-134 View Inventory and Material Dashboard 158](#uc-134-view-inventory-and-material-dashboard)
>
> [15. Operational Reports & Audit History (FE-15) 158](#operational-reports-audit-history-fe-15)
>
> [15.1 UC-135 Generate and Export Operational Reports 158](#uc-135-generate-and-export-operational-reports)
>
> [15.2 UC-136 View Audit Logs 159](#uc-136-view-audit-logs)
>
> [15.3 UC-137 Inspect Record Change History 160](#uc-137-inspect-record-change-history)

[III. Functional Requirements 160](#iii.-functional-requirements)

> [1. Authentication & Role Management (FE-01) 161](#authentication-role-management-fe-01-1)
>
> [1.1 Authentication 161](#authentication)
>
> [1.2 User Management 162](#user-management)
>
> [2. Hotel Structure (FE-02) 166](#hotel-structure-fe-02)
>
> [2.1 Room Management 166](#room-management)
>
> [2.2 Room Type Management 169](#room-type-management)
>
> [3. Room Operations (FE-03) 170](#room-operations-fe-03-1)
>
> [3.1 Room Readiness 170](#room-readiness)
>
> [4. Housekeeping Management (FE-04) 172](#housekeeping-management-fe-04-1)
>
> [4.1 Cleaning Task Management 172](#cleaning-task-management)
>
> [5. Room Inspection (FE-05) 176](#room-inspection-fe-05)
>
> [5.1 Inspection Management 176](#inspection-management)
>
> [6. Incident & Maintenance (FE-06) 179](#incident-maintenance-fe-06)
>
> [6.1 Incident Management 179](#incident-management)
>
> [6.2 Maintenance Execution 182](#maintenance-execution)
>
> [7. Asset & Equipment Management (FE-07) 186](#asset-equipment-management-fe-07-1)
>
> [7.1 Asset Management 186](#asset-management)
>
> [8. Inventory Management (FE-08) 190](#inventory-management-fe-08-1)
>
> [8.1 Inventory Monitoring 190](#inventory-monitoring)
>
> [8.2 Stock Transactions 191](#stock-transactions)
>
> [9. Material Request Management (FE-09) 195](#material-request-management-fe-09-1)
>
> [9.1 Material Requests 195](#material-requests)
>
> [10. Replenishment Management (FE-10) 199](#replenishment-management-fe-10)
>
> [10.1 Replenishment Tracking 199](#replenishment-tracking)
>
> [11. Task Assignment Management (FE-11) 201](#task-assignment-management-fe-11-1)
>
> [11.1 Operational Tasks 201](#operational-tasks)
>
> [12. Work-Time Management (FE-12) 205](#work-time-management-fe-12)
>
> [12.1 Shift Management 205](#shift-management)
>
> [12.2 Shift Handover 206](#shift-handover)
>
> [13. Internal Request Management (FE-13) 209](#internal-request-management-fe-13-1)
>
> [13.1 Internal Requests 209](#internal-requests)
>
> [14. Notification & Operational Dashboard (FE-14) 213](#notification-operational-dashboard-fe-14-1)
>
> [14.1 Operational Dashboard 213](#operational-dashboard)
>
> [15. Operational Reports & Audit History (FE-15) 216](#operational-reports-audit-history-fe-15-1)
>
> [15.1 Reporting 216](#reporting)
>
> [15.2 Audit History 217](#audit-history)
>
> [16. Background Job Inventory 219](#background-job-inventory-1)
>
> [16.1 Check Overdue and At-Risk Work (JOB-01) 219](#check-overdue-and-at-risk-work-job-01)
>
> [16.2 Check Low Stock (JOB-02) 219](#check-low-stock-job-02)
>
> [16.3 Retry Failed Notifications (JOB-03) 220](#retry-failed-notifications-job-03)
>
> [16.4 Generate Preventive Maintenance Occurrences (JOB-04) 220](#generate-preventive-maintenance-occurrences-job-04)
>
> [16.5 Generate Recurring Tasks (JOB-05) 221](#generate-recurring-tasks-job-05)
>
> [16.6 Clean Up Old Notifications and Audit Log Entries (JOB-06) 221](#clean-up-old-notifications-and-audit-log-entries-job-06)
>
> [17. External API Inventory 221](#external-api-inventory-1)
>
> [17.1 HTTP Method: URL pattern (API-ID) 221](#http-method-url-pattern-api-id)
>
> [17.2 VnPay Webhook (API-02) 222](#vnpay-webhook-api-02)
>
> [17.3 SePay Webhook (API-03) 222](#sepay-webhook-api-03)

[IV. Non-Functional Requirements 222](#iv.-non-functional-requirements)

> [4. Non-Functional Requirements 222](#non-functional-requirements)
>
> [4.1 Interfaces 222](#interfaces)
>
> [4.2 Quality Attributes 223](#quality-attributes)

[V. Requirement Appendix 228](#v.-requirement-appendix)

> [1. Business Rules 228](#business-rules)
>
> [2. System Messages 250](#_heading=)
>
> [3. Other Requirements 253](#_heading=)
>
> [3.1 Common Exception Flows 253](#_heading=)
>
> [3.2 System Parameters 253](#_heading=)
>
> [3.3 Status Catalog 254](#_heading=)
>
> [3.4 Status Transitions 256](#_heading=)
>
> [3.5 KPI and Report Catalog 261](#_heading=)
>
> [3.6 Scheduled Processing 262](#scheduled-processing)

# Change Log

<table>
<colgroup>
<col style="width: 14%" />
<col style="width: 7%" />
<col style="width: 13%" />
<col style="width: 64%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Date</strong></th>
<th><strong>A*<br />
M, D</strong></th>
<th><strong>In charge</strong></th>
<th><strong>Change Description</strong></th>
</tr>
<tr class="odd">
<th>30/09/2026</th>
<th>A</th>
<th>BaoDG</th>
<th>Add User Requirements</th>
</tr>
<tr class="header">
<th>01/10/2026</th>
<th>M</th>
<th>BaoDG</th>
<th>Update User Requirements</th>
</tr>
<tr class="odd">
<th>01/10/2026</th>
<th>A</th>
<th>KienTN</th>
<th>Add Screen Flow</th>
</tr>
<tr class="header">
<th>03/10/2026</th>
<th>M</th>
<th>KienTN</th>
<th>Update Screen Flow</th>
</tr>
<tr class="odd">
<th>04/10/2026</th>
<th>A</th>
<th>KienTN</th>
<th>Add Screen List</th>
</tr>
<tr class="header">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="odd">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="header">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="odd">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="header">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="odd">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="header">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
<tr class="odd">
<th></th>
<th></th>
<th></th>
<th></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

\*A - Added M - Modified D - Deleted

# Definition and Acronyms 

| **Acronym** | **Definition**            |
|-------------|---------------------------|
| COS         | Cafeteria Ordering System |
| …           |                           |

