# Decisions required before implementation

## Blocking authorization decisions

1. Xác nhận permission chính xác cho UC011: USER_VIEW, USER_MANAGE, ROLE_MANAGE hoặc tổ hợp có quy định rõ; không tạo permission thứ63.
2. Xác nhận Global-only unbound ALL hay scoped reviewer; ai được xem access report cross-branch? Nếu scoped, chỉ phần paths trong branch hay được full target report?
3. Xác nhận self-review: có yêu cầu cùng reviewer permission không, không mở self bằng targetId alone.
4. Chốt whitelist report: raw assignments/ineligible reasons, branch memberships và primary department; target inactive user có được review cấu hình nhưng đánh dấu no-effective hay không?

Chỉ Task22 minimal detail O1 được duyệt; approval này không bao gồm bốn mục trên. Đủ thông tin hiện tại để chặn implementation, không cần phỏng đoán policy.

## Technical decisions sau gate

- Assigned/granted/effective/conditional DTO; effective snapshot không là resource-specific authorization.
- Reuse target-evaluation logic từ caller-bound foundation mà không giả principal hoặc duplication; cần approval shared impact.
- Consistent evaluatedAt/query snapshot, filter matching, stable ordering, limits/paging và missing/deleted behavior.
- Multi-department SRS chưa có model tương ứng; không thêm schema trong task.
- Grant effective không chứng minh session validity, lockout hay SoD eligibility; resource/business adapters vẫn authoritative.
- Scoped visibility của Users list/detail chưa triển khai; không mở rộng trong UC011.

Không sửa Task20A/21/22 hoặc source để resolve những quyết định này. Không database/migration/catalog edits, không stage/commit/push/merge/delete.
