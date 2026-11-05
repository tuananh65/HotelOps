# Cleaning state matrix — current source vs proposed activation

Nguồn: baseline `a5b3f66...` Services/Housekeeping/{HousekeepingAssignmentsService,WorkProgressService,InspectionExecutionService}.cs, DTOs/Housekeeping/InspectionWorkflowDtos.cs; SRS DRAFT FE04 UC028–038/state matrix near16287; Task73 conditional approval. Official ledger tại28f0a5e...:HANDOVER_CODEX_A_20261105/FULL_UC_LEDGER.csv. Không thay ledger hoặc đổi tên UC theo label màn hình.

| UC / ledger | Transition theo SRS | Current executable | Room/time/notification | FS007 status / next gate |
| --- | --- | --- | --- | --- |
| UC028 Create / BLOCKED_EXTERNAL | new→PENDING_ASSIGNMENT | No Create HTTP writer | Derived readiness/event/audit; no direct mandatory notification | PROPOSED: UC028_CREATE_PROPOSAL + shared sidecars/reducer approval |
| UC029 Assign / PARTIAL | PENDING_ASSIGNMENT→ASSIGNED | POST assign active, legacy PENDING; optional explicit null-workflow adoption; notification NOT_INTEGRATED | Shift eligibility; no Working Workday requirement or room status update in existing initial assign; mandatory assignment event | BLOCKED activation of frontend writes until A01/B01/B02/C01/read contract accepted/applied |
| UC030 Reassign / NOT_IMPLEMENTED | ASSIGNED/IN_PROGRESS/ON_HOLD→ASSIGNED | No cleaning Reassign action | Close prior open interval + preserve history, room recalc, notify old/new | PROPOSED integration; no reuse of initial Assign to overwrite active responsibility |
| UC031 Start / NOT_IMPLEMENTED | ASSIGNED/ON_HOLD→IN_PROGRESS | No cleaning Start action | Own active assignment, Working Workday, one open interval; first-start time, derived readiness; no SRS mandatory notification for Start itself | PROPOSED; A1 source/order and own Workday access/activation review |
| UC032 Progress / COMPLETE_VERIFIED_V1 | IN_PROGRESS→IN_PROGRESS | GET/PUT progress active | Own current assignment; frozen items/note/evidence/audit; percent100 does not complete | APPROVED_AND_APPLIED scoped existing implementation, no modification |
| UC033 Blocker context | IN_PROGRESS→ON_HOLD | No cleaning blocker writer | Source blocker/interval close/readiness | Outside initial FS008 mutation scope |
| UC034 Complete / NOT_IMPLEMENTED | IN_PROGRESS→PENDING_INSPECTION | No cleaning Complete action | Required items/evidence, end+interval close, room readiness and mandatory supervisor event | PROPOSED downstream; final-completer/inspection identity and routing required |
| UC037 Cancel context / PARTIAL | PENDING_ASSIGNMENT/ASSIGNED→CANCELLED | Two GET monitor active; no Cancel | Cycle selection/readiness, audit and assignee notification | Read APPROVED_AND_APPLIED; cancellation not included |
| UC041 Inspect assign context / PARTIAL | New inspection→ASSIGNED for pending cycle | Existing POST /cleaning-tasks/{id}/inspections | Exact inspector/version/SoD, notification missing | Existing permission INSPECTION_PERFORM, no invented INSPECTION_ASSIGN |
| UC044/045 conclusion / NOT_IMPLEMENTED | PENDING_CONCLUSION→PASSED/FAILED; cleaning PENDING_INSPECTION→COMPLETED/REWORK_REQUIRED | Inspection finish only PENDING_CONCLUSION, not approval/rejection | Current-cycle conclusion/readiness + mandatory events | BLOCKED downstream, not substituted by progress/finish |
| UC046 Rework request / PARTIAL | new→PENDING_ACCEPTANCE | POST inspections/{id}/reworks exists | Defect lineage, eligible assignee, mandatory notification missing | Not execution-ready |
| UC035/036 Rework execution context | PENDING_ACCEPTANCE→ACCEPTED→IN_PROGRESS→PENDING_REINSPECTION | No receive/start/complete rework actions | Own defects/remediation/time + reinspection/readiness | No invented rework pause state |

CLEANING_ASSIGN/CLEANING_EXECUTE/CLEANING_VIEW exist in SQL catalog; no new codes/grants or role IDs. Status column legacy PENDING/IN_PROGRESS/COMPLETED/CANCELLED and nullable workflow are not interchangeable. For new Create proposal map PENDING_ASSIGNMENT→legacy PENDING explicitly; pending inspection/rework stays open until authoritative terminal conclusion. Null legacy workflow blocks creation/adoption until reviewed.

Workday naming: UC138 is shift template/roster management; UC139 is Workday; UC037 is Monitor, not Start; UC038 is turnover history, not an execution writer. Current baseline WorkdaysController is NonController. Historical A1/B1.1 evidence does not activate its routes here; B1.2 is not required for initial Create/Assign.

Tên chính thức trong ledger: UC-138 **Manage Operational Workday and Shift Records**, UC-139 **Record Workday Start and End**. Mô tả Shift/roster trong tài liệu này là phạm vi contract UC138 được Task80 audit, không đổi tên UC. UC-038 **View Room Turnover History** vẫn giữ2 điểm lịch sử, không credit lại.
