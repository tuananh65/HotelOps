# Task83 — SRS acceptance matrix

**Không có business acceptance mới được chạy cho UC142/038 trong Task83.**
Historical evidence dưới đây là source + receipts đọc từ cf8fb02, không tính thành PASS mới.

| UC / yêu cầu SRS | Source lịch sử kiểm trực tiếp | Receipt lịch sử | Task83 live result |
|---|---|---|---|
| UC142 scoped code/name/branch/status/active-room count | RoomTypeBatchService.ListRoomTypesAsync, SQL permission branch/global filtering trước count/page | task70-accepted success; batch70Cases UC14240 HTTP/persistence; report thêm9 final-boundary checks | NOT_RUN — service/controller chưa tích hợp |
| UC142 code order, branch/status/search, pagination/empty | ListRoomTypesAsync, deterministic secondary key, filtered total | Task70 list/filter/page/empty cases | NOT_RUN |
| UC142 detail, separate update/deactivate rights | RoomTypeBatchController.Detail, ReadRoomTypeAsync; snapshot management controller tách read/update/deactivate actions | Task70 scoped detail/legacy/access tests | NOT_RUN; không triển khai UC018/019 |
| UC142 auth/session/scope/invalid query/no writes | BatchAsync retained UserEligibilityLock, session/permission initial+final gates; schema/legacy boundary | Task70 final session/grant/cancel rejection + preserved room/type/history/audit rows | NOT_RUN |
| UC038 newest cycles, cleaning type/assignees/start/end | TurnoverHistoryAsync, HistoryCycles | task72-history-final success=true,208 combined UC038/048 checks | NOT_RUN — service/controller chưa tích hợp |
| UC038 date/result filters, page/count/empty | HistoryInput, TurnoverHistoryAsync SQL filters, newest timestamp/ID order | Task72 history cases | NOT_RUN |
| UC038 inspection result, rework count, recorded cancellation reason | HistoryCycles, Reworks, cleaning_tasks/CANCEL audit lookup | Task72 cycle data/timeline cases | NOT_RUN; không tự sinh reason/history |
| UC038 cycle timeline/checklist version/defects/rework | HistoryInspectionDetails, HistoryCycleDetailDto, relational consistency filters | Task72 version/defect/history cases | NOT_RUN |
| UC038 attachment metadata/private access | HistoryEvidence, OperationalHistoryFiles.HistoryFile; scoped download controller route | Task72 unrelated attachment404, private/provider fields hidden | NOT_RUN |
| UC038 auth/scope/revocation/cancellation/read consistency | BatchAsync; CLEANING_VIEW + INSPECTION_VIEW same authoritative room branch | Task72 actual SQL revocation ordering and HTTPS cancellation/rollback labels | NOT_RUN |
| UC038 read-only | No business mutation in TurnoverHistoryAsync; scoped DTO queries | Task72 original no-write checks | NOT_RUN |

Positive ALL/BRANCH/OWN business tests Task83: NOT_RUN. Không tạo grants hoặc fake SQL session.
Không tạo endpoint để biến một UC đã accepted thành UC mới. Không khẳng định snapshot tests đã
chạy lại hoặc endpoint hiện đã usable. Build/security kết quả mới, nếu chạy, chỉ là baseline checks
và được ghi riêng trong TASK83_VALIDATION.json; không hoàn tất hai UC ở checkout này.
