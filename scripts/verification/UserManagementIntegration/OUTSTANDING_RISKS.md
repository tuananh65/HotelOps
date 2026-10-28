# Release risks / blockers

## Blocking

1.**Missing startup schema fail-fast** đã chứng minh thực tế. HealthCanConnect200 không đủ;newEFoldDBSQL207. Exact Program.cs diff đã cung cấp,**chưa áp dụng** vì task yêu cầu report safe DEV1 fix và không tự mở rộng source change.
2.Current forward/rollback làtrial-only,không production deployment artifact. Không remove guards hoặc runHotelOpsDB chỉ để gắn nhãnready.

## Notes cần nghiệm thu trước rollout

- Proposed startup diff chưa build/run trongapp;negative schema tests phải chuyển từ observationunsafe sangexpect process exit/notlisten khi fix được duyệt.
- Least-privilege wholeappSQL principal/DDL separation chưaruntime certify;test dùng developer integrated principal.
- Old binary/new schema coexistence và comprehensive DEV2 business workflows chưa chạy.82 projections + noDEV2source diff không thay nghiệp vụ integration suite.
- Revocation-vs-in-flight mutation không global serializable;source recheck trong transaction nhưng không lock toàn grant/membership aggregate. Không tự sửa sharedwriters hoặc businessrules.
- Auth Userwrites đổi rowversion,profile412 là expected;EF concurrency ảnh hưởng future tracked/detachedUserwriters.
- Cold-start metrics chưa thu đầy đủ,NU1900 chưa khắc phục;không disable security auditing.
- Runtime harness dùng Windows Development và pinned temporary TLS;không chứng nhận production DataProtection/TLS/CORS rollout.
- Integration test-only grants chỉtrial và đãcleanup,catalog chính thức giữ nguyên. Không seed role T27 hoặc receipts.
- Task22/20A–26/local artifacts/worktrees giữ nguyên. Không commit/push/merge/stage.

**Final verdict: BLOCKED FOR RELEASE**,dù patched-schema functional integration đã có bằng chứng tốt.
