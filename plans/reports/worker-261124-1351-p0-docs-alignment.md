# P0 documentation alignment

Đã tạo hồ sơ source-grounded trong `docs/requirements/p0-261124`:

- README.md: nguồn A/B/continuation, thứ tự ưu tiên, trạng thái bằng chứng.
- business-workflows.md: tám actor, UC-P0-01…11, routing/state, supply invariants, readiness, UX override.
- api-contracts.md: route/DTO thực tế supply, workday, typed requests, room reports, transfer/asset; version/key/authorization boundaries.
- database-and-recovery.md: chín bảng mới, script dependencies, schema counts, TEST-only cutover/recovery, evidence limitations.
- traceability.md: A/B → source → acceptance; khoảng trống và 28 E2E chưa chạy.
- permission-matrix.md: sửa HK/Reception room reporting qua IR_CREATE; Storekeeper MR_APPROVE_L1 BRANCH, bỏ pending decision lỗi thời.
- store-policy.md: nối mốc worker chưa apply với xác nhận migration của Integration Owner.
- SRS_CHANGE_NOTES.md: chỉ append section P0, giữ nguyên historical SRS/notes.

Không sửa source, script migration, Compact UX cũ, test runner hay database. Không build/deploy/server. Source inspected includes RequestWorkflow/RoomIssue/Supply/Workday controller+contracts/services, AssetService, InsightService, finalize/default SQL và worker reports. Các quyết định mới override rõ các UX/nghiệp vụ cũ mà không xóa lịch sử tài liệu.

## Gaps cần Integration Owner xử lý

1. `InsightService.Dashboard.attention` hiện là overdue work projection, chưa đủ Action Center A: transfer approvals, operational pending assign/verify, asset handover, stocktake discrepancies, shift handover, replenishment pending OM. Không khôi phục OM approval MR thường.
2. Transfer backend tồn tại; A §21 sent/received/history UI và scenario discrepancy/notification/concurrency chưa được chứng minh.
3. AssetService hiện có move/handover/condition/retire, nhưng đầy đủ A asset acceptance và report transfer/ledger phải kiểm riêng.
4. Request sorting thiếu backend sort contract; display handler names/legacy incident deeplinks/supply hidden-filter UX được worker UI nêu chưa kiểm runtime.
5. Full 28 E2E, actual two-branch reconciliation, role/API negatives và concurrency chưa có evidence; docs cố ý NOT_TESTED.

Evidence note: TEST schema commit/counts + rollback/idempotency và prior builds được ghi rõ là xác nhận của Integration Owner, không giả lập log/artifact local. Final build đang integration không dùng build cũ để báo cuối đã pass.

Validation: `node --check` và validator Markdown đều **Passed** (exit 0): 7 documents, local links, UTF-8 replacement characters, giới hạn 800 dòng, Storekeeper/IR_CREATE markers, đúng một section P0 append. Lần validator đầu lỗi regex chứa dấu gạch dài khi qua PowerShell stdin; đổi kiểm tra heading sang ASCII prefix và chạy lại đạt, không sửa tài liệu để né kiểm tra. `git diff --stat` xác nhận SRS_CHANGE_NOTES chỉ thêm 28 dòng. Không chạy C# build/test vì chỉ docs và được phân công tránh shared builds.

Unresolved business questions: không có. Các gap trên là triển khai/kiểm thử còn phải làm, không phải yêu cầu duyệt mới.
