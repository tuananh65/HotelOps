> **Navigation:** [SRS overview](../SRS_README.md) · [Full converted SRS](../HotelOps_SRS_DRAFT.md) · [Pending revisions](../SRS_CHANGE_NOTES.md)

# I. Overall Requirements

## 1. Context Diagram

<img src="../assets/media/image11.png" style="width:6.53646in;height:6.6015in" />

## 2. Main Business Flow

### 2.1 Room Cleaning, Inspection and Readiness (BF-01)

***Trigger:** A room requires cleaning or is identified as not ready for operation.*

***End condition:** The room has passed inspection, has no open blocking incident, and is confirmed as ready for operation.*

<img src="../assets/media/image30.png" style="width:4.13021in;height:7.8152in" />

### 2.2 Incident, Maintenance and Asset Resolution (BF-02)

***Trigger:** An operational incident is reported for a room, area, or hotel asset.*

***End condition:** The incident has been resolved, the maintenance result has been verified, and the incident is closed with any affected room or asset status updated accordingly.*

<img src="../assets/media/image42.png" style="width:4.02536in;height:7.82813in" />

### 2.3 Material Request, Stock Issue and Return (BF-03)

***Trigger:*** A Department Head creates a request for materials required for operational work.

***End condition:*** The requested materials have been fully or partially issued, any remaining quantity is tracked for replenishment, and any unused materials have been returned when applicable

<img src="../assets/media/image52.png" style="width:6.28438in;height:7.01389in" />

### 2.4 Replenishment and Supplier Delivery (BF-04)

***Trigger***: Available stock falls below the required level or is insufficient to fulfill approved material demand.

***End condition***: The received quantity has been recorded into inventory, the stock balance has been updated, and any remaining delivery quantity continues to be tracked until the replenishment request is completed.

<img src="../assets/media/image10.png" style="width:6.28438in;height:7.84722in" />

###  

### 2.5 Operational Task, Recurring Work and Work-Time (BF-05)

***Trigger***: A Department Head creates an ad-hoc operational task or a scheduled occurrence is generated from a recurring task rule.

***End condition***: The assigned work has been completed and verified, actual work-time has been recorded, and any required handover or rework has been completed.

<img src="../assets/media/image17.png" style="width:6.28438in;height:7.20833in" />

### 2.6 Cross-Department Internal Request (BF-06)

***Trigger***: A Department Head submits an internal request for operational support from another department.

***End condition***: The requested work has been processed and the result has either been confirmed by the requesting Department Head or the request has been reopened for further action.

<img src="../assets/media/image1.png" style="width:3.72083in;height:7.35938in" />

## 3. Conceptual Data Model

### 3.1 Entity Relationship Diagram

<img src="../assets/media/image50.png" style="width:6.28438in;height:4.05556in" />

### 3.2 Entity Descriptions

#### Entity: Branch

| **Field**                   | **Value**                                                                                                      |
|-----------------------------|----------------------------------------------------------------------------------------------------------------|
| **Purpose**                 | Represents a hotel operating branch managed within HotelOps.                                                   |
| **Key business attributes** | Branch code, branch name, status                                                                               |
| **Business identity**       | Branch code must uniquely identify a hotel branch.                                                             |
| **Status / lifecycle**      | Active → Inactive. A branch should only be deactivated after relevant open operational work has been resolved. |

#### Entity: Department

| **Field**                   | **Value**                                                         |
|-----------------------------|-------------------------------------------------------------------|
| **Purpose**                 | Represents an operational department belonging to a hotel branch. |
| **Key business attributes** | Branch, department code, department name, status                  |
| **Business identity**       | Department code is unique within its Branch.                      |
| **Status / lifecycle**      | Active → Inactive                                                 |

#### Entity: User

| **Field**                   | **Value**                                                                       |
|-----------------------------|---------------------------------------------------------------------------------|
| **Purpose**                 | Represents an authenticated internal user who participates in hotel operations. |
| **Key business attributes** | Username, full name, department, account status                                 |
| **Business identity**       | Username must uniquely identify a User within the system.                       |
| **Status / lifecycle**      | Active ↔ Locked; Active → Inactive                                              |

#### Entity: Role Assignment

| **Field**                   | **Value**                                                         |
|-----------------------------|-------------------------------------------------------------------|
| **Purpose**                 | Defines the operational role and access scope assigned to a User. |
| **Key business attributes** | User, role, branch scope, department scope, effective status      |
| **Business identity**       | Defined by the combination of User, Role, and assigned scope.     |
| **Status / lifecycle**      | Active → Revoked                                                  |

#### Entity: Room

| **Field**                   | **Value**                                                                                     |
|-----------------------------|-----------------------------------------------------------------------------------------------|
| **Purpose**                 | Represents a physical hotel room and its current operational readiness.                       |
| **Key business attributes** | Branch, room number, operational status                                                       |
| **Business identity**       | Room number must be unique within a Branch.                                                   |
| **Status / lifecycle**      | Room status is updated through authorized operational actions and retains its status history. |

#### Entity: Housekeeping Task

| **Field**                   | **Value**                                                                             |
|-----------------------------|---------------------------------------------------------------------------------------|
| **Purpose**                 | Represents cleaning work created for a hotel Room and assigned to Housekeeping Staff. |
| **Key business attributes** | Room, assignee, priority, due time, cleaning checklist                                |
| **Business identity**       | Task number uniquely identifies a Housekeeping Task.                                  |
| **Status / lifecycle**      | Assigned → In Progress → Awaiting Inspection → Completed / Rework                     |

#### Entity: Inspection

| **Field**                   | **Value**                                                                          |
|-----------------------------|------------------------------------------------------------------------------------|
| **Purpose**                 | Represents the quality inspection performed after room-cleaning work is submitted. |
| **Key business attributes** | Housekeeping Task, inspector, checklist, result, failed items                      |
| **Business identity**       | Inspection number uniquely identifies an Inspection.                               |
| **Status / lifecycle**      | Submitted → Pass / Fail                                                            |

#### Entity: Incident

| **Field**                   | **Value**                                                                              |
|-----------------------------|----------------------------------------------------------------------------------------|
| **Purpose**                 | Represents a reported operational fault or issue affecting a room, location, or asset. |
| **Key business attributes** | Branch, room/location, asset, category, priority                                       |
| **Business identity**       | Incident number uniquely identifies an Incident.                                       |
| **Status / lifecycle**      | Open → Assigned → In Progress → Resolved → Closed / Reopened                           |

#### Entity: Work Order

| **Field**                   | **Value**                                                   |
|-----------------------------|-------------------------------------------------------------|
| **Purpose**                 | Represents maintenance work created to resolve an Incident. |
| **Key business attributes** | Incident, technician, diagnosis, work result, evidence      |
| **Business identity**       | Work order number uniquely identifies a Work Order.         |
| **Status / lifecycle**      | Assigned → In Progress → Verification → Closed / Rework     |

#### Entity: Asset

| **Field**                   | **Value**                                                                      |
|-----------------------------|--------------------------------------------------------------------------------|
| **Purpose**                 | Represents hotel equipment or another operational asset tracked by the system. |
| **Key business attributes** | Asset code, branch, room/location, condition                                   |
| **Business identity**       | Asset code is unique within its Branch.                                        |
| **Status / lifecycle**      | Active → Under Repair → Retired                                                |

#### Entity: Material

| **Field**                   | **Value**                                                        |
|-----------------------------|------------------------------------------------------------------|
| **Purpose**                 | Represents an operational supply item used by hotel departments. |
| **Key business attributes** | Material code, material name, unit, stock threshold              |
| **Business identity**       | Material code uniquely identifies a Material.                    |
| **Status / lifecycle**      | Active → Inactive                                                |

#### Entity: Store

| **Field**                   | **Value**                                                                     |
|-----------------------------|-------------------------------------------------------------------------------|
| **Purpose**                 | Represents an internal storage location used to manage operational materials. |
| **Key business attributes** | Branch, store code, status                                                    |
| **Business identity**       | Store code is unique within its Branch.                                       |
| **Status / lifecycle**      | Active → Inactive                                                             |

#### Entity: Stock Transaction

| **Field**                   | **Value**                                                                                                     |
|-----------------------------|---------------------------------------------------------------------------------------------------------------|
| **Purpose**                 | Represents a posted inventory quantity movement.                                                              |
| **Key business attributes** | Store, Material, signed quantity, transaction type, reference                                                 |
| **Business identity**       | Posting number or idempotency reference uniquely identifies the transaction event.                            |
| **Status / lifecycle**      | Draft → Posted. A posted transaction is corrected through an explicit reversal rather than being overwritten. |

#### Entity: Material Request

| **Field**                   | **Value**                                                                       |
|-----------------------------|---------------------------------------------------------------------------------|
| **Purpose**                 | Represents a department's request for operational materials.                    |
| **Key business attributes** | Requester, department, purpose, status                                          |
| **Business identity**       | Request number uniquely identifies a Material Request.                          |
| **Status / lifecycle**      | Draft → Submitted → Approved / Rejected → Partially Issued / Issued / Cancelled |

#### Entity: Request Line

| **Field**                   | **Value**                                                                                   |
|-----------------------------|---------------------------------------------------------------------------------------------|
| **Purpose**                 | Represents an individual Material and its quantities within a Material Request.             |
| **Key business attributes** | Material, requested quantity, approved quantity, issued quantity                            |
| **Business identity**       | Identified within a Material Request by its request and line number.                        |
| **Status / lifecycle**      | Quantities are accumulated and updated according to approval and material issue activities. |

#### Entity: Replenishment Request

| **Field**                   | **Value**                                                                                  |
|-----------------------------|--------------------------------------------------------------------------------------------|
| **Purpose**                 | Represents a lightweight internal requirement to replenish insufficient operational stock. |
| **Key business attributes** | Store, Material, replenishment need, source note, received quantity                        |
| **Business identity**       | Request number uniquely identifies a Replenishment Request.                                |
| **Status / lifecycle**      | Proposed → Approved → Partially Received → Received / Cancelled                            |

#### Entity: Operational Task

| **Field**                   | **Value**                                                 |
|-----------------------------|-----------------------------------------------------------|
| **Purpose**                 | Represents assigned ad-hoc or recurring operational work. |
| **Key business attributes** | Branch, owner/assignee, priority, due time, evidence      |
| **Business identity**       | Task number uniquely identifies an Operational Task.      |
| **Status / lifecycle**      | Open → In Progress → Verification → Closed / Reopened     |

#### Entity: Work-Time Entry

| **Field**                   | **Value**                                                          |
|-----------------------------|--------------------------------------------------------------------|
| **Purpose**                 | Represents an operational work interval recorded for a User.       |
| **Key business attributes** | User, work date, shift context, start time, end time, related task |
| **Business identity**       | Entry number uniquely identifies a Work-Time Entry.                |
| **Status / lifecycle**      | Open → Submitted → Reviewed / Returned                             |

#### Entity: Internal Request

| **Field**                   | **Value**                                                                     |
|-----------------------------|-------------------------------------------------------------------------------|
| **Purpose**                 | Represents a non-stock operational service request between hotel departments. |
| **Key business attributes** | Source department, destination department, responsible owner, due time        |
| **Business identity**       | Request number uniquely identifies an Internal Request.                       |
| **Status / lifecycle**      | Submitted → Accepted → In Progress → Confirmed / Returned                     |

#### Entity: Notification

| **Field**                   | **Value**                                                                 |
|-----------------------------|---------------------------------------------------------------------------|
| **Purpose**                 | Represents an in-system notification generated from an operational event. |
| **Key business attributes** | Recipient, linked record, notification type, read time                    |
| **Business identity**       | A system-generated notification reference identifies each Notification.   |
| **Status / lifecycle**      | Unread → Read                                                             |

#### Entity: Audit Event

| **Field**                   | **Value**                                                                               |
|-----------------------------|-----------------------------------------------------------------------------------------|
| **Purpose**                 | Represents the immutable history of significant actions and data changes in the system. |
| **Key business attributes** | Actor, event time, affected object, old value, new value, action                        |
| **Business identity**       | A system-generated event reference identifies each Audit Event.                         |
| **Status / lifecycle**      | Append-only; existing Audit Events are not overwritten.                                 |

### 3.3 Data Business Rules

> **REVIEW REQUIRED — annotation, not original SRS:** Some existing rules on Department Head scope and room status calculation may conflict with the pending role clarifications. Pay special attention to GBR-041 and GBR-043/GBR-044 in the later Business Rules appendix. Do not alter or implement those semantics without approval.


| **Rule**                                                    | **Description**                                                                                                                                                    | **Enforced On**                                     |
|-------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| Unique room number within branch                            | No two Rooms within the same Branch may have the same Room Number.                                                                                                 | Room creation and room information update           |
| Ready room requires valid inspection                        | A Room may be marked Ready only when it has a current passed Inspection and no open blocking Incident.                                                             | Room readiness update                               |
| Cleaning completion does not automatically make room Ready  | Completing a Housekeeping Task changes the task to Awaiting Inspection. The Room must not automatically become Ready.                                              | Cleaning completion submission                      |
| Failed inspection requires rework                           | A failed Inspection must record the failed checklist items and return the related Housekeeping Task for correction and re-inspection.                              | Inspection decision                                 |
| Stock issue cannot exceed available quantity                | Material issue quantity must not exceed the currently available stock. Stock balance and the corresponding ledger posting must be committed consistently.          | Stock issue posting                                 |
| Material request quantities remain separate                 | Requested Quantity, Approved Quantity, and Issued Quantity must be stored and tracked separately for each Request Line.                                            | Material request approval and issue                 |
| Posted stock transactions are not overwritten               | A posted Stock Transaction requiring correction must be handled through an explicit reversal or correction transaction rather than modifying the original posting. | Stock transaction correction                        |
| Access is restricted by role and operational scope          | A User may only access records permitted by their assigned Role, Branch, and Department scope.                                                                     | Every protected read and write operation            |
| Maintenance closure requires verification                   | A repair may only be completed and its Incident closed after the maintenance result has been documented and verified by the authorized reviewer.                   | Maintenance completion and incident closure         |
| Inactive records remain historically traceable              | Deactivated Users and inactive master records must remain available when referenced by historical operational records.                                             | User or master-data deactivation                    |
| Duplicate submission must not create duplicate transactions | Repeating the same transactional request must not create a second stock posting, approval event, or equivalent business transaction.                               | Transactional create, approval, and posting actions |
| Work-time is operational only                               | Work-Time Entry data is used for operational monitoring and workload visibility only. It must not calculate salary, leave, benefits, or biometric attendance.      | Work-time recording and reporting                   |

## 4. User Requirements

### 4.1 Actors

> **REVIEW REQUIRED — annotation, not original SRS:** The actor list below predates the newly proposed **Reception** role. The existing **Department Head** description may suggest a single-department manager; the clarified intended scope is to manage departments within one hotel branch/facility. Actor definitions need revision.


<table>
<colgroup>
<col style="width: 6%" />
<col style="width: 18%" />
<col style="width: 75%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>#</strong></th>
<th><strong>Actor</strong></th>
<th><strong>Description</strong></th>
</tr>
<tr class="odd">
<th><blockquote>
<p>1</p>
</blockquote></th>
<th>Administrator</th>
<th>Quản trị viên hệ thống. Chịu trách nhiệm thiết lập dữ liệu gốc của khách sạn (hotel master data), quản lý tài khoản/quyền truy cập, và thiết lập cấu hình kho. Có quyền tra cứu và nhận các báo cáo nhật ký thay đổi của hệ thống (audit logs).</th>
</tr>
<tr class="header">
<th><blockquote>
<p>2</p>
</blockquote></th>
<th>Operations Manager</th>
<th>Quản lý vận hành. Xử lý các dữ liệu quản lý phòng, quản lý tài sản, điều chỉnh hoặc hủy giao dịch kho, và giải quyết các sự cố leo thang. Thiết lập các tiêu chí giám sát, yêu cầu báo cáo và tiếp nhận các dashboard/báo cáo về tình trạng sẵn sàng của toàn hệ thống.</th>
</tr>
<tr class="odd">
<th><blockquote>
<p>3</p>
</blockquote></th>
<th>Housekeeping Supervisor</th>
<th>Giám sát buồng phòng. Phụ trách quản lý, phân công công việc vệ sinh, và thiết lập dữ liệu/checklist kiểm tra phòng. Tra cứu lịch sử luân chuyển phòng, nhận các cảnh báo liên quan đến vệ sinh/kiểm tra và theo dõi tình trạng sẵn sàng của phòng.</th>
</tr>
<tr class="header">
<th><blockquote>
<p>4</p>
</blockquote></th>
<th>Housekeeping Staff</th>
<th>Nhân viên dọn phòng. Nhận các thông báo phân công công việc và thông tin lỗi sau khi giám sát kiểm tra. Gửi lại hệ thống dữ liệu thực hiện công việc vệ sinh và kết quả dọn lại (rework) nếu có.</th>
</tr>
<tr class="odd">
<th><blockquote>
<p>5</p>
</blockquote></th>
<th>Maintenance Lead</th>
<th>Trưởng nhóm kỹ thuật/bảo trì. Chịu trách nhiệm quản lý dữ liệu sự cố, thiết lập lịch bảo trì phòng ngừa định kỳ và tra cứu lịch sử bảo trì tài sản. Nhận các cảnh báo khi có sự cố nghiêm trọng (critical incident) xảy ra.</th>
</tr>
<tr class="header">
<th><blockquote>
<p>6</p>
</blockquote></th>
<th>Maintenance Staff</th>
<th>Nhân viên kỹ thuật/bảo trì. Nhận thông báo công việc, thông tin phân công sửa chữa và lịch bảo trì định kỳ từ hệ thống. Trực tiếp thực hiện và cập nhật dữ liệu tiến độ/kết quả sửa chữa.</th>
</tr>
<tr class="odd">
<th><blockquote>
<p>7</p>
</blockquote></th>
<th>Storekeeper</th>
<th>Thủ kho. Quản lý dữ liệu gốc vật tư, ghi nhận các giao dịch xuất/nhập/tồn kho, cấp phát vật tư thực tế và tạo yêu cầu bổ sung kho. Nhận các cảnh báo tồn kho thấp, danh sách yêu cầu vật tư đã được duyệt và thông tin biến động số dư kho.</th>
</tr>
<tr class="header">
<th><blockquote>
<p>8</p>
</blockquote></th>
<th>Department Head</th>
<th>Trưởng bộ phận. Quản lý các công việc vận hành, phê duyệt yêu cầu cấp phát vật tư, quản lý yêu cầu nội bộ, bàn giao tài sản và tra cứu khối lượng công việc của nhân sự. Nhận thông báo chờ phê duyệt, cảnh báo công việc quá hạn và thông tin chi tiết về khối lượng công việc.</th>
</tr>
<tr class="odd">
<th><blockquote>
<p>9</p>
</blockquote></th>
<th>Owner</th>
<th>Chủ sở hữu/Ban giám đốc. Yêu cầu hệ thống trích xuất các dashboard và báo cáo. Nhận và theo dõi các dashboard vận hành, báo cáo phân tích tổng hợp cùng các thông báo quản lý cấp cao.</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.2 Use Cases (UC)

| **ID** | **UC Name**                                  | **Feature**   | **Description**                                                                                                                                             |
|--------|----------------------------------------------|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| UC-001 | Login                                        | FE-01         | Logs in using a provisioned account, verifies credentials, and checks account status.                                                                       |
| UC-002 | Logout                                       | FE-01         | Logs out and terminates the current session.                                                                                                                |
| UC-003 | Change Password                              | FE-01         | Changes personal password after verifying credentials according to security policies.                                                                       |
| UC-004 | Create User Account                          | FE-01         | Creates an internal staff account, declaring basic info and initial status.                                                                                 |
| UC-005 | Update User Account                          | FE-01         | Updates user account info and management attributes.                                                                                                        |
| UC-006 | Activate/Deactivate User Account             | FE-01         | Activates or deactivates an account; blocks access upon deactivation while preserving operation history.                                                    |
| UC-007 | Assign User to Branch/Department             | FE-01         | Assigns an account to a branch, department, and scope of responsibility, and adjusts it when staff are transferred.                                         |
| UC-008 | Create/Update User Role                      | FE-01         | Declares and updates system roles based on operational responsibilities.                                                                                    |
| UC-009 | Configure Role Permissions                   | FE-01         | Configures view, create, update, approve, and specific processing permissions by Feature or functional group.                                               |
| UC-010 | Assign/Revoke User Roles                     | FE-01         | Assigns or revokes user roles, verifying the scope of permissions after changes.                                                                            |
| UC-011 | Review User Access Permissions               | FE-01         | Looks up effective user permissions, including roles, branches, departments, and access limits.                                                             |
| UC-012 | Update Branch Information                    | FE-02         | Updates hotel branch information, including name, address, contact details, and operational status.                                                         |
| UC-013 | View Branch Structure                        | FE-02         | Views the organizational structure, including lists of floors, areas, rooms, and departments by branch.                                                     |
| UC-014 | Create Floor/Area                            | FE-02         | Creates a new floor or area within a branch, declaring its code, name, and location.                                                                        |
| UC-015 | Update Floor/Area                            | FE-02         | Edits floor, area information, and related descriptive details.                                                                                             |
| UC-016 | Deactivate Floor/Area                        | FE-02         | Deactivates a floor or area; checks for rooms and related tasks before deactivation.                                                                        |
| UC-017 | Create Room Type                             | FE-02         | Declares room types and classification attributes for operational management.                                                                               |
| UC-018 | Update Room Type                             | FE-02         | Updates the code, name, description, and management attributes of a room type.                                                                              |
| UC-019 | Deactivate Room Type                         | FE-02         | Deactivates a room type while retaining historical data.                                                                                                    |
| UC-020 | Create Room                                  | FE-02         | Creates a room profile, declaring room number, room type, floor, and managing branch.                                                                       |
| UC-021 | Update Room Information                      | FE-02         | Updates room type, area, and operational attributes of a room.                                                                                              |
| UC-022 | Deactivate Room                              | FE-02         | Deactivates room operational management after checking and resolving open tasks.                                                                            |
| UC-023 | View Room Directory                          | FE-02         | Searches, looks up, and filters rooms by branch, floor, room type, or status.                                                                               |
| UC-024 | Create Department                            | FE-02         | Declares internal operational departments, linking them to a branch and management scope.                                                                   |
| UC-025 | Update Department                            | FE-02         | Updates department information and its scope of responsibility.                                                                                             |
| UC-026 | Deactivate Department                        | FE-02         | Deactivates a department after verifying related accounts, tasks, and open requests.                                                                        |
| UC-027 | Manage Operational Room Status               | FE-03         | Monitors and processes room status based on cleaning progress, inspection results, and open incidents.                                                      |
| UC-028 | Create Cleaning Task                         | FE-04         | Creates a cleaning task for a room, specifying the cleaning type, priority level, requirements, and due date.                                               |
| UC-029 | Assign Cleaning Task                         | FE-04         | Assigns tasks based on branch, assigned area, and current workload.                                                                                         |
| UC-030 | Reassign Cleaning Task                       | FE-04         | Reassigns the task when the assigned staff cannot continue or requires reallocation.                                                                        |
| UC-031 | Start Cleaning Task                          | FE-04         | Receives, starts the cleaning task, and records the actual start time.                                                                                      |
| UC-032 | Update Cleaning Progress                     | FE-04         | Updates progress, notes, execution time, and arising issues.                                                                                                |
| UC-033 | Report Cleaning Blocker                      | FE-04         | Records inability to complete cleaning due to broken equipment, lack of supplies, or unsuitable room conditions.                                            |
| UC-034 | Complete Cleaning Task                       | FE-04         | Confirms cleaning completion, records end time, attaches evidence if any, and changes room status to pending inspection.                                    |
| UC-035 | Receive Rework Task                          | FE-04         | Receives substandard cleaning items returned from the quality inspection process.                                                                           |
| UC-036 | Complete Cleaning Rework                     | FE-04         | Completes items requiring rework and submits the room for reinspection.                                                                                     |
| UC-037 | Monitor Cleaning Tasks                       | FE-04         | Monitors unassigned, in-progress, overdue, pending inspection, or rework tasks.                                                                             |
| UC-038 | View Room Turnover History                   | FE-04         | Looks up cleaning history, assigned staff, time, inspection results, and rework count for each room.                                                        |
| UC-039 | Create Room Inspection Checklist             | FE-05         | Creates a room quality inspection checklist template, including criteria, items, and passing conditions.                                                    |
| UC-040 | Update Room Inspection Checklist             | FE-05         | Adjusts inspection criteria and manages checklist versions, preserving data of completed inspections.                                                       |
| UC-041 | Assign Room Inspection                       | FE-05         | Assigns an inspector for a room that has completed cleaning and is pending inspection.                                                                      |
| UC-042 | Perform Room Inspection                      | FE-05         | Inspects according to the applied checklist and records pass or fail results for each criterion.                                                            |
| UC-043 | Record Inspection Defects                    | FE-05         | Records failed items, describes defects, and attaches images and related evidence.                                                                          |
| UC-044 | Approve Room Inspection                      | FE-05         | Confirms the room meets requirements and checks blocking conditions before transitioning to Ready status.                                                   |
| UC-045 | Reject Room Inspection                       | FE-05         | Confirms the room fails inspection, records the reason, and lists items requiring rework.                                                                   |
| UC-046 | Request Cleaning Rework                      | FE-05         | Returns substandard work to cleaning staff or reassigns it, along with a list of defects to fix.                                                            |
| UC-047 | Reinspect Room                               | FE-05         | Reinspects reworked items and confirms mandatory conditions before final conclusion.                                                                        |
| UC-048 | View Room Inspection History                 | FE-05         | Looks up inspection history, results, failure count, associated staff, and evidence.                                                                        |
| UC-049 | Report Incident                              | FE-06         | Creates an incident report, specifying the branch, room/area, or related asset, describing the issue and discovery time.                                    |
| UC-050 | Classify Incident                            | FE-06         | Classifies incidents by technical group, severity, operational impact, and processing requirements.                                                         |
| UC-051 | Attach Incident Evidence                     | FE-06         | Attaches images, documents, or notes to clarify the incident status.                                                                                        |
| UC-052 | Triage Incident                              | FE-06         | Triages incidents, determines priority level, response/resolution deadlines, and initial action plan.                                                       |
| UC-053 | Assign Maintenance Work                      | FE-06         | Assigns a technician, specifies processing responsibilities, and sets the expected completion deadline.                                                     |
| UC-054 | Accept and Start Maintenance                 | FE-06         | Receives maintenance work and records the actual start time for processing.                                                                                 |
| UC-055 | Update Maintenance Progress                  | FE-06         | Updates progress, technical inspection results, repair actions, used materials, and arising issues.                                                         |
| UC-056 | Block Room Due to Incident                   | FE-06         | Flags incidents that affect room usability, preventing the room from transitioning to Ready until resolved.                                                 |
| UC-057 | Escalate Critical Incident                   | FE-06         | Escalates critical or potentially overdue incidents to management for resource reallocation.                                                                |
| UC-058 | Resolve Maintenance Incident                 | FE-06         | Records repair results, actual completion time, and evidence, then submits for verification.                                                                |
| UC-059 | Verify and Close Incident                    | FE-06         | Verifies repair results, confirms completion, closes the ticket, and updates room blocking conditions if applicable.                                        |
| UC-060 | Reopen Incident                              | FE-06         | Reopens an incident when repair results are unsatisfactory or the defect recurs, preserving previous handling history.                                      |
| UC-061 | Create Preventive Maintenance Schedule       | FE-06         | Creates a preventive or periodic maintenance schedule for equipment, assets, rooms, or areas.                                                               |
| UC-062 | Execute Scheduled Maintenance                | FE-06         | Executes tasks from the maintenance schedule, records results, and updates the next maintenance due date.                                                   |
| UC-063 | Manage Work Categories                       | FE-06 + FE-11 | Manages task category catalogs such as cleaning, room inspection, maintenance, and operational support.                                                     |
| UC-064 | Manage Operational Reference Data            | FE-06 + FE-11 | Manages shared reference data such as priority levels, status groups, and interruption reasons.                                                             |
| UC-065 | Register Asset/Equipment                     | FE-07         | Creates an asset profile, declaring code, name, type, identification details, and managing branch.                                                          |
| UC-066 | Update Asset Information                     | FE-07         | Edits identification information, description, and management details of an asset or equipment.                                                             |
| UC-067 | Assign Asset to Location                     | FE-07         | Assigns an asset to a branch, room, area, or department currently using it.                                                                                 |
| UC-068 | Transfer Asset Location                      | FE-07         | Records asset movement between rooms, areas, departments, or branches; logs previous and new locations.                                                     |
| UC-069 | Record Asset Handover                        | FE-07         | Confirms asset handover between individuals or departments, recording giver, receiver, time, and condition.                                                 |
| UC-070 | Update Asset Condition                       | FE-07         | Updates asset conditions such as active, damaged, under repair, or retired.                                                                                 |
| UC-071 | View Asset Profile                           | FE-07         | Looks up profile, current location, usage status, and responsible department of assets based on access rights.                                              |
| UC-072 | Review Asset Maintenance History             | FE-07         | Looks up incidents, repair count, results, and periodic maintenance history for each asset.                                                                 |
| UC-073 | Review Assets by Location                    | FE-07         | Generates asset statistics by branch, room, area, department, type, or usage status.                                                                        |
| UC-074 | Retire/Deactivate Asset                      | FE-07         | Retires asset operational management after verifying open tasks, handovers, and active profiles.                                                            |
| UC-075 | Manage Warehouses                            | FE-08         | Declares, updates, and deactivates warehouses; defines managing branch, responsible department, and operational status.                                     |
| UC-076 | Manage Inventory Items                       | FE-08         | Declares and updates material catalogs, material codes, types, descriptions, and inventory management info.                                                 |
| UC-077 | Manage Units of Measurement                  | FE-08         | Manages units of measurement and conversion rules when materials are managed in multiple units.                                                             |
| UC-078 | Configure Minimum Stock Levels               | FE-08         | Configures minimum stock thresholds by material and warehouse to trigger alerts and replenishment.                                                          |
| UC-079 | Create Stock Receipt                         | FE-08         | Creates a stock receipt, specifying receipt source, receiving warehouse, materials, quantities, and related documents.                                      |
| UC-080 | Verify and Post Stock Receipt                | FE-08         | Verifies actual received quantities, confirms the receipt, and updates to increase stock.                                                                   |
| UC-081 | Issue Stock                                  | FE-08         | Creates and confirms a stock issue note for valid requests, recording materials, quantities, departments, and related work.                                 |
| UC-082 | Return Materials to Stock                    | FE-08         | Receives excess or returned materials, verifies quantities, and records stock return.                                                                       |
| UC-083 | Create Stock Transfer                        | FE-08         | Creates a stock transfer note for materials between warehouses within the same branch or across branches.                                                   |
| UC-084 | Confirm Stock Transfer Receipt               | FE-08         | Verifies and confirms actual received quantities at the destination warehouse, completes the transfer, and logs discrepancies.                              |
| UC-085 | Conduct Stocktake                            | FE-08         | Initiates a stocktake and records the actual counted quantities of materials at the designated warehouse.                                                   |
| UC-086 | Reconcile Stocktake Discrepancies            | FE-08         | Reconciles system stock with actual counts, recording discrepancy levels and reasons.                                                                       |
| UC-087 | Adjust Stock Balance                         | FE-08         | Reviews and confirms stock adjustments based on stocktake results or valid adjustment transactions.                                                         |
| UC-088 | Reverse Stock Transaction                    | FE-08         | Executes a reverse transaction for a posted document that needs correction, keeping the original document and history intact.                               |
| UC-089 | View Stock Balance and Movement Ledger       | FE-08         | Looks up current stock, receipt/issue history, returns, transfers, and adjustments by warehouse, material, and time frame.                                  |
| UC-090 | Return Unused Materials                      | FE-08         | Requests the return of unused or excess materials, sending them to the warehouse for receipt confirmation.                                                  |
| UC-091 | Create Material Request                      | FE-09         | Creates a material request, declaring materials, quantities, purpose, department, and required time.                                                        |
| UC-092 | Link Material Request to Work                | FE-09         | Links material requests to corresponding cleaning, incident, maintenance, or operational tasks.                                                             |
| UC-093 | Update/Cancel Material Request               | FE-09         | Edits or cancels unprocessed requests, preserving history once the ticket enters the approval workflow.                                                     |
| UC-094 | Review Material Request                      | FE-09         | Reviews material needs, evaluates usage purpose, and verifies requested quantities.                                                                         |
| UC-095 | Approve/Reject Material Request              | FE-09         | Approves, adjusts approved quantities, or rejects the request with a stated reason.                                                                         |
| UC-096 | Check Material Availability                  | FE-09         | Checks available quantities of approved materials and determines fulfillment capacity.                                                                      |
| UC-097 | Issue Approved Materials                     | FE-09         | Issues materials based on approved quantities and availability, utilizing the FE-06 stock issue workflow.                                                   |
| UC-098 | Manage Partial Material Fulfillment          | FE-09         | Records partial fulfillment quantities, remaining shortages, and pending fulfillment status.                                                                |
| UC-099 | Track Material Request History               | FE-09         | Looks up the entire lifecycle of a material request from creation, approval, fulfillment, and return, to completion or cancellation.                        |
| UC-100 | Create Replenishment Request                 | FE-10         | Creates a replenishment request when materials are insufficient or stock falls below the minimum threshold.                                                 |
| UC-101 | Track Material Replenishment                 | FE-10         | Tracks replenishment needs, pending quantities, received quantities, and ability to fulfill remaining requests.                                             |
| UC-102 | Create Operational Task                      | FE-11         | Creates an independent operational task, declaring content, task category, location, and expected output.                                                   |
| UC-103 | Set Task Priority and Deadline               | FE-11         | Sets priority levels, planned start times, completion deadlines, and expected processing duration.                                                          |
| UC-104 | Assign/Reassign Task                         | FE-11         | Assigns or reassigns the responsible person based on branch, department, and workload.                                                                      |
| UC-105 | Review and Verify Task Completion            | FE-11         | Reviews results, confirms completion, or returns the task for further processing.                                                                           |
| UC-106 | Reopen Assigned Task                         | FE-11         | Reopens failed tasks, specifies items to fix, and designates the responsible person.                                                                        |
| UC-107 | Accept Assigned Task                         | FE-11 + FE-12 | Receives assigned tasks, reviews requirements, and confirms execution responsibility.                                                                       |
| UC-108 | Start Assigned Task                          | FE-11 + FE-12 | Starts the task and records the actual start time.                                                                                                          |
| UC-109 | Update Task Progress                         | FE-11 + FE-12 | Updates progress, stage results, notes, and task evidence.                                                                                                  |
| UC-110 | Pause/Resume Task                            | FE-11 + FE-12 | Pauses or resumes the task, recording interruption time and reasons.                                                                                        |
| UC-111 | Record Actual Working Time                   | FE-11 + FE-12 | Records actual working time, including start, end, and interruption periods.                                                                                |
| UC-112 | Complete Assigned Task                       | FE-11 + FE-12 | Confirms task completion, updates results, execution time, and mandatory handover info.                                                                     |
| UC-113 | Review Task Workload and Time Logs           | FE-11 + FE-12 | Looks up tasks by employee, department, status, priority, deadline, and execution time.                                                                     |
| UC-114 | Create Internal Request                      | FE-13         | Creates an internal support request, specifying the sending department, request type, content, branch, priority, and desired deadline.                      |
| UC-115 | Route and Assign Internal Request            | FE-13         | Receives requests, identifies processors, or routes them to the responsible department.                                                                     |
| UC-116 | Accept/Reject Internal Request               | FE-13         | Accepts or rejects the request, logging the reason and response time.                                                                                       |
| UC-117 | Update Internal Request Progress             | FE-13         | Updates progress, notes, processing results, and issues affecting deadlines.                                                                                |
| UC-118 | Create Linked Operational Work               | FE-13         | Creates or links corresponding cleaning, repair, material fulfillment, or operational tasks.                                                                |
| UC-119 | Record Request Handover                      | FE-13         | Records request handover between staff or departments, including giver, receiver, time, and content.                                                        |
| UC-120 | Complete Internal Request                    | FE-13         | Confirms completion of related tasks, records results, and closes the request.                                                                              |
| UC-121 | Reopen Internal Request                      | FE-13         | Reopens requests when results are unsatisfactory, preserving previous processing and handover history.                                                      |
| UC-122 | Escalate Overdue Internal Request            | FE-13         | Processes overdue or at-risk requests, reallocating resources or adjusting priority levels.                                                                 |
| UC-123 | View Internal Request History                | FE-13         | Looks up request status, assignees, deadlines, processing history, and results based on granted permissions.                                                |
| UC-124 | Receive Assignment Notifications             | FE-14         | Receives notifications regarding new tasks, reassigned tasks, or important task-related changes.                                                            |
| UC-125 | Receive Approval Notifications               | FE-14         | Receives notifications when material requests, internal requests, or pending tasks are approved, rejected, or returned.                                     |
| UC-126 | Receive Inspection and Incident Alerts       | FE-14         | Receives alerts for failed room inspections, critical incidents, or incidents affecting room operational readiness.                                         |
| UC-127 | Receive Overdue Work Alerts                  | FE-14         | Receives alerts for overdue tasks, incidents, or internal requests.                                                                                         |
| UC-128 | Receive Low Stock Alerts                     | FE-14         | Receives alerts when materials drop below the declared minimum stock threshold.                                                                             |
| UC-129 | View Notification Center                     | FE-14         | Views, searches, and filters notifications by type, time, priority, and read status.                                                                        |
| UC-130 | Acknowledge Notifications                    | FE-14         | Marks notifications as read or acknowledged, and accesses related operational records.                                                                      |
| UC-131 | View Room Readiness Dashboard                | FE-14         | Monitors the count and percentage of rooms that are Ready, pending cleaning, cleaning in progress, pending inspection, failed, or blocked.                  |
| UC-132 | View Task Workload Dashboard                 | FE-14         | Generates task statistics by status, department, employee, priority, time, and overdue status.                                                              |
| UC-133 | View Incident and Maintenance Dashboard      | FE-14         | Monitors open incidents, severity, response/completion times, recurring incidents, and periodic maintenance.                                                |
| UC-134 | View Inventory and Material Dashboard        | FE-14         | Monitors inventory levels, materials below threshold, receipts/issues, fulfillment requests, and unfulfilled replenishment needs.                           |
| UC-135 | Generate and Export Operational Reports      | FE-15         | Generates and exports operational reports by time, branch, or department for rooms, tasks, incidents, maintenance, assets, and inventory.                   |
| UC-136 | View Audit Logs                              | FE-15         | Looks up the history of creations, updates, approvals, rejections, completions, and critical actions by user, time, and entity.                             |
| UC-137 | Inspect Record Change History                | FE-15         | Views detailed changes, the actor, time, action, entity, and before/after data if available.                                                                |
| UC-138 | Manage Operational Workday and Shift Records | FE-12         | Manages workday info, shift definitions, and assignees by department/branch; data is strictly for operational coordination, not payroll.                    |
| UC-139 | Record Workday Start and End                 | FE-12         | Records the actual start and end time of a workday/shift; distinguishes total shift time from task time logged in UC-098.                                   |
| UC-140 | Manage Operational Shift Handover            | FE-12         | Records shift handover details, including incomplete tasks, unready rooms, open incidents, and pending internal requests; requires receiver acknowledgment. |
| UC-141 | View User List                               | FE-01         | Search, view, and filter the list of user accounts by status, department, or role.                                                                          |
| UC-142 | View Room Types List                         | FE-02         | Search, look up, and filter the list of room types by code, name, or status.                                                                                |
| UC-143 | View Incidents List                          | FE-06         | Search, look up, and filter incident reports by department, location, severity, status, or time period.                                                     |
| UC-144 | View Maintenance List                        | FE-06         | Search, look up, and filter maintenance tasks (arising from incidents or scheduled preventive maintenance) by assignee, status, due date, or time period.   |

### 4.3 Use Case Diagrams

*In this section, you need to provide the UC diagram(s) to show the actor-UCs and UC-UC relationships like the sample below. You can have multiple UC diagrams for the system, each diagram is for one human actor or one workflow\]*

#### 4.3.1 UCs for Administrator

#### 4.3.2 UCs for Operations Manager

<img src="../assets/media/image56.png" style="width:6.28438in;height:5.13889in" />

#### 4.3.3 UCs for Housekeeping Supervisor

<img src="../assets/media/image59.png" style="width:6.28438in;height:8.93056in" />

#### 4.3.4 UCs for Housekeeping Staff

<img src="../assets/media/image58.png" style="width:6.28438in;height:9in" />

#### 4.3.5 UCs for Maintenance Lead

<img src="../assets/media/image55.png" style="width:6.28438in;height:6.34722in" />

#### 4.3.6 UCs for Maintenance Staff

#### <img src="../assets/media/image53.png" style="width:6.28438in;height:8.08333in" /> 

#### 4.3.7 UCs for Storekeeper

<img src="../assets/media/image57.png" style="width:6.28438in;height:8.73611in" />

#### 4.3.8 UCs for Department Head

<img src="../assets/media/image54.png" style="width:6.28438in;height:5.44444in" />

#### 4.3.9 UCs for Owner

<img src="../assets/media/image32.png" style="width:6.28438in;height:3.98611in" />

#### 4.3.10 UCs for User

<img src="../assets/media/image22.png" style="width:4.36458in;height:3.25in" />

### 4.4 Permission Matrix

> **NOT APPROVED — annotation, not original SRS:** The entire permission matrix below is a **draft**, not a final implementation authority. Reception is missing; Operations Manager permissions need reassessment; Department Head scope requires clarification. Reconcile the matrix against the final Use Cases and obtain explicit approval before implementing RBAC.


Full = unrestricted · R=Restricted = conditions apply (see footnotes) · No = denied

<table style="width:100%;">
<colgroup>
<col style="width: 15%" />
<col style="width: 13%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
<col style="width: 7%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Entity</strong></th>
<th><strong>Action</strong></th>
<th><strong>A</strong></th>
<th><strong>OM</strong></th>
<th><strong>O</strong></th>
<th><strong>HK Sup</strong></th>
<th><p><strong>HK</strong></p>
<p><strong>Staff</strong></p></th>
<th><strong>ML</strong></th>
<th><strong>MS</strong></th>
<th><strong>SK</strong></th>
<th><strong>DH</strong></th>
</tr>
<tr class="odd">
<th>User account</th>
<th>Create / Update / Deactivate</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="header">
<th>User account</th>
<th>Login / Logout / Change Password</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
</tr>
<tr class="odd">
<th>Role &amp; Permission</th>
<th>Configure / Assign</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="header">
<th>Hotel Master Data</th>
<th>Create / Edit / Deactivate</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Hotel Master Data</th>
<th>View Directory</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>R¹</th>
<th>Full</th>
<th>R¹</th>
<th>Full</th>
<th>Full</th>
</tr>
<tr class="header">
<th>Cleaning Task</th>
<th>Create / Assign</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Cleaning Task</th>
<th>Execute / Update Progress</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="header">
<th>Room Inspection</th>
<th>Perform / Approve / Reject</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Incident</th>
<th>Report / Attach Evidence</th>
<th>Full</th>
<th>Full</th>
<th>No</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
</tr>
<tr class="header">
<th>Incident</th>
<th>Triage / Assign / Close</th>
<th>No</th>
<th>R²</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Maintenance Task</th>
<th>Execute / Update Progress</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>Full</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="header">
<th>Asset</th>
<th>Register / Retire</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Asset</th>
<th>Handover / Update Condition</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
</tr>
<tr class="header">
<th>Inventory Catalog</th>
<th>Manage Items / Stock Levels</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Stock Transaction</th>
<th>Receipt / Issue / Transfer</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
</tr>
<tr class="header">
<th>Stock Transaction</th>
<th>Adjust / Reverse</th>
<th>No</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Material Request</th>
<th>Create / Submit</th>
<th>Full</th>
<th>Full</th>
<th>No</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
</tr>
<tr class="header">
<th>Material Request</th>
<th>Approve / Reject</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
</tr>
<tr class="odd">
<th>Material Request</th>
<th>Issue Approved Materials</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
<th>No</th>
</tr>
<tr class="header">
<th>Operational Task</th>
<th>Create / Assign / Verify</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
</tr>
<tr class="odd">
<th>Operational Task</th>
<th>Execute / Update</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="header">
<th>Internal Request</th>
<th>Create / Submit</th>
<th>Full</th>
<th>Full</th>
<th>No</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
<th>Full</th>
</tr>
<tr class="odd">
<th>Internal Request</th>
<th>Accept / Route / Close</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>Full</th>
</tr>
<tr class="header">
<th>Dashboard &amp; Report</th>
<th>View / Export</th>
<th>No</th>
<th>Full</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
<tr class="odd">
<th>Audit Log</th>
<th>View / Inspect</th>
<th>Full</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
<th>No</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

¹ Restricted: Can only view data within their assigned branch or department.

² Restricted: Can escalate or view, but primary triage is done by Maintenance Lead.

A: Admin

OM: Operation Manager  
O: Owner  
HK Sup: Housekeeping Supervisor  
HK Staff: Housekeeping Staff  
ML: Maintenance Lead  
MS:Maintenance Staff  
SK: Storekeeper

DH: Department Head

## 5. System Functionalities

### 5.1 Screen Inventory

#### a. Screen Flow

##### a.1. Screen Flow Administrator

<img src="../assets/media/image6.png" style="width:6.28438in;height:3.91667in" />

*Figure 5.1.1 Administrator Screen Flow Diagram*

##### a.2. Screen Flow Operations Manager 

<img src="../assets/media/image27.png" style="width:6.28438in;height:4.20833in" />

*Figure 5.1.2 Operations Manager Screen Flow Diagram*

##### a.3. Screen Flow Housekeeping Supervisor 

<img src="../assets/media/image23.png" style="width:6.28438in;height:3.52778in" />

*Figure 5.1.3 Housekeeping Supervisor Screen Flow Diagram*

##### a.4. Screen Flow Housekeeping Staff

<img src="../assets/media/image8.png" style="width:6.28438in;height:2.54167in" />

*Figure 5.1.4 Housekeeping Staff Screen Flow Diagram*

##### a.5. Screen Flow Maintenance Lead 

<img src="../assets/media/image39.png" style="width:6.28438in;height:3.45833in" />

*Figure 5.1.5 Maintenance Lead Screen Flow Diagram*

##### a.6. Screen Flow Maintenance Staff 

<img src="../assets/media/image26.png" style="width:6.28438in;height:2.79167in" />

*Figure 5.1.6 Maintenance Staff Screen Flow Diagram*

##### a.7. Screen Flow Storekeeper 

<img src="../assets/media/image41.png" style="width:6.28438in;height:4.08333in" />

*Figure 5.1.7 Storekeeper Screen Flow Diagram*

##### a.8. Screen Flow Department Head

<img src="../assets/media/image9.png" style="width:6.28438in;height:4in" />

*Figure 5.1.8 Department Head Screen Flow Diagram*

##### a.9. Screen Flow Owner 

<img src="../assets/media/image20.png" style="width:6.28438in;height:3.63889in" />

*Figure 5.1.9 Owner Screen Flow Diagram*

#### b. Screen List

| **\#** | **Screen Name**                        | **Feature**                                  | **Description**                                                                                                                                          |
|--------|----------------------------------------|----------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1      | Sign In                                | Authentication (FE-01)                       | Allows any user to log in with username/email and password and reach the role-specific dashboard.                                                        |
| 2      | Forgot Password                        | Authentication (FE-01)                       | Shows the instruction to contact the Administrator for a password reset (no self-service reset).                                                         |
| 3      | Change Password                        | Authentication (FE-01)                       | Allows users to change their own password, including the mandatory change after receiving a temporary password.                                          |
| 4      | Profile                                | Authentication (FE-01)                       | Displays the current user's account, roles and branch/department; provides Change Password and Logout.                                                   |
| 5      | Dashboard                              | Dashboard (FE-14)                            | Role-specific home page; for Operations Manager and Owner it shows room readiness, task workload, incident/maintenance and inventory indicators.         |
| 6      | Notification Center                    | Notification (FE-14)                         | Lists notifications and alerts; search and filter by type, time, priority and read status.                                                               |
| 7      | Notification Detail                    | Notification (FE-14)                         | Displays a notification, marks it as read/acknowledged and opens the related record.                                                                     |
| 8      | User List                              | User Management (FE-01)                      | Searches and filters user accounts; creates and activates/deactivates accounts.                                                                          |
| 9      | User Detail                            | User Management (FE-01)                      | Displays and updates an account, resets its password, assigns branch/department and roles, and shows effective permissions.                              |
| 10     | Role List                              | Access Control (FE-01)                       | Lists system roles and creates a new role.                                                                                                               |
| 11     | Role Detail                            | Access Control (FE-01)                       | Updates a role and configures its permissions by feature.                                                                                                |
| 12     | Permission Matrix                      | Access Control (FE-01)                       | Displays the permissions of all roles in one matrix (read-only overview).                                                                                |
| 13     | Branch List                            | Hotel Structure (FE-02)                      | Lists branches and creates a new branch.                                                                                                                 |
| 14     | Branch Detail                          | Hotel Structure (FE-02)                      | Displays, updates and deactivates a branch; shows its structure tree of floors, areas, rooms and departments.                                            |
| 15     | Floor/Area List                        | Hotel Structure (FE-02)                      | Lists floors and areas of a branch and creates a new floor/area.                                                                                         |
| 16     | Floor/Area Detail                      | Hotel Structure (FE-02)                      | Displays, updates and deactivates a floor/area.                                                                                                          |
| 17     | Room Type List                         | Hotel Structure (FE-02)                      | Lists room types of a branch and creates a new room type.                                                                                                |
| 18     | Room Type Detail                       | Hotel Structure (FE-02)                      | Displays, updates and deactivates a room type.                                                                                                           |
| 19     | Room List                              | Hotel Structure (FE-02)                      | Searches and filters rooms by branch, floor, area, room type and status; creates a new room.                                                             |
| 20     | Room Detail                            | Hotel Structure (FE-02)                      | Displays room information, current status reason and status history; updates, deactivates or reactivates the room.                                       |
| 21     | Department List                        | Hotel Structure (FE-02)                      | Lists departments of a branch and creates a new department.                                                                                              |
| 22     | Department Detail                      | Hotel Structure (FE-02)                      | Displays, updates and deactivates a department.                                                                                                          |
| 23     | Room Readiness Board                   | Room Operations (FE-03)                      | Displays rooms by floor with operational status (Ready, Pending Cleaning, Cleaning, Pending Inspection, Rework, Blocked) and counts; opens next actions. |
| 24     | Cleaning Task List                     | Housekeeping (FE-04)                         | Supervisor monitors tasks by status/overdue and cancels tasks; staff see their own cleaning tasks.                                                       |
| 25     | Create Cleaning Task                   | Housekeeping (FE-04)                         | Creates a cleaning task for a room with cleaning type, priority and due time.                                                                            |
| 26     | Cleaning Task Detail                   | Housekeeping (FE-04)                         | Displays a cleaning task; staff start it, update progress, report blockers and complete it.                                                              |
| 27     | Cleaning Assignment                    | Housekeeping (FE-04)                         | Assigns or reassigns a cleaning task based on area and current workload.                                                                                 |
| 28     | Rework Task List                       | Housekeeping (FE-04)                         | Lists rework tasks; staff see their own rework tasks.                                                                                                    |
| 29     | Rework Task Detail                     | Housekeeping (FE-04)                         | Displays defects to fix; staff accept, start and submit the rework with remediation notes.                                                               |
| 30     | Room Turnover History                  | Housekeeping (FE-04)                         | Displays cleaning cycles of a room with assignees, times, inspection results and rework count.                                                           |
| 31     | Inspection Checklist List              | Room Inspection (FE-05)                      | Lists inspection checklists and creates a new checklist.                                                                                                 |
| 32     | Inspection Checklist Detail            | Room Inspection (FE-05)                      | Edits criteria, manages versions, publishes or retires a checklist.                                                                                      |
| 33     | Inspection Queue                       | Room Inspection (FE-05)                      | Lists rooms pending inspection/reinspection and failed inspections without rework; assigns the inspector.                                                |
| 34     | Room Inspection                        | Room Inspection (FE-05)                      | Records Pass/Fail for each checklist criterion and defects with photos.                                                                                  |
| 35     | Inspection Result                      | Room Inspection (FE-05)                      | Displays the inspection summary and approves or rejects the room.                                                                                        |
| 36     | Request Rework                         | Room Inspection (FE-05)                      | Creates a rework task from a failed inspection with defects, assignee and due time.                                                                      |
| 37     | Reinspection                           | Room Inspection (FE-05)                      | Records Fixed/Not Fixed for each defect of a submitted rework and concludes.                                                                             |
| 38     | Inspection History                     | Room Inspection (FE-05)                      | Searches inspections with results, defects, inspectors, housekeepers and checklist version.                                                              |
| 39     | Incident List                          | Incident & Maintenance (FE-06)               | Lists incidents by status, severity, location and deadline.                                                                                              |
| 40     | Report Incident                        | Incident & Maintenance (FE-06)               | Creates an incident with location/asset, description and evidence; shows similar open incidents.                                                         |
| 41     | Incident Detail                        | Incident & Maintenance (FE-06)               | Classifies, triages, blocks room, escalates, verifies/closes or reopens an incident; adds evidence.                                                      |
| 42     | Maintenance Assignment                 | Incident & Maintenance (FE-06)               | Assigns a technician and deadline to an incident.                                                                                                        |
| 43     | Maintenance Task List                  | Incident & Maintenance (FE-06)               | Lists maintenance work from incidents and schedules; technicians see their own tasks.                                                                    |
| 44     | Maintenance Task Detail                | Incident & Maintenance (FE-06)               | Technician accepts, starts, updates progress and materials, and resolves the work.                                                                       |
| 45     | Preventive Maintenance Schedule List   | Incident & Maintenance (FE-06)               | Lists preventive maintenance schedules and creates a new schedule.                                                                                       |
| 46     | Preventive Maintenance Schedule Detail | Incident & Maintenance (FE-06)               | Updates a schedule and shows its executions and next due date.                                                                                           |
| 47     | Reference Data                         | Incident & Maintenance (FE-06), Task (FE-11) | Manages work categories, priority levels, status groups and interruption reasons.                                                                        |
| 48     | Asset List                             | Asset (FE-07)                                | Searches assets by branch, location, type and status; registers assets; shows statistics by location.                                                    |
| 49     | Asset Detail                           | Asset (FE-07)                                | Displays and updates an asset; assigns/transfers location, records handover, updates condition, retires it.                                              |
| 50     | Asset Maintenance History              | Asset (FE-07)                                | Displays incidents, repairs and preventive maintenance history of an asset.                                                                              |
| 51     | Warehouse List                         | Inventory (FE-08)                            | Lists warehouses and creates a new warehouse.                                                                                                            |
| 52     | Warehouse Detail                       | Inventory (FE-08)                            | Updates and deactivates a warehouse.                                                                                                                     |
| 53     | Inventory Dashboard                    | Inventory (FE-08)                            | Displays stock levels and items below minimum stock for the storekeeper.                                                                                 |
| 54     | Item List                              | Inventory (FE-08)                            | Lists inventory items and creates a new item.                                                                                                            |
| 55     | Item Detail                            | Inventory (FE-08)                            | Updates an item, its unit conversions and minimum stock levels per warehouse.                                                                            |
| 56     | Unit of Measurement                    | Inventory (FE-08)                            | Manages units of measurement.                                                                                                                            |
| 57     | Stock Transaction List                 | Inventory (FE-08)                            | Lists receipts, issues, returns and transfers by type; creates a new transaction.                                                                        |
| 58     | Stock Transaction Detail               | Inventory (FE-08)                            | Verifies and posts a transaction, confirms transfer receipt, or reverses a posted transaction.                                                           |
| 59     | Stocktake List                         | Inventory (FE-08)                            | Lists stocktakes and starts a new stocktake.                                                                                                             |
| 60     | Stocktake Detail                       | Inventory (FE-08)                            | Records counted quantities, reconciles discrepancies and confirms stock adjustment.                                                                      |
| 61     | Stock Balance & Ledger                 | Inventory (FE-08)                            | Displays current stock and movement history by warehouse, item and period.                                                                               |
| 62     | Material Request List                  | Material Request (FE-09)                     | Lists my requests, requests pending my approval and approved requests to issue; creates a request.                                                       |
| 63     | Material Request Detail                | Material Request (FE-09)                     | Updates/cancels, links to work, reviews, approves/rejects, checks availability, issues (full/partial) and returns unused materials.                      |
| 64     | Replenishment List                     | Replenishment (FE-10)                        | Lists replenishment requests and creates a new one.                                                                                                      |
| 65     | Replenishment Detail                   | Replenishment (FE-10)                        | Tracks requested, pending and received quantities of a replenishment request.                                                                            |
| 66     | Task List                              | Task Assignment (FE-11)                      | Lists operational tasks (department or my tasks); creates a new task.                                                                                    |
| 67     | Task Detail                            | Task Assignment (FE-11)                      | Sets priority/deadline, accepts, starts, pauses/resumes, updates, completes, verifies or reopens a task; logs working time.                              |
| 68     | Task Assignment                        | Task Assignment (FE-11)                      | Assigns or reassigns a task based on department and workload.                                                                                            |
| 69     | Workload & Time Logs                   | Task Assignment (FE-11), Work-Time (FE-12)   | Displays tasks and time logs by employee, department, status and deadline.                                                                               |
| 70     | Overdue Work                           | Task Assignment (FE-11)                      | Lists overdue tasks, incidents and internal requests and opens the related record.                                                                       |
| 71     | Shift Management                       | Work-Time (FE-12)                            | Displays the workday calendar; creates and updates shifts and their assignees.                                                                           |
| 72     | Shift Detail                           | Work-Time (FE-12)                            | Displays shift time, assigned employees and status.                                                                                                      |
| 73     | Workday Record                         | Work-Time (FE-12)                            | Staff start/end their workday; Department Head views actual start/end times.                                                                             |
| 74     | Shift Handover List                    | Work-Time (FE-12)                            | Lists shift handovers and their acknowledgement status.                                                                                                  |
| 75     | Shift Handover Detail                  | Work-Time (FE-12)                            | Records pending tasks, unready rooms, open incidents and requests; receiver acknowledges.                                                                |
| 76     | Internal Request List                  | Internal Request (FE-13)                     | Lists internal requests in scope; creates a new request.                                                                                                 |
| 77     | Internal Request Detail                | Internal Request (FE-13)                     | Routes, accepts/rejects, updates progress, links work, hands over, completes, reopens or escalates a request.                                            |
| 78     | Operational Reports                    | Reporting (FE-15)                            | Generates and exports reports by type (room, housekeeping/inspection, incident/maintenance, asset, inventory, task), period, branch and department.      |
| 79     | Audit Log                              | Audit (FE-15)                                | Searches audit entries by period, actor, entity and action.                                                                                              |
| 80     | Audit Log Detail                       | Audit (FE-15)                                | Displays the actor, time, action and before/after values of a change.                                                                                    |

### 5.2 External API Inventory

*\[Fill details of each external API into the table with the columns and sample data as below\]*

| **\#** | **API Name**      | **Calling Path**       | **Description**                                                                                                |
|--------|-------------------|------------------------|----------------------------------------------------------------------------------------------------------------|
| 1      | Employee sync API | HRM System → TalentHub | allows an external HR management system to push employee records and automatically create Interviewer accounts |

### 5.3 Background Job Inventory

*\[Fill details of each background job into the table with the columns and sample data as below\]*

| **\#** | **Job Name**                                     | **Group** | **Description**  |
|--------|--------------------------------------------------|-----------|------------------|
| 1      | Check overdue and at-risk work                   | Scheduled | Every 5 minutes  |
| 2      | Check low stock                                  | Scheduled | Every 5 minutes  |
| 3      | Retry failed notifications                       | Scheduled | Every 5 minutes  |
| 4      | Generate preventive maintenance occurrences      | Scheduled | Daily at 0:30 AM |
| 5      | Generate recurring tasks                         | Scheduled | Daily at 0:45 AM |
| 6      | Clean up old notifications and audit log entries | Scheduled | Daily at 2:00 AM |

