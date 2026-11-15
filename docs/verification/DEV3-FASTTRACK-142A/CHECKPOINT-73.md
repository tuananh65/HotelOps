# FASTTRACK-142A-ACCELERATOR — Checkpoint73

Workload canonical đã nối employee list → filtered task/time-log detail → shift time riêng, dùng dữ liệu workflow và work intervals thực. Reopen qua nhiều chu kỳ vẫn giữ đúng effort và distinct closed-task count.

- NEW_COMPLETE_VERIFIED=[UC113]; TOTAL_COMPLETE_VERIFIED=18/144; PARTIAL=126/144. C72 và C73 trong lượt này thêm UC106/UC113. [UC113 acceptance](UC113_ACCEPTANCE.md).
- SQL_HTTPS_BROWSER=PASS:346 records,40 workload checks,8 browser flows/58 stages. Live Reopen/Accept/Start tạo provisional time; Submit/independent Verify đóng interval và chuyển thành recorded effort. Department Head/Operations Manager positive; executor/foreign/revoked-role negative; SHIFT_VIEW không bị thay bằng TASK_VIEW.
- BACKEND_TESTS=610 PASS/11 SKIP; build0 warnings/errors. Solution test exit0 không chạy test project, không tính làm coverage.
- FRONTEND_TESTS=73 task PASS;175 Auth/Router host PASS; standalone TypeScript và production build PASS. Full841 C72 là historical regression; không rerun npm ci vì lockfile không đổi.
- CLEANUP=3 clone/backup removed; source restored/empty; no live host/private files. Không thay schema/generated EF/shared/production DB.
- BLOCKERS=UC111 correction thiếu mapping work_interval.correct; UC105 MAINTENANCE/INSPECTION/CLEANING requester provenance vẫn fail-closed; UC102 linked creation cần UC118. FE13 cần reconcile Pending Acceptance/Accepted với physical SUBMITTED/DRAFT và receiving-department authorization trước mutation.
- NEXT_BATCH=FE13 UC114–118 vertical lifecycle + public linked task; hoàn thiện UC111 phần độc lập khi authority đủ bằng chứng. Không dừng sprint vì mapping chưa được chứng minh.

Changed source/tests: TaskWorkloadDtos.cs, OperationalTaskWorkloadService.cs, WorkTimePeriod.cs, WorkProgressController.cs; workload.ts, TaskWorkloadPanel.tsx, TaskReadPage.tsx; TaskWorkloadContractTests.cs, operational-task-workload.test.mjs; SprintTaskWorkloadScenario.cs, SprintTaskWorkloadLifecycleScenario.cs, SprintTaskReopenScenario.cs, sprintTaskWorkloadBrowser.mjs. Receipts/matrix cập nhật kèm commit.

[Runtime](checkpoint73-runtime.json), [SQL](checkpoint73-final-sql.json), [cleanup](checkpoint73-cleanup.json), [earlier failures](checkpoint73-earlier-trials.json). Trial1 lỗi source projection, trial2 lỗi harness synchronization; final trial3 PASS. Existing warnings: Vite use-client/chunk766KB; EF20602/10102. Không giấu hoặc tính các lượt lỗi thành PASS.

Initial local/remote=e735e47d1b7eba65cd45cfb344b32445603ca569. Canonical branch integration/dev1-dev2-dev3-unblock-20261114 giữ nguyên. Commit chứa receipt xác định source đã kiểm chứng; push/remote equality báo sau commit. Sprint active, deadline2026-11-16 17:50UTC.
