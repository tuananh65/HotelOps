# UC037 current disposable SQL evidence

Baseline 68b79654a602fe36674f187ca64d1634ac8d7184. No new UC completion or production acceptance.

Direct read-only encrypted Windows-auth SQL preflight confirmed server meita\MSSQLSERVER01, database HotelOps_DEV3_142A_Recovery_2b221f40, GUID 5270D07C-7C27-477B-A60E-86485ADD7FAD. Cancellation V1 marker matches 7D61CD67E15BA9710CB30612470E5AA674EB9EECEDF4568B1346168DF1B2F8EF; readiness successor matches 7E54344D7D50C1322BD8265860C1299F675F3055FE33D5720A4834F299E5E6D9. No migration was reapplied and shared HotelOpsDB was not selected for mutation.

The sandbox connection initially failed with Windows security-package credentials unavailable. The same encrypted read outside sandbox succeeded. No encryption/authentication mechanism was disabled.

Existing FreshStartAdmissionSqlTests fixture was run, then rerun with a separate current-identity receipt built from the SQL identity read. Both runners reported 1 PASS, 0 failed/skipped. The second run emitted artifacts/uc037-current-recovery/uc037-current-recovery.trx, whose exact test name and Passed outcome were read independently. Its fixture explicitly verifies server/database/GUID and installed Start/Resume capability before synthetic writes. It exercises Start/Complete/capture and cancellation restoration/source proof in rollback transactions, including prior Passed => READY and prior Passed plus effective block => BLOCKED. It does not execute the real cancellation command with actual SQL session authorization or notification delivery.

Evidence discrepancy retained: formatted receipt JSON reads displayed an empty GUID even after writing the current copy, while the guarded runner passed. A separate fail-closed shell check rejected Guid.Empty and mismatches and returned RECEIPT_GUID_EXACT_MATCH against the SQL GUID; TRX returned Passed. The formatted-output discrepancy is not explained here. Original receipt was preserved; the current copy is an ignored local artifact, not production configuration.

Independent post-run SQL checks: zero rooms with the fixture CANCEL prefix; zero other active user transactions in this disposable database; zero disabled/untrusted foreign keys and CHECK constraints. Fixture itself asserts removal of synthetic room, operation reservation/publication and current-cycle pointer after rollback. No database service restart, FK/trigger bypass, grants, credentials, configuration activation or shared database mutation.

No application source edits in this evidence batch; previous source build results remain historical. Do not convert this component result into UI/HTTPS/full UC PASS. Next implementation/verification target is the real authorized Cancel transaction, durable delivery and approved own receipt, followed by browser acceptance. Existing historical .SQLEXPRESS cancellation fixture targets a different database and was not run or silently redirected.
