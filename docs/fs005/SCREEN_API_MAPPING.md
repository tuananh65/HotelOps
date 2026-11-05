# Screen → API mapping

| Screen / UC | Component | Mock removed | API / DTO | DB / permission / status |
| --- | --- | --- | --- | --- |
| 24 Cleaning Task List / UC037 read subset | CleaningTaskListScreen | services.visible/can, prototype create/edit controls, mock counters/insights and local DataTable paging | GET /api/branches/{branchId}/cleaning-monitor; CleaningMonitorQuery → CleaningMonitorPage | CleaningTask, CleaningRework, Room/Floor, Assignment, User, ReferenceValue, ProgressLog; CLEANING_VIEW branch/global; LIVE read |
| 26 Cleaning Task Detail / UC037 read subset | CleaningTaskDetailScreen → CleaningMonitorDetail when monitor=1 | services.visible, DetailPage/RelatedRecords mock overview/actions | GET /api/branches/{branchId}/cleaning-monitor/{kind}/{id}; CleaningMonitorTimelineQuery → CleaningMonitorDetail | assignments/progress/audit timelines, parent task lineage; same view gate; LIVE read |
| 26 known-ID UC032 | CleaningTaskDetailScreen → CleaningProgressPanel, branchId without monitor=1 | No new mock replacement needed | Existing GET/PUT progress + evidence APIs; WorkProgressDto | CLEANING_EXECUTE OWN/current assignment; unchanged implementation, GET only live in FS005 |
| 23 Readiness → list → detail | RoomReadinessBoardScreen | No mock lookup added | Existing GET /api/rooms; link includes authorized-result room number + branch, then monitor GET | ROOM_VIEW for directory; CLEANING_VIEW independently checked for monitor |

Filters: fromDate/toDate, floorId, status code, assigneeId, priorityId, overdue, search, kind, page/pageSize. Unknown query keys including prototype actor/role are not forwarded. pageSize 1–100 server, UI 10/20/50/100. Default date is today in Asia/Ho_Chi_Minh; UI displays server effective range. All timeline groups share page index and limit20; counts remain independent.

List shows recorded/workflow status without inventing transitions; null progress/overdue/deadline remains unknown. Branch shown is authorized route context, not a mock name. Assignee name comes from server. Shared getJson transport supplies cookie include/no-store/redirect error/AbortSignal. Cancelled filter requests cannot overwrite current list. DTO validators reject unsafe SQL bigint, wrong detail identity, malformed timelines and response pagination.

Supervisor view permission does not imply execution ownership. Detail provides an explicit UC032 link; backend independently checks current assignment. Rework details display parent cleaning assignment/progress history as returned by existing contract; this is not rework execution acceptance.
