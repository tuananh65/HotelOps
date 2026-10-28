# Commit and deployment plan — đề xuất,không stage/commit

## Commit order

Categories A–E trong FILE_CLASSIFICATION là ownership,file allowlist không phải bắt buộc commit theo chữcái.

1.Schema trial artifacts:rowversion-forward/rollback SQL,Verify-Schema.ps1 và database contract docs phù hợp. Không bỏ trial guards để deploy.
2.Task22 detail:4application files nhómA + Task22 verification/docs và UserDirectory/Program.cs readiness-only diff. Không thêmEF mapping mới vào commit này;detail độc lập rowversion.
3.Task26 profile atomic code group:B application + C User partial/EF mapping + profile verification/docs. **Hold** tới startup diff được review/apply/test;không commitEF mapping mà thiếu partial property,không ship profile service thiếu schema dependency.
4.Task27 integration harness/docs và historical planning docs20A/21/23–25 trong docs commit riêng. Gắn link task approvals,historical BLOCKED snapshots không làm mất approval Task26.

Program.cs hiện chỉ2 profile DI additions;future startup diff sẽ thêm cùng dependency app commit sau approval. Không dùng Task18 git-add commands vì packages15–17 đã commit. Không git add .,-A hoặc reset để chia groups. Task27 index giữ trống;developer selective stage exact reviewed paths sau fix.

Không dùng3local receipts hoặc generatedbin/obj làm dependencies. Teammates tự tạo fresh trial/fixtures.

## Deployment order DB-first

1.Resolve startup guard blocker bằng exactdiff ở DATABASE_COMPATIBILITY;negative unpatched/malformed/unavailable DB phải fail trướclisten,patched DB start. Không rollout khi criterion chưaPASS.
2.Review deployment-target SQL forward/rollback artifact riêng:current patches trial-only. Xác nhậnbackup/restore/permission/expectedDB/schema signature theo teamrelease process;không chạy HotelOpsDB trong task.
3.Trial fresh+upgrade+rollback+old/new binary matrix với least-privilege principal trước bất kỳ real rollout. Review shared User writes với DEV2,không sửa họ.
4.Drain/stop writers trong maintenance window được approve;apply approved forward **trước** newEFbinary. Verify row_version8bytes/systemtype189/nonnullable và data invariance.
5.Deploy binary có startup guard;block traffic tới khi startup+schema readiness pass. Chạy auth/cookie/session/list/detail/profile/scope smoke trên fixtures đã duyệt.
6.Monitor412 expected doUser auth writes,409 email,401/403 và startup failures;không tự mở grants.

## Rollback order

Drain/stop new binary trước. Revert app version có mapping cũ hoặc đảm bảo không token-dependent process đang chạy,rồi mới rollback schema theoapproved artifact. Drop/recreate rowversion invalidates ETags,không preserve token values. Không rollback canonical seed/permissions hoặc delete user history.

Old app may coexist with extra column nhưng chưa test old binary runtime;không dùng assumption đó thay deployment gate. Main DB chưa đổi,không tựbackup/restore/switchconfig/downgrade trong Task27.
