# DEV1 FS014 — integration dependencies

Status: PROVISIONAL V1, NOT_SHARED_ACCEPTED. Chỉ đọc FS013 readiness và source liên quan UC031/033/034; không audit/reconcile toàn bộ DEV2/DEV3.

| Dependency | Tác động và boundary |
| --- | --- |
| Mandatory Notification UC033/034 | SRS yêu cầu Housekeeping Supervisor được thông báo. Chưa có accepted event contract/producer cho hai UC; FS013 assignment publisher là provisional. Report/Complete luôn fail-closed 503 sau boundary validation, không stage inbox/intents giả. Cần event schema/version, source identity, recipients theo branch/permission, transaction participant, dedup/retry và failure semantics được duyệt trước mở ghi. |
| Resume blocker state | Developer chấp thuận current assignee/CLEANING_EXECUTE + required note trong isolated trial. Sidecar FK progress_id và resolution actor/time/note, task rowversion trigger, audit cùng transaction. Đây chưa phải approved shared schema hoặc migration. Không backfill legacy blocker theo phỏng đoán. |
| Incident / Material Request | Không có accepted source-confirmation reader. Mọi linked blocker bị chặn; opaque link ID không chứng minh nguồn resolved. Không triển khai module bên kia hoặc tạo source records. |
| WorkTime | Dùng participant/lock order hiện có trên fresh reconstructed trial. Public UC139/deployment shared vẫn chưa acceptance. Component dùng session double; không tuyên bố live auth/Workday API PASS. |
| Room lifecycle | Resume thêm readiness event cùng effective block; không đổi current-cycle pointer hoặc READY. UC034 Pending Inspection writer vẫn chưa mở. Không chỉnh Reception/Operation Manager permissions hoặc room history shared. |

Không thay generated entities, DbContext mappings, global infrastructure, database shared/production. Shared adoption cần review riêng; FS014 không cấp quyền cài candidate lên shared database. Chi tiết test/source/limitations chỉ giữ ở [README FS014](../fs014/README.md).
