> **Navigation:** [SRS overview](../SRS_README.md) · [Full converted SRS](../HotelOps_SRS_DRAFT.md) · [Pending revisions](../SRS_CHANGE_NOTES.md)

# IV. Non-Functional Requirements

## 4. Non-Functional Requirements

### 4.1 Interfaces

#### 4.1.1 User Interfaces

- The system shall provide a responsive web-based user interface for all authorized users.

- The website shall display correctly on commonly used modern browsers, including Google Chrome, Microsoft Edge, Mozilla Firefox, and Safari.

- The interface shall be usable on common desktop, laptop, and tablet screen sizes without broken layouts, overlapping components, or inaccessible functions.

- Navigation, buttons, forms, tables, status indicators, search fields, filters, and confirmation dialogs shall use a consistent layout throughout the application.

- The system shall display only menus, screens, data, and actions that the authenticated user is authorized to access.

- Operational lists shall provide appropriate search, filtering, sorting, and pagination functions where the amount of data is large.

- Important operational information such as room, task status, responsible person, priority, due time, inspection result, maintenance status, and stock status shall be clearly visible on relevant screens.

- Status values shall be displayed consistently across list, detail, dashboard, and report screens.

- Required fields shall be clearly marked and validated before data is submitted.

- Validation messages shall identify the invalid field and provide clear information so that the user can correct the input.

- The system shall display confirmation dialogs before important operations that can significantly affect operational data, such as cancellation, status completion, inventory adjustment, or deactivation.

- Success and failure messages shall be displayed immediately after a user performs an operation.

- The user interface shall use English as the primary language.

- Terminology displayed on the user interface shall remain consistent with the terminology defined in the project's use cases and business requirements.

- Date and time formats shall be displayed consistently throughout the system.

- The system shall preserve the user's search and filtering context when returning from a detail screen to a list whenever applicable.

#### 4.1.2 External Interfaces

- HotelOps shall primarily operate as an internal hotel operations web application.

- The current system baseline shall not require direct integration with guest-facing systems, online travel agencies, payment gateways, or supplier systems.

- Any communication between the web client and the application server shall use standard web protocols.

- Client-server communication in the deployed environment shall use HTTPS.

- External or infrastructure services, if configured for supporting functions such as notification delivery or file storage, shall not control the execution of the core hotel operational workflows.

- Failure of a supporting external service shall not cause unrelated core operations such as housekeeping task processing, maintenance updates, inventory recording, or internal request processing to fail.

- When a supporting service cannot be reached, the system shall display an appropriate message and record the failure for technical review where applicable.

- Authentication credentials, service keys, or configuration secrets related to infrastructure services shall not be exposed through the user interface.

### 4.2 Quality Attributes

#### 4.2.1 Usability

The system shall be designed for hotel operational users with different responsibilities and levels of technical experience. The interface shall therefore emphasize simplicity, consistency, and ease of operation.

- Users shall be able to understand and perform the main functions associated with their assigned role after a short period of system familiarization.

- Common operations shall require as few unnecessary navigation steps as possible.

- Screens serving similar purposes shall follow a consistent layout and interaction pattern.

- The system shall clearly differentiate between editable data, read-only information, available actions, and current status.

- Important actions shall be placed in visible and predictable positions on relevant screens.

- Error messages shall be written in understandable language and shall indicate how the user can resolve the problem.

- Users shall not be required to understand technical database identifiers or system implementation details to perform normal operations.

- The system shall provide appropriate empty-state messages when no data matches the selected filters.

- The system shall clearly indicate loading or processing states for operations that are not completed immediately.

- The system shall prevent repeated submission where a repeated click could create duplicate operational records.

**Role-specific usability requirements:**

- **Housekeeping Staff** shall be able to quickly identify assigned cleaning tasks, room information, priority, current status, and required work.

- **Housekeeping Supervisor** shall be able to monitor housekeeping work, assign tasks, review inspection information, and identify tasks requiring rework.

- **Maintenance Staff** shall be able to identify assigned maintenance work, related room or asset information, priority, and current work status.

- **Maintenance Lead** shall be able to review incidents, assign maintenance work, monitor progress, and verify maintenance results.

- **Storekeeper** shall be able to clearly distinguish stock receipt, issue, adjustment, current stock balance, and replenishment information.

- **Department Head** shall be able to identify pending departmental requests, material requests, operational tasks, and items requiring review.

- **Operations Manager** shall be able to view operational exceptions, room readiness information, overdue work, and cross-department operational status.

- **Owner** shall be able to access summarized cross-branch operational reports, dashboards, and audit information according to granted permissions.

- **Administrator** shall be able to manage system accounts, roles, permissions, hotel organizational structure, and authorized configuration through clearly separated administration functions.

#### 4.2.2 Reliability and Availability

- The system shall target an availability of at least **95%**, excluding planned maintenance periods.

- Core hotel operational data shall remain consistent when users create, update, approve, complete, or cancel operational records.

- A failure during a multi-step update shall not leave the related business data in a partially committed state.

- Duplicate submissions shall not create duplicate inventory transactions, task status transitions, approvals, or other critical business records.

- Failure of a non-critical supporting function shall not make the entire application unavailable.

- Unexpected application errors shall be logged to support troubleshooting.

- Technical error logs shall not expose passwords, authentication tokens, or other confidential credentials.

- The system shall preserve successfully committed business transactions if the user's connection is interrupted afterward.

- The system shall support regular backup of operational and configuration data.

- Backup data shall include information necessary to restore important operational records such as tasks, inspections, incidents, maintenance activities, inventory transactions, internal requests, user permissions, and audit history.

- Restoration procedures shall maintain relationships between related business records.

- Planned system maintenance should be performed outside periods of high hotel operational activity whenever possible.

#### 4.2.3 Performance

The system shall provide sufficient performance for daily hotel operations across the two hotel branches.

**Page and Data Response**

- Standard pages and operational lists should normally be displayed within 3 seconds under normal operating conditions.

- Standard create, update, assignment, approval, and status-change operations should normally return a result within 3 seconds after submission.

- Search, filter, sorting, and pagination operations should normally return results within 3 seconds for standard operational datasets.

- Dashboard screens should normally display their main operational information within 5 seconds.

- Standard operational reports should normally be generated within 5 seconds for commonly used filters and reporting periods.

- Reports that require aggregation of larger datasets may take longer, but the system shall provide an appropriate processing indicator if the user must wait for the result.

**Concurrent Operations**

- The system shall support concurrent access by users from both hotel branches.

- Concurrent operations shall not result in incorrect stock balances, duplicated status transitions, or overwritten operational decisions.

- When two users attempt to modify the same record, the system shall prevent an outdated update from silently overwriting a newer committed change.

- Performance of normal transactional operations shall not significantly degrade when another user generates a report or views a dashboard.

**Database Operations**

- Frequently searched information such as branch, room, department, status, assigned user, task date, incident status, asset, material, and transaction date should be appropriately optimized for retrieval.

- Large datasets shall use pagination or an equivalent data-loading approach instead of loading all available records at once.

- Database operations shall retrieve only the information required by the relevant function whenever technically feasible.

#### 4.2.4 Security

The system shall protect internal hotel operational data against unauthorized access, modification, disclosure, and loss.

**Authentication**

- Users shall authenticate before accessing protected system functions.

- Passwords shall never be stored in plain text.

- User passwords shall be stored using an appropriate secure one-way password hashing mechanism.

- Authentication credentials shall not be displayed or recorded in application logs.

- A user session shall become invalid after the user logs out.

- The system shall reject requests associated with invalid or expired authentication sessions.

**Role-Based Access Control**

- The system shall implement Role-Based Access Control (RBAC).

- Authorization shall follow the assigned role, branch, and department scope where applicable.

- The supported roles are:

  - Administrator

  - Operations Manager

  - Housekeeping Supervisor

  - Housekeeping Staff

  - Maintenance Lead

  - Maintenance Staff

  - Storekeeper

  - Department Head

  - Owner

- Users shall only be able to view or modify records that fall within their permitted access scope.

- Unauthorized functions shall not be displayed as available actions on the user interface.

- Hiding a function on the user interface shall not be considered sufficient authorization protection; the server shall also validate the user's permission before executing the request.

- A user who manually modifies a URL or request parameter shall not gain access to records outside their authorized scope.

- Branch-scoped users shall not be able to view or modify another branch's records unless explicitly granted cross-branch permission.

- Department-scoped users shall not be able to perform unauthorized actions on another department's operational records.

**Input and Data Protection**

- User input shall be validated before being stored or used in a business operation.

- Database access shall use safe parameterized operations or equivalent mechanisms to reduce SQL injection risks.

- Data displayed in web pages shall be handled appropriately to reduce Cross-Site Scripting risks.

- Uploaded files shall be validated according to configured file type and size restrictions.

- Unauthorized users shall not be able to access protected uploaded files by directly manipulating a file URL or identifier.

- Client-server communication shall use HTTPS in the deployed environment.

**Audit and Traceability**

- Important operational actions shall be recorded with sufficient information to identify who performed the action and when it occurred.

- Audit information should include the acting user, timestamp, action, affected record, and relevant status or value changes.

- Important events such as permission changes, task assignment, inspection decisions, maintenance closure, inventory adjustment, operational status changes, and record deactivation shall be traceable.

- Normal operational users shall not be permitted to modify or delete audit history.

- Deactivated users shall remain identifiable in historical operational and audit records.

#### 4.2.5 Safety and Data Protection

The system shall protect the integrity of operational information because incorrect or lost data may affect housekeeping, maintenance, room readiness, inventory, and internal hotel coordination.

- The system shall prevent incomplete or invalid operational transactions from being treated as successfully completed.

- Critical operations shall be validated before the resulting status or inventory information is committed.

- The system shall request confirmation before actions that may significantly change or deactivate important operational data.

- Inventory adjustments shall retain sufficient information to identify the reason, responsible user, time, and resulting stock change.

- Posted inventory transactions shall not be silently overwritten; corrections shall remain traceable.

- A completed housekeeping task shall retain its historical information for subsequent inspection and audit purposes.

- Inspection results shall remain associated with the corresponding housekeeping task and room.

- Maintenance activities shall remain traceable to the associated incident, room, and asset where applicable.

- Closing a maintenance incident shall not automatically change unrelated room operational information without the required business action.

- Operational task history shall be retained after completion to support reporting and accountability.

- Users shall receive a clear warning when an operation cannot be completed because another related record has changed or an applicable business condition has not been satisfied.

- System failures shall not cause successfully committed operational data to disappear or revert without an explicit recovery process.

- Backup and restoration procedures shall preserve business relationships and transaction consistency.

