# Execution / Replacement / Rollback Plan — chưa được phép thực thi

Task02E chỉ chuẩn bị manifest. Tất cả 371 records có `executionEligible=false`, `approvedForExecution=false`. **Không có destructive script, archive thật, backup mới, reset hoặc COMMIT trong task này.** Không thay đổi HotelOpsDB hay trial. Đọc kèm [manifest](FIXTURE_RESET_MANIFEST.md) và [JSON](FIXTURE_RESET_MANIFEST.json).

## 1. Những điều kiện phải đạt trước khi tạo changeset thực thi

1. **Provenance:** cung cấp seed/generator gốc và hash/version, execution evidence, mapping business code/branch/PK; hoặc hồ sơ xác nhận per-row đủ tin cậy do chủ dữ liệu/DEV1/DEV2 ký. Hiện 28 rows UNCERTAIN, không được xóa hoặc REPLACE gián tiếp.
2. **Exact rowset:** owner chọn disposition per key, không ký blanket reset. Bản sửa manifest phải có version + digest + ngày/target; muốn đụng PRESERVE rows phải xin duyệt mới, không đổi classification để lách closure.
3. **Dependencies:** xác định full FK + typed/polymorphic row closure; zero unreviewed incoming/outgoing reference. Các endpoint chưa implement không được xem như đã kiểm thử workflow.
4. **Protected history:** requests8/9 có approvals9/10 protected. Ledger WH-FB có POSTED docs3/8 và completed request3/fulfillment3 protected. Vì vậy current B reset của các parent này **BLOCKED**, kể cả khi chứng minh origin một số con là synthetic. Cần duyệt ranh giới khác hoặc chuyển sang workflow bảo tồn history; không tự xóa approvals/ledger.
5. **Replacement mappings:** DEV2 chọn nguồn/canonical warehouse cho fixture mới nếu cần, approved quantities/UoM và scenario; DEV1 chốt staff/branch/department hợp lệ, request/shift scenario. Không tự chọn warehouse nhận stock thật; giữ users1/4/5/19, grants và credential.
6. **Accounts/sessions:** chốt exact account retirement fields và cách giữ manager history khi departments Inactive. Giữ users và password data. Nếu deactivation cần revoke user11 session thì sửa manifest để cho phép **chỉ revoked_at theo FE-01**, không reset/delete/unrevoke session. Role/branch assignment changes không tự bao gồm trong account flag change.
7. **Execution approval riêng:** đồng ý tạo/run exact trial-only scripts sau review; digest nội dung đã duyệt, recovery rehearsal, expected counts/hashes và audit actor semantics. Chọn B định hướng không đáp ứng điều kiện này.

## 2. Fixture Replacement Plan (thiết kế để review, không là seed đã được duyệt)

Không có old row được xác nhận synthetic, nên chưa có old→new mapping được phép áp dụng. Các scenario sau là tiêu chí cho replacement definitions sau khi nguồn/closure được duyệt, không phát sinh giao dịch hay dữ liệu hiện tại.

| Scenario | Fixture mới đề xuất | Invariants / dữ liệu giữ nguyên | Điểm phải duyệt trước |
| --- | --- | --- | --- |
| BE1 branch identity | Tái dùng accounts/fixed roles hiện có | User1 OM B1; User4 DH B1; User5 Storekeeper B1; User19 DH B2; không tạo user/password/session mới để thay họ | Không gán User4 về một department hoặc disable DH do ca cũ |
| BE1 canonical shift | Một ca PLANNED mới thuộc HK hoặc MT trong branch được duyệt, ngày tương lai so với lần thực thi, chưa có actual attendance | Staff phải Active, đúng branch/department/role theo fixture được duyệt; DH có thể tạo/phân công theo branch; không copy check-in/check-out cũ | Chốt staff/template/date và supported status mapping; không invent terminal status cho old shifts4/5 |
| BE1 attendance lifecycle | Bắt đầu từ ca mới, để bài test thực sự gọi start/end khi workflow có implementation | Thời gian actual do test action được ghi nhãn tạo ra, không backdate để hợp thức old check-in; không claim endpoint đã tồn tại | Triển khai/kiểm thử workflow là task riêng nếu chưa có service |
| Internal request hợp lệ | Một request mới thuộc HK/MT/WH/FO, type được duyệt, initial state theo schema/workflow thống nhất | Old request13 và CREATED event không bị sửa thành đã hủy. Có thể giữ DRAFT để kiểm thử validation mà không yêu cầu cancel chưa có rule | Chọn type/requester/receiver; nếu cần test cancel draft, approve business rule trước |
| BE2 material demand | Request mới với staff/department canonical, item/UoM và warehouse do DEV2 chọn | Không tái tạo approval giả; test approval/issue/receive chỉ tạo event thực khi scenario đó được chạy và được duyệt. Không tự ghi RECEIVED vào old fulfillments | Nhu cầu8/9 giữ hay thay, treatment protected approvals, branch/source mapping |
| BE2 inventory | Bộ draft hoặc scenario test mới có ledger arithmetic tự nhất quán tại kho canonical được duyệt | Preserve posted WH-FB history cho tới khi có phê duyệt cụ thể. Không ghi movement bù chỉ để giảm WH-FB về0. Items14/15 và UoM là protected master | Approved fixture provenance, scope history, numbers và test-only lifecycle; không tự chọn WH-MAIN |
| Negative cases | Sai branch, inactive department, inactive role, user restricted, tự duyệt | Kỳ vọng request bị từ chối và không có side effect; ưu tiên request test input, không tạo inactive/invalid assignments vào core baseline | Các permission/business rules chỉ dùng bản đã chốt, không mở Permission Matrix |

Fixture business codes mới cần namespace được duyệt (ví dụ TEST-LEGACY-REPLACEMENT-*), version, expected outcomes và mappings theo code/branch; không hard-code role/branch/personnel IDs vào shared seed. Không lấy tên sản phẩm FB-* làm bằng chứng hàng vô hiệu hoặc synthetic. Existing inactive catalogs không được seed lại Active.

Nếu chưa thể đạt WH-FB zero balance mà không động protected ledger, **không có replacement plan hoàn chỉnh cho WH-FB**. Tạo thêm fixture kho khác không giải quyết blocker cũ. Tương tự, ca mới hợp lệ không cho phép xóa/đóng ca cũ UNCERTAIN.

## 3. Quy trình thực thi sau phê duyệt (chưa chạy)

### Bước 1 — Freeze manifest / preflight chỉ đọc

- Chọn approved manifest version/digest và record set. Kiểm tra đúng server, database name, database identity/fingerprint, phiên bản schema và trạng thái guard. HotelOpsDB phải bị loại khỏi mọi lệnh ghi.
- Kiểm tra actual PK, expected field values và safe-row hashes; đối chiếu table baseline, FK/CHECK/default/index/trigger metadata và full protected fields trong vùng bảo mật nếu cần. Safe projection không gồm password/PII và **không đủ thay thế full backup hoặc complete row validation**.
- Kiểm tra quantities zero-issued/open demand, stock sums, stocktake/reversal links, work intervals/attendance, actual user/role/branch state, sessions và actor. Đọc lại mọi typed reference. Có drift thì dừng, không thay baseline tự động.
- Bố trí cửa sổ trial không có writer khác. Không đưa database chính vào chế độ gián đoạn hoặc đổi connection string backend.

### Bước 2 — Backup trial riêng và recovery test

- Tạo current COPY_ONLY/CHECKSUM backup tên duy nhất ngoài Git; không overwrite. VERIFYONLY; kiểm thử khả năng restore vào DB tên mới nếu được phê duyệt, data/log riêng, không WITH REPLACE.
- Không dùng backup trước Task02C như thể chứa tám thay đổi mới. Không tự restore/drop trial nếu bước sau lỗi.
- Lưu receipt: exact server/target, manifest digest, backup checksum/path có quyền hạn chế, snapshot windows và schema metadata hash. Không ghi credentials trong receipt Git.

### Bước 3 — Archive exact keys ngoài repository

- Chỉ archive rows đã được duyệt và closure cần recovery. Full-row dữ liệu users/sessions nếu cần phải nằm trong backup/archive bảo mật, không trong JSON Git.
- Mỗi component có counts, digests, PK mappings, schema metadata, root/child references và archive version. Xác minh có thể phục hồi trước khi viết destructive script.
- Audit logs cũ giữ nguyên; archive không tạo quyền xóa audit hoặc biến sự kiện sai thành đã hoàn tất.

### Bước 4 — Validate manifest và dependency closure

- So sánh exact keys dưới locking strategy được review; có incoming protected/external row ngoài manifest thì BLOCKED.
- Parent replacement không tự kế thừa quyền thay children: approvals9/10 và completed request3/fulfillment3 là ví dụ gate đang chặn.
- Kiểm tra polymorphic aliases đúng source/type; không dựa vào số ID hoặc tên bảng đoán alias. Không xóa notification MATERIAL_REQUEST13 vì đang xử lý INTERNAL_REQUEST13.
- Nhóm BE1/BE2 review cùng nhau nếu inventory references hoặc active responsibility cắt module boundary.

### Bước 5 — Dry Run trong transaction

- Chỉ sau khi provenance/rowset được duyệt, dựng script có guard server/target/schema, XACT_ABORT, expected state/cardinality và actual affected key capture. Không tắt FK/CHECK/unique index/trigger để ép chạy.
- Archive/remove/replace nếu được duyệt: child-before-parent theo đồ thị; insert parent-before-child; xử lý reference cycles bằng changeset đã review, không NULL FK ngầm hoặc invent quan hệ.
- Dùng FE-01 user-before-session locks khi account retirement; toàn bộ account status/revocation/audit cùng transaction. Không tạo credentials mới hay khôi phục revoked session.
- Audit hành động thực sự (fixture retirement/rebuild hoặc deactivate), ghi approved reason/manifest reference và actor/system semantics đúng; không giả requester/approver hoặc ghi “đã giao hàng/đã hết ca” khi không xảy ra.
- So actual key/field diff với approved manifest: không chỉ so số lượng. Không row nào ngoài scope được đổi; protected hashes phải đạt; rồi ROLLBACK. Identity gaps do rollback có thể có, không reseed để che.

### Bước 6 — Review Dry Run rồi COMMIT trên trial riêng

- Lưu Dry Run diff, approvals và digest script cuối. Chỉ thực thi COMMIT khi có phê duyệt riêng của bản cụ thể; không lấy task lập manifest làm phê duyệt COMMIT.
- Chia component transaction theo dependencies/recovery strategy đã duyệt. Nếu một component không đóng closure, không commit một phần khiến protected data bị orphan hoặc workflow bị treo.
- Không replay khi chỉ một nửa changeset đã chạy; dừng để đối chiếu receipt và checkpoint.

### Bước 7 — UC-026 và retirement departments

- Sau xử lý dependency, chạy read-only preflight bổ sung: legacy active users/warehouses/types/categories=0, open requests/tasks/shifts=0, assets in custody=0, warehouse balances/open supply workflows đã hết.
- Giữ guard SQL52005 nguyên nghĩa và không bỏ transaction guard. Script Task02B hiện có @@TRANCOUNT=0 precondition; gọi nó ở ranh giới phù hợp ngoài transaction khác, không nhúng bằng cách xóa guard.
- Khi mọi điều kiện đạt: đúng bốn department3/5/6/10 is_active=false, canonical8 vẫn Active, manager/history fields theo disposition đã duyệt và audit nguyên tử. Nếu guard còn blocker thì không retire, không báo PASS.
- Dependencies đã commit trước mà retirement thất bại phải được báo như trạng thái trung gian; không nói toàn bộ workflow đã rollback. Cần recovery/compensation được review, không destructive replay.

### Bước 8 — Verification sau COMMIT

- Release04 chỉ đọc; exact roles/permissions/grants và scopes, fixed OM/DH/Storekeeper unchanged.
- Không active user/warehouse/catalog thuộc department Inactive; no open responsibility/orphan; stocks và completed history theo disposition; old audits và credentials intact.
- Sessions bị revoke nếu accounts disabled; không reset/xóa session history hoặc giả rằng account inactive là đã revoke mọi session.
- DBCC CHECKDB, CHECKCONSTRAINTS; FK enabled/trusted, unique indexes; schema comparison phạm vi đã duyệt.
- Compare Trial After→approved expected after, Trial Before→After cho phép diff trong manifest, Source After→Source Before **riêng**; không đòi source=trial.
- Test scenarios DEV1/DEV2, cả positive/negative, khi workflow implementation tồn tại. Chưa có endpoint thì ghi Not Run thay vì suy từ SQL verification.

## 4. Chống chạy lặp / idempotency

- Không tự tạo bảng tracking schema mới. Dùng execution receipt ngoài repo có exact target/database identity, manifest/script digests, per-step before/after fingerprints, key counts và approved checkpoints.
- Khi before-state khớp và chưa có receipt completed mới đủ điều kiện chạy. Nếu after-state khớp receipt completed thì báo already-applied, không DML. Mixed state, unknown row hoặc digest khác thì BLOCKED.
- Không dùng DELETE WHERE code LIKE 'FB%' hoặc xóa trước để seed lại. Không update existing user/password theo username matching. New fixture phải có namespace/version và conflict fail-closed.
- Bản JSON hiện tại có approvedForExecution=false; không lật một flag rồi coi là đã có chữ ký. Khi có chứng cứ/approval cần version mới, review diff và digest, nhất là khi boundary PRESERVE thay đổi.

## 5. Rollback và recovery

| Thời điểm | Cách xử lý |
| --- | --- |
| Preflight/provenance không đạt | Không có writes; sửa hồ sơ bằng chứng, không sửa data để khớp precondition |
| Dry Run lỗi/trước COMMIT | Rollback transaction, so hashes; giữ lỗi/diff để review, không bỏ guard |
| Đã commit fixture changes | Dừng writer, lưu current state, dùng reviewed inverse changeset và exact-key archive hoặc restore checkpoint vào DB mới. Không restore đè source/trial tự động |
| Có changes ngoài phạm vi sau checkpoint | Không inverse/restore mù; reconcile và xin quyết định recovery mới |
| Account/session đã revoke | Không tự un-revoke hoặc tái tạo credential để “khớp backup”; giữ security state, dùng quy trình đăng nhập/khôi phục hợp lệ sau này |

Recovery cần phục hồi PK/FK gốc, identity behavior và reference mappings bằng phương án đã test; không tự tắt FK hoặc reset identity. Backup/archive ngoài Git có ACL phù hợp; manifest metadata không là bản sao đầy đủ để recover.

## 6. Review sign-off cần có

- Project owner: provenance và exact dispositions; chấp thuận các sample nào được thay, không blanket delete.
- DEV1: request/shift/user/session, giữ User4 và DH/OM scope, field-level manager-history policy, không fake time.
- DEV2: ledger/request/fulfillment/approval/stocktake boundary, dữ liệu test cần giữ, warehouse mapping và expected stock arithmetic.
- Cả hai: protected history, fixture scenarios, recovery rehearsal, exact script/manifest digests và quyền chạy trial-only riêng.

**Kết luận:** quy trình được thiết kế để review, không có bước nào đã được thực thi trong Task02E. Hiện thiếu provenance cho 28 rows và protected dependencies chặn nhiều candidate reset. Chưa có kế hoạch reset executable; chỉ khi hồ sơ mới giải quyết những điểm này và guard hết blocker mới hoàn tất Department Retirement.
