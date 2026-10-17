> **Navigation:** [SRS overview](../SRS_README.md) · [Full converted SRS](../HotelOps_SRS_DRAFT.md) · [Pending revisions](../SRS_CHANGE_NOTES.md)

# II. Use Case Specifications

*\[Provide specifications for the use cases (UCs) that are covered in the system. The UCs are grouped by the system features and even sub features. **You just need to provide UC specifications for UCs involved in the main workflows (business processes) or the complex ones**. Other UCs (i.e CRUD or data-viewing UCs) are simple, and you just need to refer the descriptions in the Functional Requirement (part III) below)\]*

## 1. Authentication & Role Management (FE-01)

### 1.1 UC-001 Login

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-001 Login</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Owner, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user wants to access the functions of HotelOps.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows an internal user to sign in with a provisioned account so that the system can identify the user and apply the user's roles and scope.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> The user has an internal account created by the Administrator (UC-004).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><p><strong>POST-01:</strong> Success: an active session exists and the user sees only functions granted by the user's roles and scope.</p>
<p><strong>POST-02:</strong> Failure: no session is created; failed attempts and locks are recorded.</p></th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Login</strong></em></p>
<blockquote>
<p>1. User opens the Login page.</p>
<p>2. System displays the Login form (username or email, password) and the Forgot Password link.</p>
<p>3. User enters the username or email and the password and selects Login.</p>
<p>4. System checks that both fields are filled (GBR-003).</p>
<p>5. System finds the account and verifies the password (GBR-019).</p>
<p>6. System checks that the account is Active and not locked (GBR-020).</p>
<p>7. System resets the failed-attempt counter, creates a session and calculates the effective permissions from the user's roles and scope (GBR-021, GBR-002).</p>
<p>8. System records the login in the audit log (GBR-008).</p>
<p>9. System opens the user's home page (notifications and the functions allowed for the user's roles).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.1_Forgot Password</strong></em></p>
<p>User selects Forgot Password and enters a username or email.</p>
<ol type="1">
<li><p>System displays the same instruction for every input, without revealing whether the account exists (MSG14, GBR-024).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 7.1_Temporary Password</strong></em></p>
<p>The password used is a valid temporary password.</p>
<ol type="1">
<li><p>System creates a restricted session that only allows changing the password (GBR-023).</p></li>
<li><p>System opens the Change Password screen (UC-003); the flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Missing Information</strong></em></p>
<p>Username/email or password is empty.</p>
<ol type="1">
<li><p>System shows which field is required (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 5.1_Invalid Credentials</strong></em></p>
<p>The account does not exist, the password is wrong, or the account is Inactive.</p>
<ol type="1">
<li><p>If the account exists, System increases its failed-attempt counter (GBR-020).</p></li>
<li><p>System displays the same generic message for all three cases (MSG11, GBR-019).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 5.2_Lock Threshold Reached</strong></em></p>
<p>The failed attempt reaches SYS-PAR-01.</p>
<ol type="1">
<li><p>System sets locked_until = now + SYS-PAR-02 minutes (GBR-020).</p></li>
<li><p>System records the lock in the audit log (GBR-008).</p></li>
<li><p>System displays MSG11; the flow ends.</p></li>
</ol>
<p><em><strong>Step 6.1_Account Temporarily Locked</strong></em></p>
<p>The password is correct but locked_until is later than now.</p>
<ol type="1">
<li><p>System does not create a session and displays the unlock time (MSG12, GBR-020).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 7.2_Temporary Password Expired</strong></em></p>
<p>The temporary password is older than SYS-PAR-05 hours.</p>
<ol type="1">
<li><p>System does not create a session (MSG13, GBR-023).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Once or twice per user per shift.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-002, GBR-003, GBR-008, GBR-019, GBR-020, GBR-021, GBR-023, GBR-024</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.2 UC-002 Logout

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-002 Logout</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Owner, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A logged-in user wants to end the working session.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows a signed-in user to end the current session.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The session can no longer be used; any further request requires a new login.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Logout</strong></em></p>
<blockquote>
<p>1. User selects Logout from the user menu.</p>
<p>2. System revokes the current session (GBR-021).</p>
<p>3. System records the logout in the audit log (GBR-008).</p>
<p>4. System clears the client session and opens the Login page.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Session Already Expired</strong></em></p>
<p>The session is already expired or revoked.</p>
<ol type="1">
<li><p>System treats the logout as successful and opens the Login page.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Once or twice per user per shift.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-008, GBR-021</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.3 UC-003 Change Password

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-003 Change Password</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Owner, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user wants to change the password, or must change a temporary password after the first login or a reset.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows a user to change their own password, including the mandatory change after receiving a temporary password.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> For a forced change, the user is in a restricted session created from a temporary password (UC-001, Step 7.1).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The new password is effective; other sessions are revoked; the restricted session (if any) becomes a normal session.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Change Password</strong></em></p>
<blockquote>
<p>1. User opens Change Password.</p>
<p>2. System displays fields: current password, new password, confirm new password.</p>
<p>3. User enters the three fields and selects Save.</p>
<p>4. System verifies the current password (GBR-019).</p>
<p>5. System checks that the new password matches the confirmation (GBR-022).</p>
<p>6. System stores the new password hash, clears the temporary-password flag and revokes all other sessions of the user (GBR-021, GBR-023).</p>
<p>7. System records the change in the audit log without the password values (GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Wrong Current Password</strong></em></p>
<p>The current password is incorrect.</p>
<ol type="1">
<li><p>System displays MSG03 for the current password field.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 5.1_Policy Not Met</strong></em></p>
<p>The new password does not match the confirmation, violates the policy, or equals the current password.</p>
<ol type="1">
<li><p>System lists the unmet policy items (MSG03, GBR-022).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; at the first login and when a user changes the password.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-008, GBR-019, GBR-021, GBR-022, GBR-023</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.4 UC-004 Create User Account

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-004 Create User Account</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new employee needs an account to use HotelOps.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to create an internal account for a hotel employee and provision a temporary password.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The account exists; the employee must change the temporary password at first login (UC-001, UC-003).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create User Account</strong></em></p>
<blockquote>
<p>1. Administrator opens User Management and selects Create Account.</p>
<p>2. System displays the form: full name, username, email, phone, branch, department, role(s).</p>
<p>3. Administrator enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-003, GBR-025, GBR-031, GBR-004).</p>
<p>5. System creates the account as Active with the selected roles and scope (GBR-031).</p>
<p>6. System generates a random temporary password valid for SYS-PAR-05 hours (GBR-023).</p>
<p>7. System saves the account, role assignments and scope (without the password value) and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays the temporary password once so the Administrator can hand it to the employee (MSG15).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Username or Email</strong></em></p>
<p>The username or email already exists.</p>
<ol type="1">
<li><p>System shows the duplicated field (MSG35, GBR-025).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>Another field is invalid (e.g. the department does not belong to the branch).</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-023, GBR-025, GBR-031</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.5 UC-005 Update User Account

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-005 Update User Account</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An employee's account information changes or the employee has forgotten the password.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to update an account's personal information and to reset a forgotten password.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The account exists in the Administrator's scope.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Account information is updated; after a reset the employee must log in with the temporary password and change it.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update User Account</strong></em></p>
<blockquote>
<p>1. Administrator searches for and opens the account.</p>
<p>2. System displays the account details with the current row version.</p>
<p>3. Administrator edits full name, email or phone and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-003, GBR-025, GBR-005).</p>
<p>5. System saves the changes and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Reset Password</strong></em></p>
<p>The employee has forgotten the password and Administrator selects Reset Password after verifying the person.</p>
<ol type="1">
<li><p>System asks User to confirm the password reset of the account (MSG33).</p></li>
<li><p>System generates a new temporary password, marks the account for a forced change and revokes all sessions of the account (GBR-023, GBR-024).</p></li>
<li><p>System saves the reset event (without the password value) and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System displays the temporary password once (MSG15).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Email</strong></em></p>
<p>The new email is used by another account.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.2_Role or Status Change Requested</strong></em></p>
<p>Administrator tries to change roles or Active status here.</p>
<ol type="1">
<li><p>System informs that roles are changed in UC-010 and status in UC-006 (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-023, GBR-024, GBR-025</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.6 UC-006 Activate/Deactivate User Account

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-006 Activate/Deactivate User Account</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An employee stops or resumes using HotelOps (e.g. leaves or returns to work).</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to deactivate an account of an employee who leaves or must not use the system, or to reactivate it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The account exists in the Administrator's scope.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Inactive accounts cannot log in; their history remains visible (GBR-009). Reactivated accounts can log in again.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Activate/Deactivate User Account</strong></em></p>
<blockquote>
<p>1. Administrator opens the account and selects Deactivate (or Activate for an Inactive account).</p>
<p>2. System checks the dependencies: last Administrator, own account, open assigned work (GBR-026, GBR-027, GBR-028).</p>
<p>3. System asks User to confirm the status change of the account (MSG33).</p>
<p>4. Administrator enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-012, GBR-005).</p>
<p>6. System changes the status from Active to Inactive and revokes all of the account's sessions (GBR-021).</p>
<p>7. System saves the status change and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 6.1_Reactivate Account</strong></em></p>
<p>The account is Inactive and Administrator selected Activate.</p>
<ol type="1">
<li><p>System changes the status from Inactive to Active; an existing lock (locked_until) is kept until it expires.</p></li>
<li><p>Return to step 7 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Open Work Exists</strong></em></p>
<p>The account still has open assigned work.</p>
<ol type="1">
<li><p>System lists the open work items (MSG34).</p></li>
<li><p>Administrator asks the responsible lead to reassign them; the flow ends.</p></li>
</ol>
<p><em><strong>Step 2.2_Last Administrator or Own Account</strong></em></p>
<p>The account is the last Administrator or the Administrator's own account.</p>
<ul>
<li><p>System rejects the action (MSG03, GBR-026, GBR-027).</p></li>
<li><p>The flow ends.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-009, GBR-012, GBR-021, GBR-026, GBR-027, GBR-028</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.7 UC-007 Assign User to Branch/Department

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-007 Assign User to Branch/Department</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An employee's working branch or department changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to assign an account to branches and departments, and to change the assignment when the employee is transferred.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The account is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The account can only see and act on data in its new scope.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign User to Branch/Department</strong></em></p>
<blockquote>
<p>1. Administrator opens the account and selects Branch/Department Assignment.</p>
<p>2. System displays the current assignments and the active branches/departments.</p>
<p>3. Administrator adds or removes branch/department assignments and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-031, GBR-004, GBR-028, GBR-005).</p>
<p>5. System saves the new scope and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System recalculates the account's effective permissions at its next request (GBR-002).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Open Work in Removed Scope</strong></em></p>
<p>The account has open assigned work in a branch/department being removed.</p>
<ol type="1">
<li><p>System lists the work items (MSG34, GBR-028).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The department does not belong to the branch or is inactive.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-028, GBR-031</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.8 UC-008 Create/Update User Role

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-008 Create/Update User Role</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new role is needed or the information of an existing role changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to create, rename and deactivate roles that group permissions.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The role is available (or no longer available) for assignment in UC-010. Renaming a role does not change its permissions.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create/Update User Role</strong></em></p>
<blockquote>
<p>1. Administrator opens Role Management.</p>
<p>2. System displays the roles with status and number of users.</p>
<p>3. Administrator selects Create Role (or an existing role), enters code, name and description and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-029, GBR-003, GBR-005).</p>
<p>5. System saves the role and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deactivate Role</strong></em></p>
<p>Administrator selects Deactivate on a custom role.</p>
<ol type="1">
<li><p>System asks User to confirm the deactivation of the role (MSG33).</p></li>
<li><p>System checks that deactivation does not remove the last Administrator permission set (GBR-026) and that the role is not a system role (GBR-029).</p></li>
<li><p>System changes the role from Active to Inactive; users keep their other roles (GBR-009).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The role code already exists.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-009, GBR-026, GBR-029</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.9 UC-009 Configure Role Permissions

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-009 Configure Role Permissions</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The functions a role may perform need to change.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to choose which catalog permissions a role grants.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The role is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The role's permissions are updated; scope and segregation-of-duty rules still apply.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Configure Role Permissions</strong></em></p>
<blockquote>
<p>1. Administrator opens a role and selects Permissions.</p>
<p>2. System displays the Permission Catalog grouped by feature, with the role's current grants (GBR-030).</p>
<p>3. Administrator selects or clears permissions and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-030, GBR-026, GBR-005).</p>
<p>5. System saves the role permissions with before/after lists and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System applies the new permissions to all users of the role at their next request (GBR-002).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Last Administrator Permissions</strong></em></p>
<p>The change would remove permission administration from the last Administrator.</p>
<ol type="1">
<li><p>System rejects the change (MSG03, GBR-026).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-026, GBR-030</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.10 UC-010 Assign/Revoke User Roles

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-010 Assign/Revoke User Roles</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An employee's responsibilities change and the account needs roles granted or revoked.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to grant or revoke roles of an account.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The account is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The account's effective permissions reflect the new role set.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign/Revoke User Roles</strong></em></p>
<blockquote>
<p>1. Administrator opens the account and selects Roles.</p>
<p>2. System displays the assigned roles and the active roles available.</p>
<p>3. Administrator adds or removes roles and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-004, GBR-026, GBR-027, GBR-018, GBR-005).</p>
<p>5. System saves the role assignments and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System recalculates the account's effective permissions (GBR-002).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Revocation Not Allowed</strong></em></p>
<p>The revocation removes the last Administrator or the Administrator's own Administrator role.</p>
<ol type="1">
<li><p>System rejects the change (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.2_Executor Role Removed With Open Work</strong></em></p>
<p>A removed role is needed for open work assigned to the account (e.g. Housekeeping Staff with open cleaning tasks).</p>
<ul>
<li><p>System lists the open work (MSG34, GBR-028).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when staff join, leave or change duties (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-018, GBR-026, GBR-027, GBR-028</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.11 UC-011 Review User Access Permissions

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-011 Review User Access Permissions</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Administrator needs to check what a user can actually access.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to look up what a user can actually do: roles, scope and resulting permissions.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Review User Access Permissions</strong></em></p>
<blockquote>
<p>1. Administrator opens Access Review and selects an account.</p>
<p>2. System displays the account's roles, branches, departments and the effective permission list per feature (GBR-002).</p>
<p>3. Administrator optionally filters by feature or permission.</p>
<p>4. System shows, for each permission, which role grants it.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 1.12 UC-141 View User List

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-141 View User List</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Administrator needs to find or review user accounts.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to search, view and filter the list of user accounts by status, department or role, and to open an account to review or update it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View User List</strong></em></p>
<blockquote>
<p>1. Administrator opens the User List screen.</p>
<p>2. System displays the accounts in the Administrator's scope with full name, username, email, branch/department, roles and account status, sorted by full name (GBR-015).</p>
<p>3. Administrator filters by branch, department, role and account status or searches by full name, username or email.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. Administrator selects an account.</p>
<p>6. System displays the account details with its roles and effective permissions (UC-005, UC-011).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 2. Hotel Structure Management (FE-02)

### 2.1 UC-012 Update Branch Information

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-012 Update Branch Information</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A branch's information changes, a new branch opens or a branch stops operating.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to maintain branch information (name, address, contact, status) and to add a new branch.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Branch data is up to date; inactive branches cannot be selected for new records.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Branch Information</strong></em></p>
<blockquote>
<p>1. Administrator opens Branch Management and selects a branch.</p>
<p>2. System displays the branch details with the current row version.</p>
<p>3. Administrator edits name, address, contact phone or email and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-032, GBR-003, GBR-005).</p>
<p>5. System saves the branch and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Create Branch</strong></em></p>
<p>Administrator selects Create Branch.</p>
<ol type="1">
<li><p>System displays an empty branch form; the timezone is fixed to SYS-PAR-10 (GBR-032).</p></li>
<li><p>Administrator enters code, name, address and contact and selects Save.</p></li>
<li><p>System validates the input and business conditions (GBR-032, GBR-003).</p></li>
<li><p>System creates the branch as Active; return to Step 5.</p></li>
</ol>
<p><em><strong>Step 3.1_Deactivate Branch</strong></em></p>
<p>Administrator selects Deactivate.</p>
<ol type="1">
<li><p>System checks dependencies (GBR-033); if any exist it lists them (MSG34) and the flow ends.</p></li>
<li><p>System asks User to confirm the deactivation of the branch (MSG33).</p></li>
<li><p>System changes the branch from Active to Inactive; return to Step 5.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The branch code already exists.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-032, GBR-033</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.2 UC-013 View Branch Structure

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-013 View Branch Structure</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to look up the structure of a branch, or Administrator needs a starting point to update it.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to view the structure of a branch: floors, areas, rooms and departments. Administrator also uses it as the starting point to update the branch and its floors/areas (UC-012, UC-015, UC-016).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Branch Structure</strong></em></p>
<blockquote>
<p>1. User opens Branch Structure and selects a branch in scope.</p>
<p>2. System displays the branch with its floors and areas as a tree (floor_area_id, floor_area_code, floor_area_name, parent_floor_id), the rooms of each floor, and the departments (GBR-015).</p>
<p>3. User expands a node or filters by active/inactive.</p>
<p>4. System displays the details of the selected node.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.3 UC-014 Create Floor/Area

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-014 Create Floor/Area</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new floor or area of a branch must be managed in HotelOps.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to create a floor or an area (e.g. lobby, laundry, kitchen) in a branch.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The floor/area can be used for rooms, assets and incidents.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Floor/Area</strong></em></p>
<blockquote>
<p>1. Administrator opens Branch Structure and selects Add Floor/Area.</p>
<p>2. System displays the form: branch, type (Floor or Area), code, name, parent floor (for an area), description.</p>
<p>3. Administrator enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-034, GBR-003, GBR-004).</p>
<p>5. System saves the floor/area as Active and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The code exists in the branch.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The parent is inactive, belongs to another branch, or creates a cycle.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-034</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.4 UC-015 Update Floor/Area

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-015 Update Floor/Area</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The information of a floor or area changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to edit a floor or area.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The floor/area exists and is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Rooms, assets and history keep referring to the same floor/area.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Floor/Area</strong></em></p>
<blockquote>
<p>1. Administrator opens the floor/area and selects Edit.</p>
<p>2. System displays its details with the current row version.</p>
<p>3. Administrator edits code, name, parent or description and enters a change reason, then selects Save.</p>
<p>4. System validates the input and business conditions (GBR-034, GBR-003, GBR-005).</p>
<p>5. System saves the floor/area and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The code is duplicated or the new parent is in another branch or creates a cycle.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-034</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.5 UC-016 Deactivate Floor/Area

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-016 Deactivate Floor/Area</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A floor or area is no longer used for operation.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to deactivate a floor or area that is no longer used.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The floor/area is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The floor/area cannot be selected for new records; history is kept.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Deactivate Floor/Area</strong></em></p>
<blockquote>
<p>1. Administrator opens the floor/area and selects Deactivate.</p>
<p>2. System checks for active rooms, child areas, assets and open incidents/tasks at the location (GBR-035).</p>
<p>3. System asks User to confirm the deactivation of the floor/area (MSG33).</p>
<p>4. Administrator enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-012, GBR-005).</p>
<p>6. System changes the floor/area from Active to Inactive (GBR-009).</p>
<p>7. System saves the change and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Dependencies Exist</strong></em></p>
<p>Active rooms, child areas, assets or open work exist.</p>
<ol type="1">
<li><p>System lists them (MSG34).</p></li>
<li><p>The flow ends; Administrator moves or deactivates them first.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-009, GBR-012, GBR-035</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.6 UC-017 Create Room Type

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-017 Create Room Type</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new room type with its own cleaning standard is introduced.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to define a room type used for operations (cleaning standard and default inspection checklist).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room type can be assigned to rooms.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Room Type</strong></em></p>
<blockquote>
<p>1. Administrator opens Room Types and selects Create.</p>
<p>2. System displays the form: branch, code, name, description, standard cleaning minutes, default checklist.</p>
<p>3. Administrator enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-036, GBR-003, GBR-004).</p>
<p>5. System saves the room type as Active and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The code exists in the branch.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-036</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.7 UC-018 Update Room Type

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-018 Update Room Type</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The information or operational defaults of a room type change.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to update a room type.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The room type exists and is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> New cleaning tasks and inspections use the updated defaults; past inspections keep the checklist version they used (GBR-057).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Room Type</strong></em></p>
<blockquote>
<p>1. Administrator opens the room type and selects Edit.</p>
<p>2. System displays its details with the current row version.</p>
<p>3. Administrator edits name, description, standard cleaning minutes or default checklist and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-036, GBR-003, GBR-005).</p>
<p>5. System saves the room type and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-036, GBR-057</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.8 UC-019 Deactivate Room Type

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-019 Deactivate Room Type</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A room type is no longer used.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to deactivate a room type that is no longer used.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The room type is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room type cannot be selected for rooms; history is kept.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Deactivate Room Type</strong></em></p>
<blockquote>
<p>1. Administrator opens the room type and selects Deactivate.</p>
<p>2. System checks that no active room uses the room type (GBR-037).</p>
<p>3. System asks User to confirm the deactivation of the room type (MSG33).</p>
<p>4. Administrator enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-012, GBR-005).</p>
<p>6. System changes the room type from Active to Inactive (GBR-009).</p>
<p>7. System saves the change and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Room Type In Use</strong></em></p>
<p>Active rooms use the room type.</p>
<ol type="1">
<li><p>System lists the rooms (MSG34).</p></li>
<li><p>The flow ends; Administrator changes those rooms' type first (UC-021).</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-009, GBR-012, GBR-037</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.9 UC-020 Create Room

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-020 Create Room</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new room must be managed in HotelOps.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to create a room so that it can be cleaned, inspected and tracked.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room appears on the room status board as Pending Cleaning; it becomes Ready only after a passed inspection.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Room</strong></em></p>
<blockquote>
<p>1. Administrator opens Rooms and selects Create Room.</p>
<p>2. System displays the form: branch, floor, area (optional), room number, room type, notes.</p>
<p>3. Administrator enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-038, GBR-003, GBR-004).</p>
<p>5. System creates the room as active with operational status Pending Cleaning (GBR-039, GBR-043).</p>
<p>6. System saves the room and its first status history entry and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Room Number</strong></em></p>
<p>The room number exists in the branch.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The floor or room type is inactive or belongs to another branch.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-038, GBR-039, GBR-043</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.10 UC-021 Update Room Information

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-021 Update Room Information</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The information of a room changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to update a room's number, floor, area or room type.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The room exists and is active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Room information is updated; open work keeps pointing to the same room.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Room Information</strong></em></p>
<blockquote>
<p>1. Administrator opens the room and selects Edit.</p>
<p>2. System displays its details with the current row version.</p>
<p>3. Administrator edits the fields, enters a change reason and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-038, GBR-003, GBR-004, GBR-005).</p>
<p>5. System saves the room (operational status, cleaning and inspection history are not changed) and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The room number is duplicated or the floor/type is invalid.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-005, GBR-007, GBR-008, GBR-038</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.11 UC-022 Deactivate Room

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-022 Deactivate Room</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A room is taken out of operation or returned to operation.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to deactivate a room that is taken out of operation permanently.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The room is active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Inactive rooms are excluded from the readiness denominator and cannot receive new work.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Deactivate Room</strong></em></p>
<blockquote>
<p>1. Administrator opens the room and selects Deactivate.</p>
<p>2. System checks for open cleaning, rework, inspection, incidents, effective blocks and assets in the room (GBR-040).</p>
<p>3. System asks User to confirm the deactivation of the room (MSG33).</p>
<p>4. Administrator enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-012, GBR-005).</p>
<p>6. System marks the room inactive and recalculates its operational status to Inactive (GBR-043, GBR-044).</p>
<p>7. System saves the change and the status history entry and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Reactivate Room</strong></em></p>
<p>The room is inactive and Administrator selects Activate.</p>
<ol type="1">
<li><p>System checks that its floor and room type are active (GBR-004).</p></li>
<li><p>System marks the room active and recalculates its status (Pending Cleaning until inspected) (GBR-039).</p></li>
<li><p>Return to step 7 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Open Work or Assets Exist</strong></em></p>
<p>Dependencies exist.</p>
<ol type="1">
<li><p>System lists them (MSG34).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-012, GBR-039, GBR-040, GBR-043, GBR-044</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.12 UC-023 View Room Directory

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-023 View Room Directory</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head, Administrator, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to find rooms and their current operational status.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to search the room list by branch, floor, room type and operational status.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Room Directory</strong></em></p>
<blockquote>
<p>1. User opens the Room Directory screen.</p>
<p>2. System displays rooms with number, floor, type and current operational status, sorted by floor then room number (GBR-015).</p>
<p>3. User filters by branch, floor, area, room type, operational status and active flag or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects a room.</p>
<p>6. System displays the room details, current status reason (source task/inspection/block) and links to its history.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol start="3" type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.13 UC-024 Create Department

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-024 Create Department</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new department is set up in a branch.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to create a department and link it to a branch and a Department Head.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The department can receive users, warehouses, tasks and internal requests.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Department</strong></em></p>
<blockquote>
<p>1. Administrator opens Departments and selects Create.</p>
<p>2. System displays the form: branch, code, name, description, Department Head (optional).</p>
<p>3. Administrator enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-041, GBR-003, GBR-004).</p>
<p>5. System saves the department as Active and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The code exists in the branch.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The selected Department Head is inactive or not assigned to the branch.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-041</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.14 UC-025 Update Department

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-025 Update Department</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The information or the Department Head of a department changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to update a department's name, description and Department Head.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The department is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> New approvals and requests for the department are routed to the current Department Head (GBR-111).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Department</strong></em></p>
<blockquote>
<p>1. Administrator opens the department and selects Edit.</p>
<p>2. System displays the details with the current row version.</p>
<p>3. Administrator edits the fields and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-041, GBR-003, GBR-005).</p>
<p>5. System saves the department and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-041, GBR-111</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.15 UC-026 Deactivate Department

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-026 Deactivate Department</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A department stops operating.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to deactivate a department that no longer operates.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The department is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The department cannot be selected for new records; history is kept.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Deactivate Department</strong></em></p>
<blockquote>
<p>1. Administrator opens the department and selects Deactivate.</p>
<p>2. System checks for active users, warehouses, open tasks, open internal requests and assets in custody (GBR-042).</p>
<p>3. System asks User to confirm the deactivation of the department (MSG33).</p>
<p>4. Administrator enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-012, GBR-005).</p>
<p>6. System changes the department from Active to Inactive (GBR-009).</p>
<p>7. System saves the change and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Dependencies Exist</strong></em></p>
<p>Active users, warehouses, open work or assets exist.</p>
<ol type="1">
<li><p>System lists them (MSG34).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-009, GBR-012, GBR-042</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 2.16 UC-142 View Room Types List

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-142 View Room Types List</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Administrator needs to find or review the room types of a branch.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to search, look up and filter the list of room types by code, name or status, and to open a room type to update or deactivate it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Room Types List</strong></em></p>
<blockquote>
<p>1. Administrator opens the Room Type List screen.</p>
<p>2. System displays the room types of the branches in the Administrator's scope with code, name, branch, number of active rooms and status, sorted by code (GBR-015).</p>
<p>3. Administrator filters by branch and status or searches by code or name.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. Administrator selects a room type.</p>
<p>6. System displays the room type details and the actions allowed for it (UC-018, UC-019).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 3. Room Operations (FE-03)

### 3.1 UC-027 Manage Operational Room Status

> **REVIEW REQUIRED — annotation, not original SRS:** UC-027 and its room-status workflow must be reconciled with the new Reception role. The current source describes system-derived room status; do not assume Reception can directly select/override all status values. Permissions and allowed transitions are pending.


<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-027 Manage Operational Room Status</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to monitor room readiness or act on a room that is not ready.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor and Operations Manager to monitor the operational status of all rooms, understand why a room is not Ready and act on it. Room status itself is calculated by the system from cleaning, inspection and incident records.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No status is typed by the user; any action is completed in the selected use case and the board reflects it immediately.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Operational Room Status</strong></em></p>
<blockquote>
<p>1. User opens the Room Status Board.</p>
<p>2. System displays every room in scope grouped by floor, with its operational status (Ready, Pending Cleaning, Cleaning In Progress, Pending Inspection, Rework Required, Blocked, Inactive) calculated by the status priority (GBR-043, GBR-044).</p>
<p>3. User filters by floor, area, room type or status.</p>
<p>4. System refreshes the board and the count per status (GBR-015).</p>
<p>5. User selects a room.</p>
<p>6. System displays the status reason: the current cleaning task, inspection, rework task or effective blocks with their incidents, plus the room's status history.</p>
<p>7. User chooses the next action from the room panel: create a cleaning task (UC-028), assign an inspection (UC-041), report an incident (UC-049) or open the linked record.</p>
<p>8. System opens the selected function with the room preselected; the permission of that function is checked there (GBR-002).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 6.1_View Status History</strong></em></p>
<p>User selects Status History.</p>
<ol type="1">
<li><p>System lists the room's status changes with old/new status, source event, actor and time (GBR-043).</p></li>
<li><p>Return to step 7 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Continuously during each shift (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-043, GBR-044</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 4. Housekeeping Management (FE-04)

### 4.1 UC-028 Create Cleaning Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-028 Create Cleaning Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A room requires cleaning.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to create a cleaning task for a room, with cleaning type, priority, instructions and due time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room has one open cleaning task waiting for assignment.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Cleaning Task</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens Cleaning Tasks and selects Create (or selects a room on the Room Status Board).</p>
<p>2. System displays the form: room, cleaning type (checkout, stay-over, deep cleaning), priority, due time, instructions and the room type's cleaning items.</p>
<p>3. Housekeeping Supervisor enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-003, GBR-004, GBR-045, GBR-014).</p>
<p>5. System creates the cleaning task with status Pending Assignment (GBR-045).</p>
<p>6. System recalculates the room status (Pending Cleaning unless Blocked) and stores the status history (GBR-043, GBR-044).</p>
<p>7. System saves the cleaning task and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 8.1_Assign Immediately</strong></em></p>
<p>Housekeeping Supervisor selects Save and Assign.</p>
<ol type="1">
<li><p>System continues with UC-029 for the new task.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Room Already Has an Open Cleaning Task</strong></em></p>
<p>The room has a cleaning task that is not Completed or Cancelled.</p>
<ol type="1">
<li><p>System shows the existing task (MSG03, GBR-045).</p></li>
<li><p>Housekeeping Supervisor opens the existing task; the flow ends.</p></li>
</ol>
<p><em><strong>Step 4.2_Room Inactive</strong></em></p>
<p>The room is inactive.</p>
<ol type="1">
<li><p>System rejects the creation (MSG24).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-014, GBR-043, GBR-044, GBR-045</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.2 UC-029 Assign Cleaning Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-029 Assign Cleaning Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A cleaning task is waiting for assignment.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to assign a cleaning task to a housekeeper, taking the current workload into account.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The cleaning task is Pending Assignment.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task is Assigned and visible in the housekeeper's work list.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign Cleaning Task</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens the cleaning task and selects Assign.</p>
<p>2. System lists eligible housekeepers on shift in the branch with their number of open cleaning tasks today (GBR-046).</p>
<p>3. Housekeeping Supervisor selects a housekeeper (and may adjust the due time) and selects Assign.</p>
<p>4. System validates the input and business conditions (GBR-046, GBR-014, GBR-005).</p>
<p>5. System sets the assignee and changes the task from Pending Assignment to Assigned.</p>
<p>6. System saves the assignment and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the assigned housekeeper (UC-124) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The selected user is inactive, lacks the Housekeeping Staff role or is outside the branch/department.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 7.1_Notification Not Sent</strong></em></p>
<p>The notification cannot be created.</p>
<ol type="1">
<li><p>System keeps the assignment, queues the notification for retry and displays MSG21 (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-014, GBR-046</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.3 UC-030 Reassign Cleaning Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-030 Reassign Cleaning Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The current housekeeper cannot continue the cleaning task or the workload must be rebalanced.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to move a cleaning task to another housekeeper when the current one cannot continue.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The cleaning task is Assigned, In Progress or On Hold.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Only the new assignee can work on the task; time already worked stays with the previous assignee.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reassign Cleaning Task</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens the cleaning task and selects Reassign.</p>
<p>2. System displays the current assignee and eligible housekeepers with workload (GBR-046).</p>
<p>3. Housekeeping Supervisor selects the new housekeeper, enters the reason and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-047, GBR-046, GBR-012, GBR-005).</p>
<p>5. System closes the previous assignee's open work interval at the current time, if any (GBR-138).</p>
<p>6. System sets the new assignee and changes the task to Assigned (GBR-047).</p>
<p>7. System recalculates the room status (GBR-043).</p>
<p>8. System saves the reassignment with previous and new assignee and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>9. System sends an in-app notification to the previous and the new assignee (UC-124) (GBR-010).</p>
<p>10. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The new assignee is the same person, ineligible, or the task is already Pending Inspection.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-043, GBR-046, GBR-047, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.4 UC-031 Start Cleaning Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-031 Start Cleaning Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff is ready to work on an assigned cleaning task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assigned housekeeper to start (or resume) cleaning a room; the system starts recording working time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The cleaning task is Assigned or On Hold and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task is In Progress and the housekeeper's work time is being recorded.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Start Cleaning Task</strong></em></p>
<blockquote>
<p>1. Housekeeping Staff opens My Work and selects the cleaning task.</p>
<p>2. System displays the room, cleaning type, instructions, cleaning items and due time.</p>
<p>3. Housekeeping Staff selects Start.</p>
<p>4. System validates the input and business conditions (GBR-049, GBR-136, GBR-137, GBR-005).</p>
<p>5. System changes the task to In Progress, sets the actual start time (first start only) and opens a work interval (GBR-138).</p>
<p>6. System recalculates the room status to Cleaning In Progress unless Blocked (GBR-043, GBR-044).</p>
<p>7. System saves the task, work interval and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 4.3_Resume After Blocker</strong></em></p>
<p>The task is On Hold.</p>
<ol type="1">
<li><p>Housekeeping Staff confirms that the blocker is resolved and enters a note.</p></li>
<li><p>System closes the blocker and continues at Step 5.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Workday Not Started</strong></em></p>
<p>User has no Working workday.</p>
<ol type="1">
<li><p>System displays MSG30 and offers to start the workday (UC-139).</p></li>
<li><p>Return to step 3 of the normal flow after the workday is started.</p></li>
</ol>
<p><em><strong>Step 4.2_Another Item In Progress</strong></em></p>
<p>User has another open work interval.</p>
<ol type="1">
<li><p>System displays MSG31 with the open item (GBR-137).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-043, GBR-044, GBR-049, GBR-136, GBR-137, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.5 UC-032 Update Cleaning Progress

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-032 Update Cleaning Progress</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff has made progress on a cleaning task in progress.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the housekeeper to tick cleaning items, add notes and photos, and update progress while cleaning.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The cleaning task is In Progress and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Progress is updated; the task is still In Progress (100% does not complete it).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Cleaning Progress</strong></em></p>
<blockquote>
<p>1. Housekeeping Staff opens the cleaning task in progress.</p>
<p>2. System displays the cleaning items with their state, progress and notes.</p>
<p>3. Housekeeping Staff ticks completed items, updates progress, adds notes or photos and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-050, GBR-016, GBR-005).</p>
<p>5. System saves the progress and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Attachment</strong></em></p>
<p>A file violates the attachment policy.</p>
<ol type="1">
<li><p>System rejects the file (MSG23, GBR-016).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>Progress is outside 0–100.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-016, GBR-050</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.6 UC-033 Report Cleaning Blocker

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-033 Report Cleaning Blocker</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff meets a problem that prevents the cleaning from continuing.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the housekeeper to report that cleaning cannot continue (guest still in room, broken equipment, missing supplies, unsafe condition).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The cleaning task is In Progress and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task is On Hold with an open blocker; the housekeeper can start another room; the supervisor is informed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Report Cleaning Blocker</strong></em></p>
<blockquote>
<p>1. Housekeeping Staff opens the cleaning task and selects Report Blocker.</p>
<p>2. System displays the blocker reasons (reference data) and fields for note and photo.</p>
<p>3. Housekeeping Staff selects the reason, enters a note, optionally attaches photos and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-051, GBR-003, GBR-016).</p>
<p>5. System records the blocker, changes the task from In Progress to On Hold and closes the open work interval (GBR-051, GBR-138).</p>
<p>6. System recalculates the room status (Pending Cleaning unless Blocked) (GBR-043).</p>
<p>7. System saves the blocker, task and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the Housekeeping Supervisor (UC-126) (GBR-010).</p>
<p>9. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Equipment or Room Defect</strong></em></p>
<p>The blocker is a broken fixture or equipment.</p>
<ul>
<li><p>System offers to report an incident for the room (UC-049) with the blocker note prefilled.</p></li>
<li><p>After the incident is saved, System links it to the blocker; return to Step 4.</p></li>
</ul>
<p><em><strong>Step 3.2_Missing Supplies</strong></em></p>
<p>The blocker is a lack of cleaning supplies or amenities.</p>
<ol type="1">
<li><p>System offers to create a material request (UC-091) linked to the cleaning task (UC-092).</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Same Blocker Already Open</strong></em></p>
<p>An open blocker with the same reason exists.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-051).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-007, GBR-008, GBR-010, GBR-016, GBR-043, GBR-051, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.7 UC-034 Complete Cleaning Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-034 Complete Cleaning Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff has finished cleaning the room.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the housekeeper to confirm that cleaning is finished so the room goes to inspection.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The cleaning task is In Progress and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room waits for inspection; the housekeeper's time on the task is closed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Complete Cleaning Task</strong></em></p>
<blockquote>
<p>1. Housekeeping Staff opens the cleaning task and selects Complete.</p>
<p>2. System displays the cleaning items, completion note and optional evidence photos.</p>
<p>3. Housekeeping Staff confirms all mandatory items, enters a note, optionally attaches photos and selects Complete.</p>
<p>4. System validates the input and business conditions (GBR-052, GBR-016, GBR-005).</p>
<p>5. System changes the task from In Progress to Pending Inspection, sets the actual end time and closes the work interval (GBR-052, GBR-138).</p>
<p>6. System recalculates the room status to Pending Inspection unless Blocked (GBR-043, GBR-044).</p>
<p>7. System saves the task, interval and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the Housekeeping Supervisor so an inspection can be assigned (UC-041) (GBR-010).</p>
<p>9. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Mandatory Items Not Done</strong></em></p>
<p>A mandatory item is not ticked or a blocker is open.</p>
<ol type="1">
<li><p>System lists the missing items (MSG03, GBR-052).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.2_Invalid Attachment</strong></em></p>
<p>A file violates the attachment policy.</p>
<ol type="1">
<li><p>System displays MSG23.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-016, GBR-043, GBR-044, GBR-052, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.8 UC-035 Receive Rework Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-035 Receive Rework Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff receives a rework task after a failed inspection.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the housekeeper to receive a rework task created after a failed inspection, see the defects to fix, accept it and start working.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A rework task assigned to User exists in Pending Acceptance or Accepted.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The rework is In Progress; the room stays Rework Required until the rework is submitted.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Receive Rework Task</strong></em></p>
<blockquote>
<p>1. Housekeeping Staff opens the rework task from My Work or the notification.</p>
<p>2. System displays the room, the failed inspection, the list of defects with photos and the due time.</p>
<p>3. Housekeeping Staff selects Accept.</p>
<p>4. System validates the input and business conditions (GBR-053, GBR-005).</p>
<p>5. System changes the rework task from Pending Acceptance to Accepted.</p>
<p>6. Housekeeping Staff selects Start when going to the room.</p>
<p>7. System validates the input and business conditions (GBR-053, GBR-136, GBR-137).</p>
<p>8. System changes the rework task to In Progress and opens a work interval (GBR-138).</p>
<p>9. System saves the rework task and interval and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>10. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 7.1_Workday Not Started or Busy</strong></em></p>
<p>User has no Working workday or another open work interval.</p>
<ol type="1">
<li><p>System displays MSG30 or MSG31.</p></li>
<li><p>Return to step 6 of the normal flow after resolving it.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-053, GBR-136, GBR-137, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.9 UC-036 Complete Cleaning Rework

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-036 Complete Cleaning Rework</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff has fixed all defects of a rework task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the housekeeper to submit the fixed defects so the room can be reinspected.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The rework task is In Progress and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room waits for reinspection.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Complete Cleaning Rework</strong></em></p>
<blockquote>
<p>1. Housekeeping Staff opens the rework task and selects Complete Rework.</p>
<p>2. System displays each defect with a remediation note field and optional photo.</p>
<p>3. Housekeeping Staff enters the remediation for each defect, optionally attaches photos and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-054, GBR-016, GBR-005).</p>
<p>5. System changes the rework task from In Progress to Pending Reinspection and closes the work interval (GBR-138).</p>
<p>6. System recalculates the room status to Pending Inspection unless Blocked (GBR-044).</p>
<p>7. System saves the rework results and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the Housekeeping Supervisor for reinspection (UC-047) (GBR-010).</p>
<p>9. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Defect Without Remediation</strong></em></p>
<p>A defect has no remediation note.</p>
<ol type="1">
<li><p>System highlights the defect (MSG03, GBR-054).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-016, GBR-044, GBR-054, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.10 UC-037 Monitor Cleaning Tasks

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-037 Monitor Cleaning Tasks</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Supervisor needs to follow the progress of cleaning work.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to monitor cleaning and rework tasks (unassigned, in progress, on hold, waiting inspection, rework, overdue) and cancel tasks that are no longer needed.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The supervisor sees the current workload; cancelled tasks remain in history.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Monitor Cleaning Tasks</strong></em></p>
<blockquote>
<p>1. User opens the Cleaning Monitor screen.</p>
<p>2. System displays today's cleaning and rework tasks with room, status, assignee, due time and overdue flag (GBR-055), sorted by due time (GBR-015).</p>
<p>3. User filters by date, floor, status, assignee, priority and overdue flag or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects a task to view its details, blocker, timeline and links to assign (UC-029) or reassign (UC-030).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Cancel Cleaning Task</strong></em></p>
<p>The task is no longer needed (e.g. the room is not checked out) and Housekeeping Supervisor selects Cancel.</p>
<ol type="1">
<li><p>System checks that the task is Pending Assignment or Assigned (GBR-048).</p></li>
<li><p>System asks User to confirm the cancellation of the cleaning task (MSG33).</p></li>
<li><p>Housekeeping Supervisor enters the reason and confirms (GBR-012).</p></li>
<li><p>System changes the task to Cancelled and recalculates the room status from the latest non-cancelled cycle (GBR-044).</p></li>
<li><p>System saves the cancellation and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the assignee, if any (GBR-010).</p></li>
<li><p>System displays a success message (MSG08).</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol>
<p><em><strong>Step 5.2_Cancellation Not Allowed</strong></em></p>
<p>The task has already started.</p>
<ol type="1">
<li><p>System displays MSG24; Housekeeping Supervisor may reassign it instead (UC-030).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Continuously during each shift (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-015, GBR-044, GBR-048, GBR-055</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 4.11 UC-038 View Room Turnover History

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-038 View Room Turnover History</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to review the cleaning and inspection cycles of a room.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to view the turnover history of a room: each cleaning cycle with housekeeper, times, inspection results and rework count.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Room Turnover History</strong></em></p>
<blockquote>
<p>1. User opens a room and selects Turnover History (or opens Turnover History and selects a room).</p>
<p>2. System lists the room's cleaning cycles, newest first: cleaning type, assignee(s), start/end times, inspection result, number of rework rounds, cancellation reason if any (GBR-015).</p>
<p>3. User filters by date range or result.</p>
<p>4. User selects a cycle.</p>
<p>5. System displays the cycle timeline: cleaning task, blockers, inspections with checklist version and defects, rework tasks and attachments (GBR-016).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-016</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 5. Room Quality Inspection (FE-05)

### 5.1 UC-039 Create Room Inspection Checklist

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-039 Create Room Inspection Checklist</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A room type needs an inspection standard.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to create an inspection checklist (criteria, mandatory flags, pass conditions) and publish it for a room type.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The published checklist can be used for inspections of the selected room types.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Room Inspection Checklist</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens Inspection Checklists and selects Create.</p>
<p>2. System displays the form: name, applicable room types, and a criteria list (text, group, mandatory flag).</p>
<p>3. Housekeeping Supervisor enters the criteria and selects Save as Draft.</p>
<p>4. System validates the input and business conditions (GBR-056, GBR-003).</p>
<p>5. System saves the checklist as version 1 in Draft and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. Housekeeping Supervisor reviews it and selects Publish.</p>
<p>7. System changes the version from Draft to Published (GBR-056).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The checklist has no criterion or a criterion has no text.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; when inspection standards change.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-007, GBR-008, GBR-056</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.2 UC-040 Update Room Inspection Checklist

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-040 Update Room Inspection Checklist</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The inspection standard of a room type changes or is no longer used.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to change checklist criteria while keeping completed inspections unchanged.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The checklist exists.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> New inspections use the latest published version; history shows the version actually used.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Room Inspection Checklist</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens the checklist and selects Edit.</p>
<p>2. System displays the current version and whether it has been used by an inspection (GBR-057).</p>
<p>3. Housekeeping Supervisor changes criteria and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-056, GBR-057, GBR-005).</p>
<p>5. System saves the changes into the Draft version, or creates a new Draft version if the current version is Published (GBR-057).</p>
<p>6. Housekeeping Supervisor selects Publish.</p>
<p>7. System publishes the new version and retires the previous one; inspections already assigned keep their version.</p>
<p>8. System saves the checklist versions and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>9. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Retire Checklist</strong></em></p>
<p>Housekeeping Supervisor selects Retire.</p>
<ol type="1">
<li><p>System asks User to confirm the retirement of the checklist (MSG33).</p></li>
<li><p>System changes the published version to Retired; it can no longer be used for new inspections.</p></li>
<li><p>Return to step 8 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; when inspection standards change.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-056, GBR-057</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.3 UC-041 Assign Room Inspection

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-041 Assign Room Inspection</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A cleaned room or a submitted rework is waiting for inspection.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to assign an inspector to a room whose cleaning or rework has been submitted.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The room's cleaning task is Pending Inspection (or its rework task is Pending Reinspection for UC-047).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> One inspection is assigned for the cleaning cycle.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign Room Inspection</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens Pending Inspections (or selects the room on the board) and selects Assign Inspection.</p>
<p>2. System displays the room, the cleaning cycle, the published checklist of the room type and eligible inspectors (GBR-058).</p>
<p>3. Housekeeping Supervisor selects the inspector (can be themself) and the due time, and selects Assign.</p>
<p>4. System validates the input and business conditions (GBR-058, GBR-059, GBR-056, GBR-014).</p>
<p>5. System creates the inspection with status Assigned, linked to the cleaning cycle and the checklist version.</p>
<p>6. System saves the inspection and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the inspector (UC-124) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Inspector Performed the Cleaning</strong></em></p>
<p>The selected inspector cleaned the room in this cycle.</p>
<ol type="1">
<li><p>System rejects the selection (MSG22, GBR-059).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.2_Open Inspection Exists</strong></em></p>
<p>The cycle already has an open inspection.</p>
<ol type="1">
<li><p>System shows it (MSG03); Housekeeping Supervisor may change its inspector instead.</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 4.3_No Published Checklist</strong></em></p>
<p>The room type has no published checklist.</p>
<ol type="1">
<li><p>System displays MSG03 and asks to publish a checklist (UC-039).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-014, GBR-056, GBR-058, GBR-059</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.4 UC-042 Perform Room Inspection

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-042 Perform Room Inspection</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The assigned inspector is ready to inspect the room.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assigned inspector to inspect the room against the checklist and record Pass/Fail for each criterion.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The inspection is Assigned or Inspecting and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The inspection is Pending Conclusion with complete results.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Perform Room Inspection</strong></em></p>
<blockquote>
<p>1. Inspector opens the inspection and selects Start.</p>
<p>2. System changes the inspection from Assigned to Inspecting and displays the checklist criteria.</p>
<p>3. Inspector records Pass or Fail for each criterion, with notes and photos as needed.</p>
<p>4. For each failed criterion, Inspector records the defects (UC-043).</p>
<p>5. Inspector selects Finish Inspection.</p>
<p>6. System validates the input and business conditions (GBR-060, GBR-016, GBR-005).</p>
<p>7. System changes the inspection from Inspecting to Pending Conclusion and shows the summary (passed/failed criteria, defects).</p>
<p>8. System saves the results and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>9. Inspector concludes with Approve (UC-044) or Reject (UC-045).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Save and Continue Later</strong></em></p>
<p>Inspector selects Save Draft.</p>
<ol type="1">
<li><p>System saves the results entered so far; the inspection stays Inspecting.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 6.1_Results Incomplete</strong></em></p>
<p>A mandatory criterion has no result or a failed criterion has no defect.</p>
<ul>
<li><p>System lists the missing items (MSG03, GBR-060).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-016, GBR-060</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.5 UC-043 Record Inspection Defects

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-043 Record Inspection Defects</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The inspector finds a defect during an inspection.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the inspector to record defects found during inspection with description, severity and photos.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The inspection is Inspecting or Pending Conclusion and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Defects are attached to the inspection and will be used for the rework task.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Record Inspection Defects</strong></em></p>
<blockquote>
<p>1. Inspector selects a failed criterion and selects Add Defect.</p>
<p>2. System displays the defect form: description, severity (minor/major), location in room, photos.</p>
<p>3. Inspector enters the defect and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-061, GBR-003, GBR-016).</p>
<p>5. System saves the defect linked to the criterion and inspection and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Inspection Already Concluded</strong></em></p>
<p>The inspection is Passed or Failed.</p>
<ol type="1">
<li><p>System displays MSG24 (GBR-061).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 4.2_Invalid Attachment</strong></em></p>
<p>A photo violates the attachment policy.</p>
<ol type="1">
<li><p>System displays MSG23.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-007, GBR-008, GBR-016, GBR-061</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.6 UC-044 Approve Room Inspection

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-044 Approve Room Inspection</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">All mandatory criteria of an inspection are Pass.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the inspector to approve a room that meets all mandatory criteria, completing the cleaning cycle and making the room Ready if it is not blocked.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The inspection is Pending Conclusion and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The cleaning cycle is Completed; the room is Ready unless blocked.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Approve Room Inspection</strong></em></p>
<blockquote>
<p>1. Inspector reviews the summary and selects Approve.</p>
<p>2. System validates the input and business conditions (GBR-062, GBR-059, GBR-005).</p>
<p>3. System changes the inspection from Pending Conclusion to Passed.</p>
<p>4. System changes the cleaning task from Pending Inspection to Completed (GBR-062).</p>
<p>5. System recalculates the room status: Ready if no effective block exists (GBR-043, GBR-044).</p>
<p>6. System saves the inspection, cleaning task and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the housekeeper who cleaned the room (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Room Still Blocked</strong></em></p>
<p>The room has an effective block.</p>
<ol type="1">
<li><p>System keeps the room Blocked and displays MSG17.</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Mandatory Criterion Failed</strong></em></p>
<p>At least one mandatory criterion is Fail.</p>
<ol type="1">
<li><p>System rejects approval (MSG03, GBR-062) and offers Reject (UC-045).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-043, GBR-044, GBR-059, GBR-062</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.7 UC-045 Reject Room Inspection

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-045 Reject Room Inspection</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An inspection has at least one failed criterion.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the inspector to reject a room that does not meet the checklist, so rework is required.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The inspection is Pending Conclusion and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room is Rework Required; a rework task must be created.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reject Room Inspection</strong></em></p>
<blockquote>
<p>1. Inspector reviews the summary and selects Reject.</p>
<p>2. System displays the failed criteria and defects and a field for the overall reason.</p>
<p>3. Inspector enters the reason and confirms.</p>
<p>4. System validates the input and business conditions (GBR-063, GBR-060, GBR-012, GBR-005).</p>
<p>5. System changes the inspection from Pending Conclusion to Failed.</p>
<p>6. System changes the cleaning task from Pending Inspection to Rework Required (GBR-063).</p>
<p>7. System recalculates the room status to Rework Required unless Blocked (GBR-044).</p>
<p>8. System saves the inspection, cleaning task and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>9. System sends an in-app notification to the Housekeeping Supervisor and the housekeeper who cleaned the room (UC-126) (GBR-010).</p>
<p>10. System opens Request Cleaning Rework (UC-046) with the defects preselected.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Defect Recorded</strong></em></p>
<p>The failed inspection has no defect.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-060).</p></li>
<li><p>Return to step 2 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Many times per day (about once per room that needs cleaning or inspection).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-044, GBR-060, GBR-063</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.8 UC-046 Request Cleaning Rework

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-046 Request Cleaning Rework</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An inspection has been concluded as Failed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to send the defects of a failed inspection back as a rework task to a housekeeper.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The inspection is Failed and has no open rework task.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The rework task waits for the housekeeper's acceptance (UC-035).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Request Cleaning Rework</strong></em></p>
<blockquote>
<p>1. Housekeeping Supervisor opens the failed inspection and selects Request Rework.</p>
<p>2. System displays the defects (all preselected), eligible housekeepers (the original cleaner first) and a due time field.</p>
<p>3. Housekeeping Supervisor selects the defects, assignee and due time and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-064, GBR-046, GBR-014).</p>
<p>5. System creates the rework task with status Pending Acceptance, linked to the inspection and the selected defects.</p>
<p>6. System saves the rework task and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the assignee (UC-124) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Open Rework Exists</strong></em></p>
<p>The inspection already has an open rework task.</p>
<ol type="1">
<li><p>System shows it (MSG03, GBR-064).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-014, GBR-046, GBR-064</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.9 UC-047 Reinspect Room

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-047 Reinspect Room</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Housekeeping Staff has submitted a rework task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Housekeeping Supervisor to reinspect the defects of a submitted rework task and conclude the cleaning cycle.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The rework task is Pending Reinspection; the reinspection is assigned to User (UC-041).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The cycle is Completed, or a new rework round is started.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reinspect Room</strong></em></p>
<blockquote>
<p>1. Inspector opens the reinspection.</p>
<p>2. System displays each defect of the rework task with the remediation note and photos, and the link to the original inspection (GBR-065).</p>
<p>3. Inspector records Fixed or Not Fixed for every defect, with notes and photos, and selects Conclude.</p>
<p>4. System validates the input and business conditions (GBR-065, GBR-059, GBR-016, GBR-005).</p>
<p>5. All defects are Fixed: System sets the reinspection Passed, the rework task Completed and the cleaning task Completed (GBR-065).</p>
<p>6. System recalculates the room status (Ready unless Blocked) (GBR-044).</p>
<p>7. System saves the reinspection, rework task, cleaning task and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the housekeeper (GBR-010).</p>
<p>9. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Defects Not Fixed</strong></em></p>
<p>At least one defect is Not Fixed.</p>
<ol type="1">
<li><p>System sets the reinspection Failed and the rework task Failed; the cleaning task stays Rework Required (GBR-065).</p></li>
<li><p>System records new defects for the items not fixed.</p></li>
<li><p>System saves the results and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System opens Request Cleaning Rework (UC-046) for the remaining defects; the flow ends.</p></li>
</ol>
<p><em><strong>Step 6.1_Room Still Blocked</strong></em></p>
<p>The room has an effective block.</p>
<ol type="1">
<li><p>System keeps the room Blocked and displays MSG17.</p></li>
<li><p>Return to step 7 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not All Defects Checked</strong></em></p>
<p>A defect has no result.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-016, GBR-044, GBR-059, GBR-065</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 5.10 UC-048 View Room Inspection History

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-048 View Room Inspection History</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to review past inspections and their results.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to look up inspection history: results, failed criteria, defects, inspectors, housekeepers and evidence.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Room Inspection History</strong></em></p>
<blockquote>
<p>1. User opens the Inspection History screen.</p>
<p>2. System displays inspections with room, date, inspector, checklist version, result and rework rounds, sorted by inspection date (newest first) (GBR-015).</p>
<p>3. User filters by branch, room, date range, result, inspector and housekeeper or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects an inspection.</p>
<p>6. System displays all criteria results, defects with photos, rework tasks and reinspections, and the checklist version used (GBR-057, GBR-016).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-016, GBR-057</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 6. Incident & Maintenance Management (FE-06)

### 6.1 UC-049 Report Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-049 Report Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user detects a damage, malfunction or other operational problem in a room, area or asset.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows any operational user to report a fault or service incident with its location, description, discovery time and evidence.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident waits for classification and triage by the Maintenance Lead.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Report Incident</strong></em></p>
<blockquote>
<p>1. User opens Report Incident (or selects Report Incident from a room, asset or cleaning blocker).</p>
<p>2. System displays the form: branch, room/area, asset (optional), title, description, detected time, photos.</p>
<p>3. User enters the information and selects Next.</p>
<p>4. System validates the input and business conditions (GBR-066, GBR-003, GBR-016, GBR-014).</p>
<p>5. System searches open incidents at the same room/area/asset and shows them if any (GBR-067).</p>
<p>6. User confirms creating a new incident.</p>
<p>7. System creates the incident with status Pending Triage and stores the attachments.</p>
<p>8. System saves the incident and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>9. System sends an in-app notification to the Maintenance Lead of the branch (UC-126) (GBR-010).</p>
<p>10. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Add to Existing Incident</strong></em></p>
<p>A similar open incident exists and User selects it.</p>
<ol type="1">
<li><p>System displays MSG32 and continues with UC-051 for the selected incident.</p></li>
<li><p>The flow ends without creating a new incident.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Attachment</strong></em></p>
<p>A file violates the attachment policy.</p>
<ol type="1">
<li><p>System displays MSG23.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>No room, area or asset is selected, or the asset is in another branch.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-007, GBR-008, GBR-010, GBR-014, GBR-016, GBR-066, GBR-067</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.2 UC-050 Classify Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-050 Classify Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new incident is waiting for classification.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to classify an incident by technical category, severity and impact on room readiness.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is not Closed or Cancelled.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident is classified and ready for triage.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Classify Incident</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens the incident and selects Classify.</p>
<p>2. System displays category (reference data), severity (Low, Normal, High, Critical) and the 'affects room readiness' flag.</p>
<p>3. Maintenance Lead selects the values and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-068, GBR-004, GBR-005).</p>
<p>5. System saves the classification and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Critical Severity</strong></em></p>
<p>Severity is Critical.</p>
<ol type="1">
<li><p>System creates Critical incident alerts for the Maintenance Lead and Operations Manager that require acknowledgment (UC-126, GBR-068, GBR-159).</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 5.2_Affects Room Readiness</strong></em></p>
<p>The flag is set and the incident has a room.</p>
<ol type="1">
<li><p>System suggests blocking the room (UC-056).</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-068, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.3 UC-051 Attach Incident Evidence

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-051 Attach Incident Evidence</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user has additional evidence (photos or documents) about an incident.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users involved in an incident to add photos, documents or notes that clarify it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is not Closed or Cancelled.</p>
<p><strong>PRE-03:</strong> User is the reporter, the assignee, a Maintenance Lead or an Operations Manager in scope.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The evidence is visible to everyone who can view the incident.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Attach Incident Evidence</strong></em></p>
<blockquote>
<p>1. User opens the incident and selects Add Evidence.</p>
<p>2. System displays fields for a note and files.</p>
<p>3. User enters a note and/or selects files and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-016, GBR-003).</p>
<p>5. System saves the evidence linked to the incident with author and time and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Incident Closed</strong></em></p>
<p>The incident is Closed or Cancelled.</p>
<ol type="1">
<li><p>System displays MSG24; evidence can only be viewed.</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 4.2_Invalid File</strong></em></p>
<p>A file violates the attachment policy.</p>
<ol type="1">
<li><p>System displays MSG23.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-007, GBR-008, GBR-016</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.4 UC-052 Triage Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-052 Triage Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A classified incident is waiting for triage.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to triage a new incident: set priority, response and resolution deadlines and the initial action plan, or cancel it as duplicate/invalid.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is Pending Triage and has category and severity (UC-050).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident has a priority and SLA deadlines and waits for assignment.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Triage Incident</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens the incident and selects Triage.</p>
<p>2. System displays priority options and calculates default response and resolution deadlines from the detected time and the SLA targets of each priority (GBR-069).</p>
<p>3. Maintenance Lead selects the priority, reviews the deadlines and enters the initial action plan, then selects Save.</p>
<p>4. System validates the input and business conditions (GBR-069, GBR-014, GBR-005).</p>
<p>5. System changes the incident from Pending Triage to Pending Assignment.</p>
<p>6. System saves the triage and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System opens Assign Maintenance Work (UC-053).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deadline Changed</strong></em></p>
<p>Maintenance Lead changes a default deadline.</p>
<ol type="1">
<li><p>System requires a reason and stores the default and chosen deadline (GBR-069, GBR-012).</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 3.2_Duplicate or Invalid Incident</strong></em></p>
<p>Maintenance Lead selects Cancel as Duplicate/Invalid.</p>
<ol type="1">
<li><p>System requires the original incident (for Duplicate) and a reason (GBR-070).</p></li>
<li><p>System asks User to confirm the cancellation of the incident (MSG33).</p></li>
<li><p>System changes the incident to Cancelled and removes its effective blocks, recalculating room status (GBR-075, GBR-043).</p></li>
<li><p>System saves the cancellation and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the reporter (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not Classified</strong></em></p>
<p>Category or severity is missing.</p>
<ol type="1">
<li><p>System displays MSG03 and opens UC-050.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-014, GBR-043, GBR-069, GBR-070, GBR-075</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.5 UC-053 Assign Maintenance Work

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-053 Assign Maintenance Work</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A triaged incident needs a technician.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to assign (or reassign) a technician and the expected completion time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is Pending Assignment or Assigned.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident is Assigned and appears in the technician's work list.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign Maintenance Work</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens the incident and selects Assign.</p>
<p>2. System lists eligible technicians in the branch with their open incidents and occurrences (GBR-071).</p>
<p>3. Maintenance Lead selects the technician (themself only if another active Maintenance Lead of the branch can verify), instructions and planned end time, then selects Assign.</p>
<p>4. System validates the input and business conditions (GBR-071, GBR-014, GBR-005).</p>
<p>5. System sets the assignee and changes the incident to Assigned.</p>
<p>6. System saves the assignment with previous and new assignee and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the technician (and the previous assignee when reassigning) (UC-124) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The selected user is inactive or not Maintenance Staff/Maintenance Lead of the branch.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-014, GBR-071</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.6 UC-054 Accept and Start Maintenance

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-054 Accept and Start Maintenance</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Staff, Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A technician has been assigned maintenance work.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assigned technician to accept the maintenance work and start it; the acceptance time is the response time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is Assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident is In Progress; the response time is fixed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Accept and Start Maintenance</strong></em></p>
<blockquote>
<p>1. Technician opens the incident from My Work or the notification.</p>
<p>2. System displays the incident, location, evidence, priority and deadlines.</p>
<p>3. Technician selects Accept and Start.</p>
<p>4. System validates the input and business conditions (GBR-072, GBR-136, GBR-137, GBR-005).</p>
<p>5. System records the acceptance time, changes the incident from Assigned to In Progress and opens a work interval (GBR-138).</p>
<p>6. System saves the incident and interval and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Cannot Take the Work</strong></em></p>
<p>Technician selects Decline with a reason.</p>
<ol type="1">
<li><p>System returns the incident to Pending Assignment and notifies the Maintenance Lead (GBR-012).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Workday Not Started or Busy</strong></em></p>
<p>User has no Working workday or another open work interval.</p>
<ol type="1">
<li><p>System displays MSG30 or MSG31.</p></li>
<li><p>Return to step 3 of the normal flow after resolving it.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-012, GBR-072, GBR-136, GBR-137, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.7 UC-055 Update Maintenance Progress

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-055 Update Maintenance Progress</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Staff, Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The technician makes progress, uses materials or must stop the maintenance work.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the technician to record diagnosis, actions, progress and materials used, and to put the work on hold or resume it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is In Progress or On Hold and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Progress and material usage are recorded; SLA time continues during On Hold.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Maintenance Progress</strong></em></p>
<blockquote>
<p>1. Technician opens the incident and selects Update Progress.</p>
<p>2. System displays progress, diagnosis, actions taken, materials issued for this incident and attachments.</p>
<p>3. Technician enters progress, notes and materials used and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-050, GBR-074, GBR-016, GBR-005).</p>
<p>5. System saves the progress update and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Put On Hold</strong></em></p>
<p>Work must stop (waiting for materials, vendor or guest).</p>
<ol type="1">
<li><p>Technician selects Hold, the hold reason and a note.</p></li>
<li><p>System changes the incident from In Progress to On Hold and closes the work interval (GBR-073, GBR-138).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 3.2_Resume Work</strong></em></p>
<p>The incident is On Hold and Technician selects Resume.</p>
<ol type="1">
<li><p>System checks the workday and open intervals (GBR-136, GBR-137).</p></li>
<li><p>System changes the incident to In Progress and opens a work interval.</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 3.3_Materials Needed</strong></em></p>
<p>Technician needs materials.</p>
<ol type="1">
<li><p>System opens Create Material Request (UC-091) linked to the incident (UC-092).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Materials Exceed Issued Quantity</strong></em></p>
<p>Used quantity is higher than the issued, not returned quantity.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-074).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-016, GBR-050, GBR-073, GBR-074, GBR-136, GBR-137, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.8 UC-056 Block Room Due to Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-056 Block Room Due to Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An incident makes a room unusable, or the room can be used again before the incident is closed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to block a room affected by an incident so that it cannot become Ready, and to remove the block early when the room is usable.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is not Closed or Cancelled and has a room in its affected scope.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The room is Blocked until all its effective blocks are removed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Block Room Due to Incident</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens the incident and selects Block Room.</p>
<p>2. System displays the rooms in the incident's scope and their current status.</p>
<p>3. Maintenance Lead selects the room(s), enters the reason and selects Block.</p>
<p>4. System validates the input and business conditions (GBR-075, GBR-012, GBR-004).</p>
<p>5. System creates an effective block per incident–room pair and recalculates each room's status to Blocked (GBR-075, GBR-044).</p>
<p>6. System saves the blocks and room status history and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Housekeeping Supervisor of the branch (UC-126) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Remove Block Early</strong></em></p>
<p>The room can be used before the incident is closed; Maintenance Lead selects Remove Block.</p>
<ol type="1">
<li><p>System asks User to confirm the removal of the block (MSG33).</p></li>
<li><p>Maintenance Lead enters the reason (GBR-012).</p></li>
<li><p>System changes the block from Effective to Removed and recalculates the room status (GBR-075).</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Block Already Exists</strong></em></p>
<p>The incident already blocks the room.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; a few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-010, GBR-012, GBR-044, GBR-075</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.9 UC-057 Escalate Critical Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-057 Escalate Critical Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An incident is critical, at risk of missing its deadline or overdue.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to escalate a critical, at-risk or overdue incident to the Operations Manager for extra resources or decisions.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is not Closed or Cancelled.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The Operations Manager is informed; the escalation closes automatically when the incident is closed or cancelled.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Escalate Critical Incident</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens the incident and selects Escalate.</p>
<p>2. System shows the severity, deadlines and time used against the at-risk threshold (GBR-076).</p>
<p>3. Maintenance Lead enters the escalation reason and requested support and selects Escalate.</p>
<p>4. System validates the input and business conditions (GBR-076, GBR-012).</p>
<p>5. System creates an Open escalation linked to the incident.</p>
<p>6. System saves the escalation and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Operations Manager; the alert requires acknowledgment (UC-126, UC-130) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not Eligible or Already Escalated</strong></em></p>
<p>The incident is not critical/at-risk/overdue, or an Open escalation exists.</p>
<ul>
<li><p>System displays MSG03 (GBR-076).</p></li>
<li><p>The flow ends.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; a few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-076</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.10 UC-058 Resolve Maintenance Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-058 Resolve Maintenance Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Staff, Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The technician has finished the repair.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assigned technician to submit the repair result for verification.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is In Progress and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident waits for verification; the resolution time is recorded.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Resolve Maintenance Incident</strong></em></p>
<blockquote>
<p>1. Technician opens the incident and selects Submit Result.</p>
<p>2. System displays fields: diagnosis/root cause, action taken, materials used, actual end time (server time), result photos.</p>
<p>3. Technician enters the result and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-077, GBR-074, GBR-016, GBR-005).</p>
<p>5. System changes the incident from In Progress to Pending Verification and closes the work interval (GBR-138).</p>
<p>6. System saves the result and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Maintenance Lead of the branch who did not perform the work (verifier) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>A mandatory result field is missing or materials exceed the issued quantity.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-016, GBR-074, GBR-077, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.11 UC-059 Verify and Close Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-059 Verify and Close Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A repair result is waiting for verification.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to check the repair result and close the incident, removing its room blocks, or return it for more work.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is Pending Verification.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident is Closed and rooms without other blocks can become Ready, or it is back In Progress.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Verify and Close Incident</strong></em></p>
<blockquote>
<p>1. Verifier opens the incident and selects Verify.</p>
<p>2. System displays the submitted result, evidence, materials and deadlines.</p>
<p>3. Verifier selects Accept and Close and enters a verification note.</p>
<p>4. System validates the input and business conditions (GBR-078, GBR-013, GBR-005).</p>
<p>5. System changes the incident from Pending Verification to Closed and closes its Open escalation.</p>
<p>6. System removes all effective blocks of the incident and recalculates the status of each affected room (GBR-075, GBR-044).</p>
<p>7. System saves the closure, blocks and room status and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the reporter, the technician and the Housekeeping Supervisor if a room was unblocked (GBR-010).</p>
<p>9. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Return for More Work</strong></em></p>
<p>The result is not acceptable.</p>
<ol type="1">
<li><p>Verifier selects Return and enters the reason (GBR-012).</p></li>
<li><p>System changes the incident from Pending Verification to In Progress; blocks stay effective (GBR-078).</p></li>
<li><p>System saves the return and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the technician (GBR-010).</p></li>
<li><p>System displays MSG20; the flow ends.</p></li>
</ol>
<p><em><strong>Step 8.1_Linked Asset</strong></em></p>
<p>The incident is linked to an asset in Damaged or Under Repair condition.</p>
<ol type="1">
<li><p>System notifies the Operations Manager to update the asset condition (UC-070).</p></li>
<li><p>Return to step 9 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Verifier Did the Work</strong></em></p>
<p>The verifier submitted or performed the result.</p>
<ol type="1">
<li><p>System displays MSG22 (GBR-013, GBR-078).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-013, GBR-044, GBR-075, GBR-078</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.12 UC-060 Reopen Incident

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-060 Reopen Incident</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The problem of a closed incident occurs again.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to reopen a closed incident when the problem returns, keeping the previous handling history.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The incident is Closed.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The incident goes through triage, assignment and verification again.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reopen Incident</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens the incident and selects Reopen.</p>
<p>2. System displays the closure details and reopen window (GBR-079).</p>
<p>3. Maintenance Lead enters the reason and evidence of recurrence and selects Reopen.</p>
<p>4. System validates the input and business conditions (GBR-079, GBR-012, GBR-016).</p>
<p>5. System starts a new processing cycle: the incident changes from Closed to Pending Triage; previous cycle data is kept.</p>
<p>6. System saves the reopening and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the previous technician (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Reopen Window Expired</strong></em></p>
<p>The incident was closed more than SYS-PAR-19 days ago.</p>
<ol type="1">
<li><p>System displays MSG03 and suggests reporting a new incident (UC-049).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; only when a closed problem occurs again.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-016, GBR-079</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.13 UC-061 Create Preventive Maintenance Schedule

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-061 Create Preventive Maintenance Schedule</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An asset, room or area needs periodic maintenance, or its maintenance plan changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Maintenance Lead to create a preventive maintenance schedule for an asset, room or area, and to edit or deactivate it later.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The system generates occurrences automatically before each due date (GBR-083).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Preventive Maintenance Schedule</strong></em></p>
<blockquote>
<p>1. Maintenance Lead opens Preventive Maintenance and selects Create Schedule.</p>
<p>2. System displays the form: target (asset/room/area), title, work description and check items, interval (number + days/weeks/months), first due date, default assignee.</p>
<p>3. Maintenance Lead enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-081, GBR-003, GBR-004, GBR-014).</p>
<p>5. System saves the schedule as Active with next due date = first due date and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Edit Schedule</strong></em></p>
<p>Maintenance Lead opens an Active schedule and selects Edit.</p>
<ol type="1">
<li><p>System displays the schedule with the current row version.</p></li>
<li><p>Maintenance Lead changes description, check items, interval or default assignee and selects Save.</p></li>
<li><p>System validates the input and business conditions (GBR-081, GBR-082, GBR-005).</p></li>
<li><p>System applies the change to future occurrences only; return to Step 5.</p></li>
</ol>
<p><em><strong>Step 1.2_Deactivate Schedule</strong></em></p>
<p>Maintenance Lead selects Deactivate.</p>
<ol type="1">
<li><p>System asks User to confirm the deactivation of the schedule (MSG33).</p></li>
<li><p>Maintenance Lead enters the reason (GBR-012).</p></li>
<li><p>System changes the schedule to Inactive; occurrences already generated remain (GBR-082).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Schedule</strong></em></p>
<p>The target already has an Active schedule with the same title.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when maintenance plans change (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-005, GBR-007, GBR-008, GBR-012, GBR-014, GBR-081, GBR-082, GBR-083</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.14 UC-062 Execute Scheduled Maintenance

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-062 Execute Scheduled Maintenance</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Staff, Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A preventive maintenance occurrence is due or assigned.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the technician to carry out a generated preventive maintenance occurrence, record the check results and complete it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A maintenance occurrence is Assigned to User (generated by the system, GBR-083).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The occurrence is Completed and the next one is planned.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Execute Scheduled Maintenance</strong></em></p>
<blockquote>
<p>1. Technician opens the occurrence from My Work.</p>
<p>2. System displays the target, check items, due date and history of the previous occurrence.</p>
<p>3. Technician selects Start.</p>
<p>4. System validates the input and business conditions (GBR-084, GBR-136, GBR-137).</p>
<p>5. System changes the occurrence from Assigned to In Progress and opens a work interval (GBR-138).</p>
<p>6. Technician records a result for every check item, notes and photos, and selects Complete.</p>
<p>7. System validates the input and business conditions (GBR-084, GBR-016, GBR-005).</p>
<p>8. System changes the occurrence to Completed, closes the work interval and sets the schedule's next due date from the scheduled due date (GBR-083).</p>
<p>9. System saves the occurrence, interval and schedule and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>10. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Assign or Cancel Occurrence</strong></em></p>
<p>Maintenance Lead opens a Pending Assignment or Assigned occurrence.</p>
<ol type="1">
<li><p>Maintenance Lead selects a technician, or selects Cancel with a reason (GBR-012).</p></li>
<li><p>System sets it Assigned (notifying the technician) or Cancelled.</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 6.1_Problem Found</strong></em></p>
<p>A check item fails.</p>
<ol type="1">
<li><p>Technician selects Report Incident; System opens UC-049 prefilled with the target and links the incident to the occurrence (GBR-084).</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Workday Not Started or Busy</strong></em></p>
<p>User has no Working workday or another open interval.</p>
<ol type="1">
<li><p>System displays MSG30 or MSG31.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per week, as preventive maintenance occurrences fall due.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-012, GBR-016, GBR-083, GBR-084, GBR-136, GBR-137, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.15 UC-063 Manage Work Categories

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-063 Manage Work Categories</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The categories used to classify work need to be added or changed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to manage task categories used by operational tasks and work statistics (e.g. cleaning support, maintenance, inventory, front-office support).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Active categories can be selected for new tasks.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Work Categories</strong></em></p>
<blockquote>
<p>1. Administrator opens Work Categories.</p>
<p>2. System lists categories with code, name, status and usage count.</p>
<p>3. Administrator creates or edits a category (code, name, description) and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-085, GBR-003, GBR-005).</p>
<p>5. System saves the category and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deactivate Category</strong></em></p>
<p>Administrator selects Deactivate.</p>
<ol type="1">
<li><p>System asks User to confirm the deactivation of the category (MSG33).</p></li>
<li><p>System sets the category Inactive; existing tasks keep it (GBR-085, GBR-009).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The code exists.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-009, GBR-085</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.16 UC-064 Manage Operational Reference Data

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-064 Manage Operational Reference Data</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Operational reference values (reasons, priorities, SLA targets) need to be added or changed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to manage shared reference data: incident categories, blocker and pause reasons, cleaning types, internal request types, priority labels and the SLA targets of each priority.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> New SLA targets apply to incidents triaged afterwards; existing deadlines are unchanged.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Operational Reference Data</strong></em></p>
<blockquote>
<p>1. Administrator opens Reference Data and selects a group.</p>
<p>2. System lists the values of the group with code, label, order and status (for priorities: response and resolution targets in minutes).</p>
<p>3. Administrator creates or edits a value and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-085, GBR-003, GBR-005).</p>
<p>5. System saves the value and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deactivate Value</strong></em></p>
<p>Administrator selects Deactivate.</p>
<ol type="1">
<li><p>System asks User to confirm the deactivation of the value (MSG33).</p></li>
<li><p>System sets it Inactive; history keeps its meaning (GBR-085).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Fixed Priority Code</strong></em></p>
<p>Administrator tries to add, rename the code of, or deactivate a priority.</p>
<ol type="1">
<li><p>System displays MSG03; only labels and SLA targets of LOW, NORMAL, HIGH, CRITICAL can change (GBR-085).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-085</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.17 UC-143 View Incidents List

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-143 View Incidents List</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead, Maintenance Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to find incidents to follow up or act on.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to search, look up and filter incident reports by department, location, severity, status or time period.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Incidents List</strong></em></p>
<blockquote>
<p>1. User opens the Incident List screen.</p>
<p>2. System displays the incidents visible to the user (GBR-165) with incident number, location/asset, category, severity, priority, status, assignee, deadlines and overdue flag (GBR-080), sorted by resolution deadline (GBR-015).</p>
<p>3. User filters by department, location (room/area/asset), category, severity, status, assignee, overdue flag and detected period or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects an incident.</p>
<p>6. System displays the incident details with evidence, progress and processing history.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Continuously during each shift (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-080, GBR-165</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 6.18 UC-144 View Maintenance List

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-144 View Maintenance List</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead, Maintenance Staff</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to find maintenance work to follow up or act on.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to search, look up and filter maintenance work arising from incidents or preventive maintenance schedules by assignee, status, due date or time period.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Maintenance List</strong></em></p>
<blockquote>
<p>1. User opens the Maintenance Task List screen.</p>
<p>2. System displays the maintenance work visible to the user (GBR-165) with work reference, source (incident or preventive maintenance), location/asset, priority, assignee, status, due date and overdue flag, sorted by due date (GBR-015).</p>
<p>3. User filters by source, assignee, status, due date range and overdue flag or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects a work item.</p>
<p>6. System displays the work details and the actions allowed for the user (UC-054, UC-055, UC-058, UC-062).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Continuously during each shift (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-165</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 7. Asset & Equipment Management (FE-07)

### 7.1 UC-065 Register Asset/Equipment

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-065 Register Asset/Equipment</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A new asset or piece of equipment is put into use.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to register an asset or equipment item with its identification and managing branch.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The asset is registered; it is counted at a location after UC-067.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Register Asset/Equipment</strong></em></p>
<blockquote>
<p>1. Operations Manager opens Assets and selects Register.</p>
<p>2. System displays the form: branch, asset code, name, category, brand/model, serial number, purchase date, warranty end, custodian department, notes, photos.</p>
<p>3. Operations Manager enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-086, GBR-003, GBR-004, GBR-016).</p>
<p>5. System saves the asset as Active, condition In Use, without location and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System opens Assign Asset to Location (UC-067).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The asset code exists.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-016, GBR-086</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.2 UC-066 Update Asset Information

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-066 Update Asset Information</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The information of an asset changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to edit an asset's identification and description.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The asset is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Asset information is updated; location changes use UC-068 and custody changes use UC-069.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Asset Information</strong></em></p>
<blockquote>
<p>1. Operations Manager opens the asset and selects Edit.</p>
<p>2. System displays its details with the current row version; location, custodian and condition are read-only (GBR-087).</p>
<p>3. Operations Manager edits the fields and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-086, GBR-003, GBR-005).</p>
<p>5. System saves the asset and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-086, GBR-087</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.3 UC-067 Assign Asset to Location

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-067 Assign Asset to Location</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A registered asset is placed at a location.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to place a newly registered asset at its first location.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The asset is Active and has no current location.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The asset is counted at its location.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign Asset to Location</strong></em></p>
<blockquote>
<p>1. Operations Manager opens the asset and selects Assign Location.</p>
<p>2. System displays rooms and areas of the asset's branch.</p>
<p>3. Operations Manager selects the room or area and the effective date and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-086, GBR-004, GBR-005).</p>
<p>5. System sets the current location and creates the first movement record (GBR-087).</p>
<p>6. System saves the location and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The location is inactive or in another branch.</p>
<ul>
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-086, GBR-087</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.4 UC-068 Transfer Asset Location

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-068 Transfer Asset Location</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An asset is moved to another location.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to move an asset between rooms, areas or branches with a movement record.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The asset is Active and has a current location.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The asset appears at its new location; movements are listed in its profile.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Transfer Asset Location</strong></em></p>
<blockquote>
<p>1. Operations Manager opens the asset and selects Transfer Location.</p>
<p>2. System displays the current location and the target options (rooms/areas in branches within the manager's scope).</p>
<p>3. Operations Manager selects the new location, enters the reason and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-087, GBR-004, GBR-012, GBR-005).</p>
<p>5. System updates the current location and creates a movement record with old and new location (GBR-087).</p>
<p>6. System saves the movement and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 4.2_Open Incident or Occurrence</strong></em></p>
<p>The asset has an open incident or maintenance occurrence.</p>
<ol type="1">
<li><p>System warns and requires a confirmation note (GBR-087).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Same Location</strong></em></p>
<p>The target equals the current location.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-012, GBR-087</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.5 UC-069 Record Asset Handover

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-069 Record Asset Handover</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Department Head</th>
<th>Secondary Actors</th>
<th>Department Head of the receiving department</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Custody of an asset passes to another department.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the custodian department to hand over custody of an asset to another department; the receiving Department Head acknowledges or rejects it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The asset is Active and in the sender's custody (sender's department, or any department for the Operations Manager).</p>
<p><strong>PRE-03:</strong> No pending handover exists for the asset.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Custody changes only after acknowledgment; the full handover history is kept.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Record Asset Handover</strong></em></p>
<blockquote>
<p>1. Sender opens the asset and selects Hand Over.</p>
<p>2. System displays the current custodian, condition and the receiving department options.</p>
<p>3. Sender selects the receiving department, records the condition at handover, notes and photos, and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-088, GBR-004, GBR-016).</p>
<p>5. System creates the handover as Pending Acknowledgment; custody does not change yet (GBR-088).</p>
<p>6. System saves the handover and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the receiving Department Head (GBR-010).</p>
<p>8. Receiving Department Head opens the handover, checks the asset and selects Acknowledge.</p>
<p>9. System changes the handover to Acknowledged and sets the receiving department as custodian (GBR-088).</p>
<p>10. System saves the acknowledgment and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>11. System sends an in-app notification to the sender (GBR-010).</p>
<p>12. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 8.1_Reject Handover</strong></em></p>
<p>The receiver disagrees (e.g. condition differs).</p>
<ol type="1">
<li><p>Receiver selects Reject and enters the reason (GBR-012).</p></li>
<li><p>System changes the handover to Rejected; custody stays with the sender.</p></li>
<li><p>System saves the rejection and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the sender (GBR-010).</p></li>
<li><p>System displays MSG19; the flow ends.</p></li>
</ol>
<p><em><strong>Step 8.2_Resubmit</strong></em></p>
<p>The handover was Rejected and the sender selects Resubmit.</p>
<ol type="1">
<li><p>System creates a new version of the handover in Pending Acknowledgment, keeping the rejected version (GBR-088).</p></li>
<li><p>Return to step 7 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-010, GBR-012, GBR-016, GBR-088</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.6 UC-070 Update Asset Condition

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-070 Update Asset Condition</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The physical condition of an asset changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows authorized users to update an asset's condition (In Use, Damaged, Under Repair).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The asset is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The asset condition is visible in its profile and statistics. Retirement uses UC-074.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Asset Condition</strong></em></p>
<blockquote>
<p>1. User opens the asset and selects Update Condition.</p>
<p>2. System displays the current condition and linked open incidents.</p>
<p>3. User selects the new condition, enters a note and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-089, GBR-005).</p>
<p>5. System saves the condition change and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Damaged Without Incident</strong></em></p>
<p>User sets Damaged and there is no open incident for the asset.</p>
<ol type="1">
<li><p>System offers to report an incident (UC-049).</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.1_Back to In Use With Open Incident</strong></em></p>
<p>Linked incidents are not closed.</p>
<ol type="1">
<li><p>System requires a reason (GBR-089, GBR-012).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-012, GBR-089</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.7 UC-071 View Asset Profile

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-071 View Asset Profile</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to look up an asset and its current information.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to view an asset's profile: identification, current location, custodian, condition, documents and recent activity.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Asset Profile</strong></em></p>
<blockquote>
<p>1. User opens the Assets screen.</p>
<p>2. System displays assets with code, name, location, custodian and condition, sorted by asset code (GBR-015).</p>
<p>3. User filters by branch, category, location, custodian department, condition and lifecycle status or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects an asset.</p>
<p>6. System displays the profile with movements, handovers, open incidents and schedules; files are shown only if User may view the source record (GBR-016).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-016</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.8 UC-072 Review Asset Maintenance History

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-072 Review Asset Maintenance History</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Maintenance Lead, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to review the incidents and maintenance of an asset.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to review the maintenance history of an asset: incidents, repairs, materials and preventive occurrences.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Review Asset Maintenance History</strong></em></p>
<blockquote>
<p>1. User opens an asset and selects Maintenance History.</p>
<p>2. System lists incidents and preventive occurrences of the asset, newest first, with dates, technicians, results and materials (GBR-015).</p>
<p>3. User filters by date range or type.</p>
<p>4. System displays totals: number of incidents, recurrences (reopened), average resolution time and last preventive maintenance (GBR-161).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-161</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.9 UC-073 Review Assets by Location

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-073 Review Assets by Location</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to know which assets are at a location.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to see asset statistics by branch, floor/area, room, department, category and condition.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Review Assets by Location</strong></em></p>
<blockquote>
<p>1. User opens Assets by Location.</p>
<p>2. System displays counts of active assets grouped by location and condition; retired assets are shown separately (GBR-090, GBR-161).</p>
<p>3. User changes the grouping or filters.</p>
<p>4. User selects a group to see its asset list (UC-071).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-090, GBR-161</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 7.10 UC-074 Retire/Deactivate Asset

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-074 Retire/Deactivate Asset</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An asset is no longer used.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to retire an asset that is no longer used.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The asset is Active.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The retired asset is excluded from active statistics; its history remains.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Retire/Deactivate Asset</strong></em></p>
<blockquote>
<p>1. Operations Manager opens the asset and selects Retire.</p>
<p>2. System checks open incidents, open occurrences, active schedules and pending handovers (GBR-090).</p>
<p>3. System asks User to confirm the retirement of the asset (MSG33).</p>
<p>4. Operations Manager enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-012, GBR-005).</p>
<p>6. System changes the asset lifecycle status from Active to Retired (GBR-009).</p>
<p>7. System saves the retirement and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Dependencies Exist</strong></em></p>
<p>Open work or a pending handover exists.</p>
<ol type="1">
<li><p>System lists them (MSG34).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-009, GBR-012, GBR-090</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 8. Inventory Management (FE-08)

### 8.1 UC-075 Manage Warehouses

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-075 Manage Warehouses</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A warehouse is opened, changed or closed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to declare, update and deactivate warehouses (stock locations) of a branch with their responsible department.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Active warehouses can be used in stock documents.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Warehouses</strong></em></p>
<blockquote>
<p>1. Administrator opens Warehouses.</p>
<p>2. System lists warehouses with code, name, branch, responsible department and status.</p>
<p>3. Administrator creates or edits a warehouse (code, name, branch, responsible department, address/description) and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-091, GBR-003, GBR-004, GBR-005).</p>
<p>5. System saves the warehouse and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deactivate Warehouse</strong></em></p>
<p>Administrator selects Deactivate.</p>
<ol type="1">
<li><p>System checks balances and open documents (GBR-091); if any exist it lists them (MSG34) and the flow ends.</p></li>
<li><p>System asks User to confirm the deactivation of the warehouse (MSG33).</p></li>
<li><p>System sets the warehouse Inactive.</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Duplicate Code</strong></em></p>
<p>The code exists in the branch.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-005, GBR-007, GBR-008, GBR-091</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.2 UC-076 Manage Inventory Items

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-076 Manage Inventory Items</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A material item is introduced, changed or no longer used.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to maintain the material catalog: item code, name, type (amenity, cleaning chemical, linen, spare part, other), base unit and description.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Active items can be requested, received and issued.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Inventory Items</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Items.</p>
<p>2. System lists items with code, name, type, base unit and status.</p>
<p>3. Storekeeper creates or edits an item and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-092, GBR-003, GBR-005).</p>
<p>5. System saves the item and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deactivate Item</strong></em></p>
<p>Storekeeper selects Deactivate.</p>
<ol type="1">
<li><p>System asks User to confirm the deactivation of the item (MSG33).</p></li>
<li><p>System sets the item Inactive; it cannot be used in new documents but its balances and history remain (GBR-009).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Base Unit Change After Transactions</strong></em></p>
<p>The item has stock transactions and the base unit is changed.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-092).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.2_Duplicate Code</strong></em></p>
<p>The item code exists.</p>
<ol type="1">
<li><p>System displays MSG35.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-009, GBR-092</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.3 UC-077 Manage Units of Measurement

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-077 Manage Units of Measurement</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An item needs another unit of measurement or its conversion changes.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to manage units of measurement and the conversion factor of each item's additional units (e.g. 1 box = 24 bottles).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Documents can use the unit; stock is always stored in the base unit.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Units of Measurement</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Units of Measurement.</p>
<p>2. System lists units and, per item, the conversions to its base unit.</p>
<p>3. Storekeeper adds a unit or an item conversion (unit, factor to base unit) and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-093, GBR-003, GBR-005).</p>
<p>5. System saves the unit/conversion and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Deactivate Conversion</strong></em></p>
<p>Storekeeper deactivates a conversion.</p>
<ol type="1">
<li><p>System sets it Inactive; posted documents keep the factor used (GBR-093).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Conversion</strong></em></p>
<p>The factor is ≤ 0 or the unit already has a conversion for the item.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-093).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; mainly at initial setup and when the hotel structure changes.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-093</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.4 UC-078 Configure Minimum Stock Levels

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-078 Configure Minimum Stock Levels</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The minimum stock level of an item in a warehouse must be set or changed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to set the minimum stock level of an item in a warehouse to trigger low-stock alerts and replenishment.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Low-stock alerts (UC-128) use the new levels from the next scan.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Configure Minimum Stock Levels</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Minimum Stock Levels and selects a warehouse.</p>
<p>2. System lists the warehouse's items with current available quantity and minimum level.</p>
<p>3. Storekeeper enters or changes the minimum levels (base unit) and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-094, GBR-095, GBR-005).</p>
<p>5. System saves the minimum levels and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>A minimum level is negative.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally (a few times per month).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-094, GBR-095</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.5 UC-079 Create Stock Receipt

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-079 Create Stock Receipt</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Goods arrive at a warehouse from a supplier or a replenishment.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to prepare a stock receipt (draft) for incoming materials, optionally linked to a replenishment request.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> A Draft receipt is ready to be verified and posted (UC-080).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Stock Receipt</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Stock Receipts and selects Create.</p>
<p>2. System displays the form: receiving warehouse, source (supplier name or internal), reference document no., linked replenishment (optional), lines (item, unit, expected quantity).</p>
<p>3. Storekeeper enters the information and selects Save Draft.</p>
<p>4. System validates the input and business conditions (GBR-099, GBR-095, GBR-093, GBR-121, GBR-004).</p>
<p>5. System saves the receipt as Draft (stock is not changed) and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Edit or Cancel Draft</strong></em></p>
<p>Storekeeper opens a Draft receipt.</p>
<ol type="1">
<li><p>Storekeeper edits lines and selects Save, or selects Cancel with a reason (GBR-100, GBR-012).</p></li>
<li><p>System saves the draft or changes it to Cancelled; stock is not changed (GBR-096).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Replenishment Mismatch</strong></em></p>
<p>The linked replenishment is for another warehouse or does not contain an item.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per week, once per supplier or replenishment delivery.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-012, GBR-093, GBR-095, GBR-096, GBR-099, GBR-100, GBR-121</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.6 UC-080 Verify and Post Stock Receipt

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-080 Verify and Post Stock Receipt</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A Draft receipt is ready for checking against the goods received.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to check the materials actually received against a draft receipt and post it, increasing stock.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The receipt is Draft.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Stock increases by the accepted quantities; the posted receipt cannot be edited (GBR-098).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Verify and Post Stock Receipt</strong></em></p>
<blockquote>
<p>1. Storekeeper opens the Draft receipt and selects Verify and Post.</p>
<p>2. System displays each line with expected quantity and fields for received, rejected (damaged) quantity and reason.</p>
<p>3. Storekeeper enters the actual quantities and selects Post.</p>
<p>4. System validates the input and business conditions (GBR-099, GBR-095, GBR-006, GBR-005).</p>
<p>5. System changes the receipt from Draft to Posted and creates ledger entries that increase available stock by the accepted quantities (base unit) (GBR-096, GBR-093).</p>
<p>6. If linked to a replenishment, System updates its received quantity and status (GBR-121).</p>
<p>7. System saves the receipt, ledger and replenishment and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the requesters waiting for these items (material requests with a shortage) (GBR-010).</p>
<p>9. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Quantity Difference Without Reason</strong></em></p>
<p>Received differs from expected and no reason is entered.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-099).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 4.2_Already Posted</strong></em></p>
<p>The receipt was already posted (double submission).</p>
<ol type="1">
<li><p>System does not post it again and shows the posted receipt (MSG28, GBR-006).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per week, once per supplier or replenishment delivery.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-006, GBR-007, GBR-008, GBR-010, GBR-093, GBR-095, GBR-096, GBR-098, GBR-099, GBR-121</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.7 UC-081 Issue Stock

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-081 Issue Stock</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An approved material request is ready to be issued.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to post a stock issue for an approved material request: the issued quantities leave the warehouse and are recorded against the request and its linked work.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The material request is Approved or Partially Issued.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Stock and the request's issued quantities are updated in one transaction (GBR-007).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Issue Stock</strong></em></p>
<blockquote>
<p>1. Storekeeper opens the approved request (from UC-097/UC-098) and selects Issue.</p>
<p>2. System displays each request line with approved, issued and remaining quantity and the available stock of the selected warehouse (GBR-116).</p>
<p>3. Storekeeper selects the warehouse, enters the quantity to issue per line and the receiver, and selects Post.</p>
<p>4. System validates the input and business conditions (GBR-101, GBR-095, GBR-097, GBR-093, GBR-006, GBR-005).</p>
<p>5. System creates a Posted issue document linked to the request and its linked work, and ledger entries that decrease available stock (GBR-096).</p>
<p>6. System increases the issued quantity of each request line.</p>
<p>7. System saves the issue, ledger and request lines and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System returns to the calling use case to update the request status (UC-097 or UC-098).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Insufficient Stock</strong></em></p>
<p>A quantity exceeds available stock.</p>
<ol type="1">
<li><p>System displays MSG16 (GBR-097).</p></li>
<li><p>Return to step 3 of the normal flow (Storekeeper may reduce the quantity, i.e. partial fulfillment).</p></li>
</ol>
<p><em><strong>Step 4.2_Exceeds Remaining Approved Quantity</strong></em></p>
<p>A quantity exceeds the remaining approved quantity.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-101).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-006, GBR-007, GBR-008, GBR-093, GBR-095, GBR-096, GBR-097, GBR-101, GBR-116</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.8 UC-082 Return Materials to Stock

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-082 Return Materials to Stock</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Unused materials are returned to the warehouse.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to receive materials returned by a department (from a return request) and post the return.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A return request created in UC-090 is Pending Receipt (Draft).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Usable returned materials are back in stock; damaged ones are recorded but not added.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Return Materials to Stock</strong></em></p>
<blockquote>
<p>1. Storekeeper opens the pending return.</p>
<p>2. System displays the original issue, returned items and declared quantities.</p>
<p>3. Storekeeper inspects the materials and enters, per line, the quantity accepted as usable and the damaged quantity with reason, then selects Post.</p>
<p>4. System validates the input and business conditions (GBR-102, GBR-095, GBR-006, GBR-005).</p>
<p>5. System changes the return from Draft to Posted and creates ledger entries increasing available stock by the accepted quantities only (GBR-102).</p>
<p>6. System saves the return and ledger and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Fewer Items Received Than Declared</strong></em></p>
<p>The physical quantity is lower than declared.</p>
<ol type="1">
<li><p>Storekeeper enters the actual quantity; the difference is recorded with a note and the return is closed with what was received.</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Return Exceeds Issued Quantity</strong></em></p>
<p>The cumulative returned quantity would exceed the issued quantity.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-102).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-006, GBR-007, GBR-008, GBR-010, GBR-095, GBR-102</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.9 UC-083 Create Stock Transfer

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-083 Create Stock Transfer</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Stock must be moved from one warehouse to another.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to create a transfer of materials from one warehouse to another (same or different branch) and dispatch it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Dispatched quantities are in transit and not counted in either warehouse's available stock.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Stock Transfer</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Stock Transfers and selects Create.</p>
<p>2. System displays the form: source warehouse, destination warehouse, lines (item, unit, quantity), note.</p>
<p>3. Storekeeper enters the information and selects Save Draft.</p>
<p>4. System validates the input and business conditions (GBR-103, GBR-095, GBR-093, GBR-004, GBR-002).</p>
<p>5. System saves the transfer as Draft and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. When the goods leave, Storekeeper selects Dispatch.</p>
<p>7. System validates the input and business conditions (GBR-103, GBR-097, GBR-006, GBR-005).</p>
<p>8. System changes the transfer from Draft to In Transit, decreasing source stock and recording the in-transit quantity (GBR-103).</p>
<p>9. System saves the dispatch and ledger and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>10. System sends an in-app notification to the Storekeeper of the destination warehouse (GBR-010).</p>
<p>11. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 6.1_Cancel Draft</strong></em></p>
<p>Storekeeper selects Cancel on a Draft transfer.</p>
<ol type="1">
<li><p>Storekeeper enters a reason; System sets the transfer Cancelled (no stock effect).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 7.1_Insufficient Source Stock</strong></em></p>
<p>A line exceeds available stock.</p>
<ol type="1">
<li><p>System displays MSG16.</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-006, GBR-007, GBR-008, GBR-010, GBR-093, GBR-095, GBR-097, GBR-103</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.10 UC-084 Confirm Stock Transfer Receipt

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-084 Confirm Stock Transfer Receipt</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A dispatched transfer arrives at the destination warehouse.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the destination Storekeeper to confirm materials received from a transfer, possibly in several batches, and record discrepancies.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The transfer is In Transit or Partially Received; User manages the destination warehouse.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Stock at the destination is updated; every dispatched unit is received, damaged or missing.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Confirm Stock Transfer Receipt</strong></em></p>
<blockquote>
<p>1. Storekeeper opens the transfer and selects Confirm Receipt.</p>
<p>2. System displays each line with dispatched and already confirmed quantities.</p>
<p>3. Storekeeper enters received, damaged and missing quantities per line (with reasons for damaged/missing) and selects Confirm.</p>
<p>4. System validates the input and business conditions (GBR-104, GBR-095, GBR-006, GBR-005).</p>
<p>5. System increases destination stock by the received quantities and records damaged/missing quantities as transfer losses (GBR-104).</p>
<p>6. System sets the transfer to Received when all dispatched quantities are confirmed, otherwise Partially Received.</p>
<p>7. System saves the confirmation and ledger and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System sends an in-app notification to the source Storekeeper (GBR-010).</p>
<p>9. System displays the result (MSG08 or MSG18).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Exceeds Outstanding Quantity</strong></em></p>
<p>Confirmed quantities exceed what is still in transit.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-006, GBR-007, GBR-008, GBR-010, GBR-095, GBR-104</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.11 UC-085 Conduct Stocktake

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-085 Conduct Stocktake</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A periodic or ad-hoc stock count of a warehouse is required.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to start a stocktake of a warehouse and record the physically counted quantities.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The counted stocktake is ready for reconciliation (UC-086).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Conduct Stocktake</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Stocktakes and selects Start Stocktake for a warehouse.</p>
<p>2. System checks that no other stocktake is open for the warehouse (GBR-105).</p>
<p>3. System creates the stocktake In Progress with the cutoff time = now and lists all items with balance or minimum level in the warehouse.</p>
<p>4. Storekeeper enters the counted quantity of each item (base unit) and selects Save; counting can be saved several times.</p>
<p>5. Storekeeper selects Submit Count.</p>
<p>6. System validates the input and business conditions (GBR-105, GBR-095, GBR-005).</p>
<p>7. System changes the stocktake to Counted.</p>
<p>8. System saves the counts and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>9. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 4.1_Cancel Stocktake</strong></em></p>
<p>Storekeeper cancels an In Progress stocktake with a reason.</p>
<ol type="1">
<li><p>System sets it Cancelled; no adjustment is created.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Stocktake Already Open</strong></em></p>
<p>An open stocktake exists for the warehouse.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 6.1_Items Not Counted</strong></em></p>
<p>Some items have no counted quantity.</p>
<ol type="1">
<li><p>System lists them (MSG03); Storekeeper enters 0 or the count.</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">About once per month per warehouse, plus ad-hoc counts.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-095, GBR-105</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.12 UC-086 Reconcile Stocktake Discrepancies

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-086 Reconcile Stocktake Discrepancies</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A counted stocktake has differences from the system quantities.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to compare counted quantities with system quantities at the cutoff and explain each discrepancy.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The stocktake is Counted.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Discrepancies are explained and wait for the Operations Manager.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reconcile Stocktake Discrepancies</strong></em></p>
<blockquote>
<p>1. Storekeeper opens the stocktake and selects Reconcile.</p>
<p>2. System calculates, per item, the system quantity at the cutoff (posted movements up to the cutoff) and the discrepancy (GBR-105, GBR-106).</p>
<p>3. Storekeeper enters a cause for each non-zero discrepancy and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-106, GBR-005).</p>
<p>5. System changes the stocktake to Pending Adjustment.</p>
<p>6. System saves the reconciliation and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Operations Manager for adjustment approval (UC-087) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.1_No Discrepancy</strong></em></p>
<p>All discrepancies are 0.</p>
<ol type="1">
<li><p>System changes the stocktake to Reconciled without an adjustment (GBR-106).</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Missing Cause</strong></em></p>
<p>A discrepancy has no cause.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">About once per month per warehouse, plus ad-hoc counts.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-105, GBR-106</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.13 UC-087 Adjust Stock Balance

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-087 Adjust Stock Balance</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A reconciled stocktake waits for adjustment, or a stock error must be corrected.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to approve and post stock adjustments, from a reconciled stocktake or for an error correction.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A stocktake is Pending Adjustment, or an error correction is needed.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Stock balances equal the counted quantities; the adjustment is traceable to its stocktake.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Adjust Stock Balance</strong></em></p>
<blockquote>
<p>1. Operations Manager opens the stocktake in Pending Adjustment.</p>
<p>2. System displays the discrepancies with causes and the resulting balances.</p>
<p>3. Operations Manager reviews and selects Approve Adjustment with a note.</p>
<p>4. System validates the input and business conditions (GBR-107, GBR-097, GBR-013, GBR-006, GBR-005).</p>
<p>5. System creates a Posted adjustment document whose lines equal the discrepancies and changes the stocktake to Reconciled (GBR-107).</p>
<p>6. System saves the adjustment, ledger and stocktake and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Error-Correction Adjustment</strong></em></p>
<p>Operations Manager selects New Adjustment without a stocktake.</p>
<ol type="1">
<li><p>Operations Manager selects warehouse, items, signed quantities and the correction reason (GBR-012).</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 3.1_Return Stocktake for Recount</strong></em></p>
<p>Causes are not convincing.</p>
<ol type="1">
<li><p>Operations Manager selects Return with a reason; System sets the stocktake back to Counted and notifies the Storekeeper (MSG20).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Negative Stock</strong></em></p>
<p>The adjustment would make stock negative.</p>
<ol type="1">
<li><p>System displays MSG16.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">About once per month per warehouse, plus ad-hoc counts.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-006, GBR-007, GBR-008, GBR-012, GBR-013, GBR-097, GBR-107</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.14 UC-088 Reverse Stock Transaction

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-088 Reverse Stock Transaction</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A posted stock document is found to be wrong.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to reverse a posted stock document that was wrong, by creating an offsetting document while keeping the original.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The document is Posted and is a receipt, issue, return or adjustment.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The ledger shows both the original and the reversal (GBR-098).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reverse Stock Transaction</strong></em></p>
<blockquote>
<p>1. Operations Manager opens the posted document and selects Reverse.</p>
<p>2. System checks that it has not been reversed, has no dependent later documents and that reversal keeps stock non-negative (GBR-108).</p>
<p>3. System asks User to confirm the reversal of the document (MSG33).</p>
<p>4. Operations Manager enters the reason and confirms.</p>
<p>5. System validates the input and business conditions (GBR-108, GBR-012, GBR-006, GBR-005).</p>
<p>6. System creates a Posted reversal document with opposite quantities, marks the original as Reversed, and for an issue reduces the request's issued quantity (GBR-108).</p>
<p>7. System saves the reversal and ledger and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Reversal Not Allowed</strong></em></p>
<p>The document was already reversed, has dependent documents or would cause negative stock.</p>
<ol type="1">
<li><p>System displays the reason (MSG34 or MSG16).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; only to correct posted errors.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-006, GBR-007, GBR-008, GBR-012, GBR-098, GBR-108</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.15 UC-089 View Stock Balance and Movement Ledger

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-089 View Stock Balance and Movement Ledger</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to check stock balances or stock movements, or Operations Manager needs to find a posted document to reverse.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to look up current stock and the movement ledger by warehouse, item and period. Operations Manager uses this list of posted documents to select the document to reverse (UC-088).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Stock Balance and Movement Ledger</strong></em></p>
<blockquote>
<p>1. User opens Stock Balance.</p>
<p>2. System displays, per warehouse and item, available quantity, in-transit quantity and minimum level, highlighting items below minimum (GBR-109).</p>
<p>3. User filters by warehouse, item, type or below-minimum flag, or selects an item to view its ledger.</p>
<p>4. System displays the posted movements in the period (receipts, issues, returns, transfers, adjustments, reversals) with opening and closing balances (GBR-109, GBR-015).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-109</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 8.16 UC-090 Return Unused Materials

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-090 Return Unused Materials</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A department has issued materials it did not use.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows a user who received materials to request returning unused or excess quantities to the warehouse.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> User (or User's department) received the materials through a posted issue.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The Storekeeper receives and posts the return in UC-082.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Return Unused Materials</strong></em></p>
<blockquote>
<p>1. User opens the posted issue (from the material request) and selects Return Materials.</p>
<p>2. System displays the issued lines with quantity issued and already returned.</p>
<p>3. User enters the quantities to return and the reason and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-102, GBR-095).</p>
<p>5. System creates a return request (stock document Draft – Pending Receipt); stock is not changed (GBR-096).</p>
<p>6. System saves the return request and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Storekeeper of the warehouse (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Withdraw Return</strong></em></p>
<p>The return is still Pending Receipt and User selects Withdraw.</p>
<ol type="1">
<li><p>System sets it Cancelled.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Quantity Too High</strong></em></p>
<p>The quantity exceeds what can still be returned.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-095, GBR-096, GBR-102</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 9. Material Request Management (FE-09)

### 9.1 UC-091 Create Material Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-091 Create Material Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A department needs materials for operational work.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows an operational user to request materials from inventory, stating items, quantities, purpose, receiving department/location and needed-by time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request waits for review and approval.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Material Request</strong></em></p>
<blockquote>
<p>1. User opens Material Requests and selects Create.</p>
<p>2. System displays the form: receiving department/location, purpose, needed-by time, linked work (optional), lines (item, unit, quantity).</p>
<p>3. User enters the information and selects Submit (or Save Draft).</p>
<p>4. System validates the input and business conditions (GBR-110, GBR-095, GBR-093, GBR-004, GBR-014).</p>
<p>5. System creates the request with status Pending Approval and determines the approver (GBR-111).</p>
<p>6. System saves the request and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the approver (UC-125) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Save Draft</strong></em></p>
<p>User selects Save Draft.</p>
<ol type="1">
<li><p>System saves the request as Draft without notifying anyone; it can be edited and submitted later (UC-093).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 3.2_Link to Work</strong></em></p>
<p>User selects the work record the materials are for.</p>
<ol type="1">
<li><p>System continues with UC-092 for the selection.</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-010, GBR-014, GBR-093, GBR-095, GBR-110, GBR-111</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.2 UC-092 Link Material Request to Work

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-092 Link Material Request to Work</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A material request must be traced to the work it serves.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the requester to link a material request to the cleaning task, incident, maintenance occurrence or operational task that will use the materials.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> User is the creator of the request.</p>
<p><strong>PRE-03:</strong> The request is not Cancelled or Rejected.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Issued materials and their usage are traceable to the work record.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Link Material Request to Work</strong></em></p>
<blockquote>
<p>1. User opens the request and selects Link to Work.</p>
<p>2. System lists open work records of the same branch that User can view.</p>
<p>3. User selects the record and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-118, GBR-005).</p>
<p>5. System saves the link and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The work record is closed, cancelled or in another branch.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-118</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.3 UC-093 Update/Cancel Material Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-093 Update/Cancel Material Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The content of a material request must change or the request is no longer needed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the creator to edit, resubmit or cancel a material request before materials are issued.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> User is the creator of the request.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The approval always refers to the latest content; history of changes is kept.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update/Cancel Material Request</strong></em></p>
<blockquote>
<p>1. User opens the request and selects Edit.</p>
<p>2. System displays the request with the current row version and its review history.</p>
<p>3. User changes lines, purpose or needed-by time and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-112, GBR-110, GBR-095, GBR-005).</p>
<p>5. System saves the changes; a Draft or Needs Information request becomes Pending Approval; an Approved request without issues returns to Pending Approval and its approved quantities are cleared (GBR-112).</p>
<p>6. System saves the request with before/after values and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the approver (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Cancel Request</strong></em></p>
<p>User selects Cancel.</p>
<ol type="1">
<li><p>System checks that nothing has been issued (GBR-113).</p></li>
<li><p>System asks User to confirm the cancellation of the request (MSG33).</p></li>
<li><p>User enters the reason (GBR-012).</p></li>
<li><p>System changes the request to Cancelled.</p></li>
<li><p>System saves the cancellation and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the approver if it had been submitted (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not Editable</strong></em></p>
<p>The request is Partially Issued, Fully Issued, Rejected, Cancelled or Remainder Closed.</p>
<ol type="1">
<li><p>System displays MSG24.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-095, GBR-110, GBR-112, GBR-113</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.4 UC-094 Review Material Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-094 Review Material Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A material request is submitted for approval.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the approver to review a material request: purpose, quantities, linked work, recent consumption and current stock.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Pending Approval and User is its approver (GBR-111).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request is decided in UC-095 or returned to the requester.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Review Material Request</strong></em></p>
<blockquote>
<p>1. Approver opens the request from the notification or Pending Approvals.</p>
<p>2. System displays the lines, purpose, linked work, the requester's recent requests for the same items and the available stock (read-only, not reserved) (GBR-114).</p>
<p>3. Approver decides to approve/reject (UC-095) or to ask for more information.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Request More Information</strong></em></p>
<p>The request lacks justification.</p>
<ol type="1">
<li><p>Approver selects Need Information and enters the question (GBR-012).</p></li>
<li><p>System changes the request from Pending Approval to Needs Information (GBR-114).</p></li>
<li><p>System saves the review and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the requester (GBR-010).</p></li>
<li><p>System displays MSG20; the flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-111, GBR-114</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.5 UC-095 Approve/Reject Material Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-095 Approve/Reject Material Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The approver has reviewed a material request and is ready to decide.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the approver to approve a material request (possibly with reduced quantities), reject it, or close the unissued remainder of a partially issued request.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> User is the request's approver (GBR-111).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Only approved quantities can be issued.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Approve/Reject Material Request</strong></em></p>
<blockquote>
<p>1. Approver opens the Pending Approval request and selects Decide.</p>
<p>2. System displays each line with requested quantity and an approved quantity field (default = requested).</p>
<p>3. Approver enters the approved quantities and selects Approve.</p>
<p>4. System validates the input and business conditions (GBR-115, GBR-013, GBR-005).</p>
<p>5. System changes the request from Pending Approval to Approved and stores the approved quantities (GBR-115).</p>
<p>6. System saves the decision and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester and the Storekeeper (UC-125) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Close Remainder</strong></em></p>
<p>The request is Partially Issued and the remainder will not be issued.</p>
<ol type="1">
<li><p>Approver selects Close Remainder and enters the reason (GBR-117).</p></li>
<li><p>System changes the request to Remainder Closed; issued quantities are unchanged.</p></li>
<li><p>System saves the closure and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the requester and the Storekeeper (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 3.1_Reject</strong></em></p>
<p>Approver selects Reject.</p>
<ol type="1">
<li><p>Approver enters the reason (GBR-012).</p></li>
<li><p>System changes the request to Rejected.</p></li>
<li><p>System saves the decision and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the requester (GBR-010).</p></li>
<li><p>System displays MSG19; the flow ends.</p></li>
</ol>
<p><em><strong>Step 4.1_All Quantities Zero</strong></em></p>
<p>Every approved quantity is 0.</p>
<ol type="1">
<li><p>System treats the decision as a rejection and asks for a reason (GBR-115).</p></li>
<li><p>Continue at Step 3.1.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.2_Approver Is Requester</strong></em></p>
<p>The approver created the request.</p>
<ul>
<li><p>System displays MSG22 (GBR-013).</p></li>
<li><p>The flow ends.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-013, GBR-111, GBR-115, GBR-117</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.6 UC-096 Check Material Availability

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-096 Check Material Availability</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper, Department Head, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to know whether approved quantities can be issued.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to check whether approved materials can be issued: available stock per warehouse versus remaining approved quantity.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Approved or Partially Issued, or is being reviewed by its approver.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed; nothing is reserved (GBR-114).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Check Material Availability</strong></em></p>
<blockquote>
<p>1. User opens the request and selects Check Availability.</p>
<p>2. System displays, per line, remaining approved quantity, available stock per warehouse in the branch, in-transit quantity and the fulfillable quantity (GBR-116).</p>
<p>3. User selects a warehouse to see the result for that warehouse.</p>
<p>4. System highlights lines that cannot be fully issued and suggests partial fulfillment (UC-098) or replenishment (UC-100).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-114, GBR-116</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.7 UC-097 Issue Approved Materials

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-097 Issue Approved Materials</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A material request has been approved.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to issue all remaining approved materials of a request, using the stock issue process of UC-081.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Approved or Partially Issued.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request is Fully Issued; stock is reduced.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Issue Approved Materials</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Requests to Fulfil and selects the request.</p>
<p>2. System displays the remaining approved quantities and availability (UC-096).</p>
<p>3. Storekeeper selects Issue Remaining.</p>
<p>4. System performs the stock issue for all remaining quantities (UC-081) (GBR-101, GBR-097).</p>
<p>5. System changes the request to Fully Issued (GBR-116).</p>
<p>6. System saves the request status together with the issue and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester that materials are ready for pickup (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not Enough Stock</strong></em></p>
<p>Some remaining quantity is not available.</p>
<ol type="1">
<li><p>System displays MSG16 and offers partial fulfillment (UC-098) and replenishment (UC-100).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-097, GBR-101, GBR-116</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.8 UC-098 Manage Partial Material Fulfillment

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-098 Manage Partial Material Fulfillment</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Available stock does not cover the full approved quantity.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to issue part of an approved request when stock is short, keeping the remainder open for later issues.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Approved or Partially Issued.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The remainder stays open until issued or closed by the approver (UC-095).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Partial Material Fulfillment</strong></em></p>
<blockquote>
<p>1. Storekeeper opens the request and selects Partial Issue.</p>
<p>2. System displays remaining approved quantity and fulfillable quantity per line (GBR-116).</p>
<p>3. Storekeeper enters the quantities to issue now (less than or equal to the fulfillable quantity) and selects Issue.</p>
<p>4. System performs the stock issue (UC-081) (GBR-101).</p>
<p>5. System changes the request to Partially Issued while a remainder exists, or Fully Issued when nothing remains (GBR-116).</p>
<p>6. System saves the request status together with the issue and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester with issued and remaining quantities (GBR-010).</p>
<p>8. System displays the result (MSG18).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 7.1_Create Replenishment for Shortage</strong></em></p>
<p>Stock will not cover the remainder.</p>
<ol type="1">
<li><p>System offers to create a replenishment request for the shortage (UC-100).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-101, GBR-116</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 9.9 UC-099 Track Material Request History

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-099 Track Material Request History</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to follow the status and history of material requests.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to follow the full lifecycle of material requests: creation, review, approval, issues, returns, cancellation or closure.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Track Material Request History</strong></em></p>
<blockquote>
<p>1. User opens the Material Request History screen.</p>
<p>2. System displays requests visible to the user (GBR-119) with status, requester, approver and issued/approved quantities, sorted by creation time (newest first) (GBR-015).</p>
<p>3. User filters by status, department, requester, item and date range or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects a request.</p>
<p>6. System displays its timeline: submissions, reviews, decisions with approved quantities, issue and return documents, linked work, and the remaining quantity (GBR-008).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-008, GBR-015, GBR-119</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 10. Material Replenishment Management (FE-10)

### 10.1 UC-100 Create Replenishment Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-100 Create Replenishment Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Stock is below the minimum level or cannot cover approved demand.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper to request replenishment of materials that are below minimum stock or short for approved requests.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Incoming goods are received with UC-079/UC-080 linked to this request. Purchasing approval is outside V1 scope (GBR-017).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Replenishment Request</strong></em></p>
<blockquote>
<p>1. Storekeeper opens Replenishment and selects Create.</p>
<p>2. System proposes lines for items below minimum in the warehouse and for unfulfilled approved quantities, with suggested quantity (GBR-120).</p>
<p>3. Storekeeper adjusts lines (item, unit, quantity), enters supplier name and expected date (optional) and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-120, GBR-095, GBR-093, GBR-004).</p>
<p>5. System creates the replenishment request with status Requested.</p>
<p>6. System saves the replenishment request and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Operations Manager for information (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Shortage Already Requested</strong></em></p>
<p>An open replenishment already covers the same item and shortage.</p>
<ol type="1">
<li><p>System shows the existing request (MSG03, GBR-120).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-010, GBR-017, GBR-093, GBR-095, GBR-120</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 10.2 UC-101 Track Material Replenishment

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-101 Track Material Replenishment</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to follow the delivery of a replenishment request.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to track replenishment requests: requested, received and remaining quantities, and which material requests are still waiting.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Status changes only through posted receipts or closure.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Track Material Replenishment</strong></em></p>
<blockquote>
<p>1. User opens the Replenishment Tracking screen.</p>
<p>2. System displays replenishment requests with status, requested, received and remaining quantities, sorted by expected date (GBR-015).</p>
<p>3. User filters by status, warehouse, item, supplier and date range or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects a request.</p>
<p>6. System displays its lines, linked posted receipts, and the material requests waiting for these items (GBR-121).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Close Remaining Quantity</strong></em></p>
<p>The remaining quantity will not arrive and Storekeeper selects Close.</p>
<ol type="1">
<li><p>Storekeeper enters the reason (GBR-122).</p></li>
<li><p>System changes the request to Closed.</p></li>
<li><p>System saves the closure and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-015, GBR-121, GBR-122</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 11. Task Assignment Management (FE-11)

### 11.1 UC-102 Create Operational Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-102 Create Operational Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A department needs ad-hoc or recurring operational work done.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head to create an ad-hoc or recurring operational task with category, location, expected outcome and completion criteria.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task (or series) exists and waits for assignment.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Operational Task</strong></em></p>
<blockquote>
<p>1. Department Head opens Tasks and selects Create.</p>
<p>2. System displays the form: title, category, department, location (optional), description, expected outcome, completion criteria (mandatory flag), priority, planned start, deadline, source (internal request, optional).</p>
<p>3. Department Head enters the information and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-123, GBR-125, GBR-003, GBR-004, GBR-014).</p>
<p>5. System creates the task with status Pending Assignment.</p>
<p>6. System saves the task and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Recurring Task</strong></em></p>
<p>Department Head selects Repeat.</p>
<ol type="1">
<li><p>System displays recurrence fields: rule (daily, weekly on days, monthly on day), default assignee, end date (GBR-124).</p></li>
<li><p>Department Head enters them and selects Save.</p></li>
<li><p>System validates the input and business conditions (GBR-124, GBR-126).</p></li>
<li><p>System creates an Active task series; occurrences are generated automatically before each due date (GBR-124).</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 3.2_Assign Now</strong></em></p>
<p>Department Head selects Save and Assign.</p>
<ol type="1">
<li><p>System continues with UC-104 after Step 6.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Specialised Process Applies</strong></em></p>
<p>The need is cleaning, an incident or materials.</p>
<ol type="1">
<li><p>System advises to use UC-028, UC-049 or UC-091 (MSG03, GBR-123).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-014, GBR-123, GBR-124, GBR-125, GBR-126</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.2 UC-103 Set Task Priority and Deadline

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-103 Set Task Priority and Deadline</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The priority or deadline of a task must be set or changed, or the task is no longer needed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head to set or change a task's priority, planned start, deadline and expected duration, or to cancel a task that is no longer needed.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is not Closed or Cancelled.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task plan is updated and visible to the assignee.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Set Task Priority and Deadline</strong></em></p>
<blockquote>
<p>1. Department Head opens the task and selects Plan.</p>
<p>2. System displays priority, planned start, deadline, expected duration and the deadline history.</p>
<p>3. Department Head changes the values (with a reason when the task is already assigned) and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-125, GBR-014, GBR-005).</p>
<p>5. System saves the planning values keeping the previous deadline and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System sends an in-app notification to the assignee, if any (GBR-010).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Cancel Task</strong></em></p>
<p>Department Head selects Cancel Task.</p>
<ol type="1">
<li><p>System checks that no work time has been recorded (GBR-128).</p></li>
<li><p>System asks User to confirm the cancellation of the task (MSG33).</p></li>
<li><p>Department Head enters the reason (GBR-012).</p></li>
<li><p>System changes the task to Cancelled.</p></li>
<li><p>System saves the cancellation and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the assignee, if any (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 3.2_Deactivate Task Series</strong></em></p>
<p>The task belongs to a series and Department Head selects Stop Series.</p>
<ol type="1">
<li><p>System sets the series Inactive; already generated tasks remain (GBR-124).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Reason Missing</strong></em></p>
<p>The deadline of an assigned task changes without reason.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-125).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-014, GBR-124, GBR-125, GBR-128</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.3 UC-104 Assign/Reassign Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-104 Assign/Reassign Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A task needs an executor or must be given to another executor.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head to assign a task to an executor or move it to another executor, considering workload.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is Pending Assignment, Assigned, Accepted, In Progress or Paused.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The assignee must accept the task (UC-107).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Assign/Reassign Task</strong></em></p>
<blockquote>
<p>1. Department Head opens the task and selects Assign.</p>
<p>2. System lists eligible executors of the department with their open tasks and time logged today (GBR-126).</p>
<p>3. Department Head selects the executor (and a reason when reassigning) and selects Assign.</p>
<p>4. System validates the input and business conditions (GBR-126, GBR-127, GBR-005).</p>
<p>5. System closes the previous assignee's open work interval, if any, sets the new assignee and changes the task to Assigned (GBR-127, GBR-138).</p>
<p>6. System saves the assignment with previous and new assignee and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the new (and previous) assignee (UC-124) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<p>The executor is inactive, lacks an executor role or is outside the department/branch.</p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-126, GBR-127, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.4 UC-105 Review and Verify Task Completion

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-105 Review and Verify Task Completion</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An executor has completed a task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head to review a submitted task result and close it, or return it for further work.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is Pending Verification.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task is Closed, or returned to the assignee.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Review and Verify Task Completion</strong></em></p>
<blockquote>
<p>1. Department Head opens the task from Pending Verification.</p>
<p>2. System displays completion criteria results, notes, evidence and time spent.</p>
<p>3. Department Head selects Accept and enters a verification note.</p>
<p>4. System validates the input and business conditions (GBR-133, GBR-013, GBR-005).</p>
<p>5. System changes the task from Pending Verification to Closed.</p>
<p>6. System saves the verification and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the assignee (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Return for More Work</strong></em></p>
<p>The result is not acceptable.</p>
<ol type="1">
<li><p>Department Head selects Return and enters what must be fixed (GBR-012).</p></li>
<li><p>System changes the task from Pending Verification to Accepted for the same assignee (GBR-133).</p></li>
<li><p>System saves the return and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the assignee (GBR-010).</p></li>
<li><p>System displays MSG20; the flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-013, GBR-133</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.5 UC-106 Reopen Assigned Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-106 Reopen Assigned Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The result of a closed task is found to be unsatisfactory.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head to reopen a closed task whose result turns out to be unsatisfactory.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is Closed.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task must be accepted again by the assignee.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reopen Assigned Task</strong></em></p>
<blockquote>
<p>1. Department Head opens the closed task and selects Reopen.</p>
<p>2. System displays the task result and history.</p>
<p>3. Department Head enters the reason, remediation instructions and the assignee (same or new) and selects Reopen.</p>
<p>4. System validates the input and business conditions (GBR-134, GBR-126, GBR-012).</p>
<p>5. System starts a new cycle: the task changes from Closed to Assigned; previous results are kept.</p>
<p>6. System saves the reopening and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the assignee (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; only when a closed problem occurs again.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-126, GBR-134</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.6 UC-107 Accept Assigned Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-107 Accept Assigned Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An executor has been assigned a task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assigned executor to accept a task (confirming responsibility) or decline it with a reason.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is Assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task is Accepted and can be started.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Accept Assigned Task</strong></em></p>
<blockquote>
<p>1. Executor opens the task from My Work or the notification.</p>
<p>2. System displays the task details, criteria, priority and deadline.</p>
<p>3. Executor selects Accept.</p>
<p>4. System validates the input and business conditions (GBR-129, GBR-005).</p>
<p>5. System changes the task from Assigned to Accepted.</p>
<p>6. System saves the acceptance and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Decline Task</strong></em></p>
<p>Executor cannot do the task.</p>
<ol type="1">
<li><p>Executor selects Decline and enters the reason (GBR-129).</p></li>
<li><p>System changes the task to Pending Assignment and removes the assignee.</p></li>
<li><p>System saves the decline and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the Department Head (GBR-010).</p></li>
<li><p>System displays MSG19; the flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-129</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.7 UC-108 Start Assigned Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-108 Start Assigned Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The executor is ready to work on an accepted task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assignee to start an accepted task; the system starts recording working time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is Accepted and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task is In Progress.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Start Assigned Task</strong></em></p>
<blockquote>
<p>1. Executor opens the task and selects Start.</p>
<p>2. System validates the input and business conditions (GBR-130, GBR-136, GBR-137, GBR-005).</p>
<p>3. System changes the task from Accepted to In Progress, sets the actual start (first time) and opens a work interval (GBR-138).</p>
<p>4. System saves the task and interval and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>5. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Workday Not Started or Busy</strong></em></p>
<p>User has no Working workday or another open work interval.</p>
<ol type="1">
<li><p>System displays MSG30 or MSG31.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-130, GBR-136, GBR-137, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.8 UC-109 Update Task Progress

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-109 Update Task Progress</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The executor has made progress on a task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assignee to record progress, results of completion criteria, notes and evidence while working.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is In Progress or Paused and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Progress is updated; reaching 100% does not complete the task.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Task Progress</strong></em></p>
<blockquote>
<p>1. Executor opens the task and selects Update Progress.</p>
<p>2. System displays progress, completion criteria and notes.</p>
<p>3. Executor enters progress, criterion results, notes and files and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-050, GBR-016, GBR-005).</p>
<p>5. System saves the progress and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Invalid Attachment</strong></em></p>
<p>A file violates the policy.</p>
<ol type="1">
<li><p>System displays MSG23.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-016, GBR-050</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.9 UC-110 Pause/Resume Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-110 Pause/Resume Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The executor must interrupt a task or is ready to continue it.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assignee to pause an in-progress task with a reason and resume it later; paused time is not counted as work time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is In Progress or Paused and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Pause periods are visible in the task timeline and excluded from effort (GBR-140).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Pause/Resume Task</strong></em></p>
<blockquote>
<p>1. Executor opens the In Progress task and selects Pause.</p>
<p>2. System displays pause reasons (reference data) and a note field.</p>
<p>3. Executor selects the reason and confirms.</p>
<p>4. System validates the input and business conditions (GBR-131, GBR-005).</p>
<p>5. System changes the task to Paused and closes the work interval (GBR-138).</p>
<p>6. System saves the pause and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Resume</strong></em></p>
<p>The task is Paused and Executor selects Resume.</p>
<ol type="1">
<li><p>System validates the input and business conditions (GBR-130, GBR-136, GBR-137).</p></li>
<li><p>System changes the task to In Progress and opens a new work interval.</p></li>
<li><p>System saves the resume and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-130, GBR-131, GBR-136, GBR-137, GBR-138, GBR-140</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.10 UC-112 Complete Assigned Task

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-112 Complete Assigned Task</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The executor has finished a task.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the assignee to submit a task as finished with results, evidence and handover notes, for verification.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The task is In Progress and assigned to User.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The task waits for verification.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Complete Assigned Task</strong></em></p>
<blockquote>
<p>1. Executor opens the task and selects Complete.</p>
<p>2. System displays the completion criteria, result note, evidence and handover note fields.</p>
<p>3. Executor enters the results and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-132, GBR-016, GBR-005).</p>
<p>5. System changes the task from In Progress to Pending Verification, sets the actual end and closes the work interval (GBR-138).</p>
<p>6. System saves the results and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Department Head for verification (UC-105) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Mandatory Criteria Missing</strong></em></p>
<p>A mandatory criterion has no result.</p>
<ol type="1">
<li><p>System lists them (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-016, GBR-132, GBR-138</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 11.11 UC-113 Review Task Workload and Time Logs

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-113 Review Task Workload and Time Logs</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to review workload and recorded working time.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head and Operations Manager to review task workload and time logs by employee, status, priority and deadline.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Review Task Workload and Time Logs</strong></em></p>
<blockquote>
<p>1. User opens the Workload screen.</p>
<p>2. System displays per employee: open tasks, overdue tasks, tasks closed in the period, total effort from closed work intervals and provisional open time (GBR-140, GBR-135), sorted by employee name (GBR-015).</p>
<p>3. User filters by department, employee, status, priority, deadline and period or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects an employee.</p>
<p>6. System displays the employee's tasks and time log for the period; shift time is shown separately from task time (GBR-140).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ul>
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ul></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per week.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-135, GBR-140</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 12. Work-Time & Workday Tracking (FE-12)

### 12.1 UC-111 Record Actual Working Time

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-111 Record Actual Working Time</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to review recorded working time or correct a wrong time record.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows executors to review the working time recorded automatically for their work items and request corrections; the Department Head applies justified corrections.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Time records come from system events; corrections are traceable.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Record Actual Working Time</strong></em></p>
<blockquote>
<p>1. Executor opens My Time Log.</p>
<p>2. System displays the user's work intervals for the selected day (task, cleaning, rework, incident, maintenance occurrence; start, end, duration) and the workday total (GBR-138, GBR-140).</p>
<p>3. Executor selects an interval that is wrong and selects Request Correction.</p>
<p>4. Executor enters the proposed start/end and the reason and selects Submit.</p>
<p>5. System validates the input and business conditions (GBR-139, GBR-012, GBR-014).</p>
<p>6. System saves the correction request as Pending and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Department Head (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 1.1_Apply Correction</strong></em></p>
<p>Department Head opens a Pending correction request.</p>
<ol type="1">
<li><p>System displays the original interval, proposed values and overlapping intervals.</p></li>
<li><p>Department Head approves (optionally adjusting values) or rejects with a reason.</p></li>
<li><p>System validates the input and business conditions (GBR-139, GBR-013, GBR-014).</p></li>
<li><p>System updates the interval (keeping old values) and sets the request Applied, or sets it Rejected.</p></li>
<li><p>System saves the decision and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the executor (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 5.1_Overlap or Invalid Time</strong></em></p>
<p>The proposed interval overlaps another or ends before it starts.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-139).</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Daily; working time is recorded automatically and reviewed or corrected when needed.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-013, GBR-014, GBR-138, GBR-139, GBR-140</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 12.2 UC-138 Manage Operational Workday and Shift Records

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-138 Manage Operational Workday and Shift Records</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Shifts and shift assignments of a department must be planned or changed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Department Head to define shifts and assign employees of the department to shifts by date, for operational coordination (not payroll).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Employees can start their workday for assigned shifts (UC-139). Data is not used for payroll (GBR-017).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Operational Workday and Shift Records</strong></em></p>
<blockquote>
<p>1. Department Head opens Shifts.</p>
<p>2. System displays the shift definitions and the weekly roster of the department.</p>
<p>3. Department Head creates or edits a shift definition (code, name, start, end) or assigns employees to shifts on dates and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-141, GBR-003, GBR-004, GBR-005).</p>
<p>5. System saves the shifts and assignments and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System sends an in-app notification to the affected employees (GBR-010).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Cancel Assignment</strong></em></p>
<p>Department Head removes a future assignment.</p>
<ol type="1">
<li><p>System sets it Cancelled (kept in history).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Overlapping Assignment</strong></em></p>
<p>An employee would have overlapping shifts.</p>
<ol type="1">
<li><p>System displays the conflict (MSG03, GBR-141).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Weekly, when shifts are planned.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-005, GBR-007, GBR-008, GBR-010, GBR-017, GBR-141</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 12.3 UC-139 Record Workday Start and End

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-139 Record Workday Start and End</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Staff, Maintenance Staff, Storekeeper, Housekeeping Supervisor, Maintenance Lead</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An employee starts or ends the workday.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows an employee to record the start and end of the workday/shift; workday time is kept separate from task time recorded in UC-111.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Workday start/end are recorded; task time remains separate (GBR-140).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Record Workday Start and End</strong></em></p>
<blockquote>
<p>1. User selects Start Workday on the home page.</p>
<p>2. System shows today's shift assignment, if any.</p>
<p>3. User confirms.</p>
<p>4. System validates the input and business conditions (GBR-142).</p>
<p>5. System creates the workday with status Working and start time = server time (GBR-014).</p>
<p>6. System saves the workday and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
<p>8. At the end of the shift User selects End Workday.</p>
<p>9. System validates the input and business conditions (GBR-143).</p>
<p>10. System sets the end time and changes the workday to Ended.</p>
<p>11. System saves the workday end and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>12. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 4.1_No Shift Assignment</strong></em></p>
<p>User has no shift assignment today.</p>
<ul>
<li><p>System asks for a reason; the workday is flagged Unscheduled and the Department Head is notified (GBR-142).</p></li>
<li><p>Return to step 5 of the normal flow.</p></li>
</ul>
<p><em><strong>Step 8.1_Hand Over Shift</strong></em></p>
<p>User has open items to pass on.</p>
<ol type="1">
<li><p>System suggests UC-140 before ending the workday.</p></li>
<li><p>Return to step 8 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.2_Already Working</strong></em></p>
<p>User already has a Working workday.</p>
<ol type="1">
<li><p>System displays MSG03.</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 9.1_Open Work Item</strong></em></p>
<p>User still has an open work interval.</p>
<ol type="1">
<li><p>System displays MSG31 and offers Pause All.</p></li>
<li><p>If User selects Pause All, System pauses/holds every open item with reason 'end of shift' and closes the intervals (GBR-138).</p></li>
<li><p>Return to step 10 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Twice per employee per workday (start and end).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-014, GBR-138, GBR-140, GBR-142, GBR-143</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 12.4 UC-140 Manage Operational Shift Handover

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-140 Manage Operational Shift Handover</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>Receiving employee</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A shift ends and open work must be passed to the next shift.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows an employee ending a shift to hand over open work and notes to the next person, who acknowledges or rejects the handover.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> User has a Working workday.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The receiver is formally aware of open work; the snapshot stays unchanged.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Manage Operational Shift Handover</strong></em></p>
<blockquote>
<p>1. User selects Shift Handover.</p>
<p>2. System prepares a snapshot of User's open items: tasks, cleaning/rework tasks, incidents, internal requests and, for leads, unready rooms (GBR-144).</p>
<p>3. User selects the receiver (an employee of the same department on the next shift), adds notes per item and general notes, and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-144, GBR-004).</p>
<p>5. System creates the handover as Pending Acknowledgment with the snapshot.</p>
<p>6. System saves the handover and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the receiver (GBR-010).</p>
<p>8. Receiver opens the handover, reviews the items and selects Acknowledge.</p>
<p>9. System changes the handover to Acknowledged and records the time (GBR-144).</p>
<p>10. System saves the acknowledgment and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>11. System sends an in-app notification to the sender and the Department Head (GBR-010).</p>
<p>12. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 8.1_Reject Handover</strong></em></p>
<p>The receiver finds it incomplete.</p>
<ol type="1">
<li><p>Receiver selects Reject and enters the reason (GBR-012).</p></li>
<li><p>System changes the handover to Rejected and notifies the sender, who may resubmit a new version.</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 9.1_Reassign Items</strong></em></p>
<p>A lead wants an item to be done by the receiver.</p>
<ol type="1">
<li><p>The lead uses the corresponding assign use case (UC-030, UC-053, UC-104, UC-115); the handover itself does not change assignees (GBR-144).</p></li>
<li><p>Return to step 10 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Once per shift change.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-010, GBR-012, GBR-144</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 13. Internal Request Management (FE-13)

### 13.1 UC-114 Create Internal Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-114 Create Internal Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A department needs support from another department.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows an employee to send a non-material request to another department (e.g. extra towels delivered to a room, move furniture for an event, fix a lobby display) with type, priority and desired deadline.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The receiving Department Head decides on the request (UC-115/UC-116).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Internal Request</strong></em></p>
<blockquote>
<p>1. User opens Internal Requests and selects Create.</p>
<p>2. System displays the form: request type, receiving department, title, description, location (optional), priority, desired deadline, attachments.</p>
<p>3. User enters the information and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-145, GBR-003, GBR-004, GBR-016, GBR-014).</p>
<p>5. System creates the request with status Pending Acceptance for the receiving department.</p>
<p>6. System saves the request and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the Department Head of the receiving department (UC-124) (GBR-010).</p>
<p>8. System displays a success message (MSG09).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Material Need</strong></em></p>
<p>The request type is a material need.</p>
<ol type="1">
<li><p>System advises to create a material request instead (UC-091) (MSG03, GBR-110).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 4.1_Invalid Data</strong></em></p>
<ol type="1">
<li><p>System highlights the invalid fields and shows the reason (MSG03).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-010, GBR-014, GBR-016, GBR-110, GBR-145</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.2 UC-115 Route and Assign Internal Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-115 Route and Assign Internal Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An accepted internal request needs a handler, or the request belongs to another department.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the receiving Department Head to route a request to the right department, or to assign/reassign the handler of an accepted request.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> User is the Department Head of the current receiving department.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request has one handler responsible for it.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Route and Assign Internal Request</strong></em></p>
<blockquote>
<p>1. Department Head opens the request.</p>
<p>2. System displays the request, its history and the handlers of the department with their open requests.</p>
<p>3. Department Head selects a handler and selects Assign.</p>
<p>4. System validates the input and business conditions (GBR-148, GBR-004, GBR-005).</p>
<p>5. System sets the handler; an Accepted request changes to In Progress (GBR-148).</p>
<p>6. System saves the assignment and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the handler and the requester (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Route to Another Department</strong></em></p>
<p>The request belongs to another department and is Pending Acceptance.</p>
<ol type="1">
<li><p>Department Head selects Route, the target department and a reason (GBR-147, GBR-012).</p></li>
<li><p>System changes the receiving department; the request stays Pending Acceptance.</p></li>
<li><p>System saves the routing and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the new receiving Department Head and the requester (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol>
<p><em><strong>Step 4.1_Request Not Accepted Yet</strong></em></p>
<p>The request is still Pending Acceptance.</p>
<ol type="1">
<li><p>System opens UC-116 to accept it first.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-147, GBR-148</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.3 UC-116 Accept/Reject Internal Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-116 Accept/Reject Internal Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An internal request arrives at the receiving department.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the receiving Department Head to accept or reject a request, recording the reason and response time.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Pending Acceptance and User is the Department Head of the receiving department.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request is Accepted (then assigned) or Rejected.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Accept/Reject Internal Request</strong></em></p>
<blockquote>
<p>1. Department Head opens the request.</p>
<p>2. System displays the request details and its age.</p>
<p>3. Department Head selects Accept (optionally adjusting the deadline with a reason).</p>
<p>4. System validates the input and business conditions (GBR-146, GBR-014, GBR-005).</p>
<p>5. System changes the request to Accepted and records the response time.</p>
<p>6. System saves the decision and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester (UC-125) (GBR-010).</p>
<p>8. System offers to assign a handler (UC-115).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Reject</strong></em></p>
<p>The request cannot be served.</p>
<ol type="1">
<li><p>Department Head selects Reject and enters the reason (GBR-146).</p></li>
<li><p>System changes the request to Rejected.</p></li>
<li><p>System saves the decision and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the requester (GBR-010).</p></li>
<li><p>System displays MSG19; the flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-014, GBR-146</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.4 UC-117 Update Internal Request Progress

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-117 Update Internal Request Progress</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The handler makes progress on an internal request.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the handler to update progress, notes and results, and report issues affecting the deadline.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is In Progress and User is its handler or the receiving Department Head.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Progress is visible to the requester; the request stays In Progress.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Update Internal Request Progress</strong></em></p>
<blockquote>
<p>1. User opens the request and selects Update Progress.</p>
<p>2. System displays progress, notes, linked work and attachments.</p>
<p>3. User enters progress, notes or files and selects Save.</p>
<p>4. System validates the input and business conditions (GBR-149, GBR-050, GBR-016, GBR-005).</p>
<p>5. System saves the progress and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>6. System sends an in-app notification to the requester when progress reaches 100% (ready for completion) (GBR-010).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Change Deadline</strong></em></p>
<p>The receiving Department Head changes the deadline.</p>
<ul>
<li><p>Department Head enters the new deadline and reason (GBR-149).</p></li>
<li><p>Return to step 4 of the normal flow.</p></li>
</ul></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not Allowed to Change Deadline</strong></em></p>
<p>A handler who is not the Department Head changes the deadline.</p>
<ol type="1">
<li><p>System displays MSG02.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-016, GBR-050, GBR-149</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.5 UC-118 Create Linked Operational Work

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-118 Create Linked Operational Work</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head, Housekeeping Supervisor</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An internal request needs operational work to be carried out.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the receiving side to create or link the operational work needed to fulfil a request (cleaning task, incident, material request or operational task).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Accepted or In Progress.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Linked work is traceable from the request; the request is not closed automatically.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Create Linked Operational Work</strong></em></p>
<blockquote>
<p>1. User opens the request and selects Linked Work.</p>
<p>2. System displays linked records and the options Link Existing or Create New (cleaning task, incident, material request, operational task).</p>
<p>3. User selects Create New and the record type.</p>
<p>4. System opens the target use case (UC-028, UC-049, UC-091 or UC-102) prefilled from the request; permissions of that use case apply (GBR-150, GBR-002).</p>
<p>5. After the record is saved, System links it to the request and marks whether it is mandatory for completion.</p>
<p>6. System saves the link and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 3.1_Link Existing Record</strong></em></p>
<p>User selects Link Existing.</p>
<ol type="1">
<li><p>System lists open records of the selected type in the branch that User can view.</p></li>
<li><p>User selects one; return to Step 6.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Permission for Target</strong></em></p>
<p>User lacks the permission of the target use case (e.g. a Department Head creating a cleaning task).</p>
<ol type="1">
<li><p>System displays MSG02 and suggests linking an existing record or asking the responsible role (GBR-150).</p></li>
<li><p>Return to step 2 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-150</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.6 UC-119 Record Request Handover

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-119 Record Request Handover</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>Designated new handler</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The handler of an internal request can no longer handle it.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the current handler to hand over a request to another handler (staff change or shift change); responsibility moves only after acknowledgment.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is In Progress and User is its handler.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request always has exactly one responsible handler.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Record Request Handover</strong></em></p>
<blockquote>
<p>1. Handler opens the request and selects Hand Over.</p>
<p>2. System displays eligible handlers of the receiving department.</p>
<p>3. Handler selects the new handler, enters the status summary and notes and selects Submit.</p>
<p>4. System validates the input and business conditions (GBR-151, GBR-004).</p>
<p>5. System creates a handover Pending Acknowledgment; the current handler stays responsible (GBR-151).</p>
<p>6. System saves the handover and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the designated new handler (GBR-010).</p>
<p>8. New handler opens the handover and selects Acknowledge.</p>
<p>9. System changes the handover to Acknowledged and sets the new handler on the request.</p>
<p>10. System saves the acknowledgment and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>11. System sends an in-app notification to the previous handler and the receiving Department Head (GBR-010).</p>
<p>12. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 8.1_Reject Handover</strong></em></p>
<p>The new handler cannot take it.</p>
<ol type="1">
<li><p>New handler selects Reject with a reason; System sets the handover Rejected; the previous handler remains (GBR-151).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">A few times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-004, GBR-007, GBR-008, GBR-010, GBR-151</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.7 UC-120 Complete Internal Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-120 Complete Internal Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The work of an internal request has been finished.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the receiving Department Head to confirm that a request is fulfilled, record the result and close it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is In Progress.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request is Closed; the requester may reopen it within SYS-PAR-18 days.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Complete Internal Request</strong></em></p>
<blockquote>
<p>1. Department Head opens the request and selects Complete.</p>
<p>2. System displays linked work with statuses and pending handovers.</p>
<p>3. Department Head enters the result note and selects Close.</p>
<p>4. System validates the input and business conditions (GBR-152, GBR-003, GBR-005).</p>
<p>5. System changes the request from In Progress to Closed and closes its Open escalation, if any.</p>
<p>6. System saves the closure and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester (UC-125) (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Open Mandatory Work or Pending Handover</strong></em></p>
<p>Linked mandatory work is not closed or a handover is pending.</p>
<ol type="1">
<li><p>System lists them (MSG34, GBR-152).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">High</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-003, GBR-005, GBR-007, GBR-008, GBR-010, GBR-152</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.8 UC-121 Reopen Internal Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-121 Reopen Internal Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The result of a closed internal request is unsatisfactory.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the requester (or the requesting Department Head) to reopen a closed request when the result is unsatisfactory.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is Closed and User is its requester or the Department Head of the requesting department.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The request is In Progress again.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Reopen Internal Request</strong></em></p>
<blockquote>
<p>1. User opens the request and selects Reopen.</p>
<p>2. System displays the result and the reopen window (GBR-153).</p>
<p>3. User enters what is unsatisfactory and selects Reopen.</p>
<p>4. System validates the input and business conditions (GBR-153, GBR-012).</p>
<p>5. System changes the request from Closed to In Progress with the last handler; previous results and handovers are kept.</p>
<p>6. System saves the reopening and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the handler and the receiving Department Head (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Window Expired</strong></em></p>
<p>The request was closed more than SYS-PAR-18 days ago.</p>
<ol type="1">
<li><p>System displays MSG03 and suggests a new request (UC-114).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Rarely; only when a closed problem occurs again.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-012, GBR-153</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.9 UC-122 Escalate Overdue Internal Request

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-122 Escalate Overdue Internal Request</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An internal request is overdue or at risk of being overdue.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager to intervene on an overdue or at-risk request: change priority or deadline, reroute it or change its handler.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> The request is not Closed, Rejected or Cancelled and is overdue or at risk (GBR-156).</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> The escalation is closed automatically when the request is closed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Escalate Overdue Internal Request</strong></em></p>
<blockquote>
<p>1. Operations Manager opens Overdue Requests (or the overdue alert) and selects a request.</p>
<p>2. System displays age, deadline, handler, progress and history.</p>
<p>3. Operations Manager selects Escalate and enters the action: new priority, new deadline, new receiving department or new handler, plus the reason.</p>
<p>4. System validates the input and business conditions (GBR-155, GBR-012, GBR-005).</p>
<p>5. System creates an Open escalation and applies the selected changes.</p>
<p>6. System saves the escalation and changes and records the audit log in the same transaction (GBR-007, GBR-008).</p>
<p>7. System sends an in-app notification to the requester, the receiving Department Head and the handler; the alert requires acknowledgment (GBR-010).</p>
<p>8. System displays a success message (MSG08).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_Not Eligible or Already Escalated</strong></em></p>
<p>The request is not overdue/at risk or already has an Open escalation.</p>
<ol type="1">
<li><p>System displays MSG03 (GBR-155).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when a request becomes overdue.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-005, GBR-007, GBR-008, GBR-010, GBR-012, GBR-155, GBR-156</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 13.10 UC-123 View Internal Request History

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-123 View Internal Request History</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Administrator, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user needs to follow internal requests, or Operations Manager needs to select a request to handle.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to look up internal requests they sent, received or handle, with status, handler, deadline, history and results; the requester can withdraw a request not yet accepted. It is the base list for handling internal requests (UC-115, UC-116, UC-117, UC-119, UC-120, UC-121, UC-122).</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed except a withdrawal.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Internal Request History</strong></em></p>
<blockquote>
<p>1. User opens the Internal Requests screen.</p>
<p>2. System displays requests visible to the user with status, receiving department, handler, deadline and overdue flag (GBR-156), sorted by deadline (GBR-015).</p>
<p>3. User filters by direction (sent/received), status, type, priority, department and overdue flag or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects a request.</p>
<p>6. System displays the timeline: routing, decisions, handlers, handovers, progress, linked work (only records User may view), escalations and result.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Withdraw Request</strong></em></p>
<p>The request is Pending Acceptance and User is its requester.</p>
<ol type="1">
<li><p>User selects Withdraw and enters a reason (GBR-154).</p></li>
<li><p>System changes the request to Cancelled.</p></li>
<li><p>System saves the withdrawal and records the audit log in the same transaction (GBR-007, GBR-008).</p></li>
<li><p>System sends an in-app notification to the receiving Department Head (GBR-010).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Several times per day.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-007, GBR-008, GBR-010, GBR-015, GBR-154, GBR-156</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 14. Notification & Operational Dashboard (FE-14)

### 14.1 UC-124 Receive Assignment Notifications

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-124 Receive Assignment Notifications</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A work item is assigned, reassigned or handed over to the user.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to receive notifications about new or changed assignments of work to them.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A business event listed below has been committed and User is a recipient.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> User is informed; the source record is not changed by reading the notification (GBR-011).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Receive Assignment Notifications</strong></em></p>
<blockquote>
<p>1. System creates an in-app notification for each recipient when one of these events is committed: cleaning task assigned/reassigned (UC-029, UC-030), rework requested (UC-046), inspection assigned (UC-041), maintenance assigned (UC-053), maintenance occurrence generated (UC-062), task assigned/reassigned/reopened (UC-104, UC-106), internal request received/routed/assigned (UC-114, UC-115), handover submitted (UC-069, UC-119, UC-140) (GBR-010).</p>
<p>2. System shows the unread badge and the notification in the user's list.</p>
<p>3. User selects the notification.</p>
<p>4. System re-checks User's permission and opens the source record (GBR-157).</p>
<p>5. System marks the notification as Read (GBR-159).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_Source No Longer Accessible</strong></em></p>
<p>User's permission or scope on the source record has changed.</p>
<ol type="1">
<li><p>System shows the notification text but denies opening the record (MSG02, GBR-157).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Automatically, every time the triggering event occurs (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-010, GBR-011, GBR-157, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.2 UC-125 Receive Approval Notifications

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-125 Receive Approval Notifications</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A request involving the user is submitted, approved, rejected or returned.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to receive notifications about decisions on their submissions.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A business event listed below has been committed and User is a recipient.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> User is informed; the source record is not changed by reading the notification (GBR-011).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Receive Approval Notifications</strong></em></p>
<blockquote>
<p>1. System creates an in-app notification for each recipient when one of these events is committed: material request submitted, approved, rejected, returned for information, issued or remainder closed (UC-091, UC-094, UC-095, UC-097, UC-098), internal request accepted, rejected or closed (UC-116, UC-120), task verified or returned (UC-105), incident result returned or closed (UC-059), time correction decided (UC-111) (GBR-010).</p>
<p>2. System shows the unread badge and the notification in the user's list.</p>
<p>3. User selects the notification.</p>
<p>4. System re-checks User's permission and opens the source record (GBR-157).</p>
<p>5. System marks the notification as Read (GBR-159).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_Source No Longer Accessible</strong></em></p>
<p>User's permission or scope on the source record has changed.</p>
<ol type="1">
<li><p>System shows the notification text but denies opening the record (MSG02, GBR-157).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Automatically, every time the triggering event occurs (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-010, GBR-011, GBR-157, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.3 UC-126 Receive Inspection and Incident Alerts

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-126 Receive Inspection and Incident Alerts</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Maintenance Lead, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">An inspection fails, an incident is critical, a room is blocked or an incident is escalated.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows supervisors to receive alerts about failed inspections, critical incidents, room blocks and escalations that affect room readiness.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A business event listed below has been committed and User is a recipient.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> User is informed; the source record is not changed by reading the notification (GBR-011).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Receive Inspection and Incident Alerts</strong></em></p>
<blockquote>
<p>1. System creates an in-app notification for each recipient when one of these events is committed: inspection failed (UC-045), cleaning blocker reported (UC-033), incident reported (UC-049), Critical severity set (UC-050), room blocked (UC-056), incident escalated (UC-057) (GBR-010).</p>
<p>2. System shows the unread badge and the notification in the user's list.</p>
<p>3. User selects the notification.</p>
<p>4. System re-checks User's permission and opens the source record (GBR-157).</p>
<p>5. System marks the notification as Read (GBR-159).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Acknowledgment Required</strong></em></p>
<p>The alert is a Critical incident or an escalation.</p>
<ol type="1">
<li><p>System keeps the alert highlighted until User acknowledges it (UC-130) (GBR-159).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_Source No Longer Accessible</strong></em></p>
<p>User's permission or scope on the source record has changed.</p>
<ol type="1">
<li><p>System shows the notification text but denies opening the record (MSG02, GBR-157).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Automatically, every time the triggering event occurs (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-010, GBR-011, GBR-157, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.4 UC-127 Receive Overdue Work Alerts

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-127 Receive Overdue Work Alerts</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Housekeeping Supervisor, Maintenance Lead, Department Head, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The scheduled check (JOB-01) finds work that is overdue or at risk.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows responsible users to receive alerts for overdue cleaning tasks, incidents, operational tasks and internal requests.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A business event listed below has been committed and User is a recipient.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> User is informed; the source record is not changed by reading the notification (GBR-011).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Receive Overdue Work Alerts</strong></em></p>
<blockquote>
<p>1. System creates an in-app notification for each recipient when one of these events is committed: the scheduled check (every SYS-PAR-15 minutes) finds a record entering the overdue or at-risk state (GBR-158, GBR-055, GBR-080, GBR-135, GBR-156); recipients: Housekeeping Supervisor for cleaning/rework, Maintenance Lead for incidents and maintenance occurrences, Department Head for tasks and received internal requests, Operations Manager for escalated items (GBR-010).</p>
<p>2. System shows the unread badge and the notification in the user's list.</p>
<p>3. User selects the notification.</p>
<p>4. System re-checks User's permission and opens the source record (GBR-157).</p>
<p>5. System marks the notification as Read (GBR-159).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_Source No Longer Accessible</strong></em></p>
<p>User's permission or scope on the source record has changed.</p>
<ol type="1">
<li><p>System shows the notification text but denies opening the record (MSG02, GBR-157).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Automatically, at each scheduled check (every 5 minutes) when the condition is met.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-010, GBR-011, GBR-055, GBR-080, GBR-135, GBR-156, GBR-157, GBR-158, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.5 UC-128 Receive Low Stock Alerts

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-128 Receive Low Stock Alerts</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Storekeeper, Operations Manager</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">The scheduled check (JOB-02) finds stock below the minimum level.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Storekeeper (and Operations Manager) to receive alerts when an item falls below its minimum stock in a warehouse.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><p><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</p>
<p><strong>PRE-02:</strong> A business event listed below has been committed and User is a recipient.</p></th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> User is informed; the source record is not changed by reading the notification (GBR-011).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Receive Low Stock Alerts</strong></em></p>
<blockquote>
<p>1. System creates an in-app notification for each recipient when one of these events is committed: the scheduled check finds available quantity below the minimum level of a warehouse/item (GBR-158, GBR-094) (GBR-010).</p>
<p>2. System shows the unread badge and the notification in the user's list.</p>
<p>3. User selects the notification.</p>
<p>4. System re-checks User's permission and opens the source record (GBR-157).</p>
<p>5. System marks the notification as Read (GBR-159).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 5.1_Replenish</strong></em></p>
<p>User opens the alert.</p>
<ol type="1">
<li><p>System offers Create Replenishment (UC-100) prefilled with the item.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 3.1_Source No Longer Accessible</strong></em></p>
<p>User's permission or scope on the source record has changed.</p>
<ol type="1">
<li><p>System shows the notification text but denies opening the record (MSG02, GBR-157).</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Automatically, at each scheduled check (every 5 minutes) when the condition is met.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-010, GBR-011, GBR-094, GBR-157, GBR-158, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.6 UC-129 View Notification Center

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-129 View Notification Center</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Owner, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user wants to review received notifications.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to view, search and filter their notifications by type, time, priority and read status.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Notifications older than SYS-PAR-14 days are no longer listed (GBR-160).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Notification Center</strong></em></p>
<blockquote>
<p>1. User opens the Notification Center.</p>
<p>2. System lists the user's notifications, newest first, with type, title, time, priority and status (Unread/Read/Acknowledged) (GBR-157, GBR-015).</p>
<p>3. User filters by type, period, priority or status.</p>
<p>4. System refreshes the list.</p>
<p>5. User selects a notification to open its source record (see UC-124 to UC-128).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Continuously during each shift (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-157, GBR-160</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.7 UC-130 Acknowledge Notifications

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-130 Acknowledge Notifications</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator, Operations Manager, Owner, Housekeeping Supervisor, Housekeeping Staff, Maintenance Lead, Maintenance Staff, Storekeeper, Department Head</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A user has read or must acknowledge notifications.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to mark notifications as read (individually or all) and acknowledge alerts that require it.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Read/acknowledged status changes only the notification; the source record is unchanged (GBR-011).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Acknowledge Notifications</strong></em></p>
<blockquote>
<p>1. User selects one or more notifications in the Notification Center.</p>
<p>2. User selects Mark as Read or Acknowledge.</p>
<p>3. System validates the input and business conditions (GBR-159, GBR-157).</p>
<p>4. System records the read time or acknowledgment time once per notification (GBR-159).</p>
<p>5. System records acknowledgments of Critical alerts and escalations in the audit log (GBR-008).</p>
<p>6. System updates the unread badge.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.1_Mark All as Read</strong></em></p>
<p>User selects Mark All as Read.</p>
<ol type="1">
<li><p>System marks all unread notifications as Read.</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Continuously during each shift (many times per day).</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-008, GBR-011, GBR-157, GBR-159</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.8 UC-131 View Room Readiness Dashboard

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-131 View Room Readiness Dashboard</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A manager needs an overview of room readiness.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to monitor room readiness: number and percentage of rooms per operational status.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Room Readiness Dashboard</strong></em></p>
<blockquote>
<p>1. User opens the dashboard.</p>
<p>2. System calculates, at one snapshot time and within the user's scope, the number of active rooms per status (Ready, Pending Cleaning, Cleaning In Progress, Pending Inspection, Rework Required, Blocked), the Ready ratio (Ready ÷ active rooms), overdue cleaning tasks and today's first-time inspection pass rate (KPI-01 to KPI-03) (GBR-161, GBR-162).</p>
<p>3. System displays the figures with the filters and snapshot time.</p>
<p>4. User changes filters (branch, department, period).</p>
<p>5. System recalculates the dashboard.</p>
<p>6. User selects a figure to drill down to the Room Status Board (UC-027) filtered by that status.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.2_Zero Denominator</strong></em></p>
<p>A ratio has no records in its denominator.</p>
<ol type="1">
<li><p>System shows N/A for the ratio (GBR-161).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Incomplete Source Data</strong></em></p>
<p>Part of the data cannot be read.</p>
<ol type="1">
<li><p>System shows available figures marked as incomplete and lists the missing sources (MSG29); missing values are not shown as 0.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Daily; several times per day for the Operations Manager.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-161, GBR-162</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.9 UC-132 View Task Workload Dashboard

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-132 View Task Workload Dashboard</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A manager needs an overview of task workload.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to monitor task workload by status, department, employee, priority and overdue state.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Task Workload Dashboard</strong></em></p>
<blockquote>
<p>1. User opens the dashboard.</p>
<p>2. System calculates, at one snapshot time and within the user's scope, open tasks by status and priority, overdue tasks, tasks closed in the period, average completion time and effort from closed work intervals (KPI-04 to KPI-06) (GBR-161, GBR-162).</p>
<p>3. System displays the figures with the filters and snapshot time.</p>
<p>4. User changes filters (branch, department, period).</p>
<p>5. System recalculates the dashboard.</p>
<p>6. User selects a figure to drill down to the Workload screen (UC-113).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.2_Zero Denominator</strong></em></p>
<p>A ratio has no records in its denominator.</p>
<ol type="1">
<li><p>System shows N/A for the ratio (GBR-161).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Incomplete Source Data</strong></em></p>
<p>Part of the data cannot be read.</p>
<ol type="1">
<li><p>System shows available figures marked as incomplete and lists the missing sources (MSG29); missing values are not shown as 0.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Daily; several times per day for the Operations Manager.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-161, GBR-162</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.10 UC-133 View Incident and Maintenance Dashboard

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-133 View Incident and Maintenance Dashboard</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A manager needs an overview of incidents and maintenance.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to monitor incidents and maintenance: open incidents by severity, response and resolution times, SLA compliance, recurring incidents and preventive maintenance.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Incident and Maintenance Dashboard</strong></em></p>
<blockquote>
<p>1. User opens the dashboard.</p>
<p>2. System calculates, at one snapshot time and within the user's scope, open incidents by severity and status, average response and resolution time, SLA compliance ratio, reopened incidents, blocked rooms and preventive occurrences due/overdue (KPI-07 to KPI-10) (GBR-161, GBR-162).</p>
<p>3. System displays the figures with the filters and snapshot time.</p>
<p>4. User changes filters (branch, department, period).</p>
<p>5. System recalculates the dashboard.</p>
<p>6. User selects a figure to drill down to the incident or occurrence list.</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.2_Zero Denominator</strong></em></p>
<p>A ratio has no records in its denominator.</p>
<ol type="1">
<li><p>System shows N/A for the ratio (GBR-161).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Incomplete Source Data</strong></em></p>
<p>Part of the data cannot be read.</p>
<ol type="1">
<li><p>System shows available figures marked as incomplete and lists the missing sources (MSG29); missing values are not shown as 0.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Daily; several times per day for the Operations Manager.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-161, GBR-162</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 14.11 UC-134 View Inventory and Material Dashboard

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-134 View Inventory and Material Dashboard</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A manager needs an overview of inventory and material requests.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows users to monitor inventory: items below minimum, stock movements, pending material requests and open replenishment.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Inventory and Material Dashboard</strong></em></p>
<blockquote>
<p>1. User opens the dashboard.</p>
<p>2. System calculates, at one snapshot time and within the user's scope, items below minimum per warehouse, stock received/issued in the period by item group, outstanding approved quantities not yet issued, open replenishment quantities and transfer losses (KPI-11 to KPI-13) (GBR-161, GBR-162).</p>
<p>3. System displays the figures with the filters and snapshot time.</p>
<p>4. User changes filters (branch, department, period).</p>
<p>5. System recalculates the dashboard.</p>
<p>6. User selects a figure to drill down to the stock balance (UC-089), request history (UC-099) or replenishment tracking (UC-101).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.2_Zero Denominator</strong></em></p>
<p>A ratio has no records in its denominator.</p>
<ol type="1">
<li><p>System shows N/A for the ratio (GBR-161).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 2.1_Incomplete Source Data</strong></em></p>
<p>Part of the data cannot be read.</p>
<ol type="1">
<li><p>System shows available figures marked as incomplete and lists the missing sources (MSG29); missing values are not shown as 0.</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Daily; several times per day for the Operations Manager.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-161, GBR-162</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 15. Operational Reports & Audit History (FE-15)

### 15.1 UC-135 Generate and Export Operational Reports

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-135 Generate and Export Operational Reports</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Operations Manager, Owner</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">A manager needs an operational report for a period.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Operations Manager and Owner to generate operational reports for a period, branch or department and export them.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Report data is not changed; the export is audited.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Generate and Export Operational Reports</strong></em></p>
<blockquote>
<p>1. User opens Reports and selects a report type (RPT-01 to RPT-07, V.3.5).</p>
<p>2. System displays the report's filters with default period of SYS-PAR-20 days.</p>
<p>3. User sets the filters and selects Preview.</p>
<p>4. System validates the input and business conditions (GBR-015, GBR-162).</p>
<p>5. System generates the report at one snapshot time and displays the preview with the filters (GBR-161).</p>
<p>6. User selects Export and the format (CSV, XLSX or PDF).</p>
<p>7. System validates the input and business conditions (GBR-163).</p>
<p>8. System creates the file with the same filters and snapshot, records the export in the audit log and downloads it (GBR-163, GBR-008).</p>
<p>9. System displays a success message (MSG25).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 5.1_No Data</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System shows an empty report with its filters (MSG10).</p></li>
<li><p>User may still export a file with headers only.</p></li>
</ol>
<p><em><strong>Step 7.1_Too Many Rows</strong></em></p>
<p>The report exceeds SYS-PAR-12 rows.</p>
<ol type="1">
<li><p>System refuses the export (MSG26).</p></li>
<li><p>Return to step 3 of the normal flow.</p></li>
</ol>
<p><em><strong>Step 8.1_Export Failure</strong></em></p>
<p>The file cannot be created.</p>
<ol type="1">
<li><p>System displays MSG27; nothing is downloaded.</p></li>
<li><p>Return to step 6 of the normal flow.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Medium</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Weekly and at the end of each month.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-008, GBR-015, GBR-161, GBR-162, GBR-163</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 15.2 UC-136 View Audit Logs

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-136 View Audit Logs</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Administrator needs to check who performed which actions.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to look up the history of important actions by user, time, entity and action.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> Audit entries cannot be edited or deleted (GBR-164).</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>View Audit Logs</strong></em></p>
<blockquote>
<p>1. User opens the Audit Log screen.</p>
<p>2. System displays audit entries with time, actor, action, entity and summary (GBR-164), sorted by time (newest first) (GBR-015).</p>
<p>3. User filters by period, actor, entity type, action and branch or searches by keyword.</p>
<p>4. System refreshes the list with matching records in the user's scope (GBR-015).</p>
<p>5. User selects an entry to open its details (UC-137).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3">N/A</th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3"><p><em><strong>Step 4.1_No Matching Records</strong></em></p>
<p>No record matches the filters.</p>
<ol type="1">
<li><p>System displays an empty list with the applied filters (MSG10).</p></li>
<li><p>User changes the filters or the flow ends.</p></li>
</ol></th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when investigating an issue.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-015, GBR-164</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

### 15.3 UC-137 Inspect Record Change History

<table>
<colgroup>
<col style="width: 19%" />
<col style="width: 28%" />
<col style="width: 20%" />
<col style="width: 31%" />
</colgroup>
<thead>
<tr class="header">
<th>UC ID and Name</th>
<th colspan="3">UC-137 Inspect Record Change History</th>
</tr>
<tr class="odd">
<th>Primary Actors</th>
<th>Administrator</th>
<th>Secondary Actors</th>
<th>N/A</th>
</tr>
<tr class="header">
<th>Trigger</th>
<th colspan="3">Administrator needs to see how a record has changed.</th>
</tr>
<tr class="odd">
<th>Description</th>
<th colspan="3">Allows the Administrator to inspect the detailed changes of a record: who changed what, when, with before/after values.</th>
</tr>
<tr class="header">
<th>Preconditions</th>
<th colspan="3"><strong>PRE-01:</strong> User has logged in and has permission for this function (GBR-001, GBR-002).</th>
</tr>
<tr class="odd">
<th>Postconditions</th>
<th colspan="3"><strong>POST-01:</strong> No data is changed; the history screen does not allow restoring values.</th>
</tr>
<tr class="header">
<th>Normal Flow</th>
<th colspan="3"><p><em><strong>Inspect Record Change History</strong></em></p>
<blockquote>
<p>1. User opens a record's Change History, or an audit entry from UC-136.</p>
<p>2. System lists all audit entries of the record in time order with actor, action and changed fields (GBR-008).</p>
<p>3. User selects an entry.</p>
<p>4. System displays before/after values for each changed field; passwords and tokens are never recorded (GBR-008, GBR-164).</p>
</blockquote></th>
</tr>
<tr class="odd">
<th>Alternative Flows</th>
<th colspan="3"><p><em><strong>Step 2.1_No Change Recorded</strong></em></p>
<p>The record has no change after creation.</p>
<ol type="1">
<li><p>System shows only the creation entry.</p></li>
<li><p>The flow ends.</p></li>
</ol></th>
</tr>
<tr class="header">
<th>Exceptions</th>
<th colspan="3">N/A</th>
</tr>
<tr class="odd">
<th>Priority</th>
<th colspan="3">Low</th>
</tr>
<tr class="header">
<th>Frequency of Use</th>
<th colspan="3">Occasionally; when investigating an issue.</th>
</tr>
<tr class="odd">
<th>Business Rules</th>
<th colspan="3">GBR-001, GBR-002, GBR-008, GBR-164</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

