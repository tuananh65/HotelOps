# Remaining issues

- Global-only detail: scoped BRANCH/OWN visibility và UC011 effective roles/permissions không triển khai, đúng phạm vi Task22. Task21 mutation decisions vẫn còn.
- Không có rowversion/edit token; endpoint không hỗ trợ mutation và không tạo token giả.
- New verification project có NU1900 do NuGet vulnerability data không truy cập được kể cả retry escalated; solution build0 warnings/errors. Không tuyên bố mọi project build warning-free.
- Các lượt HTTP ban đầu timeout/fail trước assertions detail, cũng xảy ra với UC141 baseline harness. Readiness đã được tăng lên protected-route thay CSRF-only; root cause cold-start/policy catalog delay chưa được đo đầy đủ, không quy lỗi cho SQL hoặc source mới khi chưa có bằng chứng.
- Sau readiness,234/234 detail/list và144/144 UC141 PASS trên trials mới. Cần đánh giá cold-start behavior riêng trước deployment; không sửa shared auth policy timeout để làm test pass.
- Harness pins temporary localhost TLS certificate; không chứng minh browser CORS/SameSite/production TLS deployment hoặc least-privilege runtime SQL principal.
- Không dùng/reset HotelOpsDB. Trial databases được giữ lại; chỉ owned sessions được revoke. Local evidence files không tự commit.
