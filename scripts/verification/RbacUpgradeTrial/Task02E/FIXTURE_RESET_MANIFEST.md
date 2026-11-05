# Reset Manifest — bản 0.1, chỉ để review

Target duy nhất có thể xin duyệt sau này: `DESKTOP-JI8U50T\SQLEXPRESS` / `HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282`. `HotelOpsDB` không phải target. **Không row nào được duyệt thực thi ở manifest này.** Không có SQL reset được tạo.

Machine manifest [FIXTURE_RESET_MANIFEST.json](FIXTURE_RESET_MANIFEST.json) liệt kê từng actual PK, expected current state, safe-projection JSON/hash, group/profile, archive/rollback và missing approvals. Bảng dưới nhóm các PK có cùng quyết định; JSON là inventory đầy đủ để đối chiếu từng row. Không sử dụng PK/IDs mẫu trong migration chung.

## 1. Provenance evidence register

| Ref | Bằng chứng đã kiểm tra | Kết luận và giới hạn |
| --- | --- | --- |
| PROV-01 | Working-tree SQL và git log --all cho SQL/seed/fixture; Release01 schema-only, Release03 RBAC-only | Không có original INSERT artifact cho những numeric business rows cần reset. Không khẳng định đã tìm mọi file ngoài repository hoặc remote chưa fetch. |
| PROV-02 | `c9d7d53:docs/database/README.md` dẫn ba SQL upload, trong đó `HotelOpsDB_SampleData_Final.sql`; tài liệu nói không bao gồm raw SQL | Có dấu vết tên seed, chưa có script, hash nguồn, execution log hoặc row mapping để chứng minh từng bản ghi. |
| PROV-03 | `c9d7d53:tests/fixtures/api-fixture.mjs`, `src/mocks/createDatabase.ts`, `enrichDatabase.ts`, `prototypeData.ts` | Frontend/mock fixtures IDs khác; không có chứng cứ import vào SQL rows hiện tại. Tên tương tự, ID trùng hoặc ngày giống nhau không đủ. |
| PROV-04 | Task02 backup/restore và before/after fingerprints | Chứng minh nguồn sao chép HotelOpsDB và các changeset sau đó. Không chứng minh nguồn tạo dữ liệu trước backup là synthetic. |
| PROV-05 | Task02C guarded SQL, commit results và 8 audit records | Chứng minh 8 lần deactivate đã xảy ra trên trial. Không chứng minh nguồn gốc lúc tạo row. Audit là lịch sử thao tác thật, phải bảo tồn. |

**Quy tắc phân loại:** CONFIRMED_SYNTHETIC cần generator/seed có hash/version, row mapping và execution evidence hoặc hồ sơ provenance per-row được chủ dữ liệu xác nhận đủ căn cứ. Chỉ nói “đa số dữ liệu là seed/test” không đủ. UNCERTAIN không được REMOVE; REPLACE cũng không được dùng để lách việc xóa chưa được duyệt. PRESERVE ưu tiên khi giữ history/auth/RBAC/canonical model, dù origin chưa biết.

Trong phạm vi explicit 371 row: **0 CONFIRMED_SYNTHETIC / 28 UNCERTAIN / 343 PRESERVE**. Không đếm database toàn bộ, không đếm đôi manager fields, không cộng protected rows ngoài inventory vào 343.

## 2. 28 row UNCERTAIN — chưa được phép reset

Nguồn gốc chung cho mọi row trong bảng: thiếu original seed, PROV-01–04. Cần chứng minh provenance/duyệt exact rowset; hiện `executionEligible=false`. Không có REMOVE.

| Table / actual PK | Số row | Current state | proposedAction hiện tại → hướng sau khi đủ chứng cứ | Dependency/impact, archive và approval |
| --- | ---: | --- | --- | --- |
| warehouses.warehouse_id=4 | 1 | WH-FB Active, department6/Branch1 | DEACTIVATE có điều kiện | stockHistory + stock + threshold; không chọn kho nhận; AP-INVENTORY/AP-PROTECTED-HISTORY; O5 |
| material_requests.request_id=8,9 | 2 | APPROVED, source warehouse4; requester6/HK | PRESERVE → REPLACE được xem xét | lines9/10, approvals9/10 PRESERVE, fulfillment8/9. Parent replacement BLOCKED bởi protected approvals; AP-PROTECTED-HISTORY/AP-INVENTORY; O3 |
| material_request_lines.line_id=9,10 | 2 | approved48/24, fulfilled0 | PRESERVE → REPLACE được xem xét | FK tới headers8/9; fulfillment_lines9/10; O2/O3 |
| material_fulfillments.fulfillment_id=8,9 | 2 | PREPARING, stock_document_id NULL | PRESERVE → REPLACE được xem xét | lines9/10, requests8/9; không gán RECEIVED; AP-INVENTORY; O3 |
| material_fulfillment_lines.fulfillment_line_id=9,10 | 2 | issued0, received NULL | PRESERVE → REPLACE được xem xét | Parent8/9, request_lines9/10; không post movement; O2 |
| stock_thresholds.threshold_id=13,14 | 2 | warehouse4, alert_enabled=true | DEACTIVATE có điều kiện | Chỉ tắt alert khi kho retire; không xóa threshold/qty history, không có cột is_active để invent; O5 |
| internal_requests.internal_request_id=13 | 1 | DRAFT, type6, requester23, receiving FB6 | PRESERVE → REPLACE được xem xét | CREATED event13; no direct assignment/feedback lúc Task02D. Recheck type+ID refs; không DRAFT→CANCELLED giả; AP-REQUEST; O3 |
| internal_request_events.event_id=13 | 1 | CREATED→DRAFT, actor23 | PRESERVE → REPLACE được xem xét | Không phải audit_logs. Chỉ archive/rebuild cùng request13 khi đủ provenance; không tự thêm cancellation event; O2 |
| work_shifts.shift_id=4 | 1 | IN_PROGRESS, LAU3, ngày16/10 | PRESERVE → REPLACE được xem xét | assignments7/8, attendance7/8; thiếu actual check-out; không tạo giờ kết thúc; AP-SHIFT; O3 |
| work_shifts.shift_id=5 | 1 | PLANNED quá khứ, no children đã thấy | PRESERVE → REPLACE được xem xét | Không tự invent WorkShift CANCELLED; parent-only row vẫn cần provenance; O3 |
| shift_assignments.shift_assignment_id=7,8 | 2 | Shift4, users11/4 CONFIRMED | PRESERVE → REPLACE được xem xét | attendance7/8; giữ users, không đổi DH4; O2 |
| shift_attendance.attendance_id=7,8 | 2 | PRESENT, check-in05:55/06:00, check-out NULL | PRESERVE → REPLACE được xem xét | Child of assignments7/8; archive thời gian gốc, không sửa thành hoàn tất; O1 |
| users.user_id=11,17,18,20 | 4 | ACTIVE; legacy department3/5/6/10 | DEACTIVATE có điều kiện | Không REMOVE/REPLACE user. Preserve credentials; user11 shift/session; 17/18/20 manager fields trên depts5/6/10. AP-ACCOUNTS; O4 |
| departments.department_id=3,5,6,10 | 4 | LAU/SEC/FB B1, SEC B2 đều Active; managers4/17/18/20 | DEACTIVATE có điều kiện | Manager relationship là field cùng PK; giữ history FKs nếu duyệt. Zero blockers + audit, không clear manager field ngầm; O6 |
| internal_request_types.request_type_id=6 | 1 | FB_ORDER Active, default_department6 | DEACTIVATE có điều kiện | Sau request13 hết active responsibility; giữ code/ID meaning; AP-REQUEST; O5 |

12 DEACTIVATE là đề xuất soft-retirement, không đủ điều kiện chạy ngay. 16 hướng REPLACE đang được HOLD bằng `proposedAction=PRESERVE`; `futureRequestedAction=REPLACE` chỉ giúp review hướng B, không phải disposition đã được duyệt. Không có destructive candidate đã đủ provenance.

## 3. 343 row PRESERVE — boundary bảo vệ

| Table / phạm vi | Số row | Căn cứ bảo tồn và dependency |
| --- | ---: | --- |
| roles toàn bộ 12 PK trong JSON | 12 | Exact catalog mới; không seed/reset lại |
| permissions toàn bộ 62 PK | 62 | Cấu trúc quyền chuẩn, không là fixture reset |
| role_permissions toàn bộ 140 PK | 140 | Scope ALL/BRANCH/OWN; không sửa grants |
| user_roles toàn bộ 32 PK | 32 | Giữ fixed OM/DH/Storekeeper và lịch sử inactive assignments; đổi assignment cần manifest khác được duyệt |
| user_branch_access toàn bộ 28 access_id | 28 | Giữ trạng thái hiện tại, không reset theo user ID; nếu muốn revoke account access phải xin duyệt field changes riêng |
| audit_logs toàn bộ 22 audit_id | 22 | 14 trước cleanup + 8 Task02C; không xóa/replace history hoặc fake actor |
| user_sessions: session hiện có của user11, actual session_id trong JSON | 1 | Không reset/xóa. Sau account deactivation phải có changeset riêng cho revoked_at theo FE-01, không đụng credential |
| users.user_id=1,4,5,19 | 4 | OM B1, DH B1, Storekeeper B1, DH B2; credentials và accounts protected |
| departments: 8 canonical PK/code/branch trong JSON | 8 | HK/MT/WH/FO mỗi branch, Active; không dùng làm nơi chuyển mặc định |
| warehouses.warehouse_id=8,9 | 2 | Đã Inactive theo Task02C; giữ owner/history |
| internal_request_types.request_type_id=4,5,9 | 3 | Đã Inactive; historical completed/cancelled references |
| work_categories.category_id=2,10,11 | 3 | Đã Inactive; task14 operational COMPLETED giữ nguyên |
| inventory_items.item_id=14,15 | 2 | Master hàng/UoM và liên kết lịch sử; code FB-* không đủ lý do xóa |
| stock_documents.document_id=3,8 | 2 | POSTED receipt/issue; doc8 reference MATERIAL_REQUEST3 |
| stock_document_lines.line_id=3,4,9,10 | 4 | Giữ arithmetic/header và FK tới movements |
| stock_movements.movement_id=3,4,10,11 | 4 | Ledger +480/+240/−96/−48, không xóa để về 0 |
| stocktakes.stocktake_id=3,7 | 2 | APPROVED/COMPLETED history |
| stocktake_lines.stocktake_line_id=4,5,9 | 3 | Counted quantities/variance lịch sử, không fabricate adjustment |
| material_requests.request_id=3 | 1 | COMPLETED history, liên kết issue doc8 |
| material_request_lines.line_id=3,4 | 2 | Request3 item14/15, quantities đã thực hiện |
| material_request_approvals.approval_id=3,9,10 | 3 | Approved decision history của requests3/8/9; không xóa để thay parent |
| material_fulfillments.fulfillment_id=3 | 1 | RECEIVED, stock_document8 |
| material_fulfillment_lines.fulfillment_line_id=3,4 | 2 | Fulfillment3/request3 đã nhận, không tái tạo như chưa giao |

PRESERVE không khẳng định những row này là dữ liệu thật hay synthetic; có nghĩa **không được thay trong manifest này**. Khi một candidate REPLACE cần xóa/sửa chúng, component BLOCKED và phải trình manifest version khác; không mở rộng closure âm thầm.

Ngoài 343 explicit row, toàn bộ users/passwords/session credentials, branches, canonical department/UoM/master data và tất cả row ngoài rowset đều được default-deny protection. Không xuất các bí mật này vào JSON. Counts trên không phải số row của cả database.

## 4. Dependency profiles — áp dụng cho từng record trong JSON

| Profile | FK trực tiếp và tác động gián tiếp phải review | Business impact / closure status |
| --- | --- | --- |
| warehouse | departments/users → warehouses; stock_movements, source/destination stock_documents, stocktakes, thresholds, material_requests.source_warehouse_id, material_fulfillments.warehouse_id | WH-FB reset BLOCKED bởi tồn và protected posted history; 8/9 giữ Inactive |
| stock | headers→lines/approvals/fulfillments→fulfillment_lines; request_line FK; warehouse/items/UoM/users | Requests8/9 replacement không thể đi qua protected approvals9/10. Fulfillment lines issued0 không phải bằng chứng origin synthetic |
| stockHistory | docs→lines→movements; completed fulfillment3.stock_document_id→doc8; stocktakes.adjustment_document_id; reversal_of_document_id; maintenance_work_logs.material_issue_id | Tất cả protected; không reset riêng ledger. Không thấy maintenance logs tới docs3/8 trong SELECT cũ, vẫn phải kiểm tra lại |
| threshold | warehouse4/items14/15; alert pipeline | Có thể đề xuất alert_enabled=false sau retirement kho; không delete item hoặc stock |
| internalRequest | type6, requester23, requester/receiving departments → events/assignments/feedback | No assignment/feedback lúc bằng chứng gần nhất; event13 vẫn là child thực tế. Chưa có approved draft cancel workflow |
| shift | templates/departments/users → shifts4/5 → assignments7/8 → attendance7/8 | Reset time component BLOCKED bởi origin; không fake check-out hoặc disable User4 |
| user | users.department_id/manager_user_id; departments.manager_user_id; warehouse owners; assignments, sessions, role/branch records và historical actor FKs | Account flag là thay đổi có điều kiện sau giải quyết work; không đổi history actor hoặc reset user |
| departments | branch/manager → department, 14 incoming FK đã khảo sát Task02B, xem FK_GRAPH; liên quan users, assets, warehouse, requests, tasks, shifts, schedules, work category | Chỉ retire khi zero active responsibility, giữ historical FK; manager field không tạo thêm row được đếm |
| catalog | type→default dept/internal_requests; category→department/operational_tasks | Existing inactive rows giữ nguyên; type6 chờ request13 |
| protectedAssignments | user_roles ↔ users/roles/branch; user_branch_access ↔ users/branches | Fixed assignments và lịch sử preserved; không reassign dựa vào IDs sample |
| protectedSessions | user_sessions→users | Không xóa/reset; FE-01 revocation là update được duyệt riêng sau disable account |
| protectedMaster | items→category/UoM và mọi line/movement/threshold/count | Không đổi units hoặc tạo hàng nhận giả |
| protectedRbac | role/permission→grant; role→user_roles | Exact Release catalog must remain identical |
| protectedAudit | branch/user actor và entity_type/entity_id đa hình | Append audit thực tế cho thay đổi sau này; không sửa/xóa audit cũ |

`FK_GRAPH.json` chứa metadata 201 FK của database, không phải 201 dependency đã được cho phép thay. Closure có FK và references đa hình; graph cấp schema không tự chứng minh closure cấp row hoàn chỉnh. Baseline Task02D được tái dùng vì các table hashes hiện tại khớp riêng baseline sau Task02C; vẫn phải đánh giá row closure dưới lock trước execution.

### Liên kết đa hình và collision theo ID

- stock_documents.reference_type/reference_id: doc8→MATERIAL_REQUEST3; doc3→PURCHASE_ORDER1003, chưa có provenance nguồn purchase-order fixture.
- operational_tasks.source_type/source_id; attachments.entity_type/entity_id; notifications.source_type/source_id; audit_logs.entity_type/entity_id đều phải kiểm tra exact type + ID và aliases được source xác nhận.
- Notification MATERIAL_REQUEST13 không phải INTERNAL_REQUEST13. Attachments trùng số ID thuộc CLEANING_TASK/INCIDENT/INSPECTION/MAINTENANCE/ROOM, phải giữ. Không xóa WHERE entity_id IN (...) mà bỏ qua type.
- Không có provenance/closure cho liên kết ngoài FK thì row liên quan BLOCKED, không giả rằng count=0 vì metadata FK không chứa nó.

## 5. PLAN_ORDER / archive / rollback profiles

Đây là thứ tự tham chiếu, **không phải lệnh hoặc quyền chạy**:

- O0: xác nhận provenance/approvals/manifest digest, backup trial mới, archive ngoài repo, validate full closure. Thiếu O0 thì O1–O6 không được bắt đầu.
- O1: attendance child được duyệt nếu và chỉ nếu cả shift component đủ provenance, không protected boundary.
- O2: dependent fixture lines/events/assignments theo actual FK; không touched approvals/audits protected.
- O3: parent fixture replacement chỉ khi không còn protected child và approval rowset mới chỉ rõ old→new mapping; insert parent-before-child nếu được duyệt. Không tắt FK; không xóa parent dựa vào group name.
- O4: account deactivation/session revocation theo FE-01 trong transaction; preserve credentials và users1/4/5/19; update lịch sử actor không được phép.
- O5: retire type6, WH-FB/alerts khi zero active flows/balance. Không fake transfer để mở khóa.
- O6: UC-026 + nguyên guard52005, rồi chính xác bốn flags departments và audit; canonical8 Active.

ARCHIVE_OUTSIDE_REPOSITORY: checkpoint DB hiện tại và per-key full-row dependency archive, record counts/digests/schema/references, quyền truy cập hạn chế. Tài liệu trong Git chỉ chứa metadata. Không tạo archive thật ở Task02E.

SECURE_ARCHIVE_NO_CREDENTIALS_IN_GIT: user/session/credential fields chỉ nằm trong backup/archive được bảo vệ ngoài repo nếu cần recovery; không xuất toàn users hoặc session secret vào manifest. Projection JSON ở đây chỉ là precondition an toàn, không đủ dùng làm bản phục hồi.

CHECKPOINT_AND_EXACT_KEY_ARCHIVE: trước COMMIT rollback transaction và kiểm tra protected hashes; sau COMMIT không tự restore đè DB. Chọn reviewed compensation hoặc restore checkpoint vào DB tên mới rồi replay/inverse manifest đã kiểm chứng, không xóa audit. Restore không được overwrite thay đổi ngoài phạm vi sau checkpoint. Revoke session không được tự “unrevoke” khi rollback sau COMMIT.

## 6. Approvals còn thiếu

| Ref | Phạm vi cần duyệt |
| --- | --- |
| AP-PROVENANCE | Original seed/generator hash + row mapping/execution evidence hoặc hồ sơ provenance đủ tin cậy cho exact uncertain keys |
| AP-ROWSET | Trạng thái và action per PK/field; không ký chung “reset sample data” |
| AP-CLOSURE | Xác nhận không phá protected row, FK/polymorphic closure và nhu cầu DEV1/DEV2 |
| AP-PROTECTED-HISTORY | Nếu muốn đổi 343 protected rows phải manifest version mới, lý do/căn cứ và owners ký riêng; hiện không đề xuất xóa ledger/approvals |
| AP-INVENTORY | DEV2 quyết định preserve/rebuild component hoặc workflow hợp lệ; không chọn kho nhận hay phát sinh movement ngầm |
| AP-REQUEST | DEV1/owner chọn fixture disposition hoặc approve draft workflow mới; metadata status CHECK không đủ |
| AP-SHIFT | DEV1/owner xác nhận synthetic/evidence/mapping cho ca quá khứ, không tạo actual timestamp giả |
| AP-ACCOUNTS | Users11/17/18/20, account flag, work resolution, branch/role/session effects; credentials và fixed personnel protected |
| AP-EXECUTION | Server/DB exact, approved manifest version + digest, recovery test, expected diff và quyền COMMIT riêng sau Dry Run review |

Các bản ghi PRESERVE không cần được duyệt để giữ nguyên. Muốn thay đổi chúng mới cần manifest/approval bổ sung. `missingApprovalRefs` của uncertain rows là tối thiểu chung; approvals theo group ở bảng trên cũng bắt buộc.
